using System;
using Dahomey.Cbor.Tests.Extensions;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    /// <summary>
    /// What the reflection path does for each <c>ShouldSerialize&lt;Member&gt;()</c> shape that the
    /// generator reports as CBOR1017 or deliberately leaves silent (see <c>DiagnosticTests</c>).
    /// </summary>
    /// <remarks>
    /// These types are in no generated context, because the generator would refuse the first group.
    /// The first group is the claim behind the diagnostic -- the reflection path throws while building
    /// the mapping -- and the second is the claim behind staying silent: it writes the type normally.
    /// </remarks>
    public class ShouldSerializeReflectionRefusalTests
    {
        public class StaticOnDeclaring { public int Id { get; set; } public string Name { get; set; } public static bool ShouldSerializeName() => false; }
        public class GenericOnDeclaring { public int Id { get; set; } public string Name { get; set; } public bool ShouldSerializeName<T>() => false; }
        public class GenericBase { public bool ShouldSerializeName<T>() => false; }
        public class GenericFoundOnBase : GenericBase { public int Id { get; set; } public string Name { get; set; } }
        public class GenericAndPlain { public int Id { get; set; } public string Name { get; set; } public bool ShouldSerializeName() => false; public bool ShouldSerializeName<T>() => false; }
        public class PlainNotBoolAndGeneric { public int Id { get; set; } public string Name { get; set; } public int ShouldSerializeName() => 0; public bool ShouldSerializeName<T>() => false; }
        public struct StaticOnStruct { public int Id { get; set; } public string Name { get; set; } public static bool ShouldSerializeName() => false; }
        public struct GenericOnStruct { public int Id { get; set; } public string Name { get; set; } public bool ShouldSerializeName<T>() => false; }

        [Fact] public void StaticOnTheDeclaringTypeThrows() => Assert.Throws<ArgumentException>(() => Helper.Write(new StaticOnDeclaring()));
        [Fact] public void GenericOnTheDeclaringTypeThrows() => Assert.Throws<ArgumentException>(() => Helper.Write(new GenericOnDeclaring()));
        [Fact] public void GenericFoundOnABaseThrows() => Assert.Throws<ArgumentException>(() => Helper.Write(new GenericFoundOnBase()));
        [Fact] public void GenericAndNonGenericTogetherAreAmbiguous() => Assert.Throws<System.Reflection.AmbiguousMatchException>(() => Helper.Write(new GenericAndPlain()));
        [Fact] public void AmbiguityDoesNotWaitForTheReturnType() => Assert.Throws<System.Reflection.AmbiguousMatchException>(() => Helper.Write(new PlainNotBoolAndGeneric()));
        [Fact] public void StaticOnAStructThrows() => Assert.Throws<ArgumentException>(() => Helper.Write(new StaticOnStruct()));
        [Fact] public void GenericOnAStructThrows() => Assert.Throws<ArgumentException>(() => Helper.Write(new GenericOnStruct()));

        public class StaticBase { public static bool ShouldSerializeName() => false; }
        public class StaticInherited : StaticBase { public int Id { get; set; } public string Name { get; set; } }
        public class StaticNotBool { public int Id { get; set; } public string Name { get; set; } public static int ShouldSerializeName() => 0; }
        public class GenericNotBool { public int Id { get; set; } public string Name { get; set; } public int ShouldSerializeName<T>() => 0; }
        public class NonPublicStatic { public int Id { get; set; } public string Name { get; set; } internal static bool ShouldSerializeName() => false; }
        public class NonPublicGenericBesidePlain { public int Id { get; set; } public string Name { get; set; } public bool ShouldSerializeName() => false; internal bool ShouldSerializeName<T>() => false; }
        public class PlainHidesGenericBase : GenericBase { public int Id { get; set; } public string Name { get; set; } public bool ShouldSerializeName() => false; }
        public struct PlainOnStruct { public int Id { get; set; } public string Name { get; set; } public bool ShouldSerializeName() => false; }

        // Id 1 and Name null is A2 6249 64 01 64 4E616D65 F6 when both are written, A1 6249 64 01 when
        // the predicate omits Name.
        private const string BothWritten = "A262496401644E616D65F6";
        private const string NameOmitted = "A162496401";

        [Fact] public void AStaticInheritedFromABaseIsSkipped() => Assert.Equal(BothWritten, Helper.Write(new StaticInherited { Id = 1 }), ignoreCase: true);
        [Fact] public void AStaticThatIsNotBoolIsIgnored() => Assert.Equal(BothWritten, Helper.Write(new StaticNotBool { Id = 1 }), ignoreCase: true);
        [Fact] public void AGenericThatIsNotBoolIsIgnored() => Assert.Equal(BothWritten, Helper.Write(new GenericNotBool { Id = 1 }), ignoreCase: true);
        [Fact] public void ANonPublicStaticIsIgnored() => Assert.Equal(BothWritten, Helper.Write(new NonPublicStatic { Id = 1 }), ignoreCase: true);
        [Fact] public void ANonPublicGenericBesideAPlainOneIsNotAmbiguous() => Assert.Equal(NameOmitted, Helper.Write(new NonPublicGenericBesidePlain { Id = 1 }), ignoreCase: true);
        [Fact] public void APlainOneNearerThanAGenericBaseWins() => Assert.Equal(NameOmitted, Helper.Write(new PlainHidesGenericBase { Id = 1 }), ignoreCase: true);
        [Fact] public void APlainOneOnAStructWritesEverything() => Assert.Equal(BothWritten, Helper.Write(new PlainOnStruct { Id = 1 }), ignoreCase: true);
    }
}
