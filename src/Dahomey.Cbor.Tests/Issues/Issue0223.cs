using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization.Conventions;
using Dahomey.Cbor.Tests.Extensions;
using Xunit;

namespace Dahomey.Cbor.Tests.Issues
{
    /// <summary>
    /// A document carrying a discriminator for a subtype nobody registered used to be reported as a
    /// missing <c>CreatorMapping</c> — a remedy that is not the one the README prescribes, and not the
    /// one that works.
    /// </summary>
    /// <remarks>
    /// The asymmetry is what makes it easy to walk into: writing goes through the runtime type, so
    /// building that type's converter registers it as a side effect and no setup is needed. Reading only
    /// ever names the declared type, so the same program that produced the document cannot read it back
    /// without a <c>RegisterType</c> call — and the document looks complete, because the discriminator is
    /// sitting in it.
    /// </remarks>
    public class Issue0223
    {
        private const string ExpectedRemedy = "RegisterType<T>()";

        /// <summary>
        /// Writes on an options instance of its own, because writing registers the runtime type as a
        /// side effect of building its converter — so sharing one instance with the read would register
        /// the very subtype these tests are about, and through <c>CborOptions.Default</c> it would leak
        /// between them in whatever order they happen to run.
        /// </summary>
        private static string WriteOnItsOwnOptions<T>(T value)
        {
            return Helper.Write(value, new CborOptions());
        }

        /// <summary>
        /// The default format, where the discriminator is a named member. The value is named too, since
        /// the remedy is per subtype and the value is what says which one this document needed.
        /// </summary>
        [Fact]
        public void AnUnregisteredSubtypeNamesTheRegistrationAndTheDiscriminator()
        {
            string hex = WriteOnItsOwnOptions<Shape>(new Circle { Radius = 1.5 });

            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<Shape>(hex.HexToBytes(), new CborOptions()));

            Assert.Contains(ExpectedRemedy, exception.Message);
            Assert.Contains("discriminator \"circle\"", exception.Message);
            Assert.DoesNotContain("CreatorMapping", exception.Message);
        }

        /// <summary>An interface rather than an abstract class, which resolves the same way.</summary>
        [Fact]
        public void AnUnregisteredSubtypeOfAnInterfaceIsReportedTheSameWay()
        {
            string hex = WriteOnItsOwnOptions<IShape>(new Circle { Radius = 1.5 });

            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<IShape>(hex.HexToBytes(), new CborOptions()));

