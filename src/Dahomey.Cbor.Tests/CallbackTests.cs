using Dahomey.Cbor.Attributes;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    public class CallbackTests
    {
        private class ObjectWithCallbacks : IEquatable<ObjectWithCallbacks>
        {
            public int Id { get; set; }

            [CborIgnore] public bool OnDeserializingCalled { get; private set; }
            [CborIgnore] public bool OnDeserializedCalled { get; private set; }
            [CborIgnore] public bool OnSerializingCalled { get; private set; }
            [CborIgnore] public bool OnSerializedCalled { get; private set; }

            [OnDeserializing]
            public void OnDeserializing()
            {
                Assert.False(OnDeserializingCalled);
                Assert.False(OnDeserializedCalled);
                Assert.Equal(0, Id);
                OnDeserializingCalled = true;
            }

            [OnDeserialized]
            public void OnDeserialized()
            {
                Assert.True(OnDeserializingCalled);
                Assert.False(OnDeserializedCalled);
                OnDeserializedCalled = true;
            }

            [OnSerializing]
            public void OnSerializing()
            {
                Assert.False(OnSerializingCalled);
                Assert.False(OnSerializedCalled);
                OnSerializingCalled = true;
            }

            [OnSerialized]
            public void OnSerialized()
            {
                Assert.True(OnSerializingCalled);
                Assert.False(OnSerializedCalled);
                OnSerializedCalled = true;
            }

            public bool Equals([AllowNull] ObjectWithCallbacks other)
            {
                return Id == other.Id &&
                    OnDeserializingCalled == other.OnDeserializingCalled &&
                    OnDeserializedCalled == other.OnDeserializedCalled &&
                    OnSerializingCalled == other.OnSerializingCalled &&
                    OnSerializedCalled == other.OnSerializedCalled;
            }
        }

        [Fact]
        public void TestReadByAttribute()
        {
            const string hexBuffer = "A16249640C"; // {"Id":12}
            ObjectWithCallbacks obj = Helper.Read<ObjectWithCallbacks>(hexBuffer);

            Assert.NotNull(obj);
            Assert.True(obj.OnDeserializingCalled);
            Assert.True(obj.OnDeserializedCalled);
            Assert.False(obj.OnSerializingCalled);
            Assert.False(obj.OnSerializedCalled);
        }

        [Fact]
        public void TestWriteByAttribute()
        {
            const string hexBuffer = "A16249640C"; // {"Id":12}
            Helper.TestWrite(new ObjectWithCallbacks { Id = 12 }, hexBuffer);
        }

        [Fact]
        public void TestReadByApi()
        {
            CborOptions options = new CborOptions();
            options.Registry.ObjectMappingRegistry.Register<ObjectWithCallbacks>(om =>
            {
                om.MapMember(o => o.Id);
                om.SetOnDeserializingMethod(o => o.OnDeserializing());
                om.SetOnDeserializedMethod(o => o.OnDeserialized());
            });

            const string hexBuffer = "A16249640C"; // {"Id":12}
            ObjectWithCallbacks obj = Helper.Read<ObjectWithCallbacks>(hexBuffer, options);

            Assert.NotNull(obj);
            Assert.True(obj.OnDeserializingCalled);
            Assert.True(obj.OnDeserializedCalled);
            Assert.False(obj.OnSerializingCalled);
            Assert.False(obj.OnSerializedCalled);
        }

        [Fact]
        public void TestWriteByApi()
        {
            CborOptions options = new CborOptions();
            options.Registry.ObjectMappingRegistry.Register<ObjectWithCallbacks>(om =>
            {
                om.MapMember(o => o.Id);
                om.SetOnSerializingMethod(o => o.OnSerializing());
                om.SetOnSerializedMethod(o => o.OnSerialized());
            });

            const string hexBuffer = "A16249640C"; // {"Id":12}
            Helper.TestWrite(new ObjectWithCallbacks { Id = 12 }, hexBuffer, null, options);
        }
    }
}
