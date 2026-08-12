using Dahomey.Cbor.Attributes;
using System.Reflection;
using System.Xml.Linq;
using Xunit;
using static Dahomey.Cbor.Tests.DiscriminatorTests;

namespace Dahomey.Cbor.Tests
{
    public class ObjectFormatTests
    {
        [CborObjectFormat(CborObjectFormat.IntKeyMap)]
        class Person
        {
            [CborProperty(1)]
            public int Id { get; set; }
            [CborProperty(2)]
            public string Name { get; set; }
        }

        [CborObjectFormat(CborObjectFormat.Array)]
        class Person2
        {
            [CborProperty(0)]
            public int Id { get; set; }
            [CborProperty(1)]
            public string Name { get; set; }
        }

        [Fact]
        public void ReadIntKeyMap()
        {
            const string hexBuffer = "A2010C0263466F6F"; // {1: 12, 2: "Foo"}
            Person person = Helper.Read<Person>(hexBuffer);

            Assert.NotNull(person);
            Assert.Equal(12, person.Id);
            Assert.Equal("Foo", person.Name);
        }

        [Fact]
        public void WriteIntKeyMap()
        {
            const string hexBuffer = "A2010C0263466F6F"; // {1: 12, 2: "Foo"}
            Person person = new Person
            {
                Id = 12,
                Name = "Foo"
            };

            Helper.TestWrite(person, hexBuffer);
        }

        [Fact]
        public void ReadArray()
        {
            const string hexBuffer = "820C63466F6F"; // [12, "Foo"]
            Person2 person = Helper.Read<Person2>(hexBuffer);

            Assert.NotNull(person);
            Assert.Equal(12, person.Id);
            Assert.Equal("Foo", person.Name);
        }

        [Fact]
        public void WriteArray()
        {
            const string hexBuffer = "820C63466F6F"; // [12, "Foo"]
            Person2 person = new Person2
            {
                Id = 12,
                Name = "Foo"
            };

            Helper.TestWrite(person, hexBuffer);
        }

        class Person3
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        [Theory]
        [InlineData(CborObjectFormat.IntKeyMap, "A2010C0263466F6F")] // {1: 12, 2: "Foo"}
        [InlineData(CborObjectFormat.Array, "820C63466F6F")] // [12, "Foo"]
        public void ReadByApi(CborObjectFormat objectFormat, string hexBuffer) 
        {
            CborOptions options = new CborOptions
            {
                ObjectFormat = objectFormat
            };

            int index = objectFormat == CborObjectFormat.IntKeyMap ? 1 : 0;

            options.Registry.ObjectMappingRegistry.Register<Person3>(om =>
            {
                om.AutoMap();
                om.ClearMemberMappings();
                om.MapMember(typeof(Person3).GetProperty(nameof(Person3.Id)))
                    .SetMemberIndex(index++);
                om.MapMember(typeof(Person3).GetProperty(nameof(Person3.Name)))
                    .SetMemberIndex(index++);
            });

            Person3 person = Helper.Read<Person3>(hexBuffer, options);

            Assert.NotNull(person);
            Assert.Equal(12, person.Id);
            Assert.Equal("Foo", person.Name);
        }

        [Theory]
        [InlineData(CborObjectFormat.IntKeyMap, "A2010C0263466F6F")] // {1: 12, 2: "Foo"}
        [InlineData(CborObjectFormat.Array, "820C63466F6F")] // [12, "Foo"]
        public void WriteByApi(CborObjectFormat objectFormat, string hexBuffer)
        {
            CborOptions options = new CborOptions
            {
                ObjectFormat = objectFormat
            };

            int index = objectFormat == CborObjectFormat.IntKeyMap ? 1 : 0;

            options.Registry.ObjectMappingRegistry.Register<Person3>(om =>
            {
                om.AutoMap();
                om.ClearMemberMappings();
                om.MapMember(typeof(Person3).GetProperty(nameof(Person3.Id)))
                    .SetMemberIndex(index++);
                om.MapMember(typeof(Person3).GetProperty(nameof(Person3.Name)))
                    .SetMemberIndex(index++);
            });

            Person3 person = new Person3
            {
                Id = 12,
                Name = "Foo"
            };

            Helper.TestWrite(person, hexBuffer, null, options);
        }

        private class ObjectWithConstructor
        {
            private int id;
            private string name;

            public int Id => id;
            public string Name => name;
            public int Age { get; set; }

            public ObjectWithConstructor(int id, string name)
            {
                this.id = id;
                this.name = name;
            }
        }

