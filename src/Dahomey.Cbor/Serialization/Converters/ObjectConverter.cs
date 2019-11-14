﻿using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization.Conventions;
using Dahomey.Cbor.Serialization.Converters.Mappings;
using Dahomey.Cbor.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Dahomey.Cbor.Serialization.Converters
{
    public interface IObjectConverter
    {
        void ReadValue(ref CborReader reader, object obj, ReadOnlySpan<byte> memberName);
        bool ReadValue(ref CborReader reader, ReadOnlySpan<byte> memberName, out object value);
        IReadOnlyList<IMemberConverter> MemberConvertersForWrite { get; }
    }

    public interface IObjectConverter<out T> : IObjectConverter
        where T : class
    {
        T CreateInstance();
    }

    public class ObjectConverter<T> :
        CborConverterBase<T>,
        IObjectConverter<T>,
        ICborMapReader<ObjectConverter<T>.MapReaderContext>,
        ICborMapWriter<ObjectConverter<T>.MapWriterContext>
        where T : class
    {
        public struct MapReaderContext
        {
            public T obj;
            public IObjectConverter<T> converter;
            public Dictionary<RawString, object> creatorValues;
            public Dictionary<RawString, object> regularValues;
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
        private readonly List<IMemberConverter> _memberConvertersForWrite;
        private readonly SerializationRegistry _registry;
        private readonly IObjectMapping _objectMapping;
        private readonly Func<T> _constructor;
        private readonly bool _isInterfaceOrAbstract;
        private IDiscriminatorConvention _discriminatorConvention = null;

        public IReadOnlyList<IMemberConverter> MemberConvertersForWrite => _memberConvertersForWrite;

        public ObjectConverter(SerializationRegistry registry)
        {
            _registry = registry;
            _objectMapping = registry.ObjectMappingRegistry.Lookup<T>();

            _memberConvertersForWrite = new List<IMemberConverter>();

            foreach (IMemberMapping memberMapping in _objectMapping.MemberMappings)
            {
                IMemberConverter memberConverter = memberMapping.GenerateMemberConverter();

                if (memberMapping.CanBeDeserialized)
                {
                    _memberConvertersForRead.Add(memberConverter.MemberName, memberConverter);
                }
                else if (_objectMapping.CreatorMapping != null)
                {
                    foreach (RawString creatorMemberName in _objectMapping.CreatorMapping.MemberNames)
                    {
                        if (creatorMemberName.Buffer.Span.SequenceEqual(memberConverter.MemberName))
                        {
                            _memberConvertersForRead.Add(memberConverter.MemberName, memberConverter);
                            break;
                        }
                    }
                }

                if (memberMapping.CanBeSerialized)
                {
                    _memberConvertersForWrite.Add(memberConverter);
                }
            }

            _isInterfaceOrAbstract = typeof(T).IsInterface || typeof(T).IsAbstract;

            if (!_isInterfaceOrAbstract && _objectMapping.CreatorMapping == null)
            {
                ConstructorInfo defaultConstructorInfo = typeof(T).GetConstructor(
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

            _discriminatorConvention = registry.DiscriminatorConventionRegistry.GetConvention(typeof(T));
        }

        public T CreateInstance()
        {
            if (_isInterfaceOrAbstract)
            {
                throw new CborException("A CreatorMapping should be defined for interfaces or abstract classes");
            }

            return _constructor();
        }

        public override T Read(ref CborReader reader)
        {
            if (reader.ReadNull())
            {
                return null;
            }

            MapReaderContext context = new MapReaderContext
            {
                creatorValues = _objectMapping.CreatorMapping != null ? new Dictionary<RawString, object>() : null,
                regularValues = _objectMapping.CreatorMapping != null ? new Dictionary<RawString, object>() : null
            };

            reader.ReadMap(this, ref context);

            if (context.creatorValues != null)
            {
                context.obj = (T)_objectMapping.CreatorMapping.CreateInstance(context.creatorValues);
                if (_objectMapping.OnDeserializingMethod != null)
                {
                    ((Action<T>)_objectMapping.OnDeserializingMethod)(context.obj);
                }

                foreach (KeyValuePair<RawString, object> value in context.regularValues)
                {
                    if (!_memberConvertersForRead.TryGetValue(value.Key.Buffer.Span, out IMemberConverter memberConverter))
                    {
                        // should not happen
                        throw new CborException("Unexpected error");
                    }

                    memberConverter.Set(context.obj, value.Value);
                }
            }

            if (_objectMapping.OnDeserializedMethod != null)
            {
                ((Action<T>)_objectMapping.OnDeserializedMethod)(context.obj);
            }

            return context.obj;
        }

        public void ReadValue(ref CborReader reader, object obj, ReadOnlySpan<byte> memberName)
        {
            T value = (T)obj;

            if (!_memberConvertersForRead.TryGetValue(memberName, out IMemberConverter memberConverter))
            {
                HandleUnknownName(ref reader, typeof(T), memberName);
                reader.SkipDataItem();
            }
            else
            {
                memberConverter.Read(ref reader, value);
            }
        }

        public bool ReadValue(ref CborReader reader, ReadOnlySpan<byte> memberName, out object value)
        {
            if (!_memberConvertersForRead.TryGetValue(memberName, out IMemberConverter memberConverter))
            {
                HandleUnknownName(ref reader, typeof(T), memberName);
                reader.SkipDataItem();
                value = default;
                return false;
            }
            else
            {
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
                options = writer.Options,
                obj = value,
                lengthMode = lengthMode != LengthMode.Default 
                    ? lengthMode : _objectMapping.LengthMode != LengthMode.Default 
                        ? _objectMapping.LengthMode : writer.Options.MapLengthMode
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

                ReadOnlySpan<byte> memberName = reader.ReadRawString();

                if (context.creatorValues == null)
                {
                    context.obj = context.converter.CreateInstance();

                    if (_objectMapping.OnDeserializingMethod != null)
                    {
                        ((Action<T>)_objectMapping.OnDeserializingMethod)(context.obj);
                    }

                    context.converter.ReadValue(ref reader, context.obj, memberName);
                }
                else if (context.converter.ReadValue(ref reader, memberName, out object value))
                {
                    bool isCreatorValue = false;
                    foreach (RawString creatorMemberName in _objectMapping.CreatorMapping.MemberNames)
                    {
                        if (creatorMemberName.Buffer.Span.SequenceEqual(memberName))
                        {
                            isCreatorValue = true;
                            break;
                        }
                    }

                    if (isCreatorValue)
                    {
                        context.creatorValues.Add(new RawString(memberName), value);
                    }
                    else
                    {
                        context.regularValues.Add(new RawString(memberName), value);
                    }
                }
            }
            else
            {
                ReadOnlySpan<byte> memberName = reader.ReadRawString();
                context.converter.ReadValue(ref reader, context.obj, memberName);
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

            foreach (IMemberConverter member in context.objectConverter.MemberConvertersForWrite)
            {
                if (member.ShouldSerialize(context.obj, typeof(T), context.options))
                {
                    writableMembersCount++;
                }
            }

            return  writableMembersCount;
        }

        public bool WriteMapItem(ref CborWriter writer, ref MapWriterContext context)
        {
            while (context.memberIndex < context.objectConverter.MemberConvertersForWrite.Count)
            {
                IMemberConverter memberConverter = context.objectConverter.MemberConvertersForWrite[context.memberIndex++];
                if (memberConverter.ShouldSerialize(context.obj, typeof(T), context.options))
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
            if (reader.Options.UnhandledNameMode == UnhandledNameMode.ThrowException)
            {
                throw reader.BuildException("Unhandled name [{Encoding.ASCII.GetString(rawName)}] in class [{type.Name}] while deserializing.");
            }
        }
    }
}
