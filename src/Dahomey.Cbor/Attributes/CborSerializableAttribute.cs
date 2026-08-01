using System;

namespace Dahomey.Cbor.Attributes
{
    /// <summary>
    /// Declares that a type must be included in CBOR source generation for the annotated context.
    /// </summary>
    /// <remarks>
    /// Apply to a <c>partial</c> class deriving from
    /// <see cref="Serialization.CborSerializerContext"/>; the generator fills in the other half:
    /// <code>
    /// [CborSerializable(typeof(Person))]
    /// [CborSerializable(typeof(List&lt;Person&gt;))]
    /// public partial class MyContext : CborSerializerContext { }
    /// </code>
    /// Every closed generic the model graph touches must be declared — <c>List&lt;Person&gt;</c> as
    /// well as <c>Person</c> — because the AOT compiler only emits native code for instantiations it
    /// can see statically. The generator reports a missing declaration as a build error rather than
    /// letting it fail at run time.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class CborSerializableAttribute : Attribute
    {
        public CborSerializableAttribute(Type type)
        {
            Type = type ?? throw new ArgumentNullException(nameof(type));
        }

        /// <summary>The type to generate serialization support for.</summary>
        public Type Type { get; }
    }
}
