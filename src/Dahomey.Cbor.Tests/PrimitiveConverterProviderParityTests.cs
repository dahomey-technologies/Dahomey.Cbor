using Dahomey.Cbor.Serialization.Converters.Providers;
using Dahomey.Cbor.Tests.Cddl;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    /// <summary>
    /// Holds <c>PrimitiveConverterProvider</c> and the generator's <c>TypeCollector.IsPrimitive</c> in
    /// step. The two are a MATCHED PAIR that cannot share a list — the generator is an analyzer
    /// assembly and must not reference the runtime library — so nothing but a test can catch a one-sided
    /// edit.
    /// </summary>
    /// <remarks>
    /// Both directions are failures, and the dangerous one is silent. A type the collector omits while
    /// the provider resolves it is not refused: it is a struct, so it falls to <c>TypeKind.Object</c>
    /// and the generated context registers an <c>ObjectConverter</c> over members the reflection path
    /// never writes — a green build and the wrong bytes. That is why the assertion inspects the emitted
    /// source rather than only the diagnostics: with the type absent from both <c>IsPrimitive</c> and
    /// <c>HasNoConcreteConverter</c>, no diagnostic is reported at all. In the other direction a type
    /// the collector names while the provider resolves nothing is registered nowhere and reaches
    /// <c>ObjectConverterProvider</c> and <c>MakeGenericType</c> at run time — under Native AOT, the
    /// exact failure a generated context exists to prevent.
    /// <para>
    /// Scoped to the scalars both sides decide by name, plus the two both sides refuse. Collections,
    /// arrays, enums and the object model reach the provider too, but the collector classifies them by
    /// their own kinds rather than through <c>IsPrimitive</c>, so a blanket equivalence over every type
    /// the provider answers would compare two lists that were never meant to match.
    /// </para>
    /// </remarks>
    public class PrimitiveConverterProviderParityTests
    {
        /// <summary>
        /// The member type as C# source, the runtime type behind it, and the fully qualified name the
        /// emitter would spell if the type were collected as an object.
        /// </summary>
        public static IEnumerable<object[]> ScalarsDecidedByName()
        {
            yield return new object[]
            {
                "System.Numerics.BigInteger", typeof(System.Numerics.BigInteger), "global::System.Numerics.BigInteger",
            };
            yield return new object[]
            {
                "Dahomey.Cbor.CborDecimalFraction", typeof(CborDecimalFraction), "global::Dahomey.Cbor.CborDecimalFraction",
            };
            yield return new object[]
            {
                "Dahomey.Cbor.CborBigFloat", typeof(CborBigFloat), "global::Dahomey.Cbor.CborBigFloat",
            };
            yield return new object[] { "System.Half", typeof(Half), "global::System.Half" };
            yield return new object[] { "System.Guid", typeof(Guid), "global::System.Guid" };
            yield return new object[] { "System.DateTimeOffset", typeof(DateTimeOffset), "global::System.DateTimeOffset" };
        }

        [Theory]
        [MemberData(nameof(ScalarsDecidedByName))]
        public void TheGeneratorCarriesExactlyWhatTheProviderResolves(
            string memberType, Type type, string qualifiedName)
        {
            bool providerResolves =
                new PrimitiveConverterProvider().GetConverter(type, new CborOptions()) != null;

            (ImmutableArray<Diagnostic> diagnostics, string generated) =
                CddlGeneratorHarness.RunAndGetGeneratedSources($@"
using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;

namespace Harness
{{
    public class Holder {{ public {memberType} Value {{ get; set; }} }}

    [CborSerializable(typeof(Holder))]
    public partial class HarnessContext : CborSerializerContext {{ }}
}}
");

            bool refused = diagnostics.Any(diagnostic => diagnostic.Id == "CBOR1002");

            // The member's own mapping names the type either way, so the marker has to be the
            // registration the emitter writes only for a type collected as an object.
            bool mappedAsAnObject = generated.Contains($"ObjectConverter<{qualifiedName}>");

            if (providerResolves)
            {
                Assert.False(
                    refused,
                    $"PrimitiveConverterProvider resolves {memberType}, but the generator refuses it with "
                        + "CBOR1002. Add it to TypeCollector.IsPrimitive.");

                Assert.False(
                    mappedAsAnObject,
                    $"PrimitiveConverterProvider resolves {memberType}, but the generator collected it as an "
                        + "object and registered an ObjectConverter over its members. The generated context "
                        + "writes different bytes from the reflection path. Add it to TypeCollector.IsPrimitive.");
            }
            else
            {
                Assert.True(
                    refused,
                    $"PrimitiveConverterProvider resolves no converter for {memberType}, so at run time it "
                        + "reaches ObjectConverterProvider and MakeGenericType. The generator has to refuse it "
                        + "with CBOR1002. Add it to TypeCollector.HasNoConcreteConverter.");
            }
        }
    }
}
