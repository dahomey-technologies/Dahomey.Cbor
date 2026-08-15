using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization.Converters.Mappings;
using System.Linq;
using Xunit;

namespace Dahomey.Cbor.Tests.Issues
{
    /// <summary>
    /// <c>DiscriminatorMapping.EnsureInitialize</c> spelled the double-checked pattern out in full and
    /// never assigned the flag it tested, so both checks always passed: every read of
    /// <c>MemberName</c> took the lock, went back to the registry, and decoded the name into a fresh
    /// string.
    /// </summary>
    /// <remarks>
    /// The flag latches on having resolved a convention, not on having looked for one - assigning it
    /// unconditionally would freeze the name at <c>null</c> for a type whose subtypes are registered
    /// after the first lookup, which is what #224 is about. Now that #224 has stopped the registry
    /// memoising its own <c>null</c>, both halves of that contract are observable from here, and
    /// there is a test for each.
    /// </remarks>
    public class Issue0225
    {
        public abstract class Shape
        {
        }

        [CborDiscriminator("circle")]
        public class Circle : Shape
        {
            public double Radius { get; set; }
        }

        [CborIntDiscriminator(7)]
        public class NumberedSeven : Shape
        {
            public int Value { get; set; }
        }

        private static IMemberMapping DiscriminatorMappingOf<T>(CborOptions options)
        {
            return options.Registry.ObjectMappingRegistry
                .Lookup<T>()
                .MemberMappings
                .OfType<IDiscriminatorMapping>()
                .Single();
        }

        /// <summary>
        /// Two reads of the same mapping hand back the same string. Before the fix each one allocated,
        /// which is the visible trace of a lock and a registry lookup that were meant to happen once.
        /// </summary>
        [Fact]
        public void TheDiscriminatorMemberNameIsResolvedOnce()
        {
            CborOptions options = new CborOptions();
            options.Registry.DiscriminatorConventionRegistry.RegisterType<Circle>();

            IMemberMapping mapping = DiscriminatorMappingOf<Circle>(options);

            string first = mapping.MemberName;
            string second = mapping.MemberName;

            Assert.Equal("_t", first);
            Assert.Same(first, second);
        }

        /// <summary>
        /// The name the mapping keeps is the one the convention in force actually carries, not the
        /// default it would have had. Latching the wrong string is worse than re-deriving the right
        /// one, so the value is pinned as well as its identity.
        /// </summary>
        [Fact]
        public void TheLatchedNameIsTheOneTheConventionCarries()
        {
            CborOptions options = new CborOptions();
            options.Registry.DiscriminatorConventionRegistry.ClearConventions();
            options.Registry.DiscriminatorConventionRegistry.RegisterConvention(
                new Serialization.Conventions.DefaultDiscriminatorConvention<string>(options.Registry, "$type"));
            options.Registry.DiscriminatorConventionRegistry.RegisterType<Circle>();

            IMemberMapping mapping = DiscriminatorMappingOf<Circle>(options);

            string first = mapping.MemberName;
            string second = mapping.MemberName;

            Assert.Equal("$type", first);
            Assert.Same(first, second);
        }

        /// <summary>
        /// The other half of the latch: a read taken while nothing resolves finds no convention, and
        /// that non-answer must not be what the mapping keeps. Latching on the attempt rather than on
        /// the resolution would pin <c>null</c> here for the lifetime of the options.
        /// </summary>
        /// <remarks>
        /// The mapping is built here rather than fetched through <see cref="DiscriminatorMappingOf{T}"/>
        /// because an unresolved one cannot be reached that way: <c>ObjectMapping.EnsureInitialize</c>
        /// validates that every member mapping has a name, so reading <c>MemberMappings</c> throws
        /// before it can hand this one back. The type is one whose discriminator is an <c>int</c> in a
        /// registry holding only the <c>string</c> convention, which is what makes the first lookup
        /// resolve to nothing without contriving anything.
        /// </remarks>
        [Fact]
        public void ARegistrationAfterTheFirstReadStillReachesTheMapping()
        {
            CborOptions options = new CborOptions();
            options.Registry.DiscriminatorConventionRegistry.ClearConventions();
            options.Registry.DiscriminatorConventionRegistry.RegisterConvention(
                new Serialization.Conventions.DefaultDiscriminatorConvention<string>(options.Registry));

            IMemberMapping mapping = new DiscriminatorMapping<NumberedSeven>(
                options, options.Registry.ObjectMappingRegistry.Lookup<NumberedSeven>());

            Assert.Null(mapping.MemberName);

            options.Registry.DiscriminatorConventionRegistry.RegisterConvention(
                new Serialization.Conventions.DefaultDiscriminatorConvention<int>(options.Registry));

            Assert.Equal("_t", mapping.MemberName);
        }
    }
}
