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
            List<MemberMapping<T>> memberMappings = new List<MemberMapping<T>>();

            CborDiscriminatorAttribute discriminatorAttribute = type.GetCustomAttribute<CborDiscriminatorAttribute>();

            if (discriminatorAttribute != null)
            {
                objectMapping.SetDiscriminator(discriminatorAttribute.Discriminator);
                objectMapping.SetDiscriminatorPolicy(discriminatorAttribute.Policy);
            }

            Type namingConventionType = type.GetCustomAttribute<CborNamingConventionAttribute>()?.NamingConventionType;
            if (namingConventionType != null)
            {
                objectMapping.SetNamingConvention((INamingConvention)Activator.CreateInstance(namingConventionType));
            }

            CborLengthModeAttribute lengthModeAttribute = type.GetCustomAttribute<CborLengthModeAttribute>();
            if (lengthModeAttribute != null)
            {
                objectMapping.SetLengthMode(lengthModeAttribute.LengthMode);
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

                MemberMapping<T> memberMapping = new MemberMapping<T>(registry.ConverterRegistry, objectMapping, propertyInfo, propertyInfo.PropertyType);
                ProcessDefaultValue(propertyInfo, memberMapping);
                ProcessShouldSerializeMethod(memberMapping);
                ProcessLengthMode(propertyInfo, memberMapping);
                ProcessRequired(propertyInfo, memberMapping);
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

                MemberMapping<T> memberMapping = new MemberMapping<T>(registry.ConverterRegistry, objectMapping, fieldInfo, fieldInfo.FieldType);
                ProcessDefaultValue(fieldInfo, memberMapping);
                ProcessShouldSerializeMethod(memberMapping);
                ProcessLengthMode(fieldInfo, memberMapping);
                ProcessRequired(fieldInfo, memberMapping);

                memberMappings.Add(memberMapping);
            }

            objectMapping.AddMemberMappings(memberMappings);

            ConstructorInfo[] constructorInfos = type.GetConstructors(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

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
                objectMapping.MapCreator(constructorInfo);
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

        private void ProcessDefaultValue<T>(MemberInfo memberInfo, MemberMapping<T> memberMapping) where T : class
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

        private void ProcessShouldSerializeMethod<T>(MemberMapping<T> memberMapping) where T : class
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

        private void ProcessLengthMode<T>(MemberInfo memberInfo, MemberMapping<T> memberMapping) where T : class
        {
            CborLengthModeAttribute lengthModeAttribute = memberInfo.GetCustomAttribute<CborLengthModeAttribute>();
            if (lengthModeAttribute != null)
            {
                memberMapping.SetLengthMode(lengthModeAttribute.LengthMode);
            }
        }

        private void ProcessRequired<T>(MemberInfo memberInfo, MemberMapping<T> memberMapping) where T : class
        {
            CborRequiredAttribute jsonRequiredAttribute = memberInfo.GetCustomAttribute<CborRequiredAttribute>();
            if (jsonRequiredAttribute != null)
            {
                memberMapping.SetRequired(jsonRequiredAttribute.Policy);
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
