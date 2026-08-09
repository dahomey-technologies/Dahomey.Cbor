using Dahomey.Cbor.ObjectModel;
using Dahomey.Cbor.Tests.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Dahomey.Cbor.Tests.Issues
{
    /// <summary>
    /// Issue #172: reading a semantic tag in front of an empty text string stamped the tag onto the
    /// process-wide shared <see cref="CborString"/> instance.
    /// </summary>
    /// <remarks>
    /// The same aliasing bug #165 fixed, on the one type that fix left out. <see cref="CborString"/>
    /// caches a single value rather than a range — <c>(CborString)string.Empty</c> always returns the
    /// same instance — which was enough for it to be missed as a cache and so to keep inheriting the
    /// base <c>WithSemanticTag</c>, which tags in place. Every <c>""</c> in the process then carried
    /// the tag: empty strings written in code, and empty strings decoded from documents that never
    /// carried one.
    /// <para>
    /// Fixed the way the other sharers are: copy first, tag the copy.
    /// </para>
    /// </remarks>
    public class Issue0172
    {
        /// <summary>The repro from the issue.</summary>
        [Fact]
        public void ATagOnAnEmptyStringDoesNotLeakIntoTheSharedInstance()
        {
            // c0    tag(0)
            //    60 ""
            CborValue tagged = Cbor.Deserialize<CborValue>("C060".HexToBytes());
            CborValue fresh = (CborValue)"";

            Assert.Equal(0UL, tagged.SemanticTag);
            Assert.Null(fresh.SemanticTag);
            Assert.False(ReferenceEquals(tagged, fresh));
        }

        /// <summary>
        /// The consequence that reaches the bytes: an untagged empty string must still serialize as
        /// <c>60</c>, in a document read after the tagged one and in a value built in code.
        /// </summary>
        [Fact]
        public void AnUntaggedEmptyStringStillWritesUntagged()
        {
            CborValue tagged = Cbor.Deserialize<CborValue>("C060".HexToBytes());
            Assert.Equal(0UL, tagged.SemanticTag);

            // A different document, carrying no tag, whose item is the same empty string.
            CborArray other = Cbor.Deserialize<CborArray>("8160".HexToBytes());
            Assert.Null(other[0].SemanticTag);
            Helper.TestWrite(other, "8160");

            // And a value built in code, which was never read from anything.
            Helper.TestWrite(new CborArray { "" }, "8160");
        }

        /// <summary>The tagged value itself round trips, so copying does not lose the tag.</summary>
        [Fact]
        public void ATaggedEmptyStringSurvivesARoundTrip()
        {
            const string hexBuffer = "C060";

            CborValue value = Cbor.Deserialize<CborValue>(hexBuffer.HexToBytes());

            Assert.Equal(0UL, value.SemanticTag);
            Assert.Equal("", value.Value<string>());
            Helper.TestWrite(value, hexBuffer);
        }

        /// <summary>
        /// A non-empty string is constructed fresh per value, so it has nothing to alias. The override
        /// is unconditional and copies it anyway — one small allocation per tagged-string read, for a
        /// result indistinguishable from tagging in place.
        /// </summary>
        [Fact]
        public void ATaggedNonEmptyStringIsUnaffected()
        {
            // c0            tag(0)
            //    63 666f6f  "foo"
            const string hexBuffer = "C063666F6F";

            CborValue value = Cbor.Deserialize<CborValue>(hexBuffer.HexToBytes());

            Assert.Equal(0UL, value.SemanticTag);
            Assert.Equal("foo", value.Value<string>());
            Helper.TestWrite(value, hexBuffer);
            Assert.Null(((CborValue)"foo").SemanticTag);
        }

        /// <summary>
        /// The guard the issue asks for: every instance any type hands out from a static cache must
        /// come from a type that overrides <c>WithSemanticTag</c>, so the next type that starts
        /// caching does not repeat this.
        /// </summary>
        /// <remarks>
        /// Driven off the caches themselves rather than off a list of type names, which is the point:
        /// a hand-maintained list is exactly what missed <see cref="CborString"/>. Reflection reads
        /// the static <see cref="CborValue"/>-typed fields of every type in the object model —
        /// including <see cref="CborValue"/>'s own, which is where the shared null lives — and asserts
        /// on the runtime type of each cached instance.
        /// </remarks>
        [Fact]
        public void EveryCachedInstanceComesFromATypeThatCopiesBeforeTagging()
        {
            List<Type> cachingTypes = CachedInstances()
                .Select(instance => instance.GetType())
                .Distinct()
                .ToList();

            // The sweep is worthless if it finds nothing; CborString is the type this issue is about.
            Assert.Contains(typeof(CborString), cachingTypes);
            Assert.True(cachingTypes.Count > 1, "expected several caching types, found " + cachingTypes.Count);

            List<Type> taggingInPlace = cachingTypes.Where(type => !OverridesWithSemanticTag(type)).ToList();

            Assert.True(
                taggingInPlace.Count == 0,
                "these types hand out shared instances but tag in place: "
                    + string.Join(", ", taggingInPlace.Select(type => type.Name)));
        }

        private static IEnumerable<CborValue> CachedInstances()
        {
            IEnumerable<Type> objectModelTypes = typeof(CborValue).Assembly
                .GetTypes()
                .Where(type => typeof(CborValue).IsAssignableFrom(type));

            foreach (Type type in objectModelTypes)
            {
                FieldInfo[] fields = type.GetFields(
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

                foreach (FieldInfo field in fields)
                {
                    object value = field.GetValue(null);

                    if (value is CborValue cborValue)
                    {
                        yield return cborValue;
                    }
                    else if (value is IEnumerable<CborValue> cborValues)
                    {
                        foreach (CborValue item in cborValues)
                        {
                            yield return item;
                        }
                    }
                }
            }
        }

        private static bool OverridesWithSemanticTag(Type type)
        {
            MethodInfo method = type.GetMethod(
                "WithSemanticTag",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            return method != null && method.DeclaringType == type;
        }
    }
}
