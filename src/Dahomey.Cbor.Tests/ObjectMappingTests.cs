using Dahomey.Cbor.Serialization.Conventions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace Dahomey.Cbor.Tests
{
    [TestClass]
    public class ObjectMappingTests
    {
        [TestMethod]
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

            Assert.IsNotNull(obj);
            Assert.IsTrue(obj.Boolean);
            Assert.AreEqual(0, obj.Byte);
            Assert.AreEqual(0, obj.SByte);
            Assert.AreEqual(0, obj.Int16);
            Assert.AreEqual(0, obj.UInt16);
            Assert.AreEqual(0, obj.Int32);
            Assert.AreEqual(0u, obj.UInt32);
            Assert.AreEqual(0L, obj.Int64);
            Assert.AreEqual(0ul, obj.UInt64);
            Assert.AreEqual(0f, obj.Single);
            Assert.AreEqual(0.0, obj.Double);
            Assert.AreEqual(null, obj.String);
            Assert.AreEqual(DateTime.MinValue, obj.DateTime);
            Assert.AreEqual(EnumTest.None, obj.Enum);
        }

        [TestMethod]
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

        [TestMethod]
        public void ReadWithMemberNameAndConverter()
        {
            CborOptions options = new CborOptions();
            options.Registry.ObjectMappingRegistry.Register<ObjectWithGuid>(objectMapping =>
                objectMapping
                    .AutoMap()
                    .ClearMemberMappings()
                    .MapMember(o => o.Guid)
                        .SetMemberConverter(new GuidConverter())
                        .SetMemberName("g")
            );

            const string hexBuffer = "A1616750E2AA33E949D7AE42B8628AC805DF082F";
            ObjectWithGuid obj = Helper.Read<ObjectWithGuid>(hexBuffer, options);

            Assert.IsNotNull(obj);
            Assert.AreEqual(Guid.Parse("E933AAE2-D749-42AE-B862-8AC805DF082F"), obj.Guid);
        }

        [TestMethod]
        public void WriteWithMemberNameAndConverter()
        {
            CborOptions options = new CborOptions();
            options.Registry.ObjectMappingRegistry.Register<ObjectWithGuid>(objectMapping =>
                objectMapping
                    .AutoMap()
                    .ClearMemberMappings()
                    .MapMember(o => o.Guid)
                        .SetMemberConverter(new GuidConverter())
                        .SetMemberName("g")
            );

            ObjectWithGuid obj = new ObjectWithGuid
            {
                Guid = Guid.Parse("E933AAE2-D749-42AE-B862-8AC805DF082F")
            };

            const string hexBuffer = "A1616750E2AA33E949D7AE42B8628AC805DF082F";
            Helper.TestWrite(obj, hexBuffer, null, options);
        }

        [TestMethod]
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

            Assert.IsNotNull(obj);
            Assert.AreEqual(12, obj.IntValue);
        }

        [TestMethod]
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

        [TestMethod]
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

            Assert.IsNotNull(obj);
            Assert.IsInstanceOfType(obj, typeof(InheritedObject));
            Assert.AreEqual(12, obj.BaseValue);
            Assert.AreEqual(13, ((InheritedObject)obj).InheritedValue);
        }

        [TestMethod]
        public void WriteWithDiscriminator()
        {
            CborOptions options = new CborOptions();
            options.Registry.ObjectMappingRegistry.Register<InheritedObject>(objectMapping =>
                objectMapping
                    .AutoMap()
                    .SetDiscriminator("inherited")
            );

            InheritedObject obj = new InheritedObject
            {
                BaseValue = 12,
                InheritedValue = 13
            };

            const string hexBuffer = "A3625F7469696E686572697465646E496E6865726974656456616C75650D694261736556616C75650C";
            Helper.TestWrite<BaseObject>(obj, hexBuffer, null, options);
        }
    }
}
