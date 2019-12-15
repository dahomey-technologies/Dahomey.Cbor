using System;
using System.Collections.Generic;
using System.Text;
using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Serialization.Conventions;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    public class DiscriminatorTests
    {
        static DiscriminatorTests()
        {
            SampleClasses.Initialize();
        }

        [Fact]
        public void ReadPolymorphicObject()
        {
            CborOptions options = new CborOptions();
            options.Registry.DiscriminatorConventionRegistry.RegisterConvention(new AttributeBasedDiscriminatorConvention<string>(options.Registry));
            options.Registry.DiscriminatorConventionRegistry.RegisterType(typeof(NameObject));

            const string hexBuffer = "A16A426173654F626A656374A3625F746A4E616D654F626A656374644E616D6563666F6F62496401";
            BaseObjectHolder obj = Helper.Read<BaseObjectHolder>(hexBuffer, options);

            Assert.NotNull(obj);
            Assert.IsType<NameObject>(obj.BaseObject);
            Assert.Equal("foo", ((NameObject)obj.BaseObject).Name);
            Assert.Equal(1, obj.BaseObject.Id);
        }

        [Fact]
        public void ReadPolymorphicObjectWithDefaultDiscriminatorConvention()
        {
            const string hexBuffer = "A2625F7478374461686F6D65792E43626F722E54657374732E426173654F626A656374486F6C6465722C204461686F6D65792E43626F722E54657374736A426173654F626A656374A3625F746A4E616D654F626A656374644E616D6563666F6F62496401";

            CborOptions options = new CborOptions();
            DiscriminatorConventionRegistry registry = options.Registry.DiscriminatorConventionRegistry;
            registry.RegisterConvention(new AttributeBasedDiscriminatorConvention<string>(options.Registry));
            registry.RegisterType<NameObject>();

            object obj = Helper.Read<object>(hexBuffer, options);

            Assert.NotNull(obj);
            Assert.IsType<BaseObjectHolder>(obj);
        }

        [Theory]
        [InlineData(CborDiscriminatorPolicy.Default, "A26A426173654F626A656374A3625F746A4E616D654F626A656374644E616D6563666F6F624964016A4E616D654F626A656374A2644E616D656362617262496402")]
        [InlineData(CborDiscriminatorPolicy.Auto, "A26A426173654F626A656374A3625F746A4E616D654F626A656374644E616D6563666F6F624964016A4E616D654F626A656374A2644E616D656362617262496402")]
        [InlineData(CborDiscriminatorPolicy.Never, "A26A426173654F626A656374A2644E616D6563666F6F624964016A4E616D654F626A656374A2644E616D656362617262496402")]
        [InlineData(CborDiscriminatorPolicy.Always, "A26A426173654F626A656374A3625F746A4E616D654F626A656374644E616D6563666F6F624964016A4E616D654F626A656374A3625F746A4E616D654F626A656374644E616D656362617262496402")]
        public void WritePolymorphicObject(CborDiscriminatorPolicy discriminatorPolicy, string hexBuffer)
        {
            CborOptions options = new CborOptions();
            options.Registry.DiscriminatorConventionRegistry.RegisterConvention(new AttributeBasedDiscriminatorConvention<string>(options.Registry));
            options.Registry.DiscriminatorConventionRegistry.RegisterType(typeof(NameObject));
            options.Registry.ObjectMappingRegistry.Register<NameObject>(om =>
            {
                om.AutoMap();
                om.SetDiscriminatorPolicy(discriminatorPolicy);
            });
            options.Registry.ObjectMappingRegistry.Register<BaseObject>(om =>
            {
                om.AutoMap();
                om.SetDiscriminatorPolicy(discriminatorPolicy);
            });

            BaseObjectHolder obj = new BaseObjectHolder
            {
                BaseObject = new NameObject
                {
                    Id = 1,
                    Name = "foo"
                },
                NameObject = new NameObject
                {
                    Id = 2,
                    Name = "bar"
                }
            };

            Helper.TestWrite(obj, hexBuffer, null, options);
        }

        private class CustomDiscriminatorConvention : IDiscriminatorConvention
        {
            private readonly ReadOnlyMemory<byte> _memberName = Encoding.ASCII.GetBytes("type");
            private readonly Dictionary<int, Type> _typesByDiscriminator = new Dictionary<int, Type>();
            private readonly Dictionary<Type, int> _discriminatorsByType = new Dictionary<Type, int>();

            public ReadOnlySpan<byte> MemberName => _memberName.Span;

            public bool TryRegisterType(Type type)
            {
                int discriminator = 17;
                foreach(char c in type.Name)
                {
                    discriminator = discriminator * 23 + (int)c;
                }

                _typesByDiscriminator.Add(discriminator, type);
                _discriminatorsByType.Add(type, discriminator);

                return true;
            }

            public Type ReadDiscriminator(ref CborReader reader)
            {
                int discriminator = reader.ReadInt32();
                if (!_typesByDiscriminator.TryGetValue(discriminator, out Type type))
                {
                    throw reader.BuildException($"Unknown type discriminator: {discriminator}");
                }
                return type;
            }

            public void WriteDiscriminator(ref CborWriter writer, Type actualType)
            {
                if (!_discriminatorsByType.TryGetValue(actualType, out int discriminator))
                {
                    throw new CborException($"Unknown discriminator for type: {actualType}");
                }

                writer.WriteInt32(discriminator);
            }
        }

        [Fact]
        public void ReadWithCustomDiscriminator()
        {
            CborOptions options = new CborOptions();
            options.Registry.DiscriminatorConventionRegistry.RegisterConvention(new CustomDiscriminatorConvention());
            options.Registry.DiscriminatorConventionRegistry.RegisterType(typeof(NameObject));

            const string hexBuffer = "A16A426173654F626A656374A364747970651A22134C83644E616D6563666F6F62496401";
            BaseObjectHolder obj = Helper.Read<BaseObjectHolder>(hexBuffer, options);

            Assert.NotNull(obj);
            Assert.IsType<NameObject>(obj.BaseObject);
            Assert.Equal("foo", ((NameObject)obj.BaseObject).Name);
            Assert.Equal(1, obj.BaseObject.Id);
        }

        [Fact]
        public void WriteWithCustomDiscriminator()
        {
            CborOptions options = new CborOptions();
            options.Registry.DiscriminatorConventionRegistry.RegisterConvention(new CustomDiscriminatorConvention());
            options.Registry.DiscriminatorConventionRegistry.RegisterType(typeof(NameObject));

            const string hexBuffer = "A26A426173654F626A656374A364747970651A22134C83644E616D6563666F6F624964016A4E616D654F626A656374F6";

            BaseObjectHolder obj = new BaseObjectHolder
            {
                BaseObject = new NameObject
                {
                    Id = 1,
                    Name = "foo"
                },
            };

            Helper.TestWrite(obj, hexBuffer, null, options);
        }

        [Fact]
        public void WriteWithNoDiscriminatorConvention()
        {
            CborOptions options = new CborOptions();
            DiscriminatorConventionRegistry registry = options.Registry.DiscriminatorConventionRegistry;
            registry.ClearConventions();

            NameObject obj = new NameObject
            {
                Id = 12,
                Name = "foo"
            };

            const string hexBuffer = "A2644E616D6563666F6F6249640C";

            Helper.TestWrite(obj, hexBuffer, null, options);
        }
    }
}
