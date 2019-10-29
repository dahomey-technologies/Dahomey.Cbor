using Dahomey.Cbor.Attributes;
using System;
using System.Collections.Generic;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    public class ArrayIndefiniteLengthTests
    {
        private class ObjectWithProperty
        {
            public List<int> List { get; set; }
        }

        private class ObjectWithPropertyWithDefiniteAttribute
        {
            [CborLengthMode(LengthMode = LengthMode.DefiniteLength)]
            public List<int> List { get; set; }
        }

        private class ObjectWithPropertyWithIndefiniteAttribute
        {
            [CborLengthMode(LengthMode = LengthMode.IndefiniteLength)]
            public List<int> List { get; set; }
        }

        [Fact]
        public void Options()
        {
            CborOptions options = new CborOptions
            {
                ArrayLengthMode = LengthMode.IndefiniteLength
            };

            List<int> list = new List<int> { 1, 2, 3 };

            const string hexBuffer = "9F010203FF";
            Helper.TestWrite(list, hexBuffer, null, options);
        }

        [Fact]
        public void PropertyWithIndefiniteLengthAttribute()
        {
            ObjectWithPropertyWithIndefiniteAttribute obj = new ObjectWithPropertyWithIndefiniteAttribute
            {
                List = new List<int> { 1, 2, 3}
            };

            const string hexBuffer = "A1644C6973749F010203FF";
            Helper.TestWrite(obj, hexBuffer, null);
        }

        [Fact]
        public void PropertyWithDefiniteLengthAttribute()
        {
            CborOptions options = new CborOptions
            {
                ArrayLengthMode = LengthMode.IndefiniteLength
            };

            ObjectWithPropertyWithDefiniteAttribute obj = new ObjectWithPropertyWithDefiniteAttribute
            {
                List = new List<int> { 1, 2, 3 }
            };

            const string hexBuffer = "A1644C69737483010203";
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
                om.MapMember(o => o.List).SetLengthMode(LengthMode.IndefiniteLength);
            });

            ObjectWithProperty obj = new ObjectWithProperty
            {
                List = new List<int> { 1, 2, 3 }
            };

            const string hexBuffer = "A1644C6973749F010203FF";
            Helper.TestWrite(obj, hexBuffer, null, options);
        }
    }
}
