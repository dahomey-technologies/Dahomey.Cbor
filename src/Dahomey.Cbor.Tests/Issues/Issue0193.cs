using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization.Converters.Mappings;
using System.Reflection;
using Xunit;

namespace Dahomey.Cbor.Tests.Issues
{
    /// <summary>
    /// Issue #193: <c>MapMember</c> always appended, so a call over a member the mapping already
    /// covered left the type with that member mapped twice. It now returns the mapping already
    /// covering the member.
    /// </summary>
    /// <remarks>
    /// <c>AutoMap</c> followed by <c>MapMember</c> reads as "take the conventions, then adjust this
    /// one member", and it is the natural way to reach <c>SetIngoreIfDefault</c>, <c>SetRequired</c>,
    /// <c>SetLengthMode</c> or <c>SetConverter</c> for a single member. Appending adjusted nothing:
    /// with a rename it wrote the member under two keys — a well-formed document carrying a member it
    /// should not, which reads back without complaint — and without one it produced two mappings under
    /// the one name, which #177 refuses when the mapping is built. Only the second was loud.
    /// <para>
    /// The identity is the <see cref="MemberInfo"/>, which both the lambda and the reflection
    /// overloads have, and which is what <c>MongoDB.Bson</c>'s <c>BsonClassMap.MapMember</c> — the API
    /// this one takes its shape from — keys on for the same purpose.
    /// </para>
    /// </remarks>
    public class Issue0193
    {
        public class Adjusted
        {
            public int A { get; set; }
            public int B { get; set; }
        }

        public class BaseWithAMember
        {
            public int Inherited { get; set; }
        }

        public class DerivedWithAMember : BaseWithAMember
        {
            public int Declared { get; set; }
        }

        public class PartiallyMapped
        {
            public int A { get; set; }

            [CborIgnore]
            public int B { get; set; }
        }

        [Fact]
        public void MapMemberReturnsTheMappingAlreadyCoveringTheMember()
        {
            CborOptions options = new CborOptions();
            MemberMapping<Adjusted> fromFirstCall = null;
            MemberMapping<Adjusted> fromSecondCall = null;

            options.Registry.ObjectMappingRegistry.Register<Adjusted>(objectMapping =>
            {
                objectMapping.AutoMap();
                fromFirstCall = objectMapping.MapMember(o => o.A);
                fromSecondCall = objectMapping.MapMember(o => o.A);
            });

            Assert.Same(fromFirstCall, fromSecondCall);

            IObjectMapping objectMapping = options.Registry.ObjectMappingRegistry.Lookup<Adjusted>();
            Assert.Equal(2, objectMapping.MemberMappings.Count);
            Assert.Contains(fromFirstCall, objectMapping.MemberMappings);
        }

        /// <summary>
        /// The reflection overloads reach the same mapping as the lambda one, since the identity is
        /// the member rather than the way it was named.
        /// </summary>
        [Fact]
        public void TheReflectionOverloadReachesTheSameMapping()
        {
            CborOptions options = new CborOptions();
            MemberMapping<Adjusted> fromLambda = null;
            MemberMapping<Adjusted> fromPropertyInfo = null;

            options.Registry.ObjectMappingRegistry.Register<Adjusted>(objectMapping =>
            {
                objectMapping.AutoMap();
                fromLambda = objectMapping.MapMember(o => o.A);
                fromPropertyInfo = objectMapping.MapMember(typeof(Adjusted).GetProperty(nameof(Adjusted.A)));
            });

            Assert.Same(fromLambda, fromPropertyInfo);
        }

        /// <summary>
        /// The shape the issue reports: a rename used to write the member under both names, the old
        /// one alongside the new.
        /// </summary>
        [Fact]
        public void RenamingAMappedMemberWritesItUnderTheNewNameOnly()
        {
            CborOptions options = new CborOptions();
            options.Registry.ObjectMappingRegistry.Register<Adjusted>(objectMapping =>
                objectMapping
                    .AutoMap()
                    .MapMember(o => o.A)
                        .SetMemberName("renamed")
            );

            Adjusted obj = new Adjusted { A = 7, B = 9 };

            // a2 67 "renamed" 07 61 "B" 09 -- and no 'A', which is what used to be written too
            const string hexBuffer = "A26772656E616D656407614209";
            Helper.TestWrite(obj, hexBuffer, null, options);
        }

        [Fact]
        public void ARenamedMemberReadsBackUnderItsNewName()
        {
            CborOptions options = new CborOptions();
            options.Registry.ObjectMappingRegistry.Register<Adjusted>(objectMapping =>
                objectMapping
                    .AutoMap()
                    .MapMember(o => o.A)
                        .SetMemberName("renamed")
            );

            Adjusted obj = Helper.Read<Adjusted>("A26772656E616D656407614209", options);

            Assert.NotNull(obj);
            Assert.Equal(7, obj.A);
            Assert.Equal(9, obj.B);
        }

        /// <summary>
        /// Adjusting a member without renaming it — the reason to make the call at all — used to leave
        /// two mappings under the one name, which #177 refuses.
        /// </summary>
        [Fact]
        public void AMappedMemberCanBeAdjustedWithoutRenamingIt()
        {
            CborOptions options = new CborOptions();
            options.Registry.ObjectMappingRegistry.Register<Adjusted>(objectMapping =>
                objectMapping
                    .AutoMap()
                    .MapMember(o => o.A)
                        .SetIngoreIfDefault(true)
            );

            Adjusted obj = new Adjusted { A = 0, B = 9 };

            const string hexBuffer = "A1614209";
            Helper.TestWrite(obj, hexBuffer, null, options);
        }

        /// <summary>
        /// A member declared on a base type is the same member, though the two <see cref="MemberInfo"/>
        /// are not equal.
        /// </summary>
        /// <remarks>
        /// The conventions reflect over the derived type, so an inherited member arrives with its
        /// <c>ReflectedType</c> set to that type, while <c>o =&gt; o.Inherited</c> compiles to the
        /// accessor on the declaring type and so arrives with <c>ReflectedType</c> set to the base.
        /// Reference equality separates them; the metadata definition does not.
        /// </remarks>
        [Fact]
        public void AnInheritedMemberIsTheSameMember()
        {
            CborOptions options = new CborOptions();
            options.Registry.ObjectMappingRegistry.Register<DerivedWithAMember>(objectMapping =>
                objectMapping
                    .AutoMap()
                    .MapMember(o => o.Inherited)
                        .SetMemberName("renamed")
            );

            DerivedWithAMember obj = new DerivedWithAMember { Declared = 7, Inherited = 9 };

            const string hexBuffer = "A2684465636C61726564076772656E616D656409";
            Helper.TestWrite(obj, hexBuffer, null, options);
        }

        /// <summary>
        /// A member the conventions left out is still added, which is the half of the old behaviour
        /// that was the point of the method.
        /// </summary>
        [Fact]
        public void AMemberTheConventionsLeftOutIsStillMapped()
        {
            CborOptions options = new CborOptions();
            options.Registry.ObjectMappingRegistry.Register<PartiallyMapped>(objectMapping =>
                objectMapping
                    .AutoMap()
                    .MapMember(o => o.B)
            );

            PartiallyMapped obj = new PartiallyMapped { A = 7, B = 9 };

            const string hexBuffer = "A2614107614209";
            Helper.TestWrite(obj, hexBuffer, null, options);
        }
    }
}
