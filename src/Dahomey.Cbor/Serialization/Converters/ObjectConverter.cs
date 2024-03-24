using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization.Conventions;
using Dahomey.Cbor.Serialization.Converters.Mappings;
using Dahomey.Cbor.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Dahomey.Cbor.Serialization.Converters
{
    public interface IObjectConverter
    {
        void ReadValue(ref CborReader reader, object obj, ReadOnlySpan<byte> memberName, HashSet<IMemberConverter> readMembers);
        void ReadValue(ref CborReader reader, object obj, int memberIndex, HashSet<IMemberConverter> readMembers);
        bool ReadValue(ref CborReader reader, ReadOnlySpan<byte> memberName, HashSet<IMemberConverter> readMembers, [MaybeNullWhen(false)] out object value);
        bool ReadValue(ref CborReader reader, int memberIndex, HashSet<IMemberConverter> readMembers, [MaybeNullWhen(false)] out object value);
        IReadOnlyList<IMemberConverter> MemberConvertersForWrite { get; }
        ByteBufferDictionary<IMemberConverter> MemberConvertersForRead { get; }
        Dictionary<int, IMemberConverter> MemberConvertersForReadByIndex { get; }
        IReadOnlyList<IMemberConverter> RequiredMemberConvertersForRead { get; }
        IObjectMapping ObjectMapping { get; }
    }

    public interface IObjectConverter<out T> : IObjectConverter
    {
        T CreateInstance();
    }

    public class ObjectConverter<T> :
        CborConverterBase<T>,
        IObjectConverter<T>,
        ICborMapReader<ObjectConverter<T>.ReaderContext>,
        ICborMapWriter<ObjectConverter<T>.WriterContext>,
        ICborArrayReader<ObjectConverter<T>.ReaderContext>,
        ICborArrayWriter<ObjectConverter<T>.WriterContext>
    {
        public struct ReaderContext
        {
            public T obj;
            public IObjectConverter<T> converter;
            public Dictionary<RawString, object>? creatorValues;
            public Dictionary<RawString, object>? regularValues;
            public Dictionary<int, object>? creatorValuesByIndex;
            public Dictionary<int, object>? regularValuesByIndex;
            public HashSet<IMemberConverter>? readMembers;
            public int memberIndex;
        }

        public struct WriterContext
        {
            public CborOptions options;
            public T obj;
            public int memberIndex;
            public IObjectConverter objectConverter;
            public LengthMode lengthMode;
        }

        private readonly ByteBufferDictionary<IMemberConverter> _memberConvertersForRead = new ByteBufferDictionary<IMemberConverter>();
        private readonly Dictionary<int, IMemberConverter> _memberConvertersForReadByIndex = new();
        public List<IMemberConverter> _requiredMemberConvertersForRead = new List<IMemberConverter>();
        private readonly List<IMemberConverter> _memberConvertersForWrite;
        private readonly CborOptions _options;
        private readonly SerializationRegistry _registry;
        private readonly IObjectMapping _objectMapping;
        private readonly Func<T>? _constructor;
        private readonly bool _isInterfaceOrAbstract;
        private readonly bool _isStruct;
        private readonly IDiscriminatorConvention? _discriminatorConvention = null;

        public IReadOnlyList<IMemberConverter> MemberConvertersForWrite => _memberConvertersForWrite;
        public ByteBufferDictionary<IMemberConverter> MemberConvertersForRead => _memberConvertersForRead;
        public Dictionary<int, IMemberConverter> MemberConvertersForReadByIndex => _memberConvertersForReadByIndex;
        public IReadOnlyList<IMemberConverter> RequiredMemberConvertersForRead => _requiredMemberConvertersForRead;
        public IObjectMapping ObjectMapping => _objectMapping;

        public ObjectConverter(CborOptions options)
        {
            _options = options;
            _registry = options.Registry;
            _objectMapping = _registry.ObjectMappingRegistry.Lookup<T>();

            _memberConvertersForWrite = new List<IMemberConverter>();

            foreach (IMemberMapping memberMapping in _objectMapping.MemberMappings)
            {
                IMemberConverter memberConverter = memberMapping.GenerateMemberConverter();

                bool isCreatorMember = false;

                switch (_objectMapping.ObjectFormat)
                {
                    case CborObjectFormat.StringKeyMap:
                        isCreatorMember = _objectMapping.IsCreatorMember(memberConverter.MemberName);
                        break;

                    case CborObjectFormat.IntKeyMap:
                    case CborObjectFormat.Array:
                        isCreatorMember = _objectMapping.IsCreatorMember(memberConverter.MemberIndex!.Value);
                        break;
                }

                if (memberMapping.CanBeDeserialized || isCreatorMember)
                {
                    switch (_objectMapping.ObjectFormat)
                    {
                        case CborObjectFormat.StringKeyMap:
                            _memberConvertersForRead.Add(memberConverter.MemberName, memberConverter);
                            break;
                        case CborObjectFormat.IntKeyMap:
                        case CborObjectFormat.Array:
                            if (memberConverter.MemberIndex.HasValue)
                            {
                                _memberConvertersForReadByIndex.Add(memberConverter.MemberIndex.Value, memberConverter);
                            }
                            break;
                    }

                    if (memberConverter.RequirementPolicy == RequirementPolicy.AllowNull
                        || memberConverter.RequirementPolicy == RequirementPolicy.Always)
                    {
                        _requiredMemberConvertersForRead.Add(memberConverter);
                    }
                }

                if (memberMapping.CanBeSerialized)
                {
                    _memberConvertersForWrite.Add(memberConverter);
                }
            }

            _isInterfaceOrAbstract = typeof(T).IsInterface || typeof(T).IsAbstract;
            _isStruct = typeof(T).IsStruct();

            if (!_isInterfaceOrAbstract && !_isStruct && _objectMapping.CreatorMapping == null)
            {
                ConstructorInfo? defaultConstructorInfo = typeof(T).GetConstructor(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    null,
                    Type.EmptyTypes,
                    null);

                if (defaultConstructorInfo == null)
                {
                    throw new CborException($"Cannot find a default constructor on type {typeof(T)}");
                }

                _constructor = defaultConstructorInfo.CreateDelegate<T>();
            }

            _discriminatorConvention = _registry.DiscriminatorConventionRegistry.GetConvention(typeof(T));
        }

        public T CreateInstance()
        {
            if (_isInterfaceOrAbstract || _constructor == null)
            {
                throw new CborException("A CreatorMapping should be defined for interfaces or abstract classes");
            }

            return _constructor();
        }

        public override T Read(ref CborReader reader)
        {
            if (reader.ReadNull())
            {
                return default!;
            }

            ReaderContext context = new ReaderContext
            {
                readMembers = _requiredMemberConvertersForRead.Count != 0 ? new HashSet<IMemberConverter>() : null
            };

            switch (_objectMapping.ObjectFormat)
            {
                case CborObjectFormat.StringKeyMap:
                    {
                        context.creatorValues = _objectMapping.CreatorMapping != null ? new() : null;
                        context.regularValues = _objectMapping.CreatorMapping != null ? new () : null;
                        reader.ReadMap(this, ref context);
                    }
                    break;
                case CborObjectFormat.IntKeyMap:
                    {
                        context.creatorValuesByIndex = _objectMapping.CreatorMapping != null ? new() : null;
                        context.regularValuesByIndex = _objectMapping.CreatorMapping != null ? new() : null;
                        reader.ReadMap(this, ref context);
                    }
                    break;
                case CborObjectFormat.Array:
                    {
                        context.creatorValuesByIndex = _objectMapping.CreatorMapping != null ? new() : null;
                        context.regularValuesByIndex = _objectMapping.CreatorMapping != null ? new() : null;
                        reader.ReadArray(this, ref context);
                    }
                    break;
            }

            if (context.converter == null)
            {
                context.converter = this;
            }
            IObjectMapping objectMapping = context.converter.ObjectMapping;

            if (objectMapping.CreatorMapping != null)
            {
                switch (_objectMapping.ObjectFormat)
                {
                    case CborObjectFormat.StringKeyMap:
                        {
                            context.obj = (T)objectMapping.CreatorMapping.CreateInstance(context.creatorValues!);
                            if (objectMapping.OnDeserializingMethod != null)
                            {
                                ((Action<T>)objectMapping.OnDeserializingMethod)(context.obj);
                            }

                            foreach (KeyValuePair<RawString, object> value in context.regularValues!)
                            {
                                if (!context.converter.MemberConvertersForRead.TryGetValue(value.Key.Buffer.Span, out IMemberConverter? memberConverter))
                                {
                                    // should not happen
                                    throw new CborException("Unexpected error");
                                }

                                memberConverter.Set(context.obj, value.Value);
                            }
                        }
                        break;
                    case CborObjectFormat.IntKeyMap:
                    case CborObjectFormat.Array:
                        {
                            context.obj = (T)objectMapping.CreatorMapping.CreateInstance(context.creatorValuesByIndex!);
                            if (objectMapping.OnDeserializingMethod != null)
                            {
                                ((Action<T>)objectMapping.OnDeserializingMethod)(context.obj);
                            }

                            foreach (KeyValuePair<int, object> value in context.regularValuesByIndex!)
                            {
                                if (!context.converter.MemberConvertersForReadByIndex.TryGetValue(value.Key, out IMemberConverter? memberConverter))
                                {
                                    // should not happen
                                    throw new CborException("Unexpected error");
                                }

                                memberConverter.Set(context.obj, value.Value);
                            }
                        }
                        break;
                }
            }

            if (context.readMembers != null)
            {
                foreach (IMemberConverter memberConverter in context.converter.RequiredMemberConvertersForRead)
                {
                    if (!context.readMembers.Contains(memberConverter))
                    {
                        throw new CborException($"Required property '{Encoding.UTF8.GetString(memberConverter.MemberName)}' not found in JSON.");
                    }
                }
            }

            if (objectMapping.OnDeserializedMethod != null)
            {
                ((Action<T>)objectMapping.OnDeserializedMethod)(context.obj);
            }

            return context.obj;
        }

        public void ReadValue(ref CborReader reader, object obj, ReadOnlySpan<byte> memberName, HashSet<IMemberConverter> readMembers)
        {
            T value = (T)obj;

            if (!_memberConvertersForRead.TryGetValue(memberName, out IMemberConverter? memberConverter))
            {
                HandleUnknownName(ref reader, typeof(T), memberName);
                reader.SkipDataItem();
            }
            else
            {
                if (readMembers != null)
                {
                    readMembers.Add(memberConverter);
                }
                memberConverter.Read(ref reader, value);
            }
        }
        public void ReadValue(ref CborReader reader, object obj, int memberIndex, HashSet<IMemberConverter> readMembers)
        {
            T value = (T)obj;

            if (!_memberConvertersForReadByIndex.TryGetValue(memberIndex, out IMemberConverter? memberConverter))
            {
                HandleUnknownIndex(ref reader, typeof(T), memberIndex);
                reader.SkipDataItem();
            }
            else
            {
                if (readMembers != null)
                {
                    readMembers.Add(memberConverter);
                }
                memberConverter.Read(ref reader, value);
            }
        }

        public bool ReadValue(ref CborReader reader, ReadOnlySpan<byte> memberName, HashSet<IMemberConverter> readMembers, [MaybeNullWhen(false)] out object value)
        {
            if (!_memberConvertersForRead.TryGetValue(memberName, out IMemberConverter? memberConverter))
            {
                HandleUnknownName(ref reader, typeof(T), memberName);
                reader.SkipDataItem();
                value = default!;
                return false;
            }
            else
            {
                if (readMembers != null)
                {
                    readMembers.Add(memberConverter);
                }
                value = memberConverter.Read(ref reader);
                return true;
            }
        }

        public bool ReadValue(ref CborReader reader, int memberIndex, HashSet<IMemberConverter> readMembers, [MaybeNullWhen(false)] out object value)
        {
            if (!_memberConvertersForReadByIndex.TryGetValue(memberIndex, out IMemberConverter? memberConverter))
            {
                HandleUnknownIndex(ref reader, typeof(T), memberIndex);
                reader.SkipDataItem();
                value = default!;
                return false;
            }
            else
            {
                if (readMembers != null)
                {
                    readMembers.Add(memberConverter);
                }
                value = memberConverter.Read(ref reader);
                return true;
            }
        }

        public override void Write(ref CborWriter writer, T value, LengthMode lengthMode)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            if (_objectMapping.OnSerializingMethod != null)
            {
                ((Action<T>)_objectMapping.OnSerializingMethod)(value);
            }

            WriterContext context = new WriterContext
            {
                options = _options,
                obj = value,
                lengthMode = lengthMode != LengthMode.Default
                    ? lengthMode : _objectMapping.LengthMode != LengthMode.Default
                        ? _objectMapping.LengthMode : _options.MapLengthMode
            };

            Type declaredType = typeof(T);
            Type actualType = value.GetType();

            if (_objectMapping.CreatorMapping == null && actualType != declaredType)
            {
                var converter = _registry.ConverterRegistry.Lookup(actualType);

                if (converter is IObjectConverter objectConverter)
                {
                    context.objectConverter = objectConverter;
                }
                else if (converter is not null)
                {
                    converter.Write(ref writer, value);
                    return;
                }
                else
                {
                    throw new CborException($"No converter found for type {actualType.Name}");
                }
            }
            else
            {
                context.objectConverter = this;
            }

            switch (_objectMapping.ObjectFormat)
            {
                case CborObjectFormat.StringKeyMap:
                case CborObjectFormat.IntKeyMap:
                    writer.WriteMap(this, ref context);
                    break;
                case CborObjectFormat.Array:
                    writer.WriteArray(this, ref context);
                    break;
            }

            if (_objectMapping.OnSerializedMethod != null)
            {
                ((Action<T>)_objectMapping.OnSerializedMethod)(value);
            }
        }

        void ICborMapReader<ReaderContext>.ReadBeginMap(int size, ref ReaderContext context)
        {
        }

        void ICborMapReader<ReaderContext>.ReadMapItem(ref CborReader reader, ref ReaderContext context)
        {
            ReadItem(ref reader, ref context);
        }

        void ICborArrayReader<ReaderContext>.ReadBeginArray(int size, ref ReaderContext context)
        {
        }

        void ICborArrayReader<ReaderContext>.ReadArrayItem(ref CborReader reader, ref ReaderContext context)
        {
            ReadItem(ref reader, ref context);
        }

        int ICborMapWriter<WriterContext>.GetMapSize(ref WriterContext context)
        {
            return GetSize(ref context);
        }

        bool ICborMapWriter<WriterContext>.WriteMapItem(ref CborWriter writer, ref WriterContext context)
        {
            return WriteItem(ref writer, ref context);
        }

        int ICborArrayWriter<WriterContext>.GetArraySize(ref WriterContext context)
        {
            return GetSize(ref context);
        }

        bool ICborArrayWriter<WriterContext>.WriteArrayItem(ref CborWriter writer, ref WriterContext context)
        {
            return WriteItem(ref writer, ref context);
        }

        private void ReadItem(ref CborReader reader, ref ReaderContext context)
        {
            if (context.obj == null || context.converter == null)
            {
                if (context.converter == null)
                {
                    if (_discriminatorConvention != null)
                    {
                        switch (_objectMapping.ObjectFormat)
                        {
                            case CborObjectFormat.StringKeyMap:
                                {
                                    CborReaderBookmark bookmark = reader.GetBookmark();

                                    if (FindItem(ref reader, _discriminatorConvention.MemberName))
                                    {
                                        // discriminator value
                                        Type actualType = _discriminatorConvention.ReadDiscriminator(ref reader);

                                        if (!_objectMapping.ObjectType.IsAssignableFrom(actualType))
                                        {
                                            throw new CborException($"expected type {_objectMapping.ObjectType} is not assignable from actual type {actualType}");
                                        }

                                        context.converter = (IObjectConverter<T>)_registry.ConverterRegistry.Lookup(actualType);
                                        ICreatorMapping? creatorMapping = context.converter.ObjectMapping.CreatorMapping;
                                        context.creatorValues = creatorMapping != null ? new() : null;
                                        context.regularValues = creatorMapping != null ? new() : null;
                                    }
                                    else
                                    {
                                        context.converter = this;
                                    }

                                    reader.ReturnToBookmark(bookmark);
                                }
                                break;
                            case CborObjectFormat.IntKeyMap:
                                {
                                    CborReaderBookmark bookmark = reader.GetBookmark();

                                    if (FindItem(ref reader, 0)) // discriminator index is always 0
                                    {
                                        // discriminator value
                                        Type actualType = _discriminatorConvention.ReadDiscriminator(ref reader);

                                        if (!_objectMapping.ObjectType.IsAssignableFrom(actualType))
                                        {
                                            throw new CborException($"expected type {_objectMapping.ObjectType} is not assignable from actual type {actualType}");
                                        }

                                        context.converter = (IObjectConverter<T>)_registry.ConverterRegistry.Lookup(actualType);
                                        ICreatorMapping? creatorMapping = context.converter.ObjectMapping.CreatorMapping;
                                        context.creatorValuesByIndex = creatorMapping != null ? new() : null;
                                        context.regularValuesByIndex = creatorMapping != null ? new() : null;
                                    }
                                    else
                                    {
                                        context.converter = this;
                                    }

                                    reader.ReturnToBookmark(bookmark);
                                }
                                break;
                            case CborObjectFormat.Array:
                            default:
                                // discriminator is always the first item
                                // we need a Semantic Tag to check if the discriminator is present
                                if (reader.TryReadSemanticTag(out ulong semanticTag) && semanticTag == _options.DiscriminatorSemanticTag)
                                {
                                    // discriminator value
                                    Type actualType = _discriminatorConvention.ReadDiscriminator(ref reader);

                                    if (!_objectMapping.ObjectType.IsAssignableFrom(actualType))
                                    {
                                        throw new CborException($"expected type {_objectMapping.ObjectType} is not assignable from actual type {actualType}");
                                    }

                                    context.converter = (IObjectConverter<T>)_registry.ConverterRegistry.Lookup(actualType);
                                    ICreatorMapping? creatorMapping = context.converter.ObjectMapping.CreatorMapping;
                                    context.creatorValuesByIndex = creatorMapping != null ? new() : null;
                                    context.regularValuesByIndex = creatorMapping != null ? new() : null;
                                }
                                else
                                {
                                    context.converter = this;
                                }

                                // increment to skip discriminator index even when the semantic tag is not present
                                context.memberIndex++;
                                break;
                        }
                    }
                    else
                    {
                        context.converter = this;
                    }
                }

                if (context.creatorValues == null && context.creatorValuesByIndex == null)
                {
                    if (!_isStruct && context.obj == null)
                    {
                        context.obj = context.converter.CreateInstance();
                    }

                    if (context.converter.ObjectMapping.OnDeserializingMethod != null)
                    {
                        ((Action<T>)context.converter.ObjectMapping.OnDeserializingMethod)(context.obj);
                    }
                }

                if (_objectMapping.ObjectFormat == CborObjectFormat.Array && context.converter != this)
                {
                    // discrimnator read with no ReturnToBoomark, must exit here
                    return;
                }
            }
            else if (context.converter == null)
            {
                context.converter = this;
            }

            switch (_objectMapping.ObjectFormat)
            {
                case CborObjectFormat.StringKeyMap:
                    {
                        ReadOnlySpan<byte> memberName = reader.ReadRawString();
                        if (context.creatorValues == null)
                        {
                            if (_isStruct)
                            {
                                ReadValueForStruct(ref reader, ref context.obj, memberName, context.readMembers!);
                            }
                            else
                            {
                                context.converter.ReadValue(ref reader, context.obj!, memberName, context.readMembers!);
                            }
                        }
                        else if (context.converter.ReadValue(ref reader, memberName, context.readMembers!, out object? value))
                        {
                            if (context.converter.ObjectMapping.IsCreatorMember(memberName))
                            {
                                context.creatorValues.Add(new RawString(memberName), value);
                            }
                            else
                            {
                                context.regularValues!.Add(new RawString(memberName), value);
                            }
                        }
                    }
                    break;
                case CborObjectFormat.IntKeyMap:
                    {
                        int memberIndex = reader.ReadInt32();

                        if (context.creatorValuesByIndex == null)
                        {
                            if (_isStruct)
                            {
                                ReadValueForStruct(ref reader, ref context.obj, memberIndex, context.readMembers!);
                            }
                            else
                            {
                                context.converter.ReadValue(ref reader, context.obj!, memberIndex, context.readMembers!);
                            }
                        }
                        else if (context.converter.ReadValue(ref reader, memberIndex, context.readMembers!, out object? value))
                        {
                            if (context.converter.ObjectMapping.IsCreatorMember(memberIndex))
                            {
                                context.creatorValuesByIndex.Add(memberIndex, value);
                            }
                            else
                            {
                                context.regularValuesByIndex!.Add(memberIndex, value);
                            }
                        }
                    }
                    break;
                case CborObjectFormat.Array:
                    if (context.creatorValuesByIndex == null)
                    {
                        if (_isStruct)
                        {
                            ReadValueForStruct(ref reader, ref context.obj, context.memberIndex, context.readMembers!);
                        }
                        else
                        {
                            context.converter.ReadValue(ref reader, context.obj!, context.memberIndex, context.readMembers!);
                        }
                    }
                    else if (context.converter.ReadValue(ref reader, context.memberIndex, context.readMembers!, out object? value))
                    {
                        if (context.converter.ObjectMapping.IsCreatorMember(context.memberIndex))
                        {
                            context.creatorValuesByIndex.Add(context.memberIndex, value);
                        }
                        else
                        {
                            context.regularValuesByIndex!.Add(context.memberIndex, value);
                        }
                    }

                    context.memberIndex++;
                    break;
            }
        }

        private void ReadValueForStruct(ref CborReader reader, ref T instance, ReadOnlySpan<byte> memberName, HashSet<IMemberConverter> readMembers)
        {
            if (_memberConvertersForRead.TryGetValue(memberName, out IMemberConverter? memberConverter))
            {
                if (readMembers != null)
                {
                    readMembers.Add(memberConverter);
                }

                ((IMemberConverter<T>)memberConverter).Read(ref reader, ref instance);
            }
            else
            {
                reader.SkipDataItem();
            }
        }

        private void ReadValueForStruct(ref CborReader reader, ref T instance, int memberIndex, HashSet<IMemberConverter> readMembers)
        {
            if (_memberConvertersForReadByIndex.TryGetValue(memberIndex, out IMemberConverter? memberConverter))
            {
                if (readMembers != null)
                {
                    readMembers.Add(memberConverter);
                }

                ((IMemberConverter<T>)memberConverter).Read(ref reader, ref instance);
            }
            else
            {
                reader.SkipDataItem();
            }
        }

        private static bool FindItem(ref CborReader reader, ReadOnlySpan<byte> name)
        {
            do
            {
                ReadOnlySpan<byte> memberName = reader.ReadRawString();
                if (memberName.SequenceEqual(name))
                {
                    return true;
                }

                reader.SkipDataItem();
            }
            while (reader.MoveNextMapItem());

            return false;
        }

        private static bool FindItem(ref CborReader reader, int index)
        {
            do
            {
                int memberIndex = reader.ReadInt32();
                if (memberIndex == index)
                {
                    return true;
                }

                reader.SkipDataItem();
            }
            while (reader.MoveNextMapItem());

            return false;
        }

        private int GetSize(ref WriterContext context)
        {
            if (context.lengthMode == LengthMode.IndefiniteLength)
            {
                return -1;
            }

            int writableMembersCount = 0;

            foreach (IMemberConverter memberConverter in context.objectConverter.MemberConvertersForWrite)
            {
                if (_isStruct)
                {
                    IMemberConverter<T> typedMemberConverter = (IMemberConverter<T>)memberConverter;

                    if (typedMemberConverter.ShouldSerialize(ref context.obj, typeof(T)))
                    {
                        writableMembersCount++;
                    }
                }
                else if (memberConverter.ShouldSerialize(context.obj!, typeof(T), context.options))
                {
                    writableMembersCount++;
                }
            }

            return writableMembersCount;
        }

        private bool WriteItem(ref CborWriter writer, ref WriterContext context)
        {
            while (context.memberIndex < context.objectConverter.MemberConvertersForWrite.Count)
            {
                IMemberConverter memberConverter = context.objectConverter.MemberConvertersForWrite[context.memberIndex++];
                if (_isStruct)
                {
                    IMemberConverter<T> typedMemberConverter = (IMemberConverter<T>)memberConverter;

                    if (typedMemberConverter.ShouldSerialize(ref context.obj, typeof(T)))
                    {
                        switch (_objectMapping.ObjectFormat)
                        {
                            case CborObjectFormat.StringKeyMap:
                                writer.WriteString(memberConverter.MemberName);
                                break;
                            case CborObjectFormat.IntKeyMap:
                                if (memberConverter.MemberIndex.HasValue)
                                {
                                    writer.WriteInt32(memberConverter.MemberIndex.Value);
                                }
                                break;
                            case CborObjectFormat.Array:
                                //nothing to write here
                                break;
                        }

                        typedMemberConverter.Write(ref writer, ref context.obj);
                        break;
                    }
                }
                else if (memberConverter.ShouldSerialize(context.obj!, typeof(T), context.options))
                {
                    switch (_objectMapping.ObjectFormat)
                    {
                        case CborObjectFormat.StringKeyMap:
                            writer.WriteString(memberConverter.MemberName);
                            break;
                        case CborObjectFormat.IntKeyMap:
                            if (memberConverter.MemberIndex.HasValue)
                            {
                                writer.WriteInt32(memberConverter.MemberIndex.Value);
                            }
                            break;
                        case CborObjectFormat.Array:
                            //nothing to write here
                            break;
                    }

                    memberConverter.Write(ref writer, context.obj!);
                    break;
                }
            }

            return context.memberIndex < context.objectConverter.MemberConvertersForWrite.Count;
        }

        private void HandleUnknownName(ref CborReader reader, Type type, ReadOnlySpan<byte> rawName)
        {
            if (_options.UnhandledNameMode == UnhandledNameMode.ThrowException)
            {
                throw reader.BuildException($"Unhandled name [{Encoding.ASCII.GetString(rawName)}] in class [{type.Name}] while deserializing.");
            }
        }

        private void HandleUnknownIndex(ref CborReader reader, Type type, int memberIndex)
        {
            if (_options.UnhandledNameMode == UnhandledNameMode.ThrowException)
            {
                throw reader.BuildException($"Unhandled index [{memberIndex}] in class [{type.Name}] while deserializing.");
            }
        }
    }
}
