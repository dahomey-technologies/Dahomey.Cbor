using Xunit;
using System.Collections.Generic;
using System.Linq;

namespace Dahomey.Cbor.Tests
{

    public class InterfaceCollectionTests
    {
        private class ObjectWithICollection
        {
            public ICollection<int> Collection { get; set; }
        }

        [Fact]
        public void ICollection()
        {
            const string hexBuffer = "A16A436F6C6C656374696F6E820C0D";
            ObjectWithICollection obj = Helper.Read<ObjectWithICollection>(hexBuffer);

            Assert.NotNull(obj);
            Assert.NotNull(obj.Collection);
            Assert.IsType<List<int>>(obj.Collection);
            Assert.Equal(2, obj.Collection.Count);
            Assert.Equal(12, obj.Collection.ElementAt(0));
            Assert.Equal(13, obj.Collection.ElementAt(1));

            Helper.TestWrite(obj, hexBuffer);
        }

        private class ObjectWithISet
        {
            public ISet<int> Collection { get; set; }
        }

        [Fact]
        public void ISet()
        {
            const string hexBuffer = "A16A436F6C6C656374696F6E820C0D";
            ObjectWithISet obj = Helper.Read<ObjectWithISet>(hexBuffer);

            Assert.NotNull(obj);
            Assert.NotNull(obj.Collection);
            Assert.IsType<HashSet<int>>(obj.Collection);
            Assert.Equal(2, obj.Collection.Count);
            Assert.Equal(12, obj.Collection.ElementAt(0));
            Assert.Equal(13, obj.Collection.ElementAt(1));

            Helper.TestWrite(obj, hexBuffer);
        }

        private class ObjectWithIDictionary
        {
            public IDictionary<int, int> Collection { get; set; }
        }

        [Fact]
        public void IDictionary()
        {
            const string hexBuffer = "A16A436F6C6C656374696F6EA20C0C0D0D";
            ObjectWithIDictionary obj = Helper.Read<ObjectWithIDictionary>(hexBuffer);

            Assert.NotNull(obj);
            Assert.NotNull(obj.Collection);
            Assert.IsType<Dictionary<int,int>>(obj.Collection);
            Assert.Equal(2, obj.Collection.Count);
            Assert.Equal(12, obj.Collection[12]);
            Assert.Equal(13, obj.Collection[13]);

            Helper.TestWrite(obj, hexBuffer);
        }
    }
}
