// Annotations-only nullable context, as in CddlSchemaTests.cs: without it every reference-typed
// member below would render as `X / nil` and the pinned rules would not match.
#nullable enable annotations
using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Tests.Extensions;
using Xunit;

namespace Dahomey.Cbor.Tests.Cddl
{
    public abstract class CddlShape
    {
        public int Id { get; set; }
    }

    [CborDiscriminator("circle")]
    public class CddlCircle : CddlShape
    {
        public double Radius { get; set; }
    }

    [CborDiscriminator("square")]
    public class CddlSquare : CddlShape
    {
        public double Side { get; set; }
    }

    public class CddlDrawing
    {
        public CddlShape Shape { get; set; }
        public CddlCircle KnownCircle { get; set; }
    }

    [CborSerializable(typeof(CddlDrawing))]
    [CborSerializable(typeof(CddlCircle))]
    [CborSerializable(typeof(CddlSquare))]
    [CborCddlSchema]
    public partial class CddlPolymorphicContext : CborSerializerContext
    {
    }

    [CborObjectFormat(CborObjectFormat.Array)]
    public class CddlArrayBase
    {
        [CborProperty(1)]
        public int Id { get; set; }
    }

    [CborObjectFormat(CborObjectFormat.Array)]
    [CborIntDiscriminator(1)]
    public class CddlArrayDerived : CddlArrayBase
    {
        [CborProperty(2)]
        public string Name { get; set; }
    }

    [CborSerializable(typeof(CddlArrayBase))]
    [CborSerializable(typeof(CddlArrayDerived))]
    [CborCddlSchema]
    public partial class CddlArrayPolyContext : CborSerializerContext
    {
    }

    public abstract class CddlEvent
    {
        public int Id { get; set; }
    }

    [CborDiscriminator("input")]
    public class CddlInputEvent : CddlEvent
    {
        public int Device { get; set; }
    }

    [CborDiscriminator("click")]
    public class CddlClickEvent : CddlInputEvent
    {
        public int X { get; set; }
    }

    public class CddlEventLog
    {
        public CddlEvent Any { get; set; }
        public CddlInputEvent Input { get; set; }
    }

    [CborSerializable(typeof(CddlEventLog))]
    [CborSerializable(typeof(CddlInputEvent))]
    [CborSerializable(typeof(CddlClickEvent))]
    [CborCddlSchema]
    public partial class CddlNestedPolyContext : CborSerializerContext
    {
    }

    public class CddlPolymorphismTests
    {
        private static readonly CddlPolymorphicContext Context =
            CborSerializerContext.Default<CddlPolymorphicContext>();

        /// <summary>
        /// SetDiscriminator inserts the discriminator at registration, after SetMemberMappings, so it
        /// is not in the generator's member list at all. A member walk alone omits it silently -- and
        /// it is the field a non-.NET consumer most needs.
        /// </summary>
        [Fact]
        public void DiscriminatorAppearsInThePolymorphicRule()
        {
            string schema = CddlPolymorphicContext.CddlSchema.Replace("\r\n", "\n");

            Assert.Contains("CddlCircle-poly = {\n  \"_t\": \"circle\",\n", schema);
        }

        /// <summary>
        /// Under the effective default policy of Auto the discriminator is written only when the
        /// object's type differs from the declared type, so the same class needs two rules: one for
        /// each call site.
        /// </summary>
        [Fact]
        public void ConcreteReferenceUsesTheBareRule()
        {
            string schema = CddlPolymorphicContext.CddlSchema.Replace("\r\n", "\n");

            Assert.Contains("CddlCircle = {\n  \"Radius\": float,\n", schema);
            Assert.Contains("\"KnownCircle\": CddlCircle,", schema);
        }

        [Fact]
        public void BaseIsATypeChoiceOverThePolymorphicVariants()
        {
            string schema = CddlPolymorphicContext.CddlSchema.Replace("\r\n", "\n");

            Assert.Contains("CddlShape-poly = CddlCircle-poly / CddlSquare-poly", schema);
            Assert.Contains("\"Shape\": CddlShape-poly,", schema);
        }

        /// <summary>
        /// An abstract base cannot be instantiated, so nothing is ever serialized as exactly a
        /// CddlShape and no bare rule for it may exist.
        /// </summary>
        [Fact]
        public void AbstractBaseHasNoBareRule()
        {
            string schema = CddlPolymorphicContext.CddlSchema.Replace("\r\n", "\n");

            Assert.DoesNotContain("CddlShape = ", schema);
        }

        /// <summary>
        /// A concrete base is serialized as itself -- writing no discriminator -- as well as being the
        /// static type of a polymorphic member, so its own bare rule is the first arm of its choice.
        /// </summary>
        [Fact]
        public void ConcreteBaseIsTheFirstArmOfItsOwnTypeChoice()
        {
            string schema = CddlArrayPolyContext.CddlSchema.Replace("\r\n", "\n");

            Assert.Contains("CddlArrayBase = [\n  -2147483648..2147483647,\n]", schema);
            Assert.Contains("CddlArrayBase-poly = CddlArrayBase / CddlArrayDerived-poly", schema);
        }

