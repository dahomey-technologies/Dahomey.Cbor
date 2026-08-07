using System.Collections.Generic;
using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Tests.Extensions;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    public class GeneratedPerson
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public bool Active { get; set; }
        public double Score { get; set; }
        public List<string> Tags { get; set; }
        public GeneratedAddress Address { get; set; }
    }

    public class GeneratedAddress
    {
        public string City { get; set; }
        public int Number { get; set; }
    }

    public enum GeneratedColour
    {
        Red = 0,
        Green = 1,
    }

    public class GeneratedShapes
    {
        public GeneratedColour Colour { get; set; }
        public int[] Sizes { get; set; }
        public int? Optional { get; set; }
        public Dictionary<string, int> Counts { get; set; }
    }

    [CborSerializable(typeof(GeneratedPerson))]
    [CborSerializable(typeof(GeneratedShapes))]
    public partial class TestCborContext : CborSerializerContext
    {
    }

    public class ReusedOptionsProbe
    {
        public int Id { get; set; }
    }

    /// <summary>
    /// A context that takes caller-supplied options, which is the shape that can be handed options
    /// already carrying converters.
    /// </summary>
    [CborSerializable(typeof(ReusedOptionsProbe))]
    public partial class ReusedOptionsContext : CborSerializerContext
    {
        public ReusedOptionsContext()
        {
        }

        public ReusedOptionsContext(CborOptions options)
            : base(options)
        {
        }
    }

    /// <summary>
    /// End-to-end check on the source generator: a generated context must produce byte-identical
    /// output to the reflection path, and round-trip. Byte-identity is the contract that makes the
    /// generated path a drop-in rather than a second dialect.
    /// </summary>
    public class GeneratedContextTests
    {
        private static readonly TestCborContext Context = CborSerializerContext.Default<TestCborContext>();

        private static GeneratedPerson SamplePerson() => new GeneratedPerson
        {
            Id = 42,
            Name = "Ada",
            Active = true,
            Score = 99.5,
            Tags = new List<string> { "math", "cbor" },
            Address = new GeneratedAddress { City = "London", Number = 7 },
        };

        private static GeneratedShapes SampleShapes() => new GeneratedShapes
        {
            Colour = GeneratedColour.Green,
            Sizes = new[] { 1, 2, 3 },
            Optional = 5,
            Counts = new Dictionary<string, int> { ["a"] = 1 },
        };

        [Fact]
        public void GeneratedContextWritesIdenticalBytesToReflectionPath()
        {
            GeneratedPerson person = SamplePerson();

            string reflection = Helper.Write(person);
            string generated = Helper.Write(person, Context.Options);

            Assert.Equal(reflection, generated);
        }

        [Fact]
        public void GeneratedContextHandlesEnumsArraysNullablesAndDictionaries()
        {
            GeneratedShapes shapes = SampleShapes();

            string reflection = Helper.Write(shapes);
            string generated = Helper.Write(shapes, Context.Options);

            Assert.Equal(reflection, generated);
        }

        [Fact]
        public void GeneratedContextRoundTrips()
        {
            GeneratedPerson person = SamplePerson();

            string hexBuffer = Helper.Write(person, Context.Options);
            GeneratedPerson rehydrated = Cbor.Deserialize<GeneratedPerson>(
                hexBuffer.HexToBytes(), Context.Options);

            Assert.Equal(42, rehydrated.Id);
            Assert.Equal("Ada", rehydrated.Name);
            Assert.True(rehydrated.Active);
            Assert.Equal(99.5, rehydrated.Score);
            Assert.Equal(new[] { "math", "cbor" }, rehydrated.Tags);
            Assert.Equal("London", rehydrated.Address.City);
            Assert.Equal(7, rehydrated.Address.Number);
        }

        [Fact]
        public void GeneratedShapesRoundTrip()
        {
            GeneratedShapes shapes = SampleShapes();

            string hexBuffer = Helper.Write(shapes, Context.Options);
            GeneratedShapes rehydrated = Cbor.Deserialize<GeneratedShapes>(
                hexBuffer.HexToBytes(), Context.Options);

            Assert.Equal(GeneratedColour.Green, rehydrated.Colour);
            Assert.Equal(new[] { 1, 2, 3 }, rehydrated.Sizes);
            Assert.Equal(5, rehydrated.Optional);
            Assert.Equal(1, rehydrated.Counts["a"]);
        }

        /// <summary>
        /// The typed accessor is the point of the context: reaching a converter without a
        /// dictionary lookup.
        /// </summary>
        [Fact]
        public void TypedAccessorsAreGenerated()
        {
            Assert.NotNull(Context.GeneratedPerson);
            Assert.NotNull(Context.GeneratedShapes);
            Assert.Same(Context.GeneratedPerson, Context.GeneratedPerson);
        }

        [Fact]
        public void DefaultContextIsShared()
        {
            Assert.Same(Context, CborSerializerContext.Default<TestCborContext>());
        }

        /// <summary>
        /// Options that have already read or written carry a converter for every type they touched,
        /// and <c>RegisterConverter</c> is <c>TryAdd</c> — so a context constructed over them
        /// registers nothing at all.
        /// </summary>
        /// <remarks>
        /// Refusing is the only outcome that is visible. Silently registering nothing leaves a context
        /// that looks configured and serves every type it declares from the reflection path it exists
        /// to replace, which works on CoreCLR and is exactly what fails under Native AOT — so the
        /// failure would surface only in a published binary.
        /// </remarks>
        [Fact]
        public void AContextOverAlreadyUsedOptionsIsRefused()
        {
            CborOptions options = new CborOptions();

            // Writing through these options caches a converter for the type the context declares.
            Helper.Write(new ReusedOptionsProbe { Id = 1 }, options);

            CborException exception = Assert.Throws<CborException>(
                () => new ReusedOptionsContext(options));

            Assert.Contains(nameof(ReusedOptionsProbe), exception.Message);
        }

        /// <summary>
        /// The same constructor over unused options is the supported case — combining generated
        /// registrations with settings of the caller's own — and must still work.
        /// </summary>
        [Fact]
        public void AContextOverUnusedOptionsRegistersOntoThem()
        {
            CborOptions options = new CborOptions { EnumFormat = ValueFormat.WriteToString };

            ReusedOptionsContext context = new ReusedOptionsContext(options);

            Assert.Same(options, context.Options);
            Assert.Equal(ValueFormat.WriteToString, context.Options.EnumFormat);
            Assert.Equal("A16249640C", Helper.Write(new ReusedOptionsProbe { Id = 12 }, context.Options));
        }
    }
}
