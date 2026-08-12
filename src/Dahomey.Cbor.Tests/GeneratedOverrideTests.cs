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

    [CborSerializable(typeof(GeneratedOverrideHolder))]
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
    }
}
