using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization.Conventions;
using Dahomey.Cbor.Tests.Extensions;
using System;
using Xunit;

namespace Dahomey.Cbor.Tests.Issues
{
    /// <summary>
    /// Issue #224: registering a subtype had no effect once anything had asked the registry about the
    /// base type, and said nothing about it. The lookup that resolved nothing cached that null under
    /// the base type, and the propagation from the subtype could not displace it, so the remedy the
    /// error message names failed to take on the very options the caller had in hand.
    /// </summary>
    /// <remarks>
    /// Reading a member declared as the base is enough to poison the entry: it builds the base type's
    /// converter, which resolves the convention. So the sequence below - deserialize, fail, register,
    /// deserialize again - is the ordinary way into it rather than a contrived one, and every test
    /// here reuses one <see cref="CborOptions"/> across the registration for that reason.
    /// </remarks>
    public class Issue0224
    {
        public abstract class Shape { }

        [CborDiscriminator("circle")]
        public class Circle : Shape { public double Radius { get; set; } }

        [CborDiscriminator("square")]
        public class Square : Shape { public double Side { get; set; } }

        public class Holder { public Shape Shape { get; set; } }

        public interface IPayload { }

        [CborDiscriminator("text")]
        public class TextPayload : IPayload { public string Text { get; set; } }

        public class PayloadHolder { public IPayload Payload { get; set; } }

        public abstract class Numbered { }

        [CborIntDiscriminator(7)]
        public class NumberedSeven : Numbered { public int Value { get; set; } }

        public class NumberedHolder { public Numbered Numbered { get; set; } }

        // a1 655368617065 a2 625f74 66636972636c65 66526164697573 f93e00
        //   {"Shape": {"_t": "circle", "Radius": 1.5}}
        private const string CircleInHolder = "A1655368617065A2625F7466636972636C6566526164697573F93E00";

        // {"Payload": {"_t": "text", "Text": "hi"}}
        private const string TextInHolder = "A1675061796C6F6164A2625F7464746578746454657874626869";

        // {"Numbered": {"_t": 7, "Value": 42}}
        private const string SevenInHolder = "A1684E756D6265726564A2625F74076556616C7565182A";

        [Fact]
        public void RegisterTypeAfterTheBaseTypeWasResolved()
        {
            CborOptions options = new CborOptions();
            DiscriminatorConventionRegistry registry = options.Registry.DiscriminatorConventionRegistry;

            Assert.Null(registry.GetConvention(typeof(Shape)));

            registry.RegisterType<Circle>();

            Assert.NotNull(registry.GetConvention(typeof(Shape)));
        }

        [Fact]
        public void RegisterTypeAfterAFailedRead()
        {
            CborOptions options = new CborOptions();

            // The message is #223's business; what matters here is that the read failed, because that
            // is what resolved - and used to freeze - the convention for Shape.
            Assert.Throws<CborException>(() => Helper.Read<Holder>(CircleInHolder, options));

            options.Registry.DiscriminatorConventionRegistry.RegisterType<Circle>();

            Holder holder = Helper.Read<Holder>(CircleInHolder, options);

            Circle circle = Assert.IsType<Circle>(holder.Shape);
            Assert.Equal(1.5, circle.Radius);
        }

        [Fact]
        public void RegisterTypeAfterAFailedReadThroughAnInterface()
        {
            CborOptions options = new CborOptions();

            Assert.Throws<CborException>(() => Helper.Read<PayloadHolder>(TextInHolder, options));

            options.Registry.DiscriminatorConventionRegistry.RegisterType<TextPayload>();

            PayloadHolder holder = Helper.Read<PayloadHolder>(TextInHolder, options);

            Assert.Equal("hi", Assert.IsType<TextPayload>(holder.Payload).Text);
        }

        [Fact]
        public void RegisterTypeAfterAFailedReadWithAnIntDiscriminator()
        {
            CborOptions options = new CborOptions();

            Assert.Throws<CborException>(() => Helper.Read<NumberedHolder>(SevenInHolder, options));

            options.Registry.DiscriminatorConventionRegistry.RegisterType<NumberedSeven>();

            NumberedHolder holder = Helper.Read<NumberedHolder>(SevenInHolder, options);

            Assert.Equal(42, Assert.IsType<NumberedSeven>(holder.Numbered).Value);
        }

        /// <summary>
        /// Reading the base type directly, rather than as a member, resolves the convention through a
        /// different converter. It has to pick the registration up too.
        /// </summary>
        [Fact]
        public void RegisterTypeAfterAFailedReadOfTheBaseTypeItself()
        {
            // {"_t": "circle", "Radius": 1.5}
            const string bareCircle = "A2625F7466636972636C6566526164697573F93E00";

            CborOptions options = new CborOptions();

            Assert.Throws<CborException>(() => Helper.Read<Shape>(bareCircle, options));

            options.Registry.DiscriminatorConventionRegistry.RegisterType<Circle>();

            Assert.Equal(1.5, Assert.IsType<Circle>(Helper.Read<Shape>(bareCircle, options)).Radius);
        }

        /// <summary>
        /// A second subtype registered later has to reach the same converter, which by then holds a
        /// perfectly good convention and no longer re-resolves.
        /// </summary>
        /// <remarks>
        /// It does not need to: the registry answers per hierarchy, not per subtype, so the convention
        /// the first registration installed already resolves the second one's discriminator. This
        /// pins that the version check, which stops looking once the answer is non-null, does not cost
        /// the later registration its effect.
        /// </remarks>
        [Fact]
        public void RegisterASecondSubtypeAfterTheFirstReadSucceeded()
        {
            // {"Shape": {"_t": "square", "Side": 3.0}}
            const string squareInHolder = "A1655368617065A2625F74667371756172656453696465F94200";

            CborOptions options = new CborOptions();
            DiscriminatorConventionRegistry registry = options.Registry.DiscriminatorConventionRegistry;

            registry.RegisterType<Circle>();
            Assert.IsType<Circle>(Helper.Read<Holder>(CircleInHolder, options).Shape);

            registry.RegisterType<Square>();

            Assert.Equal(3.0, Assert.IsType<Square>(Helper.Read<Holder>(squareInHolder, options).Shape).Side);
        }

        /// <summary>
        /// Reads do not move the version. That is what keeps re-resolution off the steady-state path:
        /// a converter holding a null answer compares two equal ints and asks nothing, so a type with
        /// no discriminator anywhere does not pay a registry lookup per object once the types in play
        /// have been seen.
        /// </summary>
        [Fact]
        public void ReadingDoesNotMoveTheVersion()
        {
            CborOptions options = new CborOptions();
            DiscriminatorConventionRegistry registry = options.Registry.DiscriminatorConventionRegistry;

            registry.RegisterType<Circle>();
            Helper.Read<Holder>(CircleInHolder, options);

            int version = registry.Version;

            Helper.Read<Holder>(CircleInHolder, options);
            Helper.Read<Holder>(CircleInHolder, options);

            Assert.Equal(version, registry.Version);
        }

        /// <summary>
        /// A type whose hierarchy carries no discriminator at all keeps resolving to null, however many
        /// unrelated registrations happen around it.
        /// </summary>
        [Fact]
        public void UnrelatedRegistrationsDoNotInventAConvention()
        {
            CborOptions options = new CborOptions();
            DiscriminatorConventionRegistry registry = options.Registry.DiscriminatorConventionRegistry;

            Assert.Null(registry.GetConvention(typeof(Holder)));

            registry.RegisterType<Circle>();

            Assert.Null(registry.GetConvention(typeof(Holder)));
        }
    }
}
