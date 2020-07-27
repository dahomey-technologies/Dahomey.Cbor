﻿using Dahomey.Cbor.Util;
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
        private List<RawString>? _memberNames = null;
        private List<object?>? _defaultValues = null;

        public IReadOnlyCollection<RawString>? MemberNames => _memberNames;

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

        public void SetMemberNames(IReadOnlyCollection<RawString> memberNames)
        {
            _memberNames = memberNames.ToList();
        }

        public void SetMemberNames(params string[] memberNames)
        {
            _memberNames = memberNames
                .Select(m => new RawString(m))
                .ToList();
        }

        object ICreatorMapping.CreateInstance(Dictionary<RawString, object> values)
        {
            if (_memberNames == null || _defaultValues == null)
            {
                throw new CborException("Initialize has not been called");
            }

            object?[] args = new object[_memberNames.Count];

            for (int i = 0; i < _memberNames.Count; i++)
            {
                if (values.TryGetValue(_memberNames[i], out object? value))
                {
                    args[i] = value;
                }
                else
                {
                    args[i] = _defaultValues[i];
                }
            }

            return _delegate.DynamicInvoke(args) ?? throw new InvalidOperationException("Cannot instantiate type");
        }

        public void Initialize()
        {
            bool createMemberNames = _memberNames == null;

            IReadOnlyCollection<IMemberMapping> memberMappings = _objectMapping.MemberMappings;
            if (_memberNames == null)
            {
                _memberNames = new List<RawString>(_parameters.Length);
            }
            else if (_memberNames.Count != _parameters.Length)
            {
                throw new CborException($"Size mismatch between creator parameters and member names");
            }

            _defaultValues = new List<object?>(_parameters.Length);

            for (int i = 0; i < _parameters.Length; i++)
            {
                ParameterInfo parameter = _parameters[i];
                IMemberMapping? memberMapping;

                if (createMemberNames)
                {
                    memberMapping = memberMappings
                        .Where(m => !(m is IDiscriminatorMapping))
                        .FirstOrDefault(m => string.Compare(m.MemberName, parameter.Name, ignoreCase: true) == 0);

                    if (memberMapping == null || memberMapping.MemberName == null)
                    {
                        _memberNames.Add(RawString.Empty);
                    }
                    else
                    {
                        _memberNames.Add(new RawString(memberMapping.MemberName, Encoding.ASCII));
                    }
                }
                else
                {
                    memberMapping = memberMappings
                        .FirstOrDefault(m => string.Compare(m.MemberName, _memberNames[i].ToString(), ignoreCase: true) == 0);

                    if (memberMapping == null)
                    {
                        throw new CborException($"Cannot find a field or property named {_memberNames[i]} on type {_objectMapping.ObjectType.FullName}");
                    }
                }

                if (memberMapping != null)
                {
                    if (memberMapping.MemberType != parameter.ParameterType)
                    {
                        throw new CborException($"Type mismatch between creator argument and field or property named {parameter.Name} on type {_objectMapping.ObjectType.FullName}");
                    }

                    _defaultValues.Add(memberMapping.DefaultValue);
                }
                else
                {
                    _defaultValues.Add(parameter.ParameterType.GetDefaultValue());
                }
            }
        }
    }
}
