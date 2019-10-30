using Dahomey.Cbor.Attributes;
using System.Collections.Generic;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    public class DictionaryIndefiniteLengthTests
    {
        private class ObjectWithProperty
        {
            public Dictionary<int, string> Dictionary { get; set; }
        }

        private class ObjectWithPropertyWithDefiniteAttribute
        {
            [CborLengthMode(LengthMode = LengthMode.DefiniteLength)]
            public Dictionary<int, string> Dictionary { get; set; }
        }

        private class ObjectWithPropertyWithIndefiniteAttribute
        {
            [CborLengthMode(LengthMode = LengthMode.IndefiniteLength)]
            public Dictionary<int, string> Dictionary { get; set; }
        }

        [Fact]
        public void Options()
        {
            CborOptions options = new CborOptions
            {
                MapLengthMode = LengthMode.IndefiniteLength
            };

            Dictionary<int, string> dictionary = new Dictionary<int, string>
            {
                { 1, "foo" },
                { 2, "bar" }
            };

            const string hexBuffer = "BF0163666F6F0263626172FF";
            Helper.TestWrite(dictionary, hexBuffer, null, options);
        }

        [Fact]
        public void PropertyWithIndefiniteLengthAttribute()
        {
            ObjectWithPropertyWithIndefiniteAttribute obj = new ObjectWithPropertyWithIndefiniteAttribute
            {
                Dictionary = new Dictionary<int, string>
                {
                    { 1, "foo" },
                    { 2, "bar" }
                }
            };

            const string hexBuffer = "A16A44696374696F6E617279BF0163666F6F0263626172FF";
            Helper.TestWrite(obj, hexBuffer, null);
        }

        [Fact]
        public void PropertyWithDefiniteLengthAttribute()
        {
            CborOptions options = new CborOptions
            {
                MapLengthMode = LengthMode.IndefiniteLength
            };

            ObjectWithPropertyWithDefiniteAttribute obj = new ObjectWithPropertyWithDefiniteAttribute
            {
                Dictionary = new Dictionary<int, string>
                {
                    { 1, "foo" },
                    { 2, "bar" }
                }
            };

            const string hexBuffer = "BF6A44696374696F6E617279A20163666F6F0263626172FF";
            Helper.TestWrite(obj, hexBuffer, null, options);
        }

        [Fact]
        public void PropertyIndefiniteLengthObjectMapping()
        {
            CborOptions options = new CborOptions();
            options.Registry.ObjectMappingRegistry.Register<ObjectWithProperty>(om =>
            {
                om.AutoMap();
                om.ClearMemberMappings();
                om.MapMember(o => o.Dictionary).SetLengthMode(LengthMode.IndefiniteLength);
            });

            ObjectWithProperty obj = new ObjectWithProperty
            {
                Dictionary = new Dictionary<int, string>
                {
                    { 1, "foo" },
                    { 2, "bar" }
                }
            };

            const string hexBuffer = "A16A44696374696F6E617279BF0163666F6F0263626172FF";
            Helper.TestWrite(obj, hexBuffer, null, options);
        }
    }
}
