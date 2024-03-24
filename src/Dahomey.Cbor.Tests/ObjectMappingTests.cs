using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Serialization.Conventions;
using Dahomey.Cbor.Serialization.Converters.Mappings;
using Xunit;
using System;
using System.Linq;
using System.Reflection;

namespace Dahomey.Cbor.Tests
{
    public class ObjectMappingTests
    {
        [Fact]
        public void ReadWithMapMember()
        {
            CborOptions options = new CborOptions();
            options.Registry.ObjectMappingRegistry.Register<SimpleObject>(objectMapping =>
                objectMapping
                    .AutoMap()
                    .ClearMemberMappings()
                    .MapMember(o => o.Boolean)
            );

            const string hexBuffer = "AE67426F6F6C65616EF56553427974650D64427974650C65496E7431360E6655496E7431360F65496E743332106655496E7433321165496E743634126655496E7436341366537472696E6766737472696E676653696E676C65FA41A1AE1466446F75626C65FB40363AE147AE147B684461746554696D6574323031342D30322D32315431393A30303A30305A64456E756D6656616C756531";
            SimpleObject obj = Helper.Read<SimpleObject>(hexBuffer, options);

            Assert.NotNull(obj);
            Assert.True(obj.Boolean);
            Assert.Equal(0, obj.Byte);
            Assert.Equal(0, obj.SByte);
            Assert.Equal(0, obj.Int16);
            Assert.Equal(0, obj.UInt16);
            Assert.Equal(0, obj.Int32);
            Assert.Equal(0u, obj.UInt32);
            Assert.Equal(0L, obj.Int64);
            Assert.Equal(0ul, obj.UInt64);
            Assert.Equal(0f, obj.Single);
            Assert.Equal(0.0, obj.Double);
            Assert.Null(obj.String);
            Assert.Equal(DateTime.MinValue, obj.DateTime);
            Assert.Equal(EnumTest.None, obj.Enum);
        }

        [Fact]
        public void WriteWithMapMember()
        {
            CborOptions options = new CborOptions();
            options.Registry.ObjectMappingRegistry.Register<SimpleObject>(objectMapping =>
                objectMapping
                    .AutoMap()
                    .ClearMemberMappings()
                    .MapMember(o => o.Boolean)
            );

            SimpleObject obj = new SimpleObject
            {
                Boolean = true,
                Byte = 12,
                SByte = 13,
                Int16 = 14,
                UInt16 = 15,
                Int32 = 16,
                UInt32 = 17u,
                Int64 = 18,
                UInt64 = 19ul,
                Single = 20.21f,
                Double = 22.23,
                String = "string",
                DateTime = new DateTime(2014, 02, 21, 19, 0, 0, DateTimeKind.Utc),
                Enum = EnumTest.Value1
            };

            const string hexBuffer = "A167426F6F6C65616EF5";
            Helper.TestWrite(obj, hexBuffer, null, options);
        }

        class ObjectWithGuid
        {
            public Guid Guid { get; set; }
        }

        [Fact]
        public void ReadWithMemberNameAndConverter()
        {
            CborOptions options = new CborOptions();
            options.Registry.ObjectMappingRegistry.Register<ObjectWithGuid>(objectMapping =>
                objectMapping
                    .AutoMap()
                    .ClearMemberMappings()
                    .MapMember(o => o.Guid)
                        .SetConverter(new GuidConverter())
                        .SetMemberName("g")
            );

            const string hexBuffer = "A1616750E2AA33E949D7AE42B8628AC805DF082F";
            ObjectWithGuid obj = Helper.Read<ObjectWithGuid>(hexBuffer, options);

            Assert.NotNull(obj);
            Assert.Equal(Guid.Parse("E933AAE2-D749-42AE-B862-8AC805DF082F"), obj.Guid);
        }

