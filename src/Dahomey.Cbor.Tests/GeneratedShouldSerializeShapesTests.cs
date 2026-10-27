using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Tests.Extensions;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    // Each type below is one shape of ShouldSerialize<Member>() the reflection path resolves in a
    // particular way: it looks the method up on the member's DECLARING type, public and
    // parameterless only, and accepts it only when it returns bool. The generated path must agree
    // on every one of them, or the same value writes different bytes depending on the path.

    public class ShouldSerializeBase
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    /// <summary>Declares the predicate on the derived type only: the member's declaring type has none.</summary>
    public class ShouldSerializeOnDerivedOnly : ShouldSerializeBase
    {
        public bool ShouldSerializeName() => false;
    }

    public class ShouldSerializeBaseWithPredicate
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public bool ShouldSerializeName() => false;
    }

    /// <summary>Inherits a predicate declared where the member is.</summary>
    public class ShouldSerializeInherited : ShouldSerializeBaseWithPredicate
    {
        public int Extra { get; set; }
    }

    /// <summary>Hides the base predicate with <c>new</c>; the member's declaring type still owns the old one.</summary>
    public class ShouldSerializeHidden : ShouldSerializeBaseWithPredicate
    {
        public new bool ShouldSerializeName() => true;
    }

    public class ShouldSerializeVirtualBase
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public virtual bool ShouldSerializeName() => true;
    }

    /// <summary>An override is dispatched virtually, so the derived body governs.</summary>
    public class ShouldSerializeOverridden : ShouldSerializeVirtualBase
    {
        public override bool ShouldSerializeName() => false;
    }

    /// <summary>An overridden property is redeclared, so the derived type is its declaring type.</summary>
    public class ShouldSerializeOverridingProperty : ShouldSerializeBaseWithVirtualProperty
    {
        public override string Name { get; set; }
        public bool ShouldSerializeName() => false;
    }

    public class ShouldSerializeBaseWithVirtualProperty
    {
        public int Id { get; set; }
        public virtual string Name { get; set; }
    }

    public class ShouldSerializeNotBool
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int ShouldSerializeName() => 0;
    }

    public class ShouldSerializeWithParameter
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public bool ShouldSerializeName(int x) => false;
    }

    public class ShouldSerializeNonPublic
    {
        public int Id { get; set; }
        public string Name { get; set; }
        internal bool ShouldSerializeName() => false;
    }

    public struct ShouldSerializeStruct
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public bool ShouldSerializeName() => false;
    }

    [CborSerializable(typeof(ShouldSerializeOnDerivedOnly))]
    [CborSerializable(typeof(ShouldSerializeInherited))]
    [CborSerializable(typeof(ShouldSerializeHidden))]
    [CborSerializable(typeof(ShouldSerializeOverridden))]
    [CborSerializable(typeof(ShouldSerializeOverridingProperty))]
    [CborSerializable(typeof(ShouldSerializeNotBool))]
    [CborSerializable(typeof(ShouldSerializeWithParameter))]
    [CborSerializable(typeof(ShouldSerializeNonPublic))]
    [CborSerializable(typeof(ShouldSerializeStruct))]
    public partial class ShouldSerializeShapesContext : CborSerializerContext
    {
    }

    public class GeneratedShouldSerializeShapesTests
    {
        private static void Agree<T>(T value)
        {
            ShouldSerializeShapesContext context = new ShouldSerializeShapesContext();
            Assert.Equal(Helper.Write(value), Helper.Write(value, context.Options), ignoreCase: true);
        }

        [Fact] public void DeclaredOnTheDerivedTypeOnly() => Agree(new ShouldSerializeOnDerivedOnly { Id = 1, Name = "n" });
        [Fact] public void Inherited() => Agree(new ShouldSerializeInherited { Id = 1, Name = "n" });
        [Fact] public void HiddenWithNew() => Agree(new ShouldSerializeHidden { Id = 1, Name = "n" });
        [Fact] public void Overridden() => Agree(new ShouldSerializeOverridden { Id = 1, Name = "n" });
        [Fact] public void OnAnOverridingProperty() => Agree(new ShouldSerializeOverridingProperty { Id = 1, Name = "n" });
        [Fact] public void NotReturningBool() => Agree(new ShouldSerializeNotBool { Id = 1, Name = "n" });
        [Fact] public void WithAParameter() => Agree(new ShouldSerializeWithParameter { Id = 1, Name = "n" });
        [Fact] public void NonPublic() => Agree(new ShouldSerializeNonPublic { Id = 1, Name = "n" });
        [Fact] public void OnAStruct() => Agree(new ShouldSerializeStruct { Id = 1, Name = "n" });
    }
}
