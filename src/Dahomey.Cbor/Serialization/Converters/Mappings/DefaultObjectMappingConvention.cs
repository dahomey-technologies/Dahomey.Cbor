using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization.Conventions;
using Dahomey.Cbor.Util;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Serialization;
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

            if (discriminatorAttribute != null)
            {
                objectMapping.SetDiscriminatorPolicy(discriminatorAttribute.Policy);
            }

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

                MemberMapping memberMapping = new MemberMapping(registry.ConverterRegistry, objectMapping, propertyInfo, propertyInfo.PropertyType);
                ProcessDefaultValue(propertyInfo, memberMapping);
                ProcessShouldSerializeMethod(memberMapping);
                memberMappings.Add(memberMapping);
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

                MemberMapping memberMapping = new MemberMapping(registry.ConverterRegistry, objectMapping, fieldInfo, fieldInfo.FieldType);
                ProcessDefaultValue(fieldInfo, memberMapping);
                ProcessShouldSerializeMethod(memberMapping);

                memberMappings.Add(memberMapping);
            }

            objectMapping.SetMemberMappings(memberMappings);

            ConstructorInfo[] constructorInfos = type.GetConstructors();

            ConstructorInfo constructorInfo = constructorInfos
                .FirstOrDefault(c => c.IsDefined(typeof(CborConstructorAttribute)));

            if (constructorInfo != null)
            {
                CborConstructorAttribute constructorAttribute = constructorInfo.GetCustomAttribute<CborConstructorAttribute>();
                CreatorMapping creatorMapping = objectMapping.MapCreator(constructorInfo);
                if (constructorAttribute.MemberNames != null)
                {
                    creatorMapping.SetMemberNames(constructorAttribute.MemberNames);
                }
            }
            // if no default constructor, pick up first one
            else if (constructorInfos.Length > 0 && !constructorInfos.Any(c => c.GetParameters().Length == 0))
            {
                constructorInfo = constructorInfos[0];

                CreatorMapping creatorMapping = objectMapping.MapCreator(constructorInfo);
                creatorMapping.SetMemberNames(constructorInfo.GetParameters()
                    .Select(p => new RawString(p.Name))
                    .ToList());
            }

            MethodInfo methodInfo = type.GetMethods()
                .FirstOrDefault(m => m.IsDefined(typeof(OnDeserializingAttribute)));
            if (methodInfo != null)
            {
                objectMapping.SetOnDeserializingMethod(GenerateCallbackDelegate<T>(methodInfo));
            }
            else if (type.GetInterfaces().Any(i => i == typeof(ISupportInitialize)))
            {
                objectMapping.SetOnDeserializingMethod(t => ((ISupportInitialize)t).BeginInit());
            }

            methodInfo = type.GetMethods()
                .FirstOrDefault(m => m.IsDefined(typeof(OnDeserializedAttribute)));
            if (methodInfo != null)
            {
                objectMapping.SetOnDeserializedMethod(GenerateCallbackDelegate<T>(methodInfo));
            }
            else if (type.GetInterfaces().Any(i => i == typeof(ISupportInitialize)))
            {
                objectMapping.SetOnDeserializedMethod(t => ((ISupportInitialize)t).EndInit());
            }

            methodInfo = type.GetMethods()
                .FirstOrDefault(m => m.IsDefined(typeof(OnSerializingAttribute)));
            if (methodInfo != null)
            {
                objectMapping.SetOnSerializingMethod(GenerateCallbackDelegate<T>(methodInfo));
            }

            methodInfo = type.GetMethods()
                .FirstOrDefault(m => m.IsDefined(typeof(OnSerializedAttribute)));
            if (methodInfo != null)
            {
                objectMapping.SetOnSerializedMethod(GenerateCallbackDelegate<T>(methodInfo));
            }
        }

        private void ProcessDefaultValue(MemberInfo memberInfo, MemberMapping memberMapping)
        {
            DefaultValueAttribute defaultValueAttribute = memberInfo.GetCustomAttribute<DefaultValueAttribute>();
            if (defaultValueAttribute != null)
            {
                memberMapping.SetDefaultValue(defaultValueAttribute.Value);
            }

            if (memberInfo.IsDefined(typeof(CborIgnoreIfDefaultAttribute)))
            {
                memberMapping.SetIngoreIfDefault(true);
            }
        }

        private void ProcessShouldSerializeMethod(MemberMapping memberMapping)
        {
            string shouldSerializeMethodName = "ShouldSerialize" + memberMapping.MemberInfo.Name;
            Type objectType = memberMapping.MemberInfo.DeclaringType;

            MethodInfo shouldSerializeMethodInfo = objectType.GetMethod(shouldSerializeMethodName, new Type[] { });
            if (shouldSerializeMethodInfo != null &&
                shouldSerializeMethodInfo.IsPublic &&
                shouldSerializeMethodInfo.ReturnType == typeof(bool))
            {
                // obj => ((TClass) obj).ShouldSerializeXyz()
                ParameterExpression objParameter = Expression.Parameter(typeof(object), "obj");
                Expression<Func<object, bool>> lambdaExpression = Expression.Lambda<Func<object, bool>>(
                    Expression.Call(
                        Expression.Convert(objParameter, objectType), 
                        shouldSerializeMethodInfo), 
                    objParameter);

                memberMapping.SetShouldSerializeMethod(lambdaExpression.Compile());
            }
        }

        private Action<T> GenerateCallbackDelegate<T>(MethodInfo methodInfo)
        {
            // obj => obj.Callback()
            ParameterExpression objParameter = Expression.Parameter(typeof(T), "obj");
            Expression<Action<T>> lambdaExpression = Expression.Lambda<Action<T>>(
                    Expression.Call(
                        Expression.Convert(objParameter, typeof(T)),
                        methodInfo),
                objParameter);

            return lambdaExpression.Compile();
        }
    }
}
