using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    public class GeneratedOverrideBase
    {
        public virtual int Id { get; set; }
    }

    public class GeneratedOverrideHolder : GeneratedOverrideBase
    {
        public override int Id { get; set; }

        public string Name { get; set; }
    }

    /// <summary>
    /// Both attributes sit on the virtual declaration and neither is repeated on the override, which is
    /// where the collapse and the reflection path could disagree: <c>GetProperties</c> returns the
    /// override, and reflection then reads the attributes off it with inheritance.
    /// </summary>
    public class GeneratedInheritedAttributeBase
    {
        [CborProperty("id")]
        public virtual int Id { get; set; }

        [CborIgnore]
        public virtual string Secret { get; set; }
    }

    public class GeneratedInheritedAttributeHolder : GeneratedInheritedAttributeBase
    {
        public override int Id { get; set; }

        public override string Secret { get; set; }

        public string Name { get; set; }
    }

    /// <summary>
    /// The same collapse reached through a *constructed* base, which is the fragile half: it leans on
    /// <c>OverriddenProperty</c> returning the member of <c>Base&lt;int&gt;</c> rather than of the open
    /// generic, and on <c>SymbolEqualityComparer.Default</c> matching that against what
    /// <c>GetMembers()</c> yields.
    /// </summary>
    public class GeneratedGenericOverrideBase<T>
    {
        public virtual T Value { get; set; }
    }

    public class GeneratedGenericOverrideHolder : GeneratedGenericOverrideBase<int>
    {
        public override int Value { get; set; }

        public string Label { get; set; }
    }

    [CborSerializable(typeof(GeneratedOverrideHolder))]
    [CborSerializable(typeof(GeneratedInheritedAttributeHolder))]
    [CborSerializable(typeof(GeneratedGenericOverrideHolder))]
    public partial class OverrideContext : CborSerializerContext
    {
    }

    /// <summary>
    /// An overridden property is one member on both paths.
    /// </summary>
    /// <remarks>
    /// <c>Type.GetProperties</c> collapses an override onto its base, so the reflection path has always
    /// seen one property here. The generator walks the type and each of its bases, which means an
    /// override is declared twice in what it sees — and two mappings under one name write the key twice
    /// and, since #186 validates the mapping, throw while the context is being built. So this is a
    /// divergence that only a comparison against the reflection path can show, which is what
    /// <c>GeneratedCorpusTests</c> does for this type as well.
    /// </remarks>
    public class GeneratedOverrideTests
    {
        [Fact]
        public void AnOverriddenPropertyIsOneMemberOnBothPaths()
        {
            OverrideContext context = new OverrideContext();
            GeneratedOverrideHolder holder = new GeneratedOverrideHolder { Id = 7, Name = "seven" };

            string generated = Helper.Write(holder, context.Options);

            // Two members, so a map of two: Id once, not once per declaration.
            Assert.StartsWith("A2", generated, System.StringComparison.OrdinalIgnoreCase);
            Assert.Equal(Helper.Write(holder), generated, ignoreCase: true);

            GeneratedOverrideHolder read = Helper.Read<GeneratedOverrideHolder>(generated, context.Options);

            Assert.Equal(7, read.Id);
            Assert.Equal("seven", read.Name);
        }

        /// <summary>
        /// Collapsing onto the most-derived declaration matches <c>GetProperties</c> on member identity,
        /// and has to match it on where the attributes come from too. The reflection path reads them with
        /// inheritance — <c>MemberInfo.GetCustomAttribute&lt;CborPropertyAttribute&gt;()</c> and
        /// <c>IsDefined(typeof(CborIgnoreAttribute))</c> both go through the <c>inherit: true</c>
        /// overloads — so a declaration the collapse drops still carries what the member is mapped by.
        /// </summary>
        /// <remarks>
        /// This is the one place the divergence is silent rather than loud. Two mappings under one name
        /// throw while the context is built; one mapping under the wrong name writes a document that
        /// reads back as a different member's value, or as no member at all.
        /// </remarks>
        [Fact]
        public void AttributesOnTheOverriddenDeclarationAreCarried()
        {
            OverrideContext context = new OverrideContext();
            GeneratedInheritedAttributeHolder holder = new GeneratedInheritedAttributeHolder
            {
                Id = 7,
                Secret = "hidden",
                Name = "seven",
            };

            string generated = Helper.Write(holder, context.Options);

            // A2                      map of two: Secret is ignored on both paths
            //    62 6964 07           "id" (from the base declaration), not "Id"
            //    64 4E616D65 65 736576656E
            Assert.Equal("A262696407644E616D6565736576656E", generated, ignoreCase: true);
            Assert.Equal(Helper.Write(holder), generated, ignoreCase: true);

            GeneratedInheritedAttributeHolder read =
                Helper.Read<GeneratedInheritedAttributeHolder>(generated, context.Options);

            Assert.Equal(7, read.Id);
            Assert.Equal("seven", read.Name);
            Assert.Null(read.Secret);
        }

        /// <summary>
        /// The collapse reaches an override of a member declared on a constructed generic base, which no
        /// other fixture here covers and which is where <c>OverriddenProperty</c> could return a symbol
        /// that does not compare equal to anything <c>GetMembers()</c> yields.
        /// </summary>
        [Fact]
        public void AnOverrideThroughAGenericBaseIsOneMemberOnBothPaths()
        {
            OverrideContext context = new OverrideContext();
            GeneratedGenericOverrideHolder holder =
                new GeneratedGenericOverrideHolder { Value = 3, Label = "three" };

            string generated = Helper.Write(holder, context.Options);

            Assert.StartsWith("A2", generated, System.StringComparison.OrdinalIgnoreCase);
            Assert.Equal(Helper.Write(holder), generated, ignoreCase: true);

            GeneratedGenericOverrideHolder read =
                Helper.Read<GeneratedGenericOverrideHolder>(generated, context.Options);

            Assert.Equal(3, read.Value);
            Assert.Equal("three", read.Label);
        }
    }
}
