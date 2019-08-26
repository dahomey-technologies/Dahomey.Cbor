using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

namespace Dahomey.Cbor.Tests
{
    [TestClass]
    public class InterfaceCollectionTests
    {
        private class ObjectWithICollection
        {
            public ICollection<int> Collection { get; set; }
        }

        [TestMethod]
        public void ICollection()
        {
            const string hexBuffer = "A16A436F6C6C656374696F6E820C0D";
            ObjectWithICollection obj = Helper.Read<ObjectWithICollection>(hexBuffer);

            Assert.IsNotNull(obj);
            Assert.IsNotNull(obj.Collection);
            Assert.IsInstanceOfType(obj.Collection, typeof(List<int>));
            Assert.AreEqual(2, obj.Collection.Count);
            Assert.AreEqual(12, obj.Collection.ElementAt(0));
            Assert.AreEqual(13, obj.Collection.ElementAt(1));

            Helper.TestWrite(obj, hexBuffer);
        }

        private class ObjectWithISet
        {
            public ISet<int> Collection { get; set; }
        }

        [TestMethod]
        public void ISet()
        {
            const string hexBuffer = "A16A436F6C6C656374696F6E820C0D";
            ObjectWithISet obj = Helper.Read<ObjectWithISet>(hexBuffer);

            Assert.IsNotNull(obj);
            Assert.IsNotNull(obj.Collection);
            Assert.IsInstanceOfType(obj.Collection, typeof(HashSet<int>));
            Assert.AreEqual(2, obj.Collection.Count);
            Assert.AreEqual(12, obj.Collection.ElementAt(0));
            Assert.AreEqual(13, obj.Collection.ElementAt(1));

            Helper.TestWrite(obj, hexBuffer);
        }

        private class ObjectWithIDictionary
        {
            public IDictionary<int, int> Collection { get; set; }
        }

        [TestMethod]
        public void IDictionary()
        {
            const string hexBuffer = "A16A436F6C6C656374696F6EA20C0C0D0D";
            ObjectWithIDictionary obj = Helper.Read<ObjectWithIDictionary>(hexBuffer);

            Assert.IsNotNull(obj);
            Assert.IsNotNull(obj.Collection);
            Assert.IsInstanceOfType(obj.Collection, typeof(Dictionary<int,int>));
            Assert.AreEqual(2, obj.Collection.Count);
            Assert.AreEqual(12, obj.Collection[12]);
            Assert.AreEqual(13, obj.Collection[13]);

            Helper.TestWrite(obj, hexBuffer);
        }
    }
}
