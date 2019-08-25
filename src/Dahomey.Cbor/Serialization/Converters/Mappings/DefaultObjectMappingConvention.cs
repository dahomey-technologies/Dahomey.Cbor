using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization.Conventions;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Dahomey.Cbor.Serialization.Converters.Mappings
{
    public class DefaultObjectMappingConvention : IObjectMappingConvention
    {
        public void Apply<T>(SerializationRegistry registry, ObjectMapping<T> objectMapping) where T : class
        {
            Type type = objectMapping.ObjectType;
            List<MemberMapping> memberMappings = new List<MemberMapping>();

            ReadOnlyMemory<byte> typeName = Encoding.ASCII.GetBytes($"{type.FullName}, {type.Assembly.GetName().Name}");
            CborDiscriminatorAttribute discriminatorAttribute = type.GetCustomAttribute<CborDiscriminatorAttribute>();

            ReadOnlyMemory<byte> discriminator = Encoding.UTF8.GetBytes(discriminatorAttribute != null ?
                discriminatorAttribute.Discriminator :
                $"{type.FullName}, {type.Assembly.GetName().Name}");

            registry.DefaultDiscriminatorConvention.RegisterType(type, discriminator);

            Type namingConventionType = type.GetCustomAttribute<CborNamingConventionAttribute>()?.NamingConventionType;
            if (namingConventionType != null)
            {
                objectMapping.SetNamingConvention((INamingConvention)Activator.CreateInstance(namingConventionType));
            }

            PropertyInfo[] properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            foreach (PropertyInfo propertyInfo in properties)
            {
                if (propertyInfo.IsDefined(typeof(CborIgnoreAttribute)))
                {
                    continue;
                }

                if ((propertyInfo.GetMethod.IsPrivate || propertyInfo.GetMethod.IsStatic) && !propertyInfo.IsDefined(typeof(CborPropertyAttribute)))
                {
                    continue;
                }

                memberMappings.Add(new MemberMapping(registry.ConverterRegistry, objectMapping, propertyInfo, propertyInfo.PropertyType));
            }

            foreach (FieldInfo fieldInfo in fields)
            {
                if (fieldInfo.IsDefined(typeof(CborIgnoreAttribute)))
                {
                    continue;
                }

                if ((fieldInfo.IsPrivate || fieldInfo.IsStatic) && !fieldInfo.IsDefined(typeof(CborPropertyAttribute)))
                {
                    continue;
                }

                Type fieldType = fieldInfo.FieldType;

                memberMappings.Add(new MemberMapping(registry.ConverterRegistry, objectMapping, fieldInfo, fieldInfo.FieldType));
            }

            objectMapping.SetMemberMappings(memberMappings);
        }
    }
}
