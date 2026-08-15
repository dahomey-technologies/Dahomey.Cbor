using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Serialization.Converters;
using Dahomey.Cbor.Tests.Extensions;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Threading.Tasks;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    /// <summary>
    /// A refusal is reported as a refusal whichever door the caller came in through. One refusing
    /// converter, one document, every public read entry point, and the same assertion on each: the
    /// message the converter wrote reaches the caller.
    /// </summary>
    /// <remarks>
    /// This exists because the contract held everywhere except one place, and the tests were arranged by
    /// mechanism rather than by entry point, so nothing compared them. <c>ReadNextItemAsync</c> reads
    /// speculatively and treated a refusal as "not enough bytes yet", reporting a truncated stream
    /// instead — for as long as it had existed, and invisibly, because every other door reported it
    /// correctly and each was tested on its own.
    /// <para>
    /// Arranged as a theory over the entry points rather than as a test each, so the failure names the
    /// door rather than the mechanism, and so adding an entry point without a row here is a visible
    /// omission rather than a silent one.
    /// </para>
    /// </remarks>
    public class RefusalReachesEveryEntryPointTests
    {
        /// <summary>{"Id": 12} — a whole, well-formed item. Nothing here is truncated or malformed.</summary>
        private const string WholeItem = "A16249640C";

        private const string ConverterMessage = "this converter refuses what it read";

        private const string CreatorMessage = "this creator refuses 12";

        public static IEnumerable<object[]> EntryPoints()
        {
            yield return Row("Deserialize(ReadOnlySpan)",
                (bytes, options) => Cbor.Deserialize<RefusedByItsConverter>(bytes.Span, options));

            yield return Row("Deserialize(ReadOnlySequence)",
                (bytes, options) => Cbor.Deserialize<RefusedByItsConverter>(new ReadOnlySequence<byte>(bytes), options));

            yield return Row("DeserializeMultiple(ReadOnlySpan)",
                (bytes, options) => Cbor.DeserializeMultiple<RefusedByItsConverter>(bytes.Span, options));

            yield return Row("DeserializeMultiple(ReadOnlySequence)",
                (bytes, options) => Cbor.DeserializeMultiple<RefusedByItsConverter>(new ReadOnlySequence<byte>(bytes), options));

            yield return Row("DeserializeAsync(Stream)",
                (bytes, options) => Cbor.DeserializeAsync<RefusedByItsConverter>(
                    new MemoryStream(bytes.ToArray()), options).AsTask().GetAwaiter().GetResult());

            yield return Row("DeserializeAsync(PipeReader)",
                (bytes, options) => Cbor.DeserializeAsync<RefusedByItsConverter>(
                    PipeReader.Create(new ReadOnlySequence<byte>(bytes)), options).AsTask().GetAwaiter().GetResult());

            yield return Row("ReadNextItemAsync(PipeReader)",
                (bytes, options) => Cbor.ReadNextItemAsync<RefusedByItsConverter>(
                    PipeReader.Create(new ReadOnlySequence<byte>(bytes)), options).AsTask().GetAwaiter().GetResult());
        }

        /// <summary>
        /// A converter that refuses what it read. The converter is the shortest route to the contract —
        /// it is the caller's code, and every entry point reaches it the same way.
        /// </summary>
        [Theory]
        [MemberData(nameof(EntryPoints))]
        public void AConverterRefusalKeepsItsMessage(string name, Func<ReadOnlyMemory<byte>, CborOptions, object> read)
        {
            CborOptions options = new CborOptions();
            options.Registry.ConverterRegistry.RegisterConverter(
                typeof(RefusedByItsConverter), new RefusingConverter());

            CborException exception = Assert.Throws<CborException>(
                () => read(WholeItem.HexToBytes(), options));

            Assert.StartsWith(ConverterMessage, exception.Message);
            Assert.Equal("$", exception.Path);

            // The message the entry point would invent if it discarded the refusal. Named rather than
            // implied, because that substitution is the failure this test exists to catch and it is
            // otherwise indistinguishable from a genuinely truncated document.
            Assert.DoesNotContain("no item was read", exception.Message);
            Assert.DoesNotContain("Unexpected end of buffer", exception.Message);
        }

        /// <summary>
        /// The same through a <c>[CborConstructor]</c>, which is the caller's own type refusing the
        /// values a document carried rather than a converter they wrote. It reaches the entry points
        /// through <c>CreatorMapping</c>, one layer further in.
        /// </summary>
        [Theory]
        [MemberData(nameof(CreatorEntryPoints))]
        public void ACreatorRefusalKeepsItsMessage(string name, Func<ReadOnlyMemory<byte>, CborOptions, object> read)
        {
            CborException exception = Assert.Throws<CborException>(
                () => read(WholeItem.HexToBytes(), new CborOptions()));

            Assert.StartsWith(CreatorMessage, exception.Message);
            Assert.Equal("$", exception.Path);
        }

        public static IEnumerable<object[]> CreatorEntryPoints()
        {
            yield return Row("Deserialize(ReadOnlySpan)",
                (bytes, options) => Cbor.Deserialize<RefusedByItsCreator>(bytes.Span, options));

            yield return Row("Deserialize(ReadOnlySequence)",
                (bytes, options) => Cbor.Deserialize<RefusedByItsCreator>(new ReadOnlySequence<byte>(bytes), options));

            yield return Row("DeserializeAsync(Stream)",
                (bytes, options) => Cbor.DeserializeAsync<RefusedByItsCreator>(
                    new MemoryStream(bytes.ToArray()), options).AsTask().GetAwaiter().GetResult());

            yield return Row("DeserializeAsync(PipeReader)",
                (bytes, options) => Cbor.DeserializeAsync<RefusedByItsCreator>(
                    PipeReader.Create(new ReadOnlySequence<byte>(bytes)), options).AsTask().GetAwaiter().GetResult());

            yield return Row("ReadNextItemAsync(PipeReader)",
                (bytes, options) => Cbor.ReadNextItemAsync<RefusedByItsCreator>(
                    PipeReader.Create(new ReadOnlySequence<byte>(bytes)), options).AsTask().GetAwaiter().GetResult());
        }

        private static object[] Row(string name, Func<ReadOnlyMemory<byte>, CborOptions, object> read)
        {
            return new object[] { name, read };
        }

        public class RefusedByItsConverter
        {
        }

        public class RefusingConverter : CborConverterBase<RefusedByItsConverter>
        {
            public override RefusedByItsConverter Read(ref CborReader reader)
            {
                throw new CborException(ConverterMessage);
            }

            public override void Write(ref CborWriter writer, RefusedByItsConverter value)
                => throw new NotSupportedException();
        }

        public class RefusedByItsCreator
        {
            public int Id { get; set; }

            [CborConstructor]
            public RefusedByItsCreator(int id)
            {
                throw new CborException($"this creator refuses {id}");
            }
        }
    }
}