        [Fact]
        public void WriteWithMemberNameAndConverter()
        {
            CborOptions options = new CborOptions();
            options.Registry.ObjectMappingRegistry.Register<ObjectWithGuid>(objectMapping =>
                objectMapping
                    .AutoMap()
                    .ClearMemberMappings()
                    .MapMember(o => o.Guid)
                        .SetConverter(new GuidConverter())
                        .SetMemberName("g")
            );

            ObjectWithGuid obj = new ObjectWithGuid
            {
                Guid = Guid.Parse("E933AAE2-D749-42AE-B862-8AC805DF082F")
            };

            const string hexBuffer = "A1616750E2AA33E949D7AE42B8628AC805DF082F";
            Helper.TestWrite(obj, hexBuffer, null, options);
        }

        [Fact]
        public void ReadWithNamingConvention()
        {
            CborOptions options = new CborOptions();
            options.Registry.ObjectMappingRegistry.Register<IntObject>(objectMapping =>
                objectMapping
                    .AutoMap()
                    .SetNamingConvention(new CamelCaseNamingConvention())
            );

            const string hexBuffer = "A168696E7456616C75650C";
            IntObject obj = Helper.Read<IntObject>(hexBuffer, options);

            Assert.NotNull(obj);
            Assert.Equal(12, obj.IntValue);
        }

        [Fact]
        public void WriteWithNamingConvention()
        {
            CborOptions options = new CborOptions();
            options.Registry.ObjectMappingRegistry.Register<IntObject>(objectMapping =>
                objectMapping
                    .AutoMap()
                    .SetNamingConvention(new CamelCaseNamingConvention())
            );

            IntObject obj = new IntObject
            {
                IntValue = 12
            };

            const string hexBuffer = "A168696E7456616C75650C";
            Helper.TestWrite(obj, hexBuffer, null, options);
        }

        public class BaseObject
        {
            public int BaseValue { get; set; }
        }

        public class InheritedObject : BaseObject
        {
            public int InheritedValue { get; set; }
        }

        [Fact]
        public void ReadWithDiscriminator()
        {
            CborOptions options = new CborOptions();
            options.Registry.ObjectMappingRegistry.Register<InheritedObject>(objectMapping =>
                objectMapping
                    .AutoMap()
                    .SetDiscriminator("inherited")
            );

            const string hexBuffer = "A3625F7469696E686572697465646E496E6865726974656456616C75650D694261736556616C75650C";
            BaseObject obj = Helper.Read<BaseObject>(hexBuffer, options);

            Assert.NotNull(obj);
            Assert.IsType<InheritedObject>(obj);
            Assert.Equal(12, obj.BaseValue);
            Assert.Equal(13, ((InheritedObject)obj).InheritedValue);
        }

        [Fact]
        public void WriteWithDiscriminator()
        {
            CborOptions options = new CborOptions();
            options.Registry.ObjectMappingRegistry.Register<InheritedObject>(objectMapping =>
                objectMapping
                    .SetDiscriminator("inherited")
                    .AutoMap()
            );

            InheritedObject obj = new InheritedObject
            {
                BaseValue = 12,
                InheritedValue = 13
            };

            const string hexBuffer = "A3625F7469696E686572697465646E496E6865726974656456616C75650D694261736556616C75650C";
            Helper.TestWrite<BaseObject>(obj, hexBuffer, null, options);
        }

        public class OptInObjectMappingConventionProvider : IObjectMappingConventionProvider
        {
            public IObjectMappingConvention GetConvention(Type type)
            {
                // here you could filter which type should be optIn and return null for other types
                return new OptInObjectMappingConvention();
            }
        }

        public class OptInObjectMappingConvention : IObjectMappingConvention
        {
            private readonly DefaultObjectMappingConvention _defaultConvention = new DefaultObjectMappingConvention();

            public void Apply<T>(SerializationRegistry registry, ObjectMapping<T> objectMapping)
            {
                _defaultConvention.Apply(registry, objectMapping);

                // restrict to members holding CborPropertyAttribute
                objectMapping.SetMemberMappings(objectMapping.MemberMappings
                    .Where(m => m.MemberInfo != null && m.MemberInfo.IsDefined(typeof(CborPropertyAttribute)))
                    .ToList());
            }
        }

