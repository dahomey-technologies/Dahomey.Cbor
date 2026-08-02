using System;
using System.Collections.Generic;
using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Serialization.Converters;
using Dahomey.Cbor.Serialization.Converters.Mappings;
using Dahomey.Cbor.Tests.Extensions;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    /// <summary>
    /// Covers <see cref="DelegateMemberMapping{T, TM}"/> / <see cref="DelegateStructMemberMapping{T, TM}"/>,
    /// the reflection-free mapping primitives the AOT source-generated path is built on.
    ///
    /// The load-bearing assertion throughout is that a delegate-configured mapping produces
    /// <em>byte-identical</em> CBOR to the equivalent reflection-configured (AutoMap) mapping. That is
    /// what makes a generated path a drop-in replacement rather than a second dialect.
    /// </summary>
    public class DelegateMappingTests
    {
        public class Person
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        /// <summary>The reflection path, for comparison: plain AutoMap over the same type.</summary>
        private static CborOptions ReflectionOptions()
        {
            return new CborOptions();
        }

        /// <summary>
        /// The AOT-shaped path: an explicit object mapping built from delegates, plus a pre-registered
        /// converter instance. Together these avoid every reflection construct in the lookup chain —
        /// no AutoMap (no GetProperties), no Expression.Compile, no MakeGenericType, no Activator.
        /// </summary>
        private static CborOptions DelegateOptions()
        {
            CborOptions options = new CborOptions();
            CborConverterRegistry converters = options.Registry.ConverterRegistry;

            options.Registry.ObjectMappingRegistry.Register<Person>(om => om
                .SetMemberMappings(new IMemberMapping[]
                {
                    new DelegateMemberMapping<Person, int>(converters, p => p.Id, (p, v) => p.Id = v)
                        .SetMemberName("Id"),
                    new DelegateMemberMapping<Person, string>(converters, p => p.Name, (p, v) => p.Name = v)
                        .SetMemberName("Name"),
                }));

            converters.RegisterConverter(typeof(Person), new ObjectConverter<Person>(options));

            return options;
        }

        [Fact]
        public void WriteMatchesReflectionPathByteForByte()
        {
            Person person = new Person { Id = 1, Name = "foo" };

            string reflectionHex = Helper.Write(person, ReflectionOptions());
            string delegateHex = Helper.Write(person, DelegateOptions());

            Assert.Equal("A2624964016 44E616D6563666F6F".Replace(" ", ""), reflectionHex); // {"Id": 1, "Name": "foo"}
            Assert.Equal(reflectionHex, delegateHex);
        }

        [Fact]
        public void ReadMatchesReflectionPath()
        {
            const string hexBuffer = "A262496401644E616D6563666F6F"; // {"Id": 1, "Name": "foo"}

            Person viaDelegates = Helper.Read<Person>(hexBuffer, DelegateOptions());

            Assert.Equal(1, viaDelegates.Id);
            Assert.Equal("foo", viaDelegates.Name);
        }

        [Fact]
        public void RoundTrips()
        {
            CborOptions options = DelegateOptions();
            Person person = new Person { Id = 42, Name = "bar" };

            Person rehydrated = Cbor.Deserialize<Person>(Helper.Write(person, options).HexToBytes(), options);

            Assert.Equal(42, rehydrated.Id);
            Assert.Equal("bar", rehydrated.Name);
        }

        /// <summary>A read-only member (no setter) must still serialize.</summary>
        [Fact]
        public void GetterOnlyMemberIsWriteOnlyMapped()
        {
            CborOptions options = new CborOptions();
            CborConverterRegistry converters = options.Registry.ConverterRegistry;

            DelegateMemberMapping<Person, int> mapping =
                new DelegateMemberMapping<Person, int>(converters, p => p.Id, null);
            mapping.SetMemberName("Id");

            Assert.True(mapping.CanBeSerialized);
            Assert.False(mapping.CanBeDeserialized);

            options.Registry.ObjectMappingRegistry.Register<Person>(om => om
                .SetMemberMappings(new IMemberMapping[] { mapping }));
            converters.RegisterConverter(typeof(Person), new ObjectConverter<Person>(options));

            Helper.TestWrite(new Person { Id = 7, Name = "x" }, "A162496407", null, options); // {"Id": 7}
        }

        [Fact]
        public void MemberInfoIsNullAndMemberTypeIsExact()
        {
            DelegateMemberMapping<Person, string> mapping = new DelegateMemberMapping<Person, string>(
                new CborOptions().Registry.ConverterRegistry, p => p.Name, (p, v) => p.Name = v);

            Assert.Null(mapping.MemberInfo);
            Assert.Equal(typeof(string), mapping.MemberType);
        }

        // ---- object formats ----

        [Fact]
        public void IntKeyMapFormatUsesMemberIndex()
        {
            CborOptions options = new CborOptions();
            CborConverterRegistry converters = options.Registry.ConverterRegistry;

            options.Registry.ObjectMappingRegistry.Register<Person>(om =>
            {
                om.SetObjectFormat(CborObjectFormat.IntKeyMap);
                om.SetMemberMappings(new IMemberMapping[]
                {
                    new DelegateMemberMapping<Person, int>(converters, p => p.Id, (p, v) => p.Id = v)
                        .SetMemberIndex(1),
                    new DelegateMemberMapping<Person, string>(converters, p => p.Name, (p, v) => p.Name = v)
                        .SetMemberIndex(2),
                });
            });
            converters.RegisterConverter(typeof(Person), new ObjectConverter<Person>(options));

            const string hexBuffer = "A201010263666F6F"; // {1: 1, 2: "foo"}
            Helper.TestWrite(new Person { Id = 1, Name = "foo" }, hexBuffer, null, options);

            Person rehydrated = Helper.Read<Person>(hexBuffer, options);
            Assert.Equal(1, rehydrated.Id);
            Assert.Equal("foo", rehydrated.Name);
        }

        [Fact]
        public void ArrayFormatUsesMemberIndex()
        {
            CborOptions options = new CborOptions();
            CborConverterRegistry converters = options.Registry.ConverterRegistry;

            options.Registry.ObjectMappingRegistry.Register<Person>(om =>
            {
                om.SetObjectFormat(CborObjectFormat.Array);
                om.SetMemberMappings(new IMemberMapping[]
                {
                    new DelegateMemberMapping<Person, int>(converters, p => p.Id, (p, v) => p.Id = v)
                        .SetMemberIndex(0),
                    new DelegateMemberMapping<Person, string>(converters, p => p.Name, (p, v) => p.Name = v)
                        .SetMemberIndex(1),
                });
            });
            converters.RegisterConverter(typeof(Person), new ObjectConverter<Person>(options));

            const string hexBuffer = "820163666F6F"; // [1, "foo"]
            Helper.TestWrite(new Person { Id = 1, Name = "foo" }, hexBuffer, null, options);

            Person rehydrated = Helper.Read<Person>(hexBuffer, options);
            Assert.Equal(1, rehydrated.Id);
            Assert.Equal("foo", rehydrated.Name);
        }

        // ---- feature parity with the reflection path ----

        [Fact]
        public void IgnoreIfDefaultIsHonoured()
        {
            CborOptions options = new CborOptions();
            CborConverterRegistry converters = options.Registry.ConverterRegistry;

            options.Registry.ObjectMappingRegistry.Register<Person>(om => om
                .SetMemberMappings(new IMemberMapping[]
                {
                    new DelegateMemberMapping<Person, int>(converters, p => p.Id, (p, v) => p.Id = v)
                        .SetMemberName("Id")
                        .SetIgnoreIfDefault(true),
                    new DelegateMemberMapping<Person, string>(converters, p => p.Name, (p, v) => p.Name = v)
                        .SetMemberName("Name"),
                }));
            converters.RegisterConverter(typeof(Person), new ObjectConverter<Person>(options));

            // Id == 0 == default, so it is skipped: {"Name": "foo"}
            Helper.TestWrite(new Person { Id = 0, Name = "foo" }, "A1644E616D6563666F6F", null, options);
            // Id != default, so it is written
            Helper.TestWrite(new Person { Id = 5, Name = "foo" }, "A262496405644E616D6563666F6F", null, options);
        }

        [Fact]
        public void ExplicitConverterOverridesRegistryLookup()
        {
            CborOptions options = new CborOptions();
            CborConverterRegistry converters = options.Registry.ConverterRegistry;

            options.Registry.ObjectMappingRegistry.Register<Person>(om => om
                .SetMemberMappings(new IMemberMapping[]
                {
                    new DelegateMemberMapping<Person, int>(converters, p => p.Id, (p, v) => p.Id = v)
                        .SetMemberName("Id"),
                    new DelegateMemberMapping<Person, string>(converters, p => p.Name, (p, v) => p.Name = v)
                        .SetMemberName("Name")
                        .SetConverter(new ShoutingStringConverter()),
                }));
            converters.RegisterConverter(typeof(Person), new ObjectConverter<Person>(options));

            // {"Id": 1, "Name": "FOO"}
            Helper.TestWrite(new Person { Id = 1, Name = "foo" }, "A262496401644E616D6563464F4F", null, options);
        }

        private class ShoutingStringConverter : CborConverterBase<string>
        {
            public override string Read(ref CborReader reader) => reader.ReadString();

            public override void Write(ref CborWriter writer, string value)
            {
                writer.WriteString(value.ToUpperInvariant());
            }
        }

        [Fact]
        public void RequiredMemberRejectsNull()
        {
            CborOptions options = new CborOptions();
            CborConverterRegistry converters = options.Registry.ConverterRegistry;

            options.Registry.ObjectMappingRegistry.Register<Person>(om => om
                .SetMemberMappings(new IMemberMapping[]
                {
                    new DelegateMemberMapping<Person, string>(converters, p => p.Name, (p, v) => p.Name = v)
                        .SetMemberName("Name")
                        .SetRequired(RequirementPolicy.DisallowNull),
                }));
            converters.RegisterConverter(typeof(Person), new ObjectConverter<Person>(options));

            Assert.ThrowsAny<CborException>(
                () => Helper.Write(new Person { Name = null }, options));
        }

        // ---- polymorphism through the delegate path ----

        public class Shape
        {
            public int Id { get; set; }
        }

        [CborIntDiscriminator(1)]
        public class Circle : Shape
        {
            public double Radius { get; set; }
        }

        /// <summary>
        /// A delegate-mapped subtype still participates in discriminator-based polymorphism.
        /// Note the ordering requirement: SetMemberMappings replaces the whole list, so
        /// SetDiscriminator must come *after* it or the inserted discriminator entry is lost.
        /// </summary>
        [Fact]
        public void DiscriminatorWorksWithDelegateMappings()
        {
            CborOptions options = new CborOptions();
            CborConverterRegistry converters = options.Registry.ConverterRegistry;

            options.Registry.ObjectMappingRegistry.Register<Shape>(om => om
                .SetMemberMappings(new IMemberMapping[]
                {
                    new DelegateMemberMapping<Shape, int>(converters, s => s.Id, (s, v) => s.Id = v)
                        .SetMemberName("Id"),
                }));

            options.Registry.ObjectMappingRegistry.Register<Circle>(om =>
            {
                om.SetMemberMappings(new IMemberMapping[]
                {
                    new DelegateMemberMapping<Circle, double>(converters, c => c.Radius, (c, v) => c.Radius = v)
                        .SetMemberName("Radius"),
                    new DelegateMemberMapping<Circle, int>(converters, c => c.Id, (c, v) => c.Id = v)
                        .SetMemberName("Id"),
                });
                om.SetDiscriminator(1);
            });

            converters.RegisterConverter(typeof(Shape), new ObjectConverter<Shape>(options));
            converters.RegisterConverter(typeof(Circle), new ObjectConverter<Circle>(options));
            options.Registry.DiscriminatorConventionRegistry.RegisterType<Circle>();

            Shape shape = new Circle { Id = 1, Radius = 2.5 };
            string hexBuffer = Helper.Write(shape, options);

            // {"_t": 1, "Radius": 2.5, "Id": 1}
            Assert.Equal("A3625F740166526164697573F94100624964 01".Replace(" ", ""), hexBuffer);

            Shape rehydrated = Cbor.Deserialize<Shape>(hexBuffer.HexToBytes(), options);
            Circle circle = Assert.IsType<Circle>(rehydrated);
            Assert.Equal(2.5, circle.Radius);
            Assert.Equal(1, circle.Id);
        }

        // ---- structs ----

        public struct Point
        {
            public int X { get; set; }
            public int Y { get; set; }
        }

        [Fact]
        public void StructMappingRoundTripsAndMatchesReflectionPath()
        {
            CborOptions options = new CborOptions();
            CborConverterRegistry converters = options.Registry.ConverterRegistry;

            options.Registry.ObjectMappingRegistry.Register<Point>(om => om
                .SetMemberMappings(new IMemberMapping[]
                {
                    new DelegateStructMemberMapping<Point, int>(
                        converters,
                        (ref Point p) => p.X,
                        (ref Point p, int v) => p.X = v).SetMemberName("X"),
                    new DelegateStructMemberMapping<Point, int>(
                        converters,
                        (ref Point p) => p.Y,
                        (ref Point p, int v) => p.Y = v).SetMemberName("Y"),
                }));
            converters.RegisterConverter(typeof(Point), new ObjectConverter<Point>(options));

            Point point = new Point { X = 1, Y = 2 };

            string reflectionHex = Helper.Write(point, new CborOptions());
            string delegateHex = Helper.Write(point, options);

            Assert.Equal("A2615801615902", reflectionHex); // {"X": 1, "Y": 2}
            Assert.Equal(reflectionHex, delegateHex);

            Point rehydrated = Helper.Read<Point>(delegateHex, options);
            Assert.Equal(1, rehydrated.X);
            Assert.Equal(2, rehydrated.Y);
        }

        // ---- validation ----

        [Fact]
        public void MemberWithNeitherNameNorIndexThrows()
        {
            CborOptions options = new CborOptions();
            CborConverterRegistry converters = options.Registry.ConverterRegistry;

            DelegateMemberMapping<Person, int> mapping =
                new DelegateMemberMapping<Person, int>(converters, p => p.Id, (p, v) => p.Id = v);

            CborException exception = Assert.Throws<CborException>(() => mapping.GenerateMemberConverter());
            Assert.Contains("member name or a member index", exception.Message);
        }

        [Fact]
        public void MemberWithBothNameAndIndexThrows()
        {
            CborOptions options = new CborOptions();
            CborConverterRegistry converters = options.Registry.ConverterRegistry;

            DelegateMemberMapping<Person, int> mapping =
                new DelegateMemberMapping<Person, int>(converters, p => p.Id, (p, v) => p.Id = v)
                    .SetMemberName("Id")
                    .SetMemberIndex(0);

            CborException exception = Assert.Throws<CborException>(() => mapping.GenerateMemberConverter());
            Assert.Contains("cannot coexist", exception.Message);
        }

        [Fact]
        public void NullConverterRegistryThrows()
        {
            Assert.Throws<ArgumentNullException>(
                () => new DelegateMemberMapping<Person, int>(null, p => p.Id, (p, v) => p.Id = v));
        }

        /// <summary>
        /// A self-referential type must map without the converter lookup recursing forever — the
        /// reason converters are resolved lazily rather than in the constructor.
        /// </summary>
        public class Node
        {
            public int Value { get; set; }
            public Node Next { get; set; }
        }

        [Fact]
        public void SelfReferentialTypeMapsLazilyWithoutRecursing()
        {
            CborOptions options = new CborOptions();
            CborConverterRegistry converters = options.Registry.ConverterRegistry;

            options.Registry.ObjectMappingRegistry.Register<Node>(om => om
                .SetMemberMappings(new IMemberMapping[]
                {
                    new DelegateMemberMapping<Node, int>(converters, n => n.Value, (n, v) => n.Value = v)
                        .SetMemberName("Value"),
                    new DelegateMemberMapping<Node, Node>(converters, n => n.Next, (n, v) => n.Next = v)
                        .SetMemberName("Next"),
                }));
            converters.RegisterConverter(typeof(Node), new ObjectConverter<Node>(options));

            Node node = new Node { Value = 1, Next = new Node { Value = 2 } };

            string hexBuffer = Helper.Write(node, options);
            Node rehydrated = Cbor.Deserialize<Node>(hexBuffer.HexToBytes(), options);

            Assert.Equal(1, rehydrated.Value);
            Assert.Equal(2, rehydrated.Next.Value);
            Assert.Null(rehydrated.Next.Next);
        }
    }
}
