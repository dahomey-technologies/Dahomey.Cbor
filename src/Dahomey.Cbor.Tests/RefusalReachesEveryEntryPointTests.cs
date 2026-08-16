using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Serialization.Converters;
using Dahomey.Cbor.Tests.Extensions;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Reflection;
using System.Threading;
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
    /// omission rather than a silent one. The rows are the reading half of <see cref="Cbor"/>: the four
    /// single-item doors, the four multiple-item ones, and <c>ReadNextItemAsync</c>. The non-generic
    /// <c>Type</c> overloads are the same methods with the type passed rather than inferred, and reach
    /// the converter by the same route, so they are not repeated here.
    /// </para>
    /// <para>
    /// <see cref="EveryPublicReadEntryPointHasARow"/> is what makes that omission visible rather than
    /// merely asked for: it reads the entry points off <see cref="Cbor"/> and fails when one has no row.
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
            return EntryPointsFor<RefusedByItsConverter>();
        }

        public static IEnumerable<object[]> CreatorEntryPoints()
        {
            return EntryPointsFor<RefusedByItsCreator>();
        }

        /// <summary>
        /// A converter that refuses what it read. The converter is the shortest route to the contract —
        /// it is the caller's code, and every entry point reaches it the same way.
        /// </summary>
        [Theory]
        [MemberData(nameof(EntryPoints))]
        public async Task AConverterRefusalKeepsItsMessageAsync(string name, Func<ReadOnlyMemory<byte>, CborOptions, Task<object>> readAsync)
        {
            CborOptions options = new CborOptions();
            options.Registry.ConverterRegistry.RegisterConverter(
                typeof(RefusedByItsConverter), new RefusingConverter());

            CborException exception = await Assert.ThrowsAsync<CborException>(
                async () => await readAsync(WholeItem.HexToBytes(), options));

            Assert.True(exception.Message.StartsWith(ConverterMessage, StringComparison.Ordinal),
                $"{name} reported \"{exception.Message}\" rather than the converter's refusal.");
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
        public async Task ACreatorRefusalKeepsItsMessageAsync(string name, Func<ReadOnlyMemory<byte>, CborOptions, Task<object>> readAsync)
        {
            CborException exception = await Assert.ThrowsAsync<CborException>(
                async () => await readAsync(WholeItem.HexToBytes(), new CborOptions()));

            Assert.True(exception.Message.StartsWith(CreatorMessage, StringComparison.Ordinal),
                $"{name} reported \"{exception.Message}\" rather than the creator's refusal.");
            Assert.Equal("$", exception.Path);
        }

        /// <summary>
        /// The names of the reading half of <see cref="Cbor"/>. Serializing, <c>ToJson</c> and
        /// <c>DeserializeAnonymousType</c> — which forwards to <c>Deserialize</c> and so is not a door of
        /// its own — are outside what the theories claim to cover.
        /// </summary>
        private static readonly string[] ReadingMethodNames =
        {
            "Deserialize",
            "DeserializeMultiple",
            "DeserializeAsync",
            "DeserializeMultipleAsync",
            "ReadNextItemAsync",
        };

        /// <summary>
        /// The rows above are the subject rather than the authority: every public reading method of
        /// <see cref="Cbor"/> must have one. Without this, the remark asking for a row is the only thing
        /// keeping the theory level with the API, and that has now twice not been enough — a new overload
        /// would land, the suite would stay green, and the theory would cover every door but the new one,
        /// which is exactly the state <c>ReadNextItemAsync</c> was in for as long as it existed.
        /// </summary>
        [Fact]
        public void EveryPublicReadEntryPointHasARow()
        {
            HashSet<string> rows = new HashSet<string>(
                EntryPointsFor<RefusedByItsConverter>().Select(row => (string)row[0]), StringComparer.Ordinal);

            List<MethodInfo> reading = typeof(Cbor)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(method => Array.IndexOf(ReadingMethodNames, method.Name) >= 0)
                .ToList();

            // Reflection finding nothing would make every assertion below vacuously true, which is the one
            // way this test could fail to do its job while passing.
            Assert.NotEmpty(reading);

            List<string> missing = new List<string>();

            foreach (MethodInfo method in reading)
            {
                string door = DoorName(method);

                if (TakesTypeArgument(method))
                {
                    // A Type overload is the same method with the type passed rather than inferred, and
                    // reaches the converter by the same route, so it shares its twin's row rather than
                    // needing one. What has to hold is that the twin exists: a Type-only entry point would
                    // otherwise be dropped here in silence, which is the failure this test is about.
                    if (!reading.Any(other => !TakesTypeArgument(other) && DoorName(other) == door))
                    {
                        missing.Add($"{door} — a Type overload with no generic twin, so no row covers it");
                    }

                    continue;
                }

                if (!rows.Contains(door))
                {
                    missing.Add(door);
                }
            }

            Assert.True(missing.Count == 0,
                $"These read entry points have no row in {nameof(EntryPointsFor)}, so no theory asserts that a "
                + $"refusal reaches the caller through them:{Environment.NewLine}"
                + string.Join(Environment.NewLine, missing.Select(name => "  " + name)));
        }

        /// <summary>
        /// The label a row would carry for this method: its name and the kind of source it reads from,
        /// which is what distinguishes one door from another. Written to match the hand-written row names
        /// — a rename on either side fails this test rather than passing it quietly.
        /// </summary>
        private static string DoorName(MethodInfo method)
        {
            ParameterInfo source = method.GetParameters().FirstOrDefault(parameter =>
                parameter.ParameterType != typeof(Type)
                && parameter.ParameterType != typeof(CborOptions)
                && parameter.ParameterType != typeof(CancellationToken));

            string sourceName = source == null ? "?" : source.ParameterType.Name;

            // ReadOnlySpan<byte> reflects as "ReadOnlySpan`1"; the rows name the shape, not the element.
            int arity = sourceName.IndexOf('`');
            if (arity >= 0)
            {
                sourceName = sourceName.Substring(0, arity);
            }

            return $"{method.Name}({sourceName})";
        }

        private static bool TakesTypeArgument(MethodInfo method)
        {
            return method.GetParameters().Any(parameter => parameter.ParameterType == typeof(Type));
        }

        /// <summary>
        /// The same rows for either refusing type: what differs between the two theories is where the
        /// refusal comes from, not which doors have to carry it.
        /// </summary>
        /// <remarks>
        /// The synchronous doors are wrapped rather than awaited — a lambda holding a
        /// <see cref="ReadOnlySpan{T}"/> cannot be <c>async</c>, and the exception each one throws while
        /// the delegate is being invoked is caught by <c>Assert.ThrowsAsync</c> all the same.
        /// </remarks>
        private static IEnumerable<object[]> EntryPointsFor<T>()
        {
            yield return Row("Deserialize(ReadOnlySpan)",
                (bytes, options) => CompletedAsync(Cbor.Deserialize<T>(bytes.Span, options)));

            yield return Row("Deserialize(ReadOnlySequence)",
                (bytes, options) => CompletedAsync(Cbor.Deserialize<T>(new ReadOnlySequence<byte>(bytes), options)));

            yield return Row("DeserializeAsync(Stream)",
                async (bytes, options) => await Cbor.DeserializeAsync<T>(new MemoryStream(bytes.ToArray()), options));

            yield return Row("DeserializeAsync(PipeReader)",
                async (bytes, options) => await Cbor.DeserializeAsync<T>(
                    PipeReader.Create(new ReadOnlySequence<byte>(bytes)), options));

            yield return Row("DeserializeMultiple(ReadOnlySpan)",
                (bytes, options) => CompletedAsync(Cbor.DeserializeMultiple<T>(bytes.Span, options)));

            yield return Row("DeserializeMultiple(ReadOnlySequence)",
                (bytes, options) => CompletedAsync(Cbor.DeserializeMultiple<T>(new ReadOnlySequence<byte>(bytes), options)));

            yield return Row("DeserializeMultipleAsync(Stream)",
                async (bytes, options) => await Cbor.DeserializeMultipleAsync<T>(new MemoryStream(bytes.ToArray()), options));

            yield return Row("DeserializeMultipleAsync(PipeReader)",
                async (bytes, options) => await Cbor.DeserializeMultipleAsync<T>(
                    PipeReader.Create(new ReadOnlySequence<byte>(bytes)), options));

            yield return Row("ReadNextItemAsync(PipeReader)",
                async (bytes, options) => await Cbor.ReadNextItemAsync<T>(
                    PipeReader.Create(new ReadOnlySequence<byte>(bytes)), options));
        }

        private static Task<object> CompletedAsync(object result)
        {
            return Task.FromResult(result);
        }

        private static object[] Row(string name, Func<ReadOnlyMemory<byte>, CborOptions, Task<object>> readAsync)
        {
            return new object[] { name, readAsync };
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
