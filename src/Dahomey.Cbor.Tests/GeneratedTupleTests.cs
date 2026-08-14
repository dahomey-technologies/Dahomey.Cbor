using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    public class GeneratedTupleHolder
    {
        /// <summary>Seven or fewer: one converter, one type argument per element.</summary>
        public (int, string) Pair { get; set; }

        /// <summary>Past seven, so the converter's eighth argument is the <c>Rest</c>.</summary>
        public (int, int, int, int, int, int, int, int, int) Nine { get; set; }

        /// <summary>Nested twice, so the <c>Rest</c>'s own <c>Rest</c> is a one-element tuple.</summary>
        public (int, int, int, int, int, int, int, int, int, int, int, int, int, int, int) Fifteen { get; set; }

        /// <summary>Through <c>NullableConverter</c>, which resolves the tuple's converter itself.</summary>
        public (int, string)? Optional { get; set; }
    }

    [CborSerializable(typeof(GeneratedTupleHolder))]
    public partial class TupleContext : CborSerializerContext
    {
    }

    /// <summary>
    /// Tuples on the source-generated path, which is what makes them usable under Native AOT: the
    /// reflection provider builds a tuple's converter with <c>MakeGenericType</c>, and a generated
    /// context names the instantiation in source instead.
    /// </summary>
    /// <remarks>
    /// Before this, a tuple member was <c>CBOR1002</c> — the generator classified it as an unrecognised
    /// generic type and refused to build. So the coverage that matters is the corpus comparison against
    /// the reflection path, since a generated context that resolved a different converter would still
    /// produce valid CBOR.
    /// </remarks>
    public class GeneratedTupleTests
    {
        [Fact]
        public void AGeneratedContextWritesWhatTheReflectionPathWrites()
        {
            TupleContext context = new TupleContext();
            GeneratedTupleHolder holder = Sample();

            string generated = Helper.Write(holder, context.Options);

            Assert.Equal(Helper.Write(holder), generated, ignoreCase: true);
        }

        [Fact]
        public void AGeneratedContextReadsBackWhatItWrote()
        {
            TupleContext context = new TupleContext();
            GeneratedTupleHolder holder = Sample();

            GeneratedTupleHolder read = Helper.Read<GeneratedTupleHolder>(
                Helper.Write(holder, context.Options), context.Options);

            Assert.Equal(holder.Pair, read.Pair);
            Assert.Equal(holder.Nine, read.Nine);
            Assert.Equal(holder.Fifteen, read.Fifteen);
            Assert.Equal(holder.Optional, read.Optional);
        }

        /// <summary>
        /// The arity past seven is flat on the generated path too, which is the assertion that would
        /// fail if the emitted registration named the wrong converter or the wrong type arguments.
        /// </summary>
        [Fact]
        public void AnArityPastSevenIsFlatOnTheGeneratedPath()
        {
            TupleContext context = new TupleContext();

            Assert.Contains(
                "89010203040506070809",
                Helper.Write(Sample(), context.Options).ToUpperInvariant());
        }

        internal static GeneratedTupleHolder Sample()
        {
            return new GeneratedTupleHolder
            {
                Pair = (1, "two"),
                Nine = (1, 2, 3, 4, 5, 6, 7, 8, 9),
                Fifteen = (1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15),
                Optional = (7, "seven"),
            };
        }
    }
}
