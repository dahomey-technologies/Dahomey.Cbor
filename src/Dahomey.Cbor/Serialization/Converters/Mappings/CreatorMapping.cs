using Dahomey.Cbor.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Dahomey.Cbor.Serialization.Converters.Mappings
{
    public class CreatorMapping : ICreatorMapping
    {
        private readonly IObjectMapping _objectMapping;
        private readonly Delegate _delegate;
        private readonly ParameterInfo[] _parameters;
        private List<RawString> _memberNames = null;
        private List<object> _defaultValues = null;

        public IReadOnlyCollection<RawString> MemberNames
        {
            get
            {
                if (_memberNames == null)
                {
                    Initialize();
                }

                return _memberNames;
            }
        }

        public CreatorMapping(IObjectMapping objectMapping, ConstructorInfo constructorInfo)
        {
            _objectMapping = objectMapping;
            _delegate = constructorInfo.CreateDelegate();
            _parameters = constructorInfo.GetParameters();
        }

        public CreatorMapping(IObjectMapping objectMapping, Delegate @delegate)
        {
            _objectMapping = objectMapping;
            _delegate = @delegate;
            _parameters = @delegate.Method.GetParameters();
        }

        public CreatorMapping(IObjectMapping objectMapping, MethodInfo method)
        {
            _objectMapping = objectMapping;
            _delegate = method.CreateDelegate();
            _parameters = method.GetParameters();
        }

        public object CreateInstance(Dictionary<RawString, object> values)
        {
            if (_memberNames == null)
            {
                Initialize();
            }

            object[] args = new object[_memberNames.Count];

            for (int i = 0; i < _memberNames.Count; i++)
            {
                if (values.TryGetValue(_memberNames[i], out object value))
                {
                    args[i] = value;
                }
                else
                {
                    args[i] = _defaultValues[i];
                }
            }

            return _delegate.DynamicInvoke(args);
        }

        private void Initialize()
        {
            IReadOnlyCollection<IMemberMapping> memberMappings = _objectMapping.MemberMappings;
            _memberNames = new List<RawString>(_parameters.Length);
            _defaultValues = new List<object>(_parameters.Length);

            foreach(ParameterInfo parameter in _parameters)
            {
                IMemberMapping memberMapping = memberMappings
                    .FirstOrDefault(m => string.Compare(m.MemberInfo.Name, parameter.Name, ignoreCase: true) == 0);

                if (memberMapping == null)
                {
                    throw new CborException($"Cannot find a field or property named {parameter.Name} on type {_objectMapping.ObjectType.FullName}");
                }

                if (memberMapping.MemberType != parameter.ParameterType)
                {
                    throw new CborException($"Type mismatch between creator argument and field or property named {parameter.Name} on type {_objectMapping.ObjectType.FullName}");
                }

                _memberNames.Add(new RawString(memberMapping.MemberName, Encoding.ASCII));
                _defaultValues.Add(memberMapping.DefaultValue);
            }
        }
    }
}
