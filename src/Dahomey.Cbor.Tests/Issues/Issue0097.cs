using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Serialization.Converters;
using System;
using System.Collections.Generic;
using Xunit;

namespace Dahomey.Cbor.Tests.Issues
{
    public class Issue0097
    {
        [CborConverter(typeof(DynamicValueConverter))]
        public class DynamicValue
        {
            public double Value { get; set; }
            public string ValueString { get; set; }
            public BarContent BarContent { get; set; }
        }

        public class Drink
        {
            public string Name { get; set; }
            public int Temperature { get; set; }
            public int Count { get; set; }
        }

        [CborConverter(typeof(BarContentConverter))]
        public class BarContent
        {
            public List<Drink> Drinks { get; set; }
        }

        public class DynamicValueConverter : CborConverterBase<DynamicValue>
        {
            private readonly ICborConverter<BarContent> _barContentConverter;

            public DynamicValueConverter(CborOptions options)
            {
                _barContentConverter = options.Registry.ConverterRegistry.Lookup<BarContent>();
            }

            public override DynamicValue Read(ref CborReader reader)
            {
                CborDataItemType type = reader.GetCurrentDataItemType();

                switch (type)
                {
                    case CborDataItemType.Unsigned:
                    case CborDataItemType.Signed:
                    case CborDataItemType.Single:
                    case CborDataItemType.Double:
                        return new DynamicValue() { Value = reader.ReadDouble() };
                    case CborDataItemType.String:
                        return new DynamicValue() { ValueString = reader.ReadString() };
                    case CborDataItemType.Array:
                        return new DynamicValue() { BarContent = _barContentConverter.Read(ref reader) };
                    default:
                        throw new NotSupportedException();
                }
            }
        }

        public class BarContentConverter: CborConverterBase<BarContent>
        {
            private readonly ICborConverter<List<Drink>> _drinkListConverter;

            public BarContentConverter(CborOptions options)
            {
                _drinkListConverter = options.Registry.ConverterRegistry.Lookup<List<Drink>>();
            }

            public override BarContent Read(ref CborReader reader)
            {
                return new BarContent
                {
                    Drinks = _drinkListConverter.Read(ref reader)
                };
            }
        }

        [Fact]
        public void TestRead()
        {
            // 0.567
            string cbor = "FB3FE224DD2F1A9FBE"; 
            var value = Helper.Read<DynamicValue>(cbor);
            Assert.Equal(0.567, value.Value);

            // -110
            cbor = "386D"; 
            value = Helper.Read<DynamicValue>(cbor);
            Assert.Equal(-110, value.Value);

            // "Bar"
            cbor = "63426172"; 
            value = Helper.Read<DynamicValue>(cbor);
            Assert.Equal("Bar", value.ValueString);

            // [{"Name": "Beer", "Temperature": 3, "Count": 33}, {"Name": "Wine", "Temperature": 7, "Count": 6}]
            cbor = "82A3644E616D6564426565726B54656D70657261747572650365436F756E741821A3644E616D656457696E656B54656D70657261747572650765436F756E7406"; 
            value = Helper.Read<DynamicValue>(cbor);
            Assert.Equal(2, value.BarContent.Drinks.Count);
        }
    }
}