        [Theory]
        [InlineData(CborObjectFormat.IntKeyMap, "A2010C0263666F6F", 12, "foo", 0)] // {1: 12, 2: "foo"}
        [InlineData(CborObjectFormat.IntKeyMap, "A1010C", 12, null, 0)] // {1: 12}
        [InlineData(CborObjectFormat.IntKeyMap, "A3010C0263666F6F030D", 12, "foo", 13)] // {1: 12, 2: "foo", 3: 13}
        [InlineData(CborObjectFormat.Array, "820C63666F6F", 12, "foo", 0)] // [12, "foo"]
        [InlineData(CborObjectFormat.Array, "810C", 12, null, 0)] // [12]
        [InlineData(CborObjectFormat.Array, "830C63666F6F0D", 12, "foo", 13)] // [12, "foo", 13]
        public void ConstructorByApi(CborObjectFormat objectFormat, string hexBuffer, int expectedId, string expectedName, int expectedAge)
        {
            CborOptions options = new CborOptions
            {
                ObjectFormat = objectFormat
            };

            int index = objectFormat == CborObjectFormat.IntKeyMap ? 1 : 0;

            options.Registry.ObjectMappingRegistry.Register<ObjectWithConstructor>(om =>
            {
                om.AutoMap();
                om.MapCreator(o => new ObjectWithConstructor(o.Id, o.Name));
                om.ClearMemberMappings();
                om.MapMember(typeof(ObjectWithConstructor).GetProperty(nameof(ObjectWithConstructor.Id)))
                    .SetMemberIndex(index++);
                om.MapMember(typeof(ObjectWithConstructor).GetProperty(nameof(ObjectWithConstructor.Name)))
                    .SetMemberIndex(index++);
                om.MapMember(typeof(ObjectWithConstructor).GetProperty(nameof(ObjectWithConstructor.Age)))
                    .SetMemberIndex(index++);
            });

            ObjectWithConstructor obj = Helper.Read<ObjectWithConstructor>(hexBuffer, options);

            Assert.NotNull(obj);
            Assert.Equal(expectedId, obj.Id);
            Assert.Equal(expectedName, obj.Name);
            Assert.Equal(expectedAge, obj.Age);
        }

        private class ObjectWithConstructor2
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public int Age { get; set; }

            [CborConstructor]
            public ObjectWithConstructor2(int id, string name)
            {
                Id = id;
                Name = name;
            }
        }

        [Theory]
        [InlineData(CborObjectFormat.IntKeyMap, "A2010C0263666F6F", 12, "foo", 0)] // {1: 12, 2: "foo"}
        [InlineData(CborObjectFormat.IntKeyMap, "A1010C", 12, null, 0)] // {1: 12}
        [InlineData(CborObjectFormat.IntKeyMap, "A3010C0263666F6F030D", 12, "foo", 13)] // {1: 12, 2: "foo", 3: 13}
        [InlineData(CborObjectFormat.Array, "820C63666F6F", 12, "foo", 0)] // [12, "foo"]
        [InlineData(CborObjectFormat.Array, "810C", 12, null, 0)] // [12]
        [InlineData(CborObjectFormat.Array, "830C63666F6F0D", 12, "foo", 13)] // [12, "foo", 13]
        public void ConstructorByAttribute(CborObjectFormat objectFormat, string hexBuffer, int expectedId, string expectedName, int expectedAge)
        {
            CborOptions options = new CborOptions
            {
                ObjectFormat = objectFormat
            };

            int index = objectFormat == CborObjectFormat.IntKeyMap ? 1 : 0;

            options.Registry.ObjectMappingRegistry.Register<ObjectWithConstructor2>(om =>
            {
                om.AutoMap();
                om.ClearMemberMappings();
                om.MapMember(typeof(ObjectWithConstructor2).GetProperty(nameof(ObjectWithConstructor2.Id)))
                    .SetMemberIndex(index++);
                om.MapMember(typeof(ObjectWithConstructor2).GetProperty(nameof(ObjectWithConstructor2.Name)))
                    .SetMemberIndex(index++);
                om.MapMember(typeof(ObjectWithConstructor2).GetProperty(nameof(ObjectWithConstructor2.Age)))
                    .SetMemberIndex(index++);
            });

            ObjectWithConstructor2 obj = Helper.Read<ObjectWithConstructor2>(hexBuffer, options);

            Assert.NotNull(obj);
            Assert.Equal(expectedId, obj.Id);
            Assert.Equal(expectedName, obj.Name);
            Assert.Equal(expectedAge, obj.Age);
        }

