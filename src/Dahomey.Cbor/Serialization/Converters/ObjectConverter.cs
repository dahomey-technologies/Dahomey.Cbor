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
        bool ReadValue(ref CborReader reader, ReadOnlySpan<byte> memberName, HashSet<IMemberConverter> readMembers, [MaybeNullWhen(false)] out object value);
        IReadOnlyList<IMemberConverter> MemberConvertersForWrite { get; }
        IReadOnlyList<IMemberConverter> RequiredMemberConvertersForRead { get; }
    }

    public interface IObjectConverter<out T> : IObjectConverter
    {
        T CreateInstance();
    }

    public class ObjectConverter<T> :
        CborConverterBase<T>,
        IObjectConverter<T>,
        ICborMapReader<ObjectConverter<T>.MapReaderContext>,
        ICborMapWriter<ObjectConverter<T>.MapWriterContext>
    {
        public struct MapReaderContext
        {
            public T obj;
            public IObjectConverter<T> converter;
            public Dictionary<RawString, object>? creatorValues;
            public Dictionary<RawString, object>? regularValues;
            public HashSet<IMemberConverter>? readMembers;
        }

        public struct MapWriterContext
        {
            public CborOptions options;
            public T obj;
            public int memberIndex;
            public IObjectConverter objectConverter;
            public LengthMode lengthMode;
        }

        private readonly ByteBufferDictionary<IMemberConverter> _memberConvertersForRead = new ByteBufferDictionary<IMemberConverter>();
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
        public IReadOnlyList<IMemberConverter> RequiredMemberConvertersForRead => _requiredMemberConvertersForRead;

        public ObjectConverter(CborOptions options)
        {
            _options = options;
            _registry = options.Registry;
            _objectMapping = _registry.ObjectMappingRegistry.Lookup<T>();

            _memberConvertersForWrite = new List<IMemberConverter>();

            foreach (IMemberMapping memberMapping in _objectMapping.MemberMappings)
            {
                IMemberConverter memberConverter = memberMapping.GenerateMemberConverter();

                if (memberMapping.CanBeDeserialized || _objectMapping.IsCreatorMember(memberConverter.MemberName))
                {
                    _memberConvertersForRead.Add(memberConverter.MemberName, memberConverter);

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

            MapReaderContext context = new MapReaderContext
            {
                creatorValues = _objectMapping.CreatorMapping != null ? new Dictionary<RawString, object>() : null,
                regularValues = _objectMapping.CreatorMapping != null ? new Dictionary<RawString, object>() : null,
                readMembers = _requiredMemberConvertersForRead.Count != 0 ? new HashSet<IMemberConverter>() : null
            };

            reader.ReadMap(this, ref context);

            if (_objectMapping.CreatorMapping != null)
            {
                context.obj = (T)_objectMapping.CreatorMapping.CreateInstance(context.creatorValues!);
                if (_objectMapping.OnDeserializingMethod != null)
                {
                    ((Action<T>)_objectMapping.OnDeserializingMethod)(context.obj);
                }

                foreach (KeyValuePair<RawString, object> value in context.regularValues!)
                {
                    if (!_memberConvertersForRead.TryGetValue(value.Key.Buffer.Span, out IMemberConverter? memberConverter))
                    {
                        // should not happen
                        throw new CborException("Unexpected error");
                    }

                    memberConverter.Set(context.obj, value.Value);
                }
            }

            if (context.readMembers != null)
            {
                if (context.converter == null)
                {
                    context.converter = this;
                }

                foreach (IMemberConverter memberConverter in context.converter.RequiredMemberConvertersForRead)
                {
                    if (!context.readMembers.Contains(memberConverter))
                    {
                        throw new CborException($"Required property '{Encoding.UTF8.GetString(memberConverter.MemberName)}' not found in JSON.");
                    }
                }
            }

            if (_objectMapping.OnDeserializedMethod != null)
            {
                ((Action<T>)_objectMapping.OnDeserializedMethod)(context.obj);
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

            MapWriterContext context = new MapWriterContext
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
                context.objectConverter = (IObjectConverter)_registry.ConverterRegistry.Lookup(value.GetType());
            }
            else
            {
                context.objectConverter = this;
            }

            writer.WriteMap(this, ref context);

            if (_objectMapping.OnSerializedMethod != null)
            {
                ((Action<T>)_objectMapping.OnSerializedMethod)(value);
            }
        }

        public void ReadBeginMap(int size, ref MapReaderContext context)
        {
        }

        public void ReadMapItem(ref CborReader reader, ref MapReaderContext context)
        {
            if (context.obj == null)
            {
                if (context.converter == null)
                {
                    if (_discriminatorConvention != null)
                    {
                        CborReaderBookmark bookmark = reader.GetBookmark();

                        if (FindItem(ref reader, _discriminatorConvention.MemberName))
                        {
                            // discriminator value
                            Type actualType = _discriminatorConvention.ReadDiscriminator(ref reader);
                            context.converter = (IObjectConverter<T>)_registry.ConverterRegistry.Lookup(actualType);
                        }
                        else
                        {
                            context.converter = this;
                        }

                        reader.ReturnToBookmark(bookmark);
                    }
                    else
                    {
                        context.converter = this;
                    }
                }

                if (context.creatorValues == null)
                {
                    if (!_isStruct)
                    {
                        context.obj = context.converter.CreateInstance();
                    }

                    if (_objectMapping.OnDeserializingMethod != null)
                    {
                        ((Action<T>)_objectMapping.OnDeserializingMethod)(context.obj);
                    }
                }
            }
            else if (context.converter == null)
            {
                context.converter = this;
            }

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
                if (_objectMapping.IsCreatorMember(memberName))
                {
                    context.creatorValues.Add(new RawString(memberName), value);
                }
                else
                {
                    context.regularValues!.Add(new RawString(memberName), value);
                }
            }
        }

        public void ReadValueForStruct(ref CborReader reader, ref T instance, ReadOnlySpan<byte> memberName, HashSet<IMemberConverter> readMembers)
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
        public static bool FindItem(ref CborReader reader, ReadOnlySpan<byte> name)
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

        public int GetMapSize(ref MapWriterContext context)
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

        public bool WriteMapItem(ref CborWriter writer, ref MapWriterContext context)
        {
            while (context.memberIndex < context.objectConverter.MemberConvertersForWrite.Count)
            {
                IMemberConverter memberConverter = context.objectConverter.MemberConvertersForWrite[context.memberIndex++];
                if (_isStruct)
                {
                    IMemberConverter<T> typedMemberConverter = (IMemberConverter<T>)memberConverter;

                    if (typedMemberConverter.ShouldSerialize(ref context.obj, typeof(T)))
                    {
                        writer.WriteString(memberConverter.MemberName);
                        typedMemberConverter.Write(ref writer, ref context.obj);
                        break;
                    }
                }
                else if (memberConverter.ShouldSerialize(context.obj!, typeof(T), context.options))
                {
                    writer.WriteString(memberConverter.MemberName);
                    memberConverter.Write(ref writer, context.obj);
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
    }
}
