using System;
using System.Collections.Generic;
using System.Text;

namespace Dahomey.Cbor.Serialization.Conventions
{
    public interface INamingConvention
    {
        ReadOnlyMemory<byte> GetPropertyName(string name);
    }
}
