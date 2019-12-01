using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Serialization.Conventions;
using Dahomey.Cbor.Util;
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
            const string hexBuffer = "A16A426173654F626A656374A3625F746A4E616D654F626A656374644E616D6563666F6F62496401";
            BaseObjectHolder obj = Helper.Read<BaseObjectHolder>(hexBuffer);
        }

        [Theory]
        [InlineData(CborDiscriminatorPolicy.Default, "A26A426173654F626A656374A3625F746A4E616D654F626A656374644E616D6563666F6F624964016A4E616D654F626A656374A2644E616D656362617262496402")]
        [InlineData(CborDiscriminatorPolicy.Auto, "A26A426173654F626A656374A3625F746A4E616D654F626A656374644E616D6563666F6F624964016A4E616D654F626A656374A2644E616D656362617262496402")]
        [InlineData(CborDiscriminatorPolicy.Never, "A26A426173654F626A656374A2644E616D6563666F6F624964016A4E616D654F626A656374A2644E616D656362617262496402")]
        [InlineData(CborDiscriminatorPolicy.Always, "A26A426173654F626A656374A3625F746A4E616D654F626A656374644E616D6563666F6F624964016A4E616D654F626A656374A3625F746A4E616D654F626A656374644E616D656362617262496402")]
        public void WritePolymorphicObject(CborDiscriminatorPolicy discriminatorPolicy, string hexBuffer)
        {
            CborOptions options = new CborOptions();
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
    }
}
