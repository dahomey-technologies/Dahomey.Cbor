using Dahomey.Cbor.Attributes;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    public class ObjectIndefiniteLengthTests
    {
        private class Object
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        [CborLengthMode(LengthMode = LengthMode.IndefiniteLength)]
        private class ObjectWithIndefiniteAttribute
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        [CborLengthMode(LengthMode = LengthMode.DefiniteLength)]
        private class ObjectWithDefiniteAttribute
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        private class ObjectWithProperty
        {
            public Object Object { get; set; }
        }

        private class ObjectWithPropertyWithDefiniteAttribute
        {
            [CborLengthMode(LengthMode = LengthMode.DefiniteLength)]
            public Object Object { get; set; }
        }

        private class ObjectWithPropertyWithIndefiniteAttribute
        {
            [CborLengthMode(LengthMode = LengthMode.IndefiniteLength)]
            public Object Object { get; set; }
        }

        [Fact]
        public void Options()
        {
            CborOptions options = new CborOptions
            {
                MapLengthMode = LengthMode.IndefiniteLength
            };

            Object obj = new Object
            {
                Id = 12,
                Name = "foo"
            };

            const string hexBuffer = "BF6249640C644E616D6563666F6FFF";
            Helper.TestWrite(obj, hexBuffer, null, options);
        }

        [Fact]
        public void IndefiniteLengthAttribute()
        {
            ObjectWithIndefiniteAttribute obj = new ObjectWithIndefiniteAttribute
            {
                Id = 12,
                Name = "foo"
            };

            const string hexBuffer = "BF6249640C644E616D6563666F6FFF";
            Helper.TestWrite(obj, hexBuffer, null);
        }

        [Fact]
        public void DefiniteLengthAttribute()
        {
            CborOptions options = new CborOptions
            {
                MapLengthMode = LengthMode.IndefiniteLength
            };

            ObjectWithDefiniteAttribute obj = new ObjectWithDefiniteAttribute
            {
                Id = 12,
                Name = "foo"
            };

            const string hexBuffer = "A26249640C644E616D6563666F6F";
            Helper.TestWrite(obj, hexBuffer, null, options);
        }

        [Fact]
        public void IndefiniteLengthObjectMapping()
        {
            CborOptions options = new CborOptions();
            options.Registry.ObjectMappingRegistry.Register<Object>(objectMapping =>
                objectMapping
                    .AutoMap()
                    .SetLengthMode(LengthMode.IndefiniteLength)
            );

            Object obj = new Object
            {
                Id = 12,
                Name = "foo"
            };

            const string hexBuffer = "BF6249640C644E616D6563666F6FFF";
            Helper.TestWrite(obj, hexBuffer, null, options);
        }

        [Fact]
        public void DefiniteLengthObjectMapping()
        {
            CborOptions options = new CborOptions
            {
                MapLengthMode = LengthMode.IndefiniteLength
            };

            options.Registry.ObjectMappingRegistry.Register<Object>(objectMapping =>
                objectMapping
                    .AutoMap()
                    .SetLengthMode(LengthMode.DefiniteLength)
            );

            Object obj = new Object
            {
                Id = 12,
                Name = "foo"
            };

            const string hexBuffer = "A26249640C644E616D6563666F6F";
            Helper.TestWrite(obj, hexBuffer, null, options);
        }

        [Fact]
        public void PropertyWithIndefiniteLengthAttribute()
        {
            ObjectWithPropertyWithIndefiniteAttribute obj = new ObjectWithPropertyWithIndefiniteAttribute
            {
                Object = new Object
                {
                    Id = 12,
                    Name = "foo"
                }
            };

            const string hexBuffer = "A1664F626A656374BF6249640C644E616D6563666F6FFF";
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
                Object = new Object
                {
                    Id = 12,
                    Name = "foo"
                }
            };

            const string hexBuffer = "BF664F626A656374A26249640C644E616D6563666F6FFF";
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
                om.MapMember(o => o.Object).SetLengthMode(LengthMode.IndefiniteLength);
            });

            ObjectWithProperty obj = new ObjectWithProperty
            {
                Object = new Object
                {
                    Id = 12,
                    Name = "foo"
                }
            };

            const string hexBuffer = "A1664F626A656374BF6249640C644E616D6563666F6FFF";
            Helper.TestWrite(obj, hexBuffer, null, options);
        }
    }
}
