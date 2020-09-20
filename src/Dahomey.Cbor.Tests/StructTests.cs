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
    }
}