        [CborObjectFormat(CborObjectFormat.IntKeyMap)]
        public class WithId
        {
            [CborProperty(1)]
            public int Id { get; set; }
        }

        [CborDiscriminator("Person4")]
        [CborObjectFormat(CborObjectFormat.IntKeyMap)]
        public class Person4 : WithId
        {
            [CborProperty(2)]
            public string Name { get; set; }
        }

        [Fact]
        public void ReadIntKeyMapWithDiscrimator()
        {
            CborOptions options = new CborOptions();
            options.Registry.DiscriminatorConventionRegistry.RegisterType<Person4>();

            const string hexBuffer = "A30067506572736F6E34010C0263666F6F"; // {0: "Person4", 1: 12, 2: "foo"}
            WithId obj = Helper.Read<WithId>(hexBuffer, options);
            Assert.NotNull(obj);
            Person4 person = Assert.IsType<Person4>(obj);
            Assert.Equal(12, person.Id);
            Assert.Equal("foo", person.Name);

            const string hexBuffer2 = "A2010C0263666F6F"; // {1: 12, 2: "foo"}
            Person4 obj2 = Helper.Read<Person4>(hexBuffer2, options); // no inheritance, no discriminator written
            Assert.Equal(12, obj2.Id);
            Assert.Equal("foo", obj2.Name);
        }

        [Fact]
        public void WriteIntKeyMapWithDiscrimator()
        {
            CborOptions options = new CborOptions();
            options.Registry.DiscriminatorConventionRegistry.RegisterType<Person4>();

            WithId person = new Person4
            {
                Id = 12,
                Name = "foo"
            };

            const string hexBuffer = "A30067506572736F6E34010C0263666F6F"; // {0: "Person4", 1: 12, 2: "foo"}
            Helper.TestWrite(person, hexBuffer, null, options);

            Person4 person2 = new Person4 // no inheritance, no discriminator written
            {
                Id = 12,
                Name = "foo"
            };

            const string hexBuffer2 = "A2010C0263666F6F"; // {1: 12, 2: "foo"}
            Helper.TestWrite(person2, hexBuffer2, null, options);
        }

        [CborObjectFormat(CborObjectFormat.Array)]
        public class WithId2
        {
            [CborProperty(1)]
            public int Id { get; set; }
        }

        [CborDiscriminator("Person4")]
        [CborObjectFormat(CborObjectFormat.Array)]
        public class Person5 : WithId2
        {
            [CborProperty(2)]
            public string Name { get; set; }
        }

        [Fact]
        public void ReadArrayWithDiscrimator()
        {
            CborOptions options = new CborOptions();
            options.Registry.DiscriminatorConventionRegistry.RegisterType<Person5>();

            const string hexBuffer = "83D82767506572736F6E340C63666F6F"; // [39("Person4"), 12, "foo"]
            WithId2 obj = Helper.Read<WithId2>(hexBuffer, options);
            Assert.NotNull(obj);
            Person5 person = Assert.IsType<Person5>(obj);
            Assert.Equal(12, person.Id);
            Assert.Equal("foo", person.Name);

            const string hexBuffer2 = "820C63666F6F"; // [12, "foo"]
            Person5 obj2 = Helper.Read<Person5>(hexBuffer2, options); // no inheritance, no discriminator written
            Assert.Equal(12, obj2.Id);
            Assert.Equal("foo", obj2.Name);
        }

        /// <summary>
        /// A document carrying a discriminator, read as the very type that wrote it rather than
        /// through the base.
        /// </summary>
        /// <remarks>
        /// The Array arm consumes the discriminator without returning to the bookmark, so the read
        /// has to leave on the item that carried it. It recognised that item by the converter having
        /// changed - a signal that is missing exactly when the declared type is the discriminated
        /// one, since the registry hands back the converter the read already started on. That call
        /// then read the following item too, spending two on one call: the array declared three
        /// items and is read with three calls, so the last one found nothing left.
        /// </remarks>
        [Fact]
        public void ReadArrayWithDiscrimatorAsTheDiscriminatedType()
        {
            CborOptions options = new CborOptions();
            options.Registry.DiscriminatorConventionRegistry.RegisterType<Person5>();

            const string hexBuffer = "83D82767506572736F6E340C63666F6F"; // [39("Person4"), 12, "foo"]
            Person5 person = Helper.Read<Person5>(hexBuffer, options);

            Assert.NotNull(person);
            Assert.Equal(12, person.Id);
            Assert.Equal("foo", person.Name);
        }

