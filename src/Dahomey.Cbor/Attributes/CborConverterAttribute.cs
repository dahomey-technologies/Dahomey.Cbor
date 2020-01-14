using System;
using System.Collections.Generic;
using System.Text;

namespace Dahomey.Cbor.Attributes
{
    [AttributeUsage(AttributeTargets.Class|AttributeTargets.Property|AttributeTargets.Field)]
    public class CborConverterAttribute : Attribute
    {
        public Type ConverterType { get; }

        public CborConverterAttribute(Type converterType)
        {
            ConverterType = converterType;
        }
    }
}
