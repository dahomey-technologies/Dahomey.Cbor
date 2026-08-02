using System;

namespace Dahomey.Cbor.Attributes
{
    /// <summary>
    /// Emits an RFC 8610 CDDL schema for the types declared on the annotated context, as a
    /// <c>CddlSchema</c> constant.
    /// </summary>
    /// <remarks>
    /// The schema describes what the serializer <em>writes</em>: exact and closed. It is not a
    /// description of what the reader tolerates — <see cref="UnhandledNameMode"/> defaults to
    /// <see cref="UnhandledNameMode.Silent"/>, so unknown keys are accepted on read but never emitted.
    /// <code>
    /// [CborSerializable(typeof(Person))]
    /// [CborCddlSchema]
    /// public partial class MyContext : CborSerializerContext { }
    ///
    /// File.WriteAllText("person.cddl", MyContext.CddlSchema);
    /// </code>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class CborCddlSchemaAttribute : Attribute
    {
    }
}
