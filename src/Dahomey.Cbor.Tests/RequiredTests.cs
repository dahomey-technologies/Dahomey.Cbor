using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Tests.Extensions;
using System;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    public class RequiredTests
    {
        public class StringObject
        {
            public string String { get; set; }
        }

        [Theory]
        [InlineData(RequirementPolicy.Never, "A0", null)]
        [InlineData(RequirementPolicy.Never, "A166537472696E67F6", null)]
        [InlineData(RequirementPolicy.Never, "A166537472696E6763466F6F", null)]
        [InlineData(RequirementPolicy.Always, "A0", typeof(CborException))]
        [InlineData(RequirementPolicy.Always, "A166537472696E67F6", typeof(CborException))]
        [InlineData(RequirementPolicy.Always, "A166537472696E6763466F6F", null)]
        [InlineData(RequirementPolicy.AllowNull, "A0", typeof(CborException))]
        [InlineData(RequirementPolicy.AllowNull, "A166537472696E67F6", null)]
        [InlineData(RequirementPolicy.AllowNull, "A166537472696E6763466F6F", null)]
        [InlineData(RequirementPolicy.DisallowNull, "A0", null)]
        [InlineData(RequirementPolicy.DisallowNull, "A166537472696E67F6", typeof(CborException))]
        [InlineData(RequirementPolicy.DisallowNull, "A166537472696E6763466F6F", null)]
        public void TestRead(RequirementPolicy requirementPolicy, string hexBuffer, Type expectedExceptionType)
        {
            CborOptions options = new CborOptions();
            options.Registry.ObjectMappingRegistry.Register<StringObject>(objectMapping =>
                objectMapping
                    .AutoMap()
                    .ClearMemberMappings()
                    .MapMember(o => o.String).SetRequired(requirementPolicy)
            );

            Helper.TestRead<StringObject>(hexBuffer, expectedExceptionType, options);
        }

        [Theory]
        [InlineData(RequirementPolicy.Never, "", null)]
        [InlineData(RequirementPolicy.Never, null, null)]
        [InlineData(RequirementPolicy.Never, "Foo", null)]
        [InlineData(RequirementPolicy.Always, "", null)]
        [InlineData(RequirementPolicy.Always, null, typeof(CborException))]
        [InlineData(RequirementPolicy.Always, "Foo", null)]
        [InlineData(RequirementPolicy.AllowNull, "", null)]
        [InlineData(RequirementPolicy.AllowNull, null, null)]
        [InlineData(RequirementPolicy.AllowNull, "Foo", null)]
        [InlineData(RequirementPolicy.DisallowNull, "", null)]
        [InlineData(RequirementPolicy.DisallowNull, null, typeof(CborException))]
        [InlineData(RequirementPolicy.DisallowNull, "Foo", null)]
        public void TestWrite(RequirementPolicy requirementPolicy, string value, Type expectedExceptionType)
        {
            CborOptions options = new CborOptions();
            options.Registry.ObjectMappingRegistry.Register<StringObject>(objectMapping =>
                objectMapping
                    .AutoMap()
                    .ClearMemberMappings()
                    .MapMember(o => o.String).SetRequired(requirementPolicy)
            );

            StringObject obj = new StringObject
            {
                String = value
            };

            Helper.TestWrite(obj, expectedExceptionType, options);
        }

        public class StringObjectWithAttribute
        {
            [CborRequired]
            public string String { get; set; }
        }

        [Theory]
        [InlineData("A0", typeof(CborException))]
        [InlineData("A166537472696E67F6", typeof(CborException))]
        [InlineData("A166537472696E6763466F6F", null)]
        public void TestReadWithAttribute(string hexBuffer, Type expectedExceptionType)
        {
            CborOptions options = new CborOptions();
            Helper.TestRead<StringObjectWithAttribute>(hexBuffer, expectedExceptionType, options);
        }

        [Theory]
        [InlineData("", null)]
        [InlineData(null, typeof(CborException))]
        [InlineData("Foo", null)]
        public void TestWriteWithAttribute(string value, Type expectedExceptionType)
        {
            CborOptions options = new CborOptions();

            StringObjectWithAttribute obj = new StringObjectWithAttribute
            {
                String = value
            };

            Helper.TestWrite(obj, expectedExceptionType, options);
        }

        // ---- required members declared on a derived type ----

        public abstract class Base
        {
            public int X { get; set; }
        }

        [CborDiscriminator("derived")]
        public class Derived : Base
        {
            [CborRequired]
            public int Y { get; set; }
        }

        [CborDiscriminator("plain")]
        public class Plain : Base
        {
            public int Z { get; set; }
        }

        public abstract class RequiringBase
        {
            [CborRequired]
            public int X { get; set; }
        }

        [CborDiscriminator("requiringDerived")]
        public class RequiringDerived : RequiringBase
        {
            [CborRequired]
            public int Y { get; set; }
        }

        private static CborOptions PolymorphicOptions()
        {
            CborOptions options = new CborOptions();
            options.Registry.DiscriminatorConventionRegistry.RegisterType<Derived>();
            options.Registry.DiscriminatorConventionRegistry.RegisterType<Plain>();
            options.Registry.DiscriminatorConventionRegistry.RegisterType<RequiringDerived>();
            return options;
        }

        /// <summary>
        /// A requirement declared only on a derived type holds when the document is read through the
        /// base, which is the only way a polymorphic document is ever read.
        /// </summary>
        /// <remarks>
        /// Whether tracking ran was decided from the <em>declared</em> type's required members, while
        /// the check at the end iterates the <em>resolved</em> converter's - so a base declaring none
        /// switched tracking off and the check never ran. Asserting the same document through
        /// <c>Derived</c> as well is the point: the requirement used to hold or not depending on which
        /// static type the caller named, which is what <c>[CborRequired]</c> exists to make impossible.
        /// </remarks>
        [Fact]
        public void ARequiredMemberOfADerivedTypeIsEnforcedThroughTheBase()
        {
            // {"_t": "derived", "X": 1}
            const string hexBuffer = "A2625F746764657269766564615801";

            Assert.Throws<CborException>(
                () => Cbor.Deserialize<Base>(hexBuffer.HexToBytes(), PolymorphicOptions()));
            Assert.Throws<CborException>(
                () => Cbor.Deserialize<Derived>(hexBuffer.HexToBytes(), PolymorphicOptions()));
        }

        [Fact]
        public void SupplyingTheDerivedTypesRequiredMemberIsEnough()
        {
            // {"_t": "derived", "X": 1, "Y": 2}
            const string hexBuffer = "A3625F746764657269766564615801615902";

            Base value = Cbor.Deserialize<Base>(hexBuffer.HexToBytes(), PolymorphicOptions());

            Derived derived = Assert.IsType<Derived>(value);
            Assert.Equal(1, derived.X);
            Assert.Equal(2, derived.Y);
        }

        /// <summary>
        /// A sibling that requires nothing is unaffected — tracking is turned on from the resolved
        /// type, so it is not paid for by every subtype of a hierarchy that has one demanding member.
        /// </summary>
        [Fact]
        public void ASiblingWithoutRequiredMembersStillReads()
        {
            // {"_t": "plain", "X": 1}
            const string hexBuffer = "A2625F7465706C61696E615801";

            Base value = Cbor.Deserialize<Base>(hexBuffer.HexToBytes(), PolymorphicOptions());

            Assert.Equal(1, Assert.IsType<Plain>(value).X);
        }

        /// <summary>
        /// Both lists are enforced when the base requires something as well, so turning tracking on
        /// from the resolved type has not replaced the declared type's requirements with it.
        /// </summary>
        [Theory]
        // {"_t": "requiringDerived", "Y": 2}   -- X, declared on the base, is missing
        [InlineData("A2625F7470726571756972696E6744657269766564615902")]
        // {"_t": "requiringDerived", "X": 1}   -- Y, declared on the derived type, is missing
        [InlineData("A2625F7470726571756972696E6744657269766564615801")]
        public void AMissingRequirementOfEitherTypeIsRefused(string hexBuffer)
        {
            Assert.Throws<CborException>(() => Cbor.Deserialize<RequiringBase>(hexBuffer.HexToBytes(), PolymorphicOptions()));
        }

        // The other two object formats resolve the discriminator on different arms of the same
        // switch, and the Array arm leaves the read early once it has - so each needs its own case
        // rather than being taken as covered by the map above.

        [CborObjectFormat(CborObjectFormat.IntKeyMap)]
        public abstract class IntKeyBase
        {
            [CborProperty(1)]
            public int X { get; set; }
        }

        [CborObjectFormat(CborObjectFormat.IntKeyMap)]
        [CborDiscriminator("intDerived")]
        public class IntKeyDerived : IntKeyBase
        {
            [CborProperty(2)]
            [CborRequired]
            public int Y { get; set; }
        }

        [CborObjectFormat(CborObjectFormat.Array)]
        public abstract class ArrayBase
        {
            [CborProperty(1)]
            public int X { get; set; }
        }

        [CborObjectFormat(CborObjectFormat.Array)]
        [CborDiscriminator("arrDerived")]
        public class ArrayDerived : ArrayBase
        {
            [CborProperty(2)]
            [CborRequired]
            public int Y { get; set; }
        }

        [Fact]
        public void ARequiredMemberOfADerivedTypeIsEnforcedInIntKeyMapFormat()
        {
            CborOptions options = new CborOptions();
            options.Registry.DiscriminatorConventionRegistry.RegisterType<IntKeyDerived>();

            // {0: "intDerived", 1: 1}      -- key 0 is the discriminator's slot; 2 is missing
            Assert.Throws<CborException>(
                () => Cbor.Deserialize<IntKeyBase>("A2006A696E74446572697665640101".HexToBytes(), options));

            // {0: "intDerived", 1: 1, 2: 2}
            IntKeyBase value = Cbor.Deserialize<IntKeyBase>(
                "A3006A696E744465726976656401010202".HexToBytes(), options);

            Assert.Equal(2, Assert.IsType<IntKeyDerived>(value).Y);
        }

        [Fact]
        public void ARequiredMemberOfADerivedTypeIsEnforcedInArrayFormat()
        {
            CborOptions options = new CborOptions();
            options.Registry.DiscriminatorConventionRegistry.RegisterType<ArrayDerived>();

            // [39("arrDerived"), 1]        -- item 0 is the discriminator's slot; item 2 is missing
            Assert.Throws<CborException>(
                () => Cbor.Deserialize<ArrayBase>("82D8276A6172724465726976656401".HexToBytes(), options));

            // [39("arrDerived"), 1, 2]
            ArrayBase value = Cbor.Deserialize<ArrayBase>(
                "83D8276A617272446572697665640102".HexToBytes(), options);

            Assert.Equal(2, Assert.IsType<ArrayDerived>(value).Y);
        }

        [Fact]
        public void SupplyingBothRequirementsReads()
        {
            // {"_t": "requiringDerived", "X": 1, "Y": 2}
            const string hexBuffer = "A3625F7470726571756972696E6744657269766564615801615902";

            RequiringBase value = Cbor.Deserialize<RequiringBase>(hexBuffer.HexToBytes(), PolymorphicOptions());

            RequiringDerived derived = Assert.IsType<RequiringDerived>(value);
            Assert.Equal(1, derived.X);
            Assert.Equal(2, derived.Y);
        }
    }
}
