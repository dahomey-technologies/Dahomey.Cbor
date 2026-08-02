using System;
using System.Collections.Generic;
using System.Reflection;
using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization.Conventions;
using Dahomey.Cbor.Tests.Extensions;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    public class IntDiscriminatorTests
    {
        /// <summary>
        /// Deserializes without <see cref="Helper.Read{T}"/>'s cross-reader equality assertions, which
        /// cannot compare collections of reference types that lack value equality.
        /// </summary>
        private static T Deserialize<T>(string hexBuffer, CborOptions options)
        {
            return Cbor.Deserialize<T>(hexBuffer.HexToBytes(), options);
        }

        /// <summary>
        /// Converter construction happens through <see cref="Activator"/>, so a <see cref="CborException"/>
        /// raised while building an object mapping surfaces wrapped in a <see cref="TargetInvocationException"/>.
        /// </summary>
        private static CborException AssertThrowsCborException(Action action)
        {
            Exception exception = Assert.ThrowsAny<Exception>(action);

            for (Exception? current = exception; current != null; current = current.InnerException)
            {
                if (current is CborException cborException)
                {
                    return cborException;
                }
            }

            throw new Xunit.Sdk.XunitException(
                $"Expected a {nameof(CborException)} somewhere in the exception chain, got: {exception}");
        }

        public interface IIntBaseInterface
        {
            int Id { get; set; }
        }

        public class IntBaseObject : IIntBaseInterface
        {
            public int Id { get; set; }
        }

        [CborIntDiscriminator(1)]
        public class IntNameObject : IntBaseObject
        {
            public string Name { get; set; }
        }

        [CborIntDiscriminator(2)]
        public class IntDescriptionObject : IntBaseObject
        {
            public string Description { get; set; }
        }

        [CborIntDiscriminator(-1)]
        public class NegativeDiscriminatorObject : IntBaseObject
        {
        }

        [CborIntDiscriminator(1000)]
        public class LargeDiscriminatorObject : IntBaseObject
        {
        }

        public class IntObjectHolder
        {
            public IntBaseObject BaseObject { get; set; }
            public IIntBaseInterface IBaseObject { get; set; }
        }

        // {"_t": 1, "Name": "foo", "Id": 1}
        private const string NameObjectHex = "A3625F7401644E616D6563666F6F62496401";

        // {"Name": "foo", "Id": 1}
        private const string NameObjectNoDiscriminatorHex = "A2644E616D6563666F6F62496401";

        [Fact]
        public void WriteInterfacePolymorphicObject()
        {
            CborOptions options = new CborOptions();
            IIntBaseInterface obj = new IntNameObject { Id = 1, Name = "foo" };

            Helper.TestWrite(obj, NameObjectHex, null, options);
        }

        [Fact]
        public void WriteBaseClassPolymorphicObject()
        {
            CborOptions options = new CborOptions();
            IntBaseObject obj = new IntNameObject { Id = 1, Name = "foo" };

            Helper.TestWrite(obj, NameObjectHex, null, options);
        }

        [Fact]
        public void ReadInterfacePolymorphicObject()
        {
            CborOptions options = new CborOptions();
            options.Registry.DiscriminatorConventionRegistry.RegisterType<IntNameObject>();

            IIntBaseInterface obj = Helper.Read<IIntBaseInterface>(NameObjectHex, options);

            Assert.NotNull(obj);
            Assert.IsType<IntNameObject>(obj);
            Assert.Equal("foo", ((IntNameObject)obj).Name);
            Assert.Equal(1, obj.Id);
        }

        [Fact]
        public void ReadBaseClassPolymorphicObject()
        {
            CborOptions options = new CborOptions();
            options.Registry.DiscriminatorConventionRegistry.RegisterType<IntNameObject>();

            IntBaseObject obj = Helper.Read<IntBaseObject>(NameObjectHex, options);

            Assert.NotNull(obj);
            Assert.IsType<IntNameObject>(obj);
            Assert.Equal("foo", ((IntNameObject)obj).Name);
            Assert.Equal(1, obj.Id);
        }

        /// <summary>
        /// Without the discriminator, the declared type must be materialized as-is.
        /// </summary>
        [Fact]
        public void ReadWithoutDiscriminatorYieldsDeclaredType()
        {
            CborOptions options = new CborOptions();
            options.Registry.DiscriminatorConventionRegistry.RegisterType<IntNameObject>();

            IntBaseObject obj = Helper.Read<IntBaseObject>("A162496401", options); // {"Id": 1}

            Assert.NotNull(obj);
            Assert.IsType<IntBaseObject>(obj);
            Assert.Equal(1, obj.Id);
        }

        [Theory]
        [InlineData(CborDiscriminatorPolicy.Default, NameObjectHex)]
        [InlineData(CborDiscriminatorPolicy.Auto, NameObjectHex)]
        [InlineData(CborDiscriminatorPolicy.Always, NameObjectHex)]
        [InlineData(CborDiscriminatorPolicy.Never, NameObjectNoDiscriminatorHex)]
        public void WritePolymorphicObjectWithPolicy(CborDiscriminatorPolicy policy, string hexBuffer)
        {
            CborOptions options = new CborOptions();
            options.Registry.ObjectMappingRegistry.Register<IntNameObject>(om =>
            {
                om.AutoMap();
                om.SetDiscriminatorPolicy(policy);
            });

            IIntBaseInterface obj = new IntNameObject { Id = 1, Name = "foo" };

            Helper.TestWrite(obj, hexBuffer, null, options);
        }

        /// <summary>
        /// With Auto (the default), the discriminator is omitted when declared type == actual type.
        /// </summary>
        [Fact]
        public void WriteConcreteTypeOmitsDiscriminatorUnderAutoPolicy()
        {
            CborOptions options = new CborOptions();
            IntNameObject obj = new IntNameObject { Id = 1, Name = "foo" };

            Helper.TestWrite(obj, NameObjectNoDiscriminatorHex, null, options);
        }

        [Fact]
        public void WriteConcreteTypeWritesDiscriminatorUnderAlwaysPolicy()
        {
            CborOptions options = new CborOptions();
            options.Registry.ObjectMappingRegistry.Register<IntNameObject>(om =>
            {
                om.AutoMap();
                om.SetDiscriminatorPolicy(CborDiscriminatorPolicy.Always);
            });

            IntNameObject obj = new IntNameObject { Id = 1, Name = "foo" };

            Helper.TestWrite(obj, NameObjectHex, null, options);
        }

        [Fact]
        public void RoundTripHeterogeneousPolymorphicMembers()
        {
            CborOptions options = new CborOptions();
            DiscriminatorConventionRegistry registry = options.Registry.DiscriminatorConventionRegistry;
            registry.RegisterType<IntNameObject>();
            registry.RegisterType<IntDescriptionObject>();

            IntObjectHolder obj = new IntObjectHolder
            {
                BaseObject = new IntNameObject { Id = 1, Name = "foo" },
                IBaseObject = new IntDescriptionObject { Id = 2, Description = "bar" },
            };

            string hexBuffer = Helper.Write(obj, options);
            IntObjectHolder rehydrated = Helper.Read<IntObjectHolder>(hexBuffer, options);

            Assert.IsType<IntNameObject>(rehydrated.BaseObject);
            Assert.Equal("foo", ((IntNameObject)rehydrated.BaseObject).Name);
            Assert.Equal(1, rehydrated.BaseObject.Id);

            Assert.IsType<IntDescriptionObject>(rehydrated.IBaseObject);
            Assert.Equal("bar", ((IntDescriptionObject)rehydrated.IBaseObject).Description);
            Assert.Equal(2, rehydrated.IBaseObject.Id);
        }

        [Fact]
        public void RoundTripPolymorphicCollection()
        {
            CborOptions options = new CborOptions();
            DiscriminatorConventionRegistry registry = options.Registry.DiscriminatorConventionRegistry;
            registry.RegisterType<IntNameObject>();
            registry.RegisterType<IntDescriptionObject>();

            List<IntBaseObject> objects = new List<IntBaseObject>
            {
                new IntNameObject { Id = 1, Name = "foo" },
                new IntDescriptionObject { Id = 2, Description = "bar" },
                new IntNameObject { Id = 3, Name = "baz" },
            };

            string hexBuffer = Helper.Write(objects, options);
            List<IntBaseObject> rehydrated = Deserialize<List<IntBaseObject>>(hexBuffer, options);

            Assert.Equal(3, rehydrated.Count);
            Assert.IsType<IntNameObject>(rehydrated[0]);
            Assert.IsType<IntDescriptionObject>(rehydrated[1]);
            Assert.IsType<IntNameObject>(rehydrated[2]);
            Assert.Equal("baz", ((IntNameObject)rehydrated[2]).Name);
        }

        [Theory]
        [InlineData(typeof(NegativeDiscriminatorObject))]
        [InlineData(typeof(LargeDiscriminatorObject))]
        public void RoundTripNonTrivialDiscriminatorValues(System.Type actualType)
        {
            CborOptions options = new CborOptions();
            options.Registry.DiscriminatorConventionRegistry.RegisterType(actualType);

            IntBaseObject obj = (IntBaseObject)System.Activator.CreateInstance(actualType);
            obj.Id = 7;

            string hexBuffer = Helper.Write(obj, options);
            IntBaseObject rehydrated = Helper.Read<IntBaseObject>(hexBuffer, options);

            Assert.IsType(actualType, rehydrated);
            Assert.Equal(7, rehydrated.Id);
        }

        // ---- forward compatibility: unknown discriminator falls back to a designated type ----

        /// <summary>
        /// Reading data written by a newer build that added a subtype this build does not know about.
        /// Without a fallback the unknown discriminator throws; with one it materialises as the
        /// designated type, so the members this build does understand still round-trip.
        /// </summary>
        [Fact]
        public void UnknownDiscriminatorResolvesToFallbackType()
        {
            CborOptions options = new CborOptions
            {
                // The unknown subtype's extra members must be skipped rather than throwing.
                UnhandledNameMode = UnhandledNameMode.Silent,
            };
            DiscriminatorConventionRegistry registry = options.Registry.DiscriminatorConventionRegistry;
            registry.ClearConventions();
            registry.RegisterConvention(
                new DefaultDiscriminatorConvention<int>(options.Registry, "_t", typeof(IntNameObject)));
            registry.RegisterType<IntNameObject>();

            // {"_t": 999, "Name": "foo", "Id": 1} - discriminator 999 is not registered
            IntBaseObject obj = Deserialize<IntBaseObject>(
                "A3625F741903E7644E616D6563666F6F62496401", options);

            IntNameObject fallback = Assert.IsType<IntNameObject>(obj);
            Assert.Equal("foo", fallback.Name);
            Assert.Equal(1, fallback.Id);
        }

        /// <summary>
        /// Members that exist only on the real (unrecognised) subtype are skipped, not fatal.
        /// </summary>
        [Fact]
        public void FallbackTypeSkipsMembersItDoesNotDeclare()
        {
            CborOptions options = new CborOptions
            {
                UnhandledNameMode = UnhandledNameMode.Silent,
            };
            DiscriminatorConventionRegistry registry = options.Registry.DiscriminatorConventionRegistry;
            registry.ClearConventions();
            registry.RegisterConvention(
                new DefaultDiscriminatorConvention<int>(options.Registry, "_t", typeof(IntNameObject)));
            registry.RegisterType<IntNameObject>();

            // {"_t": 999, "Name": "foo", "Id": 1, "FutureField": 7}
            IntBaseObject obj = Deserialize<IntBaseObject>(
                "A4625F741903E7644E616D6563666F6F624964016B4675747572654669656C6407", options);

            IntNameObject fallback = Assert.IsType<IntNameObject>(obj);
            Assert.Equal("foo", fallback.Name);
            Assert.Equal(1, fallback.Id);
        }

        /// <summary>Without a fallback the behaviour is unchanged: unknown discriminators throw.</summary>
        [Fact]
        public void NoFallbackKeepsThrowingOnUnknownDiscriminator()
        {
            CborOptions options = new CborOptions();
            DiscriminatorConventionRegistry registry = options.Registry.DiscriminatorConventionRegistry;
            registry.ClearConventions();
            registry.RegisterConvention(new DefaultDiscriminatorConvention<int>(options.Registry, "_t"));
            registry.RegisterType<IntNameObject>();

            Assert.ThrowsAny<CborException>(
                () => Deserialize<IntBaseObject>("A2625F741903E762496401", options));
        }

        [Fact]
        public void UnknownDiscriminatorThrows()
        {
            CborOptions options = new CborOptions();
            options.Registry.DiscriminatorConventionRegistry.RegisterType<IntNameObject>();

            // {"_t": 99, "Id": 1}
            Assert.ThrowsAny<CborException>(
                () => Helper.Read<IntBaseObject>("A2625F74186362496401", options));
        }

        [CborIntDiscriminator(3)]
        public class UnrelatedObject
        {
        }

        [Fact]
        public void NonAssignableDiscriminatorThrows()
        {
            CborOptions options = new CborOptions();
            DiscriminatorConventionRegistry registry = options.Registry.DiscriminatorConventionRegistry;
            registry.RegisterType<IntNameObject>();
            registry.RegisterType<UnrelatedObject>();

            // {"_t": 3, "Id": 1}
            Assert.ThrowsAny<CborException>(
                () => Helper.Read<IntBaseObject>("A2625F740362496401", options));
        }

        [CborDiscriminator("Both")]
        [CborIntDiscriminator(4)]
        public class BothDiscriminatorsObject : IntBaseObject
        {
        }

        [Fact]
        public void BothDiscriminatorAttributesThrows()
        {
            CborOptions options = new CborOptions();

            CborException exception = AssertThrowsCborException(
                () => Helper.Write<IntBaseObject>(new BothDiscriminatorsObject(), options));

            Assert.Contains(nameof(CborDiscriminatorAttribute), exception.Message);
            Assert.Contains(nameof(CborIntDiscriminatorAttribute), exception.Message);
        }

        // ---- coexistence with the string discriminator convention ----

        [CborDiscriminator("StringDisc")]
        public class StringDiscriminatorObject : IntBaseObject
        {
            public string Name { get; set; }
        }

        public class StringBaseObject
        {
            public int Id { get; set; }
        }

        [CborDiscriminator("StringNamed")]
        public class StringNamedObject : StringBaseObject
        {
            public string Name { get; set; }
        }

        /// <summary>
        /// Two independent hierarchies, one keyed by int and one by string, must both work
        /// inside a single <see cref="CborOptions"/>.
        /// </summary>
        [Fact]
        public void IntAndStringDiscriminatedHierarchiesCoexist()
        {
            CborOptions options = new CborOptions();
            DiscriminatorConventionRegistry registry = options.Registry.DiscriminatorConventionRegistry;
            registry.RegisterType<IntNameObject>();
            registry.RegisterType<StringNamedObject>();

            IntBaseObject intObj = new IntNameObject { Id = 1, Name = "foo" };
            string intHex = Helper.Write(intObj, options);
            Assert.Equal(NameObjectHex, intHex);
            Assert.IsType<IntNameObject>(Deserialize<IntBaseObject>(intHex, options));

            StringBaseObject stringObj = new StringNamedObject { Id = 2, Name = "bar" };
            string stringHex = Helper.Write(stringObj, options);
            // {"_t": "StringNamed", "Name": "bar", "Id": 2}
            Assert.Equal("A3625F746B537472696E674E616D6564644E616D6563626172624964 02".Replace(" ", ""), stringHex);
            Assert.IsType<StringNamedObject>(Deserialize<StringBaseObject>(stringHex, options));
        }

        [Fact]
        public void StringDiscriminatorStillWritesAsString()
        {
            CborOptions options = new CborOptions();
            IntBaseObject obj = new StringDiscriminatorObject { Id = 1, Name = "foo" };

            // {"_t": "StringDisc", "Name": "foo", "Id": 1}
            const string hexBuffer = "A3625F746A537472696E6744697363644E616D6563666F6F62496401";

            Helper.TestWrite(obj, hexBuffer, null, options);
        }

        /// <summary>
        /// Known limitation, inherited from the upstream design and NOT introduced by int discriminators:
        /// <see cref="DiscriminatorConventionRegistry"/> caches exactly one convention per declared type,
        /// so a single hierarchy cannot mix int- and string-keyed discriminators. Whichever subtype is
        /// registered first wins the base type, and reading a sibling encoded with the other kind fails.
        /// Asserted here so the behaviour is explicit rather than a silent trap.
        /// </summary>
        [Fact]
        public void MixingIntAndStringDiscriminatorsInOneHierarchyIsNotSupported()
        {
            CborOptions options = new CborOptions();
            DiscriminatorConventionRegistry registry = options.Registry.DiscriminatorConventionRegistry;
            registry.RegisterType<IntNameObject>();          // claims IntBaseObject for the int convention
            registry.RegisterType<StringDiscriminatorObject>();

            // {"_t": "StringDisc", "Name": "foo", "Id": 1} read through the int-claimed base type
            const string hexBuffer = "A3625F746A537472696E6744697363644E616D6563666F6F62496401";

            CborException exception = AssertThrowsCborException(
                () => Deserialize<IntBaseObject>(hexBuffer, options));

            Assert.Contains("Invalid major type", exception.Message);
        }

        // ---- issue #138: polymorphism combined with IntKeyMap and Array object formats ----

        [CborObjectFormat(CborObjectFormat.IntKeyMap)]
        public class IntKeyMapBase
        {
            [CborProperty(1)]
            public int Id { get; set; }
        }

        [CborIntDiscriminator(1)]
        [CborObjectFormat(CborObjectFormat.IntKeyMap)]
        public class IntKeyMapDerived : IntKeyMapBase
        {
            [CborProperty(2)]
            public string Name { get; set; }
        }

        // {0: 1, 1: 12, 2: "foo"} — discriminator always occupies key 0
        private const string IntKeyMapDerivedHex = "A30001010C0263666F6F";

        [Fact]
        public void WriteIntKeyMapWithIntDiscriminator()
        {
            CborOptions options = new CborOptions();
            options.Registry.DiscriminatorConventionRegistry.RegisterType<IntKeyMapDerived>();

            IntKeyMapBase obj = new IntKeyMapDerived { Id = 12, Name = "foo" };

            Helper.TestWrite(obj, IntKeyMapDerivedHex, null, options);
        }

        [Fact]
        public void ReadIntKeyMapWithIntDiscriminator()
        {
            CborOptions options = new CborOptions();
            options.Registry.DiscriminatorConventionRegistry.RegisterType<IntKeyMapDerived>();

            IntKeyMapBase obj = Helper.Read<IntKeyMapBase>(IntKeyMapDerivedHex, options);

            IntKeyMapDerived derived = Assert.IsType<IntKeyMapDerived>(obj);
            Assert.Equal(12, derived.Id);
            Assert.Equal("foo", derived.Name);
        }

        [CborObjectFormat(CborObjectFormat.Array)]
        public class ArrayBase
        {
            [CborProperty(1)]
            public int Id { get; set; }
        }

        [CborIntDiscriminator(1)]
        [CborObjectFormat(CborObjectFormat.Array)]
        public class ArrayDerived : ArrayBase
        {
            [CborProperty(2)]
            public string Name { get; set; }
        }

        // [39(1), 12, "foo"] — discriminator is item 0, behind the discriminator semantic tag
        private const string ArrayDerivedHex = "83D827010C63666F6F";

        [Fact]
        public void WriteArrayWithIntDiscriminator()
        {
            CborOptions options = new CborOptions();
            options.Registry.DiscriminatorConventionRegistry.RegisterType<ArrayDerived>();

            ArrayBase obj = new ArrayDerived { Id = 12, Name = "foo" };

            Helper.TestWrite(obj, ArrayDerivedHex, null, options);
        }

        [Fact]
        public void ReadArrayWithIntDiscriminator()
        {
            CborOptions options = new CborOptions();
            options.Registry.DiscriminatorConventionRegistry.RegisterType<ArrayDerived>();

            ArrayBase obj = Helper.Read<ArrayBase>(ArrayDerivedHex, options);

            ArrayDerived derived = Assert.IsType<ArrayDerived>(obj);
            Assert.Equal(12, derived.Id);
            Assert.Equal("foo", derived.Name);
        }

        /// <summary>
        /// Mirrors the production configuration in issue #138: Array object format, an explicit
        /// discriminator semantic tag, and DiscriminatorPolicy.Always.
        /// </summary>
        [Fact]
        public void ArrayFormatWithAlwaysPolicyRoundTrips()
        {
            CborOptions options = new CborOptions
            {
                ObjectFormat = CborObjectFormat.Array,
                DiscriminatorPolicy = CborDiscriminatorPolicy.Always,
                DiscriminatorSemanticTag = 39uL,
            };
            options.Registry.DiscriminatorConventionRegistry.RegisterType<ArrayDerived>();

            ArrayDerived obj = new ArrayDerived { Id = 12, Name = "foo" };

            // written as the concrete type, yet Always still emits the discriminator
            string hexBuffer = Helper.Write(obj, options);
            Assert.Equal(ArrayDerivedHex, hexBuffer);

            ArrayBase rehydrated = Helper.Read<ArrayBase>(hexBuffer, options);
            ArrayDerived derived = Assert.IsType<ArrayDerived>(rehydrated);
            Assert.Equal(12, derived.Id);
            Assert.Equal("foo", derived.Name);
        }

        // ---- custom discriminator member name ----

        [Fact]
        public void CustomMemberNameRoundTrip()
        {
            CborOptions options = new CborOptions();
            DiscriminatorConventionRegistry registry = options.Registry.DiscriminatorConventionRegistry;
            registry.ClearConventions();
            registry.RegisterConvention(new DefaultDiscriminatorConvention<int>(options.Registry, "t"));
            registry.RegisterType<IntNameObject>();

            IIntBaseInterface obj = new IntNameObject { Id = 1, Name = "foo" };

            // {"t": 1, "Name": "foo", "Id": 1}
            const string hexBuffer = "A3617401644E616D6563666F6F62496401";

            Helper.TestWrite(obj, hexBuffer, null, options);

            IIntBaseInterface rehydrated = Helper.Read<IIntBaseInterface>(hexBuffer, options);
            Assert.IsType<IntNameObject>(rehydrated);
            Assert.Equal("foo", ((IntNameObject)rehydrated).Name);
        }

        [Fact]
        public void ClearedConventionsWritesNoDiscriminator()
        {
            CborOptions options = new CborOptions();
            options.Registry.DiscriminatorConventionRegistry.ClearConventions();

            IIntBaseInterface obj = new IntNameObject { Id = 1, Name = "foo" };

            Helper.TestWrite(obj, NameObjectNoDiscriminatorHex, null, options);
        }
    }
}
