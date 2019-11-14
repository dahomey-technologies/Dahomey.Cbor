using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.ObjectModel;
using Dahomey.Cbor.Serialization.Conventions;
using System;
using System.Collections.Generic;

namespace Dahomey.Cbor.Tests
{
    public static class SampleClasses
    {
        static SampleClasses()
        {
            CborOptions.Default.Registry.DiscriminatorConventionRegistry.RegisterType(typeof(NameObject));
            CborOptions.Default.Registry.DiscriminatorConventionRegistry.RegisterType(typeof(DescriptionObject));
            CborOptions.Default.Registry.DiscriminatorConventionRegistry.RegisterType(typeof(EmptyObjectWithDiscriminator));
        }

        /// <summary>
        /// The only aim of this method is to force a single call of the static constructor
        /// </summary>
        public static void Initialize()
        {
        }
    }

    public class EmptyObject
    {
    }

    [CborDiscriminator("EmptyObjectWithDiscriminator")]
    public class EmptyObjectWithDiscriminator
    {
    }

    public enum EnumTest
    {
        None = 0,
        Value1 = 1,
        Value2 = 2,
    }

    public class SimpleObject
    {
        public bool Boolean { get; set; }
        public sbyte SByte { get; set; }
        public byte Byte { get; set; }
        public ushort Int16 { get; set; }
        public short UInt16 { get; set; }
        public int Int32 { get; set; }
        public uint UInt32 { get; set; }
        public long Int64 { get; set; }
        public ulong UInt64 { get; set; }
        public string String { get; set; }
        public float Single { get; set; }
        public double Double { get; set; }
        public DateTime DateTime { get; set; }
        public EnumTest Enum { get; set; }
    }

    public class SimpleObjectWithFields
    {
        public bool Boolean;
        public sbyte SByte;
        public byte Byte;
        public ushort Int16;
        public short UInt16;
        public int Int32;
        public uint UInt32;
        public long Int64;
        public ulong UInt64;
        public string String;
        public float Single;
        public double Double;
        public DateTime DateTime;
        public EnumTest Enum;
    }

    public class IntObject
    {
        public int IntValue { get; set; }

        protected bool Equals(IntObject other)
        {
            return IntValue == other.IntValue;
        }

        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((IntObject)obj);
        }

        public override int GetHashCode()
        {
            return IntValue;
        }
    }

    public class CborValueObject
    {
        public CborValue CborValue { get; set; }
    }

    public class GuidObject
    {
        public Guid Guid { get; set; }
    }

    public class DictionaryObject
    {
        public Dictionary<int, int> IntDictionary { get; set; }
        public Dictionary<uint, IntObject> UIntDictionary { get; set; }
        public Dictionary<string, List<IntObject>> StringDictionary { get; set; }
        public Dictionary<EnumTest, Dictionary<int, IntObject>> EnumDictionary { get; set; }
    }

    public class ListObject
    {
        public List<int> IntList { get; set; }
        public List<IntObject> ObjectList { get; set; }
        public List<string> StringList { get; set; }
    }

    public class ArrayObject
    {
        public int[] IntArray { get; set; }
        public IntObject[] ObjectArray { get; set; }
        public string[] StringArray { get; set; }
    }

    public class HashSetObject
    {
        public HashSet<int> IntHashSet { get; set; }
        public HashSet<IntObject> ObjectHashSet { get; set; }
        public HashSet<string> StringHashSet { get; set; }
    }

    public class GenericObject<T>
    {
        public T Value { get; set; }
    }

    public class ObjectWithObject
    {
        public IntObject Object { get; set; }
    }

    public class ObjectWithCborProperty
    {
        [CborProperty("id")]
        public int Id { get; set; }
    }

    [CborNamingConvention(typeof(CamelCaseNamingConvention))]
    public class ObjectWithNamingConvention
    {
        public int MyValue { get; set; }
    }

    public class BaseObjectHolder
    {
        public BaseObject BaseObject { get; set; }
        public NameObject NameObject { get; set; }
    }

    public class BaseObject
    {
        public int Id { get; set; }
    }

    [CborDiscriminator("NameObject")]
    public class NameObject : BaseObject
    {
        public string Name { get; set; }
    }

    [CborDiscriminator("DescriptionObject")]
    public class DescriptionObject : BaseObject
    {
        public string Description { get; set; }
    }

    public class ObjectWithCustomConverterOnProperty
    {
        [CborConverter(typeof(GuidConverter))]
        public Guid Guid { get; set; }
    }
}
