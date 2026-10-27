using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.ObjectModel;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Serialization.Conventions;
using Dahomey.Cbor.Tests.Extensions;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    /// <summary>
    /// A map or an array holding no items is an object with every member at its default, not a
    /// null: the instance exists whether or not a first item arrives to cause it.
    /// </summary>
    public class EmptyObjectTests
    {
        public class Plain
        {
            public int Id { get; set; }

            public string Name { get; set; }
        }

        public class Holder
        {
            public Plain Inner { get; set; }
        }

        public class HolderWithInitializer
        {
            public Plain Inner { get; set; } = new Plain { Id = 7, Name = "initializer" };
        }

        public static IEnumerable<object[]> EmptyForms()
        {
            yield return new object[] { CborObjectFormat.StringKeyMap, "A0" }; // {}
            yield return new object[] { CborObjectFormat.StringKeyMap, "BFFF" }; // {_ }
            yield return new object[] { CborObjectFormat.IntKeyMap, "A0" }; // {}
            yield return new object[] { CborObjectFormat.IntKeyMap, "BFFF" }; // {_ }
            yield return new object[] { CborObjectFormat.Array, "80" }; // []
            yield return new object[] { CborObjectFormat.Array, "9FFF" }; // [_ ]
        }

        /// <summary>
        /// Options for one object format. A member carries a name or an index, never both, so the types
        /// below serve <c>StringKeyMap</c> by their names and the other two formats by the indexes
        /// given here, in the order the members are listed.
        /// </summary>
        private static CborOptions OptionsFor(CborObjectFormat format)
        {
            CborOptions options = new CborOptions { ObjectFormat = format };

            if (format != CborObjectFormat.StringKeyMap)
            {
                Index<Plain>(options, 0, "Id", "Name");
                Index<Holder>(options, 0, "Inner");
                Index<HolderWithInitializer>(options, 0, "Inner");
                Index<WithCallbacks>(options, 0, "Id");
                Index<Initialisable>(options, 0, "Id");
                Index<AllIgnoredIfDefault>(options, 0, "Id", "Name");
                Index<WithRequired>(options, 0, "Id", "Name");
                Index<HolderOfRequired>(options, 0, "Inner");
                // A discriminator occupies the first slot of a type that has one.
                Index<Animal>(options, 1, "Legs");
                Index<Dog>(options, 1, "Legs", "Name");
            }

            return options;
        }

        private static void Index<T>(CborOptions options, int first, params string[] members)
        {
            options.Registry.ObjectMappingRegistry.Register<T>(objectMapping =>
            {
                objectMapping.AutoMap();

                for (int i = 0; i < members.Length; i++)
                {
                    objectMapping.MapMember(typeof(T).GetProperty(members[i])).SetMemberIndex(first + i);
                }
            });
        }

        private static T Deserialize<T>(string hexBuffer, CborOptions options = null)
        {
            return Cbor.Deserialize<T>(hexBuffer.HexToBytes(), options ?? CborOptions.Default);
        }

        private static void AssertDefaultInstance(Plain plain)
        {
            Assert.NotNull(plain);
            Assert.Equal(0, plain.Id);
            Assert.Null(plain.Name);
        }

        // ---- every empty form, in every position an object can be read from ----

        [Theory]
        [MemberData(nameof(EmptyForms))]
        public void AnEmptyObjectAtTheRootIsAnInstance(CborObjectFormat format, string empty)
        {
            AssertDefaultInstance(Helper.Read<Plain>(empty, OptionsFor(format)));
        }

        [Theory]
        [MemberData(nameof(EmptyForms))]
        public void AnEmptyObjectInAMemberIsAnInstance(CborObjectFormat format, string empty)
        {
            string hexBuffer = format switch
            {
                CborObjectFormat.StringKeyMap => "A165496E6E6572" + empty, // {"Inner": <empty>}
                CborObjectFormat.IntKeyMap => "A100" + empty, // {0: <empty>}
                _ => "81" + empty, // [<empty>]
            };

            Holder holder = Helper.Read<Holder>(hexBuffer, OptionsFor(format));

            Assert.NotNull(holder);
            AssertDefaultInstance(holder.Inner);
        }

        [Theory]
        [MemberData(nameof(EmptyForms))]
        public void AnEmptyObjectInAListIsAnInstance(CborObjectFormat format, string empty)
        {
            List<Plain> list = Deserialize<List<Plain>>("82" + empty + empty, OptionsFor(format)); // [<empty>, <empty>]

            Assert.Equal(2, list.Count);
            AssertDefaultInstance(list[0]);
            AssertDefaultInstance(list[1]);
            Assert.NotSame(list[0], list[1]);
        }

        [Theory]
        [MemberData(nameof(EmptyForms))]
        public void AnEmptyObjectInADictionaryIsAnInstance(CborObjectFormat format, string empty)
        {
            Dictionary<string, Plain> dictionary = Deserialize<Dictionary<string, Plain>>(
                "A1616A" + empty, OptionsFor(format)); // {"j": <empty>}

            Assert.Single(dictionary);
            AssertDefaultInstance(dictionary["j"]);
        }

        // ---- the member keeps its initializer's slot, but not its initializer's value ----

        [Theory]
        [MemberData(nameof(EmptyForms))]
        public void AnEmptyObjectReplacesAMembersInitializer(CborObjectFormat format, string empty)
        {
            string hexBuffer = format switch
            {
                CborObjectFormat.StringKeyMap => "A165496E6E6572" + empty, // {"Inner": <empty>}
                CborObjectFormat.IntKeyMap => "A100" + empty, // {0: <empty>}
                _ => "81" + empty, // [<empty>]
            };

            HolderWithInitializer holder = Helper.Read<HolderWithInitializer>(hexBuffer, OptionsFor(format));

            // The document says Inner is an object with nothing in it: a fresh instance, as for any
            // other object read into a member -- neither null nor the initializer's value.
            AssertDefaultInstance(holder.Inner);
        }

        [Fact]
        public void ANullMemberOverwritesAnInitializerWithNull()
        {
            HolderWithInitializer holder = Helper.Read<HolderWithInitializer>("A165496E6E6572F6"); // {"Inner": null}

            Assert.Null(holder.Inner);
        }

        // ---- null is not an empty object ----

        [Fact]
        public void ANullIsNotAnEmptyObject()
        {
            Assert.Null(Helper.Read<Plain>("F6")); // null
            Assert.Null(Helper.Read<Plain>("F6", OptionsFor(CborObjectFormat.Array)));
            Assert.Null(Helper.Read<Plain>("F6", OptionsFor(CborObjectFormat.IntKeyMap)));
        }

        [Fact]
        public void NullListElementsReadAsNull()
        {
            List<Plain> list = Deserialize<List<Plain>>("83F6A0F6"); // [null, {}, null]

            Assert.Equal(3, list.Count);
            Assert.Null(list[0]);
            AssertDefaultInstance(list[1]);
            Assert.Null(list[2]);
        }

        [Fact]
        public void UndefinedIsRefusedAsNotAMap()
        {
            // Not read as null and not read as an empty object: undefined is neither.
            CborException exception = Assert.Throws<CborException>(() => Deserialize<Plain>("F7")); // undefined

            Assert.Equal("$", exception.Path);
        }

        // ---- lifecycle: callbacks and ISupportInitialize see the instance ----

        public class WithCallbacks
        {
            public int Id { get; set; }

            [CborIgnore]
            public List<string> Log { get; } = new List<string>();

            [OnDeserializing]
            public void Before()
            {
                Log.Add("deserializing:" + Id);
            }

            [OnDeserialized]
            public void After()
            {
                Log.Add("deserialized");
            }
        }

        public class Initialisable : ISupportInitialize
        {
            public int Id { get; set; }

            [CborIgnore]
            public List<string> Log { get; } = new List<string>();

            public void BeginInit()
            {
                Log.Add("begin");
            }

            public void EndInit()
            {
                Log.Add("end");
            }
        }

        [Theory]
        [MemberData(nameof(EmptyForms))]
        public void OnDeserializingAndOnDeserializedRunOnTheInstance(CborObjectFormat format, string empty)
        {
            WithCallbacks callbacks = Deserialize<WithCallbacks>(empty, OptionsFor(format));

            Assert.NotNull(callbacks);
            Assert.Equal(new[] { "deserializing:0", "deserialized" }, callbacks.Log);
        }

        [Theory]
        [MemberData(nameof(EmptyForms))]
        public void ISupportInitializeRunsOnTheInstance(CborObjectFormat format, string empty)
        {
            Initialisable initialisable = Helper.Read<Initialisable>(empty, OptionsFor(format));

            Assert.NotNull(initialisable);
            Assert.Equal(new[] { "begin", "end" }, initialisable.Log);
        }

        [Fact]
        public void ACallbackSeesTheSameSequenceWhetherOrNotTheObjectHoldsAnything()
        {
            WithCallbacks emptied = Deserialize<WithCallbacks>("A0"); // {}
            WithCallbacks filled = Deserialize<WithCallbacks>("A16249640C"); // {"Id": 12}

            Assert.Equal(new[] { "deserializing:0", "deserialized" }, emptied.Log);
            Assert.Equal(new[] { "deserializing:0", "deserialized" }, filled.Log);
        }

        // ---- a write that omitted everything must read back ----

        public class Empty
        {
        }

        public class AllIgnoredIfDefault
        {
            [CborIgnoreIfDefault]
            public int Id { get; set; }

            [CborIgnoreIfDefault]
            public string Name { get; set; }
        }

        [Fact]
        public void AClassWithNoMembersRoundTrips()
        {
            Helper.TestWrite(new Empty(), "A0"); // {}

            Assert.NotNull(Helper.Read<Empty>("A0"));
            Assert.NotNull(Helper.Read<Empty>("BFFF"));
        }

        [Theory]
        [InlineData(CborObjectFormat.StringKeyMap)]
        [InlineData(CborObjectFormat.IntKeyMap)]
        public void AClassWhoseMembersAreAllDefaultRoundTrips(CborObjectFormat format)
        {
            CborOptions options = OptionsFor(format);

            // Every member is omitted, so the document is an empty map...
            string written = Helper.Write(new AllIgnoredIfDefault(), options);
            Assert.Equal("A0", written); // {}

            // ...which has to come back as the object that wrote it.
            AllIgnoredIfDefault readBack = Helper.Read<AllIgnoredIfDefault>(written, options);
            Assert.NotNull(readBack);
            Assert.Equal(0, readBack.Id);
            Assert.Null(readBack.Name);
        }

        [Fact]
        public void AMemberWhoseObjectOmittedEverythingRoundTrips()
        {
            Dictionary<string, AllIgnoredIfDefault> original = new Dictionary<string, AllIgnoredIfDefault>
            {
                ["a"] = new AllIgnoredIfDefault(),
                ["b"] = new AllIgnoredIfDefault { Id = 5 },
            };

            string written = Helper.Write(original);
            Assert.Equal("A26161A06162A162496405", written); // {"a": {}, "b": {"Id": 5}}

            Dictionary<string, AllIgnoredIfDefault> readBack = Deserialize<Dictionary<string, AllIgnoredIfDefault>>(written);
            Assert.NotNull(readBack["a"]);
            Assert.Equal(5, readBack["b"].Id);
        }

        // ---- required members: the empty object is held to them like any other ----

        public class WithRequired
        {
            [CborRequired]
            public int Id { get; set; }

            public string Name { get; set; }
        }

        public class HolderOfRequired
        {
            public WithRequired Inner { get; set; }
        }

        [Theory]
        [MemberData(nameof(EmptyForms))]
        public void AnEmptyObjectIsRefusedWhenAMemberIsRequired(CborObjectFormat format, string empty)
        {
            CborException exception = Assert.Throws<CborException>(
                () => Deserialize<WithRequired>(empty, OptionsFor(format)));

            Assert.Equal("$", exception.Path);
            Assert.Contains("Required property", exception.Message);
        }

        [Theory]
        [MemberData(nameof(EmptyForms))]
        public void AnEmptyObjectInAMemberIsRefusedWhenAMemberIsRequired(CborObjectFormat format, string empty)
        {
            string hexBuffer = format switch
            {
                CborObjectFormat.StringKeyMap => "A165496E6E6572" + empty, // {"Inner": <empty>}
                CborObjectFormat.IntKeyMap => "A100" + empty, // {0: <empty>}
                _ => "81" + empty, // [<empty>]
            };

            CborException exception = Assert.Throws<CborException>(
                () => Deserialize<HolderOfRequired>(hexBuffer, OptionsFor(format)));

            Assert.Equal("$.Inner", exception.Path);
        }

        [Fact]
        public void ARequiredMemberThatIsPresentSatisfiesTheCheck()
        {
            WithRequired value = Helper.Read<WithRequired>("A16249640C"); // {"Id": 12}

            Assert.Equal(12, value.Id);
        }

        // ---- values and creator-built objects: an instance with or without items ----

        public struct PlainStruct
        {
            public int Id { get; set; }
        }

        public class ByConstructor
        {
            public int Id { get; }

            [CborConstructor]
            public ByConstructor()
            {
                Id = 9;
            }
        }

        [Fact]
        public void AStructIsAValue()
        {
            Assert.Equal(0, Helper.Read<PlainStruct>("A0").Id); // {}
            Assert.Equal(0, Helper.Read<PlainStruct>("BFFF").Id); // {_ }
        }

        [Fact]
        public void ANullableStructIsAValue()
        {
            PlainStruct? emptied = Helper.Read<PlainStruct?>("A0"); // {}

            Assert.True(emptied.HasValue);
            Assert.Equal(0, emptied.Value.Id);
            Assert.Null(Helper.Read<PlainStruct?>("F6")); // null
        }

        [Fact]
        public void AnObjectWithAConstructorMappingIsAnInstance()
        {
            Assert.Equal(9, Helper.Read<ByConstructor>("A0").Id); // {}
            Assert.Equal(9, Helper.Read<ByConstructor>("BFFF").Id); // {_ }
        }

        // ---- other containers: an empty one is empty, not null ----

        [Fact]
        public void OtherEmptyContainersAreEmptyNotNull()
        {
            Assert.Empty(Helper.Read<Dictionary<string, int>>("A0")); // {}
            Assert.Empty(Helper.Read<List<int>>("80")); // []
            Assert.Empty(Helper.Read<int[]>("9FFF")); // [_ ]
            Assert.Empty(Assert.IsType<CborObject>(Helper.Read<CborValue>("A0"))); // {}
            Assert.Empty(Assert.IsType<CborArray>(Helper.Read<CborValue>("80"))); // []
        }

        // ---- polymorphism: the discriminator is what decides the type, and an empty object has none ----

        public class Animal
        {
            public int Legs { get; set; }
        }

        [CborDiscriminator("dog")]
        public class Dog : Animal
        {
            public string Name { get; set; }
        }

        public abstract class Shape
        {
            public int Sides { get; set; }
        }

        [CborDiscriminator("square")]
        public class Square : Shape
        {
        }

        public interface IToken
        {
        }

        public class HolderOfToken
        {
            public IToken Token { get; set; }
        }

        [Theory]
        [MemberData(nameof(EmptyForms))]
        public void AnEmptyObjectOfAConcretePolymorphicBaseIsTheBase(CborObjectFormat format, string empty)
        {
            CborOptions options = OptionsFor(format);
            options.Registry.DiscriminatorConventionRegistry.RegisterType<Dog>();

            Animal animal = Helper.Read<Animal>(empty, options);

            // Nothing in the document names a subtype, exactly as in {"Legs": 4} without a discriminator.
            Assert.NotNull(animal);
            Assert.IsType<Animal>(animal);
            Assert.Equal(0, animal.Legs);
        }

        [Fact]
        public void ASubtypeIsChosenByItsDiscriminator()
        {
            CborOptions options = new CborOptions();
            options.Registry.DiscriminatorConventionRegistry.RegisterType<Dog>();

            Animal animal = Helper.Read<Animal>("A1625F7463646F67", options); // {"_t": "dog"}

            Assert.IsType<Dog>(animal);
        }

        [Fact]
        public void AnEmptyObjectIsNotTheFallbackType()
        {
            CborOptions options = new CborOptions { UnhandledNameMode = UnhandledNameMode.Silent };
            DiscriminatorConventionRegistry registry = options.Registry.DiscriminatorConventionRegistry;
            registry.ClearConventions();
            registry.RegisterConvention(new DefaultDiscriminatorConvention<string>(options.Registry, "_t", typeof(Dog)));
            registry.RegisterType<Dog>();

            // An unregistered discriminator is what the fallback exists for.
            Assert.IsType<Dog>(Helper.Read<Animal>("A1625F7463636174", options)); // {"_t": "cat"}

            // No discriminator at all is not an unknown one: the declared type, as with {"Legs": 4}.
            Assert.IsType<Animal>(Helper.Read<Animal>("A0", options)); // {}
            Assert.IsType<Animal>(Helper.Read<Animal>("A1644C65677304", options)); // {"Legs": 4}
        }

        [Fact]
        public void AnEmptyObjectOfAnAbstractTypeIsRefusedLikeAnyOtherWithoutADiscriminator()
        {
            CborOptions options = new CborOptions();
            options.Registry.DiscriminatorConventionRegistry.RegisterType<Square>();

            // {"Sides": 4}: not empty, no discriminator -- refused for want of a CreatorMapping.
            CborException nonEmpty = Assert.Throws<CborException>(
                () => Deserialize<Shape>("A165536964657304", options));
            CborException emptied = Assert.Throws<CborException>(
                () => Deserialize<Shape>("A0", options));

            Assert.Equal(nonEmpty.Message, emptied.Message);
            Assert.Contains("CreatorMapping", emptied.Message);
            Assert.Equal("$", emptied.Path);
        }

        [Fact]
        public void AnEmptyObjectOfAnInterfaceIsRefusedLikeAnyOtherWithoutADiscriminator()
        {
            CborException emptied = Assert.Throws<CborException>(() => Deserialize<IToken>("A0"));

            Assert.Contains("CreatorMapping", emptied.Message);
        }

        [Fact]
        public void AnEmptyObjectInAnInterfaceMemberIsRefusedWithItsPath()
        {
            CborException emptied = Assert.Throws<CborException>(
                () => Deserialize<HolderOfToken>("A165546F6B656EA0")); // {"Token": {}}

            Assert.Equal("$.Token", emptied.Path);
        }

        [Fact]
        public void AnAbstractTypeIsReadByItsDiscriminator()
        {
            CborOptions options = new CborOptions();
            options.Registry.DiscriminatorConventionRegistry.RegisterType<Square>();

            Shape shape = Helper.Read<Shape>("A2625F746673717561726565536964657304", options); // {"_t": "square", "Sides": 4}

            Assert.IsType<Square>(shape);
            Assert.Equal(4, shape.Sides);
        }

        // ---- the generated path shares the converter ----

        [Fact]
        public void AGeneratedContextReadsAnEmptyMapAsAnInstance()
        {
            CborOptions options = CborSerializerContext.Default<TestCborContext>().Options;

            foreach (string empty in new[] { "A0", "BFFF" })
            {
                GeneratedPerson person = Cbor.Deserialize<GeneratedPerson>(empty.HexToBytes(), options);

                Assert.NotNull(person);
                Assert.Equal(0, person.Id);
                Assert.Null(person.Name);
            }
        }

        [Fact]
        public void AGeneratedContextReadsAnEmptyMemberAsAnInstance()
        {
            CborOptions options = CborSerializerContext.Default<TestCborContext>().Options;

            GeneratedPerson person = Cbor.Deserialize<GeneratedPerson>(
                "A16741646472657373A0".HexToBytes(), options); // {"Address": {}}

            Assert.NotNull(person.Address);
            Assert.Equal(0, person.Address.Number);
            Assert.Null(person.Address.City);
        }

        [Theory]
        [InlineData("80")] // []
        [InlineData("9FFF")] // [_ ]
        public void AGeneratedContextReadsAnEmptyArrayAsAnInstance(string empty)
        {
            CborOptions options = new ArrayDefaultContext().Options;

            GeneratedTakesArrayFormat value = Cbor.Deserialize<GeneratedTakesArrayFormat>(empty.HexToBytes(), options);

            Assert.NotNull(value);
            Assert.Equal(0, value.Id);
            Assert.Null(value.Name);
        }

        [Theory]
        [InlineData("A0")] // {}
        [InlineData("BFFF")] // {_ }
        public void AGeneratedContextReadsAnEmptyIntKeyMapAsAnInstance(string empty)
        {
            CborOptions options = new InheritedOverrideContext().Options;

            InheritedOverrideDerived value = Cbor.Deserialize<InheritedOverrideDerived>(empty.HexToBytes(), options);

            Assert.NotNull(value);
            Assert.Equal(0, value.Id);
            Assert.Null(value.Name);
        }
    }
}
