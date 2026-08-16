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
        /// <summary>
        /// Labels each entry of a <see cref="CborObjectFormat.Array"/> rule with its member name, as
        /// RFC 8610 permits. Off by default, because the labels are documentation rather than part of
        /// the encoding.
        /// </summary>
        /// <remarks>
        /// An array rule is otherwise a list of bare types, which says nothing about what each
        /// position means:
        /// <code>
        /// MeasurementRecord = [ uint, uint, float, ]        // MemberNames = false
        /// MeasurementRecord = [ TimestampMs: uint, Sequence: uint, Depth: float, ]
        /// </code>
        /// In an array context a member key is documentation and does not change what validates
        /// against the rule, so turning this on is not a wire change.
        /// <para>
        /// It matters to code generators, which is the reason it exists. A generator that derives C
        /// struct fields from an array rule has only the entry's type to name a field after, so two
        /// members of one type collide — three <c>float</c> members become three fields called
        /// <c>MeasurementRecord_float</c>, which is not compilable output. The labels give each
        /// position a distinct name.
        /// </para>
        /// Labels are derived from the C# member name, folded to an ASCII identifier and made unique
        /// within the rule, so they are usable by generators that emit C identifiers from them.
        /// </remarks>
        public bool MemberNames { get; set; }
    }
}
