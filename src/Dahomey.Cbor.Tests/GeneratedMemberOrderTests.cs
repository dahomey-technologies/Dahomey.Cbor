using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    public class GeneratedFieldFirst
    {
        public int F;

        public int P { get; set; }
    }

    public class GeneratedPropertiesBase
    {
        public int BP { get; set; }
    }

    public class GeneratedPropertiesDerivedAddsField : GeneratedPropertiesBase
    {
        public int DF;
    }

    public class GeneratedMixedBase
    {
        public int BF;

        public int BP { get; set; }
    }

    public class GeneratedMixedDerived : GeneratedMixedBase
    {
        public int DF;

        public int DP { get; set; }
    }

    [CborSerializable(typeof(GeneratedFieldFirst))]
    [CborSerializable(typeof(GeneratedPropertiesDerivedAddsField))]
    [CborSerializable(typeof(GeneratedMixedDerived))]
    public partial class MemberOrderContext : CborSerializerContext
    {
    }

    /// <summary>
    /// The reflection path maps every property, then every field, each most-derived type first, so a
    /// field declared before a property, or a field added to a base that has properties, comes after
    /// them on the wire. The generated context used to follow declaration order instead (#261).
    /// </summary>
    public class GeneratedMemberOrderTests
    {
        [Fact]
        public void PropertiesComeBeforeFields()
        {
            MemberOrderContext context = new MemberOrderContext();

            AssertOrder(new GeneratedFieldFirst { F = 1, P = 2 },
                "A2615002614601", context);                         // P, F
            AssertOrder(new GeneratedPropertiesDerivedAddsField { BP = 1, DF = 2 },
                "A26242500162444602", context);                     // BP, DF
            AssertOrder(new GeneratedMixedDerived { BF = 1, BP = 2, DF = 3, DP = 4 },
                "A462445004624250026244460362424601", context);     // DP, BP, DF, BF
        }

        private static void AssertOrder<T>(T value, string expected, CborSerializerContext context)
        {
            Assert.Equal(expected, Helper.Write(value));
            Assert.Equal(expected, Helper.Write(value, context.Options));
        }
    }
}