        /// <summary>
        /// Each base names only its nearest subtypes, so a three-level hierarchy chains instead of
        /// repeating the leaf in every ancestor's choice.
        /// </summary>
        [Fact]
        public void NestedBaseChainsThroughItsIntermediate()
        {
            string schema = CddlNestedPolyContext.CddlSchema.Replace("\r\n", "\n");

            Assert.Contains("CddlEvent-poly = CddlInputEvent-poly\n", schema);
        }

        /// <summary>
        /// A concrete intermediate is written two ways: bare when the declared type is exactly it, and
        /// discriminated when it is reached through its own base. One document holds both, so a schema
        /// admitting only one of them fails here.
        /// </summary>
        [CddlFact]
        public void BothShapesOfAConcreteIntermediateValidate()
        {
            CddlEventLog value = new CddlEventLog
            {
                Any = new CddlInputEvent { Id = 1, Device = 2 },
                Input = new CddlInputEvent { Id = 3, Device = 4 },
            };

            byte[] cbor = Helper.Write(
                value, CborSerializerContext.Default<CddlNestedPolyContext>().Options).HexToBytes();

            CddlResult result = CddlTool.Validate(
                CddlNestedPolyContext.CddlSchema, "CddlEventLog", cbor);

            Assert.True(result.Ok, result.Output);
        }

        /// <summary>
        /// The leaf reached through the top of the chain carries its own discriminator, not the
        /// intermediate's.
        /// </summary>
        [CddlFact]
        public void LeafReachedThroughTheTopOfTheChainValidates()
        {
            CddlEventLog value = new CddlEventLog
            {
                Any = new CddlClickEvent { Id = 1, Device = 2, X = 3 },
                Input = new CddlClickEvent { Id = 4, Device = 5, X = 6 },
            };

            byte[] cbor = Helper.Write(
                value, CborSerializerContext.Default<CddlNestedPolyContext>().Options).HexToBytes();

            CddlResult result = CddlTool.Validate(
                CddlNestedPolyContext.CddlSchema, "CddlEventLog", cbor);

            Assert.True(result.Ok, result.Output);
        }

        [CddlFact]
        public void PolymorphicOutputValidates()
        {
            CddlDrawing value = new CddlDrawing
            {
                Shape = new CddlCircle { Id = 1, Radius = 1.5 },
                KnownCircle = new CddlCircle { Id = 2, Radius = 2.5 },
            };

            byte[] cbor = Helper.Write(value, Context.Options).HexToBytes();

            CddlResult result = CddlTool.Validate(
                CddlPolymorphicContext.CddlSchema, "CddlDrawing", cbor);

            Assert.True(result.Ok, result.Output);
        }

        /// <summary>
        /// The Array-format discriminator wraps only the first element, and the array length counts
        /// it. Pinned against the byte sequence the serializer actually produces for this shape.
        /// </summary>
        [CddlFact]
        public void ArrayFormatWrapsOnlyTheFirstElementInTheTag()
        {
            string schema = CddlArrayPolyContext.CddlSchema.Replace("\r\n", "\n");

            Assert.Contains("CddlArrayDerived-poly = [\n  #6.39(1),\n", schema);

            byte[] cbor = "83D827010C63666F6F".HexToBytes(); // [39(1), 12, "foo"]

            CddlResult result = CddlTool.Validate(
                CddlArrayPolyContext.CddlSchema, "CddlArrayDerived-poly", cbor);

            Assert.True(result.Ok, result.Output);
        }

        /// <summary>
        /// A tag around the whole array rather than the first element is the natural wrong guess, and
        /// it has to fail.
        /// </summary>
        [CddlFact]
        public void ArrayFormatRejectsATagAroundTheWholeArray()
        {
            byte[] cbor = "D82783010C63666F6F".HexToBytes(); // 39([1, 12, "foo"])

            CddlResult result = CddlTool.Validate(
                CddlArrayPolyContext.CddlSchema, "CddlArrayDerived-poly", cbor);

            Assert.False(result.Ok);
        }

        [CddlFact]
        public void PolymorphicRuleRejectsTheWrongDiscriminator()
        {
            // { "_t": "square", "Radius": 1.5, "Id": 1 } -- square's tag on circle's shape.
            byte[] cbor = "A3625F746673717561726566526164697573F93E0062496401".HexToBytes();

            CddlResult result = CddlTool.Validate(
                CddlPolymorphicContext.CddlSchema, "CddlCircle-poly", cbor);

            Assert.False(result.Ok);
        }
    }
}
