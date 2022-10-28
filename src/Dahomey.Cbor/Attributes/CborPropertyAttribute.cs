using System;

namespace Dahomey.Cbor.Attributes
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class CborPropertyAttribute : Attribute
    {
        /// <summary>
        /// Name of the property as it will be serialized/deserialized to/from CBOR
        /// </summary>
        public string? PropertyName { get; private set; }

        /// <summary>
        /// Index of the property used in CBOR serialization/deserialization
        /// </summary>
        /// - If CborArrayAttribute annotates the class, Index is interpreted as an index in a CBOR array
        /// - Otherwise, Index is interpreted as an interger key in a CBOR map
        public int? Index { get; private set; }

        public CborPropertyAttribute()
        {
        }

        public CborPropertyAttribute(string propertyName)
        {
            PropertyName = propertyName;
        }

        public CborPropertyAttribute(int index)
        {
            Index = index;
        }
    }
}