        public class OptInObject1
        {
            [CborProperty]
            public int Id { get; set; }

            public string Name { get; set; }
        }

        public class OptInObject2
        {
            public int Id { get; set; }

            [CborProperty]
            public string Name { get; set; }
        }

        [Fact]
        public void OptIn()
        {
            CborOptions options = new CborOptions();
            options.Registry.ObjectMappingConventionRegistry.RegisterProvider(
                new OptInObjectMappingConventionProvider()
            );

            OptInObject1 obj1 = new OptInObject1 { Id = 12, Name = "foo" };
            const string hexBuffer1 = "A16249640C";
            Helper.TestWrite(obj1, hexBuffer1, null, options);

            OptInObject2 obj2 = new OptInObject2 { Id = 12, Name = "foo" };
            const string hexBuffer2 = "A1644E616D6563666F6F";
            Helper.TestWrite(obj2, hexBuffer2, null, options);
        }

        public class OptInObject3
        {

            [CborProperty]
            public int OptInId { get; set; }

            [CborProperty]
            public string OptInName { get; set; }
        }

        [Fact]
        public void WriteOptInWithDefaultNamingConvention()
        {
            var options = new CborOptions()
            {
                DefaultNamingConvention = new LowerCaseNamingConvention()
            };

            options.Registry.ObjectMappingConventionRegistry.RegisterProvider(
                new OptInObjectMappingConventionProvider()
            );

            var obj3 = new OptInObject3 { OptInId = 12, OptInName = "foo" };
            const string hexBuffer1 = "A2676F7074696E69640C696F7074696E6E616D6563666F6F";
            Helper.TestWrite(obj3, hexBuffer1, null, options);
        }

        [Fact]
        public void ReadOptInWithDefaultNamingConvention()
        {
            var options = new CborOptions()
            {
                DefaultNamingConvention = new LowerCaseNamingConvention()
            };

            options.Registry.ObjectMappingConventionRegistry.RegisterProvider(
                new OptInObjectMappingConventionProvider()
            );

            var expectedObj = new OptInObject3 { OptInId = 12, OptInName = "foo" };
            var actualObj = Helper.Read<OptInObject3>("A2676F7074696E69640C696F7074696E6E616D6563666F6F", options);

            Assert.Equal(expectedObj.OptInId, actualObj.OptInId);
            Assert.Equal(expectedObj.OptInName, actualObj.OptInName);
        }

        [CborNamingConvention(typeof(SnakeCaseNamingConvention))]
        public class OptInObject4
        {

            [CborProperty]
            public int OptInId { get; set; }

            [CborProperty]
            public string OptInName { get; set; }
        }

        [Fact]
        public void WriteOptInWithDefaultNamingConventionAndCborNamingAttribute()
        {
            var options = new CborOptions()
            {
                DefaultNamingConvention = new LowerCaseNamingConvention()
            };

            options.Registry.ObjectMappingConventionRegistry.RegisterProvider(
                new OptInObjectMappingConventionProvider()
            );

            var obj4 = new OptInObject4 { OptInId = 12, OptInName = "foo" };
            const string hexBuffer1 = "A2696F70745F696E5F69640C6B6F70745F696E5F6E616D6563666F6F";
            Helper.TestWrite(obj4, hexBuffer1, null, options);
        }

        [Fact]
        public void ReadOptInWithDefaultNamingConventionAndCborNamingAttribute()
        {
            var options = new CborOptions()
            {
                DefaultNamingConvention = new LowerCaseNamingConvention()
            };

            options.Registry.ObjectMappingConventionRegistry.RegisterProvider(
                new OptInObjectMappingConventionProvider()
            );

            var expectedObj = new OptInObject4 { OptInId = 12, OptInName = "foo" };
            var actualObj = Helper.Read<OptInObject4>("A2696F70745F696E5F69640C6B6F70745F696E5F6E616D6563666F6F", options);

            Assert.Equal(expectedObj.OptInId, actualObj.OptInId);
            Assert.Equal(expectedObj.OptInName, actualObj.OptInName);
        }
    }
}
