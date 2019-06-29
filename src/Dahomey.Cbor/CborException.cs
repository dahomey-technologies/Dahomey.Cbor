using System;

namespace Dahomey.Cbor
{
    public class CborException : Exception
    {
        public CborException(string message)
            : base(message)
        {
        }
    }
}
