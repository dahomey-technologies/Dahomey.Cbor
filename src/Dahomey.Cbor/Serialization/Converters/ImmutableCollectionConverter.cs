using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Dahomey.Cbor.Serialization.Converters
{
    public class ImmutableCollectionConverter<TC, TI> :
        AbstractCollectionConverter<TC, TI>
        where TC : ICollection<TI>
    {
        private readonly Func<IEnumerable<TI>, TC> _createRangeDelegate;

        public ImmutableCollectionConverter(SerializationRegistry registry)
            : base(registry)
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
                .MakeGenericMethod(typeof(TI));
            _createRangeDelegate = (Func<IEnumerable<TI>, TC>)Delegate.CreateDelegate(typeof(Func<IEnumerable<TI>, TC>), methodInfo);
        }

        protected override TC InstantiateCollection(ICollection<TI> tempCollection)
        {
            return _createRangeDelegate(tempCollection);
        }

        protected override ICollection<TI> InstantiateTempCollection()
        {
            return new List<TI>();
        }
    }
}
