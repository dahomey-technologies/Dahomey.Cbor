using Dahomey.Cbor.Attributes;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    public class StructTests
    {
        public struct FieldSerializationStruct
        {
            public int A { get; set; }
            public int B;

            public FieldSerializationStruct(int a, int b)
            {
                A = a;
                B = b;
            }
        }

        [Fact]
        public void ReadStruct()
        {
            const string hexBuffer = "A261410C61420D";
            FieldSerializationStruct strct = Helper.Read<FieldSerializationStruct>(hexBuffer);

            Assert.Equal(12, strct.A);
            Assert.Equal(13, strct.B);
        }

        [Fact]
        public void WriteStruct()
        {
            FieldSerializationStruct strct = new FieldSerializationStruct
            {
                A = 12,
                B = 13
            };

            const string hexBuffer = "A261410C61420D";
            Helper.TestWrite(strct, hexBuffer);
        }

        public class FieldSerializationClassWithStruct
        {
            public int A { get; set; } = 11;
            public FieldSerializationStruct B = new FieldSerializationStruct(12, 13);
        }

        [Fact]
        public void ReadNestedStruct()
        {            
            const string hexBuffer = "A261410B6142A261410C61420D";
            FieldSerializationClassWithStruct strct = Helper.Read<FieldSerializationClassWithStruct>(hexBuffer);

            Assert.Equal(11, strct.A);
            Assert.Equal(12, strct.B.A);
            Assert.Equal(13, strct.B.B);
        }

        [Fact]
        public void WriteNestedStruct()
        {
            FieldSerializationClassWithStruct strct = new FieldSerializationClassWithStruct();

            const string hexBuffer = "A261410B6142A261410C61420D";
            Helper.TestWrite(strct, hexBuffer, null, new CborOptions { EnumFormat = ValueFormat.WriteToString });
        }

        public struct FieldSerializationStructNoConstructor
        {
            public int id;
        }

        [Fact]
        public void ReadStructWithNoConstructor()
        {
            const string hexBuffer = "A16269640C";
            FieldSerializationStructNoConstructor strct = Helper.Read<FieldSerializationStructNoConstructor>(hexBuffer);

            Assert.Equal(12, strct.id);
        }

        public readonly struct MyStruct
        {
            [CborConstructor("Value")]
            public MyStruct(int value)
            {
                Value = value;
            }

            public int Value { get; }
        }

        [Fact]
        public void ReadStructWithNonDefaultConstructor()
        {
            const string hexBuffer = "A16556616C75651864";
            MyStruct strct = Helper.Read<MyStruct>(hexBuffer);

            Assert.Equal(100, strct.Value);
        }

        [Fact]
        public void WriteStructWithNonDefaultConstructor()
        {
            MyStruct strct = new MyStruct(100);

            const string hexBuffer = "A16556616C75651864";
            Helper.TestWrite(strct, hexBuffer);
        }
    }
}
