using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    public class CorpusBytes
    {
        public byte[] Blob { get; set; }
        public string Name { get; set; }
    }

    public class CorpusNested
    {
        public CorpusBytes Inner { get; set; }
        public List<int> Numbers { get; set; }
    }

    [CborSerializable(typeof(CorpusBytes))]
    [CborSerializable(typeof(CorpusNested))]
    public partial class CorpusContext : CborSerializerContext
    {
    }

    /// <summary>
    /// Walks every type declared by a generated context and asserts the generated converter writes
    /// the same bytes as the reflection path.
    /// </summary>
    /// <remarks>
    /// The point is that this enumerates the <c>[CborSerializable]</c> attributes rather than a
    /// hand-written list. A per-type test only protects the types someone remembered to write a test
    /// for, so a type quietly losing its handling — <c>byte[]</c> falling back from
    /// <c>ByteArrayConverter</c> to <c>ArrayConverter&lt;byte&gt;</c>, say — produces valid CBOR that
    /// still round-trips through this library and no failure anywhere. Byte identity with the
    /// reflection path is the actual contract, and it is only meaningful if it is checked for
    /// everything a context declares.
    /// <para>
    /// Add a type to a context and it is covered here automatically. Any type added below needs a
    /// sample in <see cref="Sample"/>.
    /// </para>
    /// </remarks>
    public class GeneratedCorpusTests
    {
        private static object Sample(Type type)
        {
            if (type == typeof(CorpusBytes))
            {
                return new CorpusBytes { Blob = new byte[] { 1, 2, 3 }, Name = "blob" };
            }

            if (type == typeof(CorpusNested))
            {
                return new CorpusNested
                {
                    Inner = new CorpusBytes { Blob = new byte[] { 4, 5 }, Name = "inner" },
                    Numbers = new List<int> { 1, 2, 3 },
                };
            }

            throw new InvalidOperationException(
                $"{type} is declared on a context but has no sample; add one to {nameof(Sample)}.");
        }

        public static IEnumerable<object[]> DeclaredTypes()
        {
            return typeof(CorpusContext)
                .GetCustomAttributes<CborSerializableAttribute>()
                .Select(attribute => new object[] { attribute.Type });
        }

        [Theory]
        [MemberData(nameof(DeclaredTypes))]
        public void GeneratedBytesMatchReflectionBytes(Type type)
        {
            CorpusContext context = CborSerializerContext.Default<CorpusContext>();
            object value = Sample(type);

            string reflection = WriteAs(type, value, null);
            string generated = WriteAs(type, value, context.Options);

            Assert.Equal(reflection, generated);
        }

        /// <summary>
        /// Writes through the non-generic entry point so the declared type drives converter
        /// selection, exactly as it does for a member of that type.
        /// </summary>
        private static string WriteAs(Type type, object value, CborOptions options)
        {
            using (Util.ByteBufferWriter bufferWriter = new Util.ByteBufferWriter())
            {
                Cbor.Serialize(value, type, bufferWriter, options);
                return BitConverter.ToString(bufferWriter.WrittenSpan.ToArray()).Replace("-", string.Empty);
            }
        }

        /// <summary>
        /// The regression the corpus exists to catch: <c>byte[]</c> is a CBOR byte string, and a
        /// generated <c>ArrayConverter&lt;byte&gt;</c> would write an array of small integers instead.
        /// Both are valid CBOR and both round-trip, which is why only a byte comparison finds it.
        /// </summary>
        [Fact]
        public void ByteArrayIsWrittenAsAByteString()
        {
            CorpusContext context = CborSerializerContext.Default<CorpusContext>();

            string generated = Helper.Write(
                new CorpusBytes { Blob = new byte[] { 1, 2, 3 }, Name = "blob" }, context.Options);

            // a2                    map(2)
            //    64 426c6f62        "Blob"
            //    43 010203          h'010203'   <- byte string, not 83 01 02 03
            //    64 4e616d65        "Name"
            //    64 626c6f62        "blob"
            Assert.Equal("A264426C6F6243010203644E616D6564626C6F62", generated);
        }
    }
}
