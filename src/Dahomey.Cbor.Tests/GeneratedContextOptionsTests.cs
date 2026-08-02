using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    public enum GeneratedOptionColour
    {
        Red = 0,
        Green = 1,
    }

    public class GeneratedOptionHolder
    {
        public GeneratedOptionColour Colour { get; set; }
        public int[] Offsets { get; set; }
    }

    [CborSerializable(typeof(GeneratedOptionHolder))]
    [CborSourceGenerationOptions(
        EnumFormat = ValueFormat.WriteToString,
        TypedArrayMode = TypedArrayMode.LittleEndian)]
    public partial class GeneratedOptionContext : CborSerializerContext
    {
    }

    /// <summary>
    /// Settings that change the wire format have to be declarable on the context, or a generated
    /// context cannot express them and anything derived from the context -- a CDDL schema, for
    /// instance -- describes the wrong bytes.
    /// </summary>
    public class GeneratedContextOptionsTests
    {
        private static readonly GeneratedOptionContext Context =
            CborSerializerContext.Default<GeneratedOptionContext>();

        [Fact]
        public void EnumFormatReachesTheGeneratedOptions()
        {
            Assert.Equal(ValueFormat.WriteToString, Context.Options.EnumFormat);
        }

        [Fact]
        public void TypedArrayModeReachesTheGeneratedOptions()
        {
            Assert.Equal(TypedArrayMode.LittleEndian, Context.Options.TypedArrayMode);
        }

        [Fact]
        public void EnumIsWrittenAsTextWhenDeclared()
        {
            GeneratedOptionHolder value = new GeneratedOptionHolder
            {
                Colour = GeneratedOptionColour.Green,
                Offsets = new[] { 1 },
            };

            string hexBuffer = Helper.Write(value, Context.Options);

            Assert.Contains("65477265656E", hexBuffer); // "Green" as a text string
        }

        [Fact]
        public void NumericArrayIsTaggedWhenTypedArrayModeIsDeclared()
        {
            GeneratedOptionHolder value = new GeneratedOptionHolder
            {
                Colour = GeneratedOptionColour.Red,
                Offsets = new[] { 1 },
            };

            string hexBuffer = Helper.Write(value, Context.Options);

            // tag 78, sint32 little endian
            Assert.Contains("D84E", hexBuffer);
        }
    }
}