            Assert.Contains(ExpectedRemedy, exception.Message);
            Assert.Contains("discriminator \"circle\"", exception.Message);
        }

        /// <summary>
        /// <c>IntKeyMap</c>, where the discriminator is key 0 and the value is an integer. Both halves
        /// differ from the default format, and the value is rendered without this code knowing which
        /// kind it is.
        /// </summary>
        [Fact]
        public void AnUnregisteredSubtypeUnderIntKeyMapIsReported()
        {
            string hex = WriteOnItsOwnOptions<IntShape>(new IntCircle { Radius = 1.5 });

            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<IntShape>(hex.HexToBytes(), new CborOptions()));

            Assert.Contains(ExpectedRemedy, exception.Message);
            Assert.Contains("discriminator 7", exception.Message);
        }

        /// <summary>
        /// <c>Array</c>, where the discriminator is the first item behind a semantic tag rather than a
        /// keyed member, so the probe reaches it by a different route again.
        /// </summary>
        [Fact]
        public void AnUnregisteredSubtypeUnderArrayFormatIsReported()
        {
            string hex = WriteOnItsOwnOptions<ArrayShape>(new ArraySquare { Radius = 1.5 });

            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<ArrayShape>(hex.HexToBytes(), new CborOptions()));

            Assert.Contains(ExpectedRemedy, exception.Message);
            Assert.Contains("discriminator \"square\"", exception.Message);
        }

        /// <summary>
        /// The remedy the message names has to be the one that works, or it is worse than the message it
        /// replaced. Registering the subtype and reading the same bytes yields the subtype.
        /// </summary>
        [Fact]
        public void TheRemedyTheMessageNamesResolvesTheDocument()
        {
            string hex = WriteOnItsOwnOptions<Shape>(new Circle { Radius = 1.5 });

            CborOptions options = new CborOptions();
            options.Registry.DiscriminatorConventionRegistry.RegisterType<Circle>();

            Shape shape = Cbor.Deserialize<Shape>(hex.HexToBytes(), options);

            Assert.IsType<Circle>(shape);
            Assert.Equal(1.5, ((Circle)shape).Radius);
        }

        /// <summary>
        /// A document that carries no discriminator at all still reports the missing
        /// <c>CreatorMapping</c>, which is the correct answer for that case: nothing names a subtype, so
        /// nothing can be registered, and a creator really is what the caller owes.
        /// </summary>
        [Fact]
        public void AnUndiscriminatedDocumentStillReportsTheMissingCreatorMapping()
        {
            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<Shape>("A166526164697573F93E00".HexToBytes(), new CborOptions())); // {"Radius": 1.5}

            Assert.Contains("CreatorMapping", exception.Message);
            Assert.DoesNotContain(ExpectedRemedy, exception.Message);
        }

        /// <summary>
        /// Under the default format the member name is the convention's to choose, so clearing the
        /// conventions leaves nothing to probe with and the original message stands. Pins that the probe
        /// asks the registry rather than assuming the default member name.
        /// </summary>
        [Fact]
        public void WithNoConventionsRegisteredTheOriginalMessageStands()
        {
            string hex = WriteOnItsOwnOptions<Shape>(new Circle { Radius = 1.5 });

            CborOptions options = new CborOptions();
            options.Registry.DiscriminatorConventionRegistry.ClearConventions();

            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<Shape>(hex.HexToBytes(), options));

            Assert.Contains("CreatorMapping", exception.Message);
        }

        /// <summary>
        /// The other two formats put the discriminator at a place no convention gets to choose, so the
        /// document still answers with the conventions cleared. Pins that the probe follows the format
        /// rather than the registry where the format is what decides.
        /// </summary>
        [Fact]
        public void WithNoConventionsRegisteredAPositionalDiscriminatorIsStillFound()
        {
            string intHex = WriteOnItsOwnOptions<IntShape>(new IntCircle { Radius = 1.5 });
            string arrayHex = WriteOnItsOwnOptions<ArrayShape>(new ArraySquare { Radius = 1.5 });

            CborOptions options = new CborOptions();
            options.Registry.DiscriminatorConventionRegistry.ClearConventions();

            CborException intException = Assert.Throws<CborException>(
                () => Cbor.Deserialize<IntShape>(intHex.HexToBytes(), options));
            CborException arrayException = Assert.Throws<CborException>(
                () => Cbor.Deserialize<ArrayShape>(arrayHex.HexToBytes(), options));

            Assert.Contains("discriminator 7", intException.Message);
            Assert.Contains("discriminator \"square\"", arrayException.Message);
        }

        public abstract class Shape
        {
        }

        public interface IShape
        {
        }

        [CborDiscriminator("circle")]
        public class Circle : Shape, IShape
        {
            public double Radius { get; set; }
        }

        [CborObjectFormat(CborObjectFormat.IntKeyMap)]
        public abstract class IntShape
        {
        }

        [CborObjectFormat(CborObjectFormat.IntKeyMap)]
        [CborIntDiscriminator(7)]
        public class IntCircle : IntShape
        {
            [CborProperty(1)]
            public double Radius { get; set; }
        }

        [CborObjectFormat(CborObjectFormat.Array)]
        public abstract class ArrayShape
        {
        }

        [CborObjectFormat(CborObjectFormat.Array)]
        [CborDiscriminator("square")]
        public class ArraySquare : ArrayShape
        {
            [CborProperty(1)]
            public double Radius { get; set; }
        }
    }
}
