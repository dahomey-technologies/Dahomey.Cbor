using System;

namespace Dahomey.Cbor.Attributes
{
    /// <summary>
    /// Defines how a struct/class is serialized/deserialized in CBOR
    /// </summary>
    public enum CborObjectFormat
    {
        /// <summary>
        /// CBOR Map with string keys, which is the default format
        /// </summary>
        StringKeyMap,

        /// <summary>
        /// CBOR Map with integer keys
        /// </summary>
        /// Integer keys are defined with CborPropertyAttribute.Index
        IntKeyMap,

        /// <summary>
        /// CBOR Array
        /// </summary>
        /// Array indexes are defined with CborPropertyAttribute.Index
        Array
    }

    /// <summary>
    ///  Defines how a struct/class is serialized/deserialized in CBOR
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public class CborObjectFormatAttribute : Attribute
    {
        public CborObjectFormat ObjectFormat { get; set; }

        public CborObjectFormatAttribute(CborObjectFormat objectFormat)
        {
            ObjectFormat = objectFormat;
        }
    }
}
