// See CddlSchemaTests.cs for why "annotations", not plain "enable".
#nullable enable annotations
using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Tests.Extensions;
using Xunit;

namespace Dahomey.Cbor.Tests.Cddl
{
    /// <summary>
    /// Exercises RULING B in full: a reference-type use site renders <c>X / nil</c> unless its
    /// <c>ITypeSymbol.NullableAnnotation</c> is <c>NotAnnotated</c>. <see cref="CddlPerson"/> in
    /// <see cref="CddlSchemaTests"/> only ever exercises that one (<c>NotAnnotated</c>) branch, since
    /// its sole reference-type member is a bare <c>string</c> -- so nothing proves <c>string?</c>
    /// renders <c>tstr / nil</c>, that a member declared with nullable annotations off does too, or
    /// that the reference gem actually accepts <c>tstr / nil</c> for a member that is really null on
    /// the wire. This is a separate fixture and context precisely so it can add that coverage without
    /// touching <see cref="CddlPerson"/>'s pinned schema.
    /// </summary>
    public class CddlNullableAnnotations
    {
        /// <summary>NotAnnotated: declared plain, inside this file's active `#nullable enable
        /// annotations` context. Must render the bare rule.</summary>
        public string Required { get; set; }

        /// <summary>Annotated (<c>string?</c>). Must render <c>tstr / nil</c>.</summary>
        public string? Optional { get; set; }

#nullable disable annotations
        // None: nullable annotations are off for this one declaration -- the same state every
        // reference-type member in this codebase's test project is in by default (Nullable is
        // disabled there; see CLAUDE.md). RULING B treats this the same as Annotated, not the same
        // as NotAnnotated: an unannotated context is not a promise of non-null, so this must also
        // render `tstr / nil`.
        public string Oblivious { get; set; }
#nullable enable annotations
    }

    [CborSerializable(typeof(CddlNullableAnnotations))]
    [CborCddlSchema]
    public partial class CddlNullableContext : CborSerializerContext
    {
    }

    public class CddlNullableAnnotationTests
    {
        private static readonly CddlNullableContext Context =
            CborSerializerContext.Default<CddlNullableContext>();

        [Fact]
        public void NotAnnotatedRendersTheBareRule()
        {
            Assert.Contains(
                "\"Required\": tstr,\n",
                CddlNullableContext.CddlSchema.Replace("\r\n", "\n"));
        }

        [Fact]
        public void AnnotatedRendersNilable()
        {
            Assert.Contains(
                "\"Optional\": tstr / nil,\n",
                CddlNullableContext.CddlSchema.Replace("\r\n", "\n"));
        }

        [Fact]
        public void UnannotatedContextAlsoRendersNilable()
        {
            Assert.Contains(
                "\"Oblivious\": tstr / nil,\n",
                CddlNullableContext.CddlSchema.Replace("\r\n", "\n"));
        }

        /// <summary>
        /// The assertion RULING B exists for: the two members the schema declares nilable really do
        /// go out as CBOR null (0xF6) when left null, and the gem accepts that against the emitted
        /// `tstr / nil` schema rather than rejecting it the way a bare `tstr` would.
        /// </summary>
        [CddlFact]
        public void NullNilableMembersValidateAgainstTheSchema()
        {
            CddlNullableAnnotations value = new CddlNullableAnnotations
            {
                Required = "x",
                Optional = null,
                Oblivious = null,
            };

            string hex = Helper.Write(value, Context.Options);

            // Sanity on the premise: the null members actually went out as F6, not merely omitted or
            // some other encoding RULING B would not need to cover.
            Assert.Contains("F6", hex);

            byte[] cbor = hex.HexToBytes();

            CddlResult result = CddlTool.Validate(
                CddlNullableContext.CddlSchema, "CddlNullableAnnotations", cbor);

            Assert.True(result.Ok, result.Output);
        }
    }
}
