using Dahomey.Cbor.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace Dahomey.Cbor.Serialization.Converters.Mappings
{
    public class CreatorMapping : ICreatorMapping
    {
        private bool _isInitialized = false;
        private readonly IObjectMapping _objectMapping;
        private readonly Delegate _delegate;
        private readonly ParameterInfo[] _parameters;
        private List<RawString>? _memberNames = null;
        private List<int>? _memberIndexes = null;
        private List<object?>? _defaultValues = null;

        public IReadOnlyCollection<RawString>? MemberNames
        {
            get
            {
                EnsureInitialize();
                return _memberNames;
            }
        }

        public IReadOnlyCollection<int>? MemberIndexes
        {
            get
            {
                EnsureInitialize();
                return _memberIndexes;
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

        public void SetMemberIndexes(IReadOnlyCollection<int> memberIndexes)
        {
            _memberIndexes = memberIndexes.ToList();
        }

        public void SetMemberIndexes(params int[] memberIndexes)
        {
            _memberIndexes = memberIndexes.ToList();
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

        object ICreatorMapping.CreateInstance(Dictionary<int, object> values)
        {
            if (_memberIndexes == null || _defaultValues == null)
            {
                throw new CborException("Initialize has not been called");
            }

            object?[] args = new object[_memberIndexes.Count];

            for (int i = 0; i < _memberIndexes.Count; i++)
            {
                if (values.TryGetValue(_memberIndexes[i], out object? value))
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

        private void EnsureInitialize()
        {
            if (!_isInitialized)
            {
                lock (this)
                {
                    if (!_isInitialized)
                    {
                        switch (_objectMapping.ObjectFormat)
                        {
                            case Attributes.CborObjectFormat.StringKeyMap:
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
                                break;
                            case Attributes.CborObjectFormat.IntKeyMap:
                            case Attributes.CborObjectFormat.Array:
                                {
                                    bool createMemberIndexes = _memberIndexes == null;

                                    IReadOnlyCollection<IMemberMapping> memberMappings = _objectMapping.MemberMappings;
                                    if (_memberIndexes == null)
                                    {
                                        _memberIndexes = new List<int>(_parameters.Length);
                                    }
                                    else if (_memberIndexes.Count != _parameters.Length)
                                    {
                                        throw new CborException($"Size mismatch between creator parameters and member indexes");
                                    }

                                    _defaultValues = new List<object?>(_parameters.Length);

                                    for (int i = 0; i < _parameters.Length; i++)
                                    {
                                        ParameterInfo parameter = _parameters[i];
                                        IMemberMapping? memberMapping;

                                        if (createMemberIndexes)
                                        {
                                            memberMapping = memberMappings
                                                .Where(m => !(m is IDiscriminatorMapping))
                                                .FirstOrDefault(m => string.Compare(m.MemberInfo.Name, parameter.Name, ignoreCase: true) == 0);

                                            if (memberMapping == null || !memberMapping.MemberIndex.HasValue)
                                            {
                                                _memberIndexes.Add(0);
                                            }
                                            else
                                            {
                                                _memberIndexes.Add(memberMapping.MemberIndex.Value);
                                            }
                                        }
                                        else
                                        {
                                            memberMapping = memberMappings
                                                .FirstOrDefault(m => m.MemberIndex == _memberIndexes[i]);

                                            if (memberMapping == null)
                                            {
                                                throw new CborException($"Cannot find a field or property which index is {_memberIndexes[i]} on type {_objectMapping.ObjectType.FullName}");
                                            }
                                        }

                                        if (memberMapping != null)
                                        {
                                            if (memberMapping.MemberType != parameter.ParameterType)
                                            {
                                                throw new CborException($"Type mismatch between creator argument and field or property named {parameter.Name} with index {memberMapping.MemberIndex} on type {_objectMapping.ObjectType.FullName}");
                                            }

                                            _defaultValues.Add(memberMapping.DefaultValue);
                                        }
                                        else
                                        {
                                            _defaultValues.Add(parameter.ParameterType.GetDefaultValue());
                                        }
                                    }
                                }
                                break;
                        }

                        _isInitialized = true;
                    }
                }
            }
        }
    }
}
