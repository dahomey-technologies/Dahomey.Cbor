using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization.Conventions;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    /// <summary>
    /// Two types with one discriminator value: a document carrying it used to read as whichever was
    /// registered first, silently (#264).
    /// </summary>
    public class SharedDiscriminatorTests
    {
        public abstract class Shape
        {
        }

        [CborDiscriminator("square")]
        public class Square : Shape
        {
            public int Side { get; set; }
        }

        [CborDiscriminator("square")]
        public class Circle : Shape
        {
            public int Radius { get; set; }
        }

        // Inherits "square" from Square.
        public class ColoredSquare : Square
        {
            public int Color { get; set; }
        }

        [CborDiscriminator("square")]
        public class RedeclaredSquare : Square
        {
        }

        [CborIntDiscriminator(1)]
        public class IntSquare : Shape
        {
        }

        [CborIntDiscriminator(1)]
        public class IntCircle : Shape
        {
        }

        [CborDiscriminator("box")]
        public class Box<T> : Shape
        {
        }

        [Fact]
        public void UnrelatedTypesAreRefused()
        {
            AssertRefused<Square, Circle>();
            AssertRefused<Circle, Square>();
            AssertRefused<IntSquare, IntCircle>();
            AssertRefused<Box<int>, Box<string>>();
        }

        [Fact]
        public void ASubtypeRedeclaringItsBaseValueIsRefused()
        {
            AssertRefused<Square, RedeclaredSquare>();

            // Registering a subtype registers the bases that declare a discriminator, so the subtype
            // alone is refused.
            CborOptions options = new CborOptions();
            Assert.Throws<CborException>(
                () => options.Registry.DiscriminatorConventionRegistry.RegisterType<RedeclaredSquare>());
        }

        [Fact]
        public void ASubtypeInheritingItsBaseValueIsAllowed()
        {
            const string squareHex = "A2625F7466737175617265645369646503"; // {"_t": "square", "Side": 3}
            const string coloredHex = "A3625F746673717561726565436F6C6F7202645369646503";

            // Registered alone, the subtype registers its base, which can then be written.
            CborOptions options = new CborOptions();
            options.Registry.DiscriminatorConventionRegistry.RegisterType<ColoredSquare>();
            Assert.Equal(squareHex, Helper.Write<Shape>(new Square { Side = 3 }, options));
            Assert.Equal(coloredHex, Helper.Write<Shape>(new ColoredSquare { Side = 3, Color = 2 }, options));

            // The value reads as the type registered first: here the subtype, which reads back as itself.
            ColoredSquare colored = Helper.Read<ColoredSquare>(coloredHex, options);
            Assert.Equal(2, colored.Color);

            // Registered first, the base keeps the value.
            options = new CborOptions();
            options.Registry.DiscriminatorConventionRegistry.RegisterType<Square>();
            options.Registry.DiscriminatorConventionRegistry.RegisterType<ColoredSquare>();
            Assert.IsType<Square>(Helper.Read<Shape>(squareHex, options));
        }

        private static void AssertRefused<TFirst, TSecond>()
            where TFirst : class
            where TSecond : class
        {
            CborOptions options = new CborOptions();
            DiscriminatorConventionRegistry registry = options.Registry.DiscriminatorConventionRegistry;
            registry.RegisterType<TFirst>();

            CborException ex = Assert.Throws<CborException>(() => registry.RegisterType<TSecond>());
            Assert.Contains(typeof(TFirst).ToString(), ex.Message);
            Assert.Contains(typeof(TSecond).ToString(), ex.Message);
        }
    }
}