        /// <summary>
        /// The same case in the map formats, which resolve the discriminator behind a bookmark and
        /// so were never affected - pinned here so the two cannot drift apart.
        /// </summary>
        [Fact]
        public void ReadMapWithDiscrimatorAsTheDiscriminatedType()
        {
            CborOptions options = new CborOptions();
            options.Registry.DiscriminatorConventionRegistry.RegisterType<Person4>();

            const string hexBuffer = "A30067506572736F6E34010C0263666F6F"; // {0: "Person4", 1: 12, 2: "foo"}
            Person4 person = Helper.Read<Person4>(hexBuffer, options);

            Assert.NotNull(person);
            Assert.Equal(12, person.Id);
            Assert.Equal("foo", person.Name);
        }

        /// <summary>
        /// An array holding nothing but the discriminator: one declared item, one call, and that
        /// call has no member to read.
        /// </summary>
        [Fact]
        public void ReadArrayOfDiscrimatorAloneAsTheDiscriminatedType()
        {
            CborOptions options = new CborOptions();
            options.Registry.DiscriminatorConventionRegistry.RegisterType<Person5>();

            const string hexBuffer = "81D82767506572736F6E34"; // [39("Person4")]
            Person5 person = Helper.Read<Person5>(hexBuffer, options);

            Assert.NotNull(person);
            Assert.Equal(0, person.Id);
            Assert.Null(person.Name);
        }

        /// <summary>
        /// The indefinite-length form, which the defect never reached: its read ends on the break
        /// marker rather than on a declared count, so spending two items on one call left nothing
        /// short. Pinned so that leaving on the discriminator's item does not disturb it.
        /// </summary>
        [Fact]
        public void ReadIndefiniteArrayWithDiscrimatorAsTheDiscriminatedType()
        {
            CborOptions options = new CborOptions();
            options.Registry.DiscriminatorConventionRegistry.RegisterType<Person5>();

            const string hexBuffer = "9FD82767506572736F6E340C63666F6FFF"; // [_ 39("Person4"), 12, "foo"]
            Person5 person = Helper.Read<Person5>(hexBuffer, options);

            Assert.Equal(12, person.Id);
            Assert.Equal("foo", person.Name);
        }

        [CborObjectFormat(CborObjectFormat.Array)]
        public abstract class WithId3
        {
            [CborProperty(1)]
            public int Id { get; set; }
        }

        [CborDiscriminator("Person6")]
        [CborObjectFormat(CborObjectFormat.Array)]
        public class Person6 : WithId3
        {
            [CborProperty(2)]
            public string Name { get; set; }

            [CborConstructor]
            public Person6(int id, string name)
            {
                Id = id;
                Name = name;
            }
        }

        /// <summary>
        /// The same array format, read into a type built through a creator rather than a default
        /// constructor.
        /// </summary>
        /// <remarks>
        /// A creator collects member values and builds the instance after the loop, so the read holds
        /// no instance while it runs. Every item therefore re-entered the block that resolves the
        /// type, reached the exit meant for the discriminator's own item, and found the condition it
        /// keyed on - the converter differing from the declared type - still true, because that is a
        /// property of the read and not of the item. Every member was skipped, and the object came
        /// back built from nothing supplied, with no error raised.
        /// </remarks>
        [Fact]
        public void ReadArrayWithDiscrimatorAndCreator()
        {
            CborOptions options = new CborOptions();
            options.Registry.DiscriminatorConventionRegistry.RegisterType<Person6>();

            const string hexBuffer = "83D82767506572736F6E360C63666F6F"; // [39("Person6"), 12, "foo"]
            WithId3 obj = Helper.Read<WithId3>(hexBuffer, options);

            Person6 person = Assert.IsType<Person6>(obj);
            Assert.Equal(12, person.Id);
            Assert.Equal("foo", person.Name);
        }

        [Fact]
        public void WriteArrayWithDiscrimator()
        {
            CborOptions options = new CborOptions();
            options.Registry.DiscriminatorConventionRegistry.RegisterType<Person5>();

            WithId2 person = new Person5
            {
                Id = 12,
                Name = "foo"
            };

            const string hexBuffer = "83D82767506572736F6E340C63666F6F"; // [39("Person4"), 12, "foo"]
            Helper.TestWrite(person, hexBuffer, null, options);

            Person5 person2 = new Person5 // no inheritance, no discriminator written
            {
                Id = 12,
                Name = "foo"
            };

            const string hexBuffer2 = "820C63666F6F"; // [12, "foo"]
            Helper.TestWrite(person2, hexBuffer2, null, options);
        }
    }
}
