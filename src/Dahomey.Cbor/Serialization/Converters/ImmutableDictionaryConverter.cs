using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Dahomey.Cbor.Serialization.Converters
{
    public class ImmutableDictionaryConverter<TC, TK, TV> :
        AbstractDictionaryConverter<TC, TK, TV>
        where TC : IDictionary<TK, TV>
        where TK : notnull
    {
        private readonly Func<IEnumerable<KeyValuePair<TK, TV>>, TC> _createRangeDelegate;

        public ImmutableDictionaryConverter(CborOptions options)
            : base(options)
        {
            string? typeFullName = typeof(TC).GetGenericTypeDefinition().FullName;

            if (typeFullName == null)
            {
                throw new CborException($"Cannot find {typeof(TC)} full name");
            }

            string staticTypeFullName = typeFullName.Substring(0, typeFullName.Length - 2);
            Assembly assembly = typeof(TC).Assembly;
            Type? type = assembly.GetType(staticTypeFullName);

            if (type == null)
            {
                throw new CborException($"Cannot find type from {staticTypeFullName}");
            }

            MethodInfo methodInfo = type.GetMethods(BindingFlags.Static | BindingFlags.Public)
                .First(m => m.IsGenericMethod && m.GetGenericMethodDefinition().Name == "CreateRange")
                .MakeGenericMethod(typeof(TK), typeof(TV));
            _createRangeDelegate = (Func<IEnumerable<KeyValuePair<TK, TV>>, TC>)Delegate.CreateDelegate(typeof(Func<IEnumerable<KeyValuePair<TK, TV>>, TC>), methodInfo);
        }

        protected override TC InstantiateCollection(IDictionary<TK, TV> tempCollection)
        {
            return _createRangeDelegate(tempCollection);
        }

        protected override IDictionary<TK, TV> InstantiateTempCollection()
        {
            return new Dictionary<TK, TV>();
        }
    }
}
