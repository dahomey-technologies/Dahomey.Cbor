using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization.Conventions;
using Dahomey.Cbor.Util;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Dahomey.Cbor.Serialization.Converters
{
    public interface IObjectConverter
    {
        Type Type { get; }
        ReadOnlyMemory<byte> ClassName { get; }
        ReadOnlyMemory<byte> Discriminator { get; }
        INamingConvention NamingConvention { get; }
        void ReadValue(ref CborReader reader, object obj, ReadOnlySpan<byte> memberName);
        List<IMemberConverter> MemberConvertersForWrite { get; }
    }

    public interface IObjectConverter<out T> : IObjectConverter
        where T : class, new()
    {
        T CreateInstance();
    }

    public class ObjectConverter<T> :
        CborConverterBase<T>,
        IObjectConverter<T>,
        ICborMapReader<ObjectConverter<T>.MapReaderContext>,
        ICborMapWriter<ObjectConverter<T>.MapWriterContext>
        where T : class, new()
    {
        public struct MapReaderContext
        {
            public T obj;
            public IObjectConverter<T> converter;
        }

        public struct MapWriterContext
        {
            public enum State
            {
                Discriminator,
                Properties
            }

            public State state;
            public T obj;
            public int memberIndex;
            public IObjectConverter objectConverter;
        }

        private readonly ByteBufferDictionary<IMemberConverter> _memberConvertersForRead = new ByteBufferDictionary<IMemberConverter>();
        private readonly List<IMemberConverter> _memberConvertersForWrite;

        public Type Type { get; } = typeof(T);
        public ReadOnlyMemory<byte> ClassName { get; private set; }
        public ReadOnlyMemory<byte> Discriminator { get; private set; }
        public INamingConvention NamingConvention { get; private set; }
        public List<IMemberConverter> MemberConvertersForWrite => _memberConvertersForWrite;


        public ObjectConverter()
        {
            Type type = typeof(T);

            ClassName = Encoding.ASCII.GetBytes(string.Format("{0}, {1}", type.FullName, type.Assembly.GetName().Name));
            CborDiscriminatorAttribute discriminatorAttribute = type.GetCustomAttribute<CborDiscriminatorAttribute>();

            Discriminator = discriminatorAttribute != null ?
                Encoding.ASCII.GetBytes(discriminatorAttribute.Discriminator) :
                ClassName;

            Type namingConventionType = type.GetCustomAttribute<CborNamingConventionAttribute>()?.NamingConventionType;
            if (namingConventionType != null)
            {
                NamingConvention = (INamingConvention)Activator.CreateInstance(namingConventionType);
            }

            PropertyInfo[] properties = type.GetProperties();
            FieldInfo[] fields = type.GetFields();

            _memberConvertersForWrite = new List<IMemberConverter>(properties.Length + fields.Length);

            foreach (PropertyInfo propertyInfo in properties)
            {
                if (propertyInfo.IsDefined(typeof(CborIgnoreAttribute)))
                {
                    return;
                }

                Type propertyType = propertyInfo.PropertyType;

                IMemberConverter propertyConverter = (IMemberConverter)Activator.CreateInstance(
                                    typeof(PropertyConverter<,>).MakeGenericType(typeof(T), propertyType),
                                    propertyInfo, this);

                _memberConvertersForRead.Add(propertyConverter.MemberName, propertyConverter);
                _memberConvertersForWrite.Add(propertyConverter);
            }

            foreach (FieldInfo fieldInfo in fields)
            {
                if (fieldInfo.IsDefined(typeof(CborIgnoreAttribute)))
                {
                    return;
                }

                Type fieldType = fieldInfo.FieldType;

                IMemberConverter propertyConverter = (IMemberConverter)Activator.CreateInstance(
                                    typeof(FieldConverter<,>).MakeGenericType(typeof(T), fieldType),
                                    fieldInfo, this);

                _memberConvertersForRead.Add(propertyConverter.MemberName, propertyConverter);
                _memberConvertersForWrite.Add(propertyConverter);
            }
        }

        public T CreateInstance()
        {
            return new T();
        }

        public override T Read(ref CborReader reader)
        {
            if (reader.ReadNull())
            {
                return null;
            }

            MapReaderContext context = new MapReaderContext();
            reader.ReadMap(this, ref context);
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

        public override void Write(ref CborWriter writer, T value)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            MapWriterContext context = new MapWriterContext
            {
                obj = value,
            };

            if (value.GetType() != typeof(T))
            {
                context.state = MapWriterContext.State.Discriminator;
                context.objectConverter = (IObjectConverter)CborConverter.Lookup(value.GetType());
            }
            else
            {
                context.state = MapWriterContext.State.Properties;
                context.objectConverter = this;
            }

            writer.WriteMap(this, ref context);
        }

        public void ReadBeginMap(int size, ref MapReaderContext context)
        {
        }

        public void ReadMapItem(ref CborReader reader, ref MapReaderContext context)
        {
            // name
            ReadOnlySpan<byte> memberName = reader.ReadRawString();

            if (context.obj == null)
            {
                IDiscriminatorConvention discriminatorConvention = reader.Options.DiscriminatorConvention;

                if (memberName.SequenceEqual(discriminatorConvention.MemberName))
                {
                    // discriminator value
                    Type actualType = discriminatorConvention.ReadDiscriminator(ref reader);
                    context.converter = (IObjectConverter<T>)CborConverter.Lookup(actualType);
                    context.obj = context.converter.CreateInstance();
                }
                else
                {
                    context.converter = this;
                    context.obj = context.converter.CreateInstance();
                    context.converter.ReadValue(ref reader, context.obj, memberName);

                }
            }
            else
            {
                context.converter.ReadValue(ref reader, context.obj, memberName);
            }
        }

        public int GetMapSize(ref MapWriterContext context)
        {
            return context.state == MapWriterContext.State.Discriminator
                ? context.objectConverter.MemberConvertersForWrite.Count + 1
                : context.objectConverter.MemberConvertersForWrite.Count;
        }

        public void WriteMapItem(ref CborWriter writer, ref MapWriterContext context)
        {
            if (context.state == MapWriterContext.State.Discriminator)
            {
                writer.Options.DiscriminatorConvention.WriteDiscriminator<T>(ref writer, context.obj.GetType());
                context.state = MapWriterContext.State.Properties;
                return;
            }

            IMemberConverter memberConverter = context.objectConverter.MemberConvertersForWrite[context.memberIndex++];
            writer.WriteString(memberConverter.MemberName);
            memberConverter.Write(ref writer, context.obj);
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
