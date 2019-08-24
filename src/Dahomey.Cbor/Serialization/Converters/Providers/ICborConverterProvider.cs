using System;

namespace Dahomey.Cbor.Serialization.Converters.Providers
{
    /// <summary>
    /// An interface implemented by converter providers.
    /// </summary>
    public interface ICborConverterProvider
    {
        /// <summary>
        /// Gets a converter for a type.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="registry">The serialization registry.</param>
        /// <returns>
        /// A converter
        /// </returns>
        ICborConverter GetConverter(Type type, SerializationRegistry registry);
    }
}
