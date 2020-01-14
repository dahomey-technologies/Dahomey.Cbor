using Dahomey.Cbor.Util;
using System;

namespace Dahomey.Cbor.Serialization.Converters.Mappings
{
    public class AnonymousObjectMappingConventionProvider : IObjectMappingConventionProvider
    {
        private static readonly AnonymousObjectMappingConvention _anonymousObjectMappingConvention
            = new AnonymousObjectMappingConvention();

        public IObjectMappingConvention? GetConvention(Type type)
        {
            if (type.IsAnonymous())
            {
                return _anonymousObjectMappingConvention;
            }

            return null;
        }
    }
}
