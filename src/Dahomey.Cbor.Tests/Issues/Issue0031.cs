using System.Collections.Generic;
using Xunit;

namespace Dahomey.Cbor.Tests.Issues
{
    public class Issue0031
    {
        public class ObjectWithNullArray
        {
            public byte[] Bytes { get; set; }
        }

        public class ObjectWithNullLists
        {
            public List<string> Items { get; set; }
        }

		public class ObjectWithNullDictionary
		{
			public Dictionary<string, string> Items { get; set; }
		}

		[Fact]
        public void TestNullReferenceList()
        {

            var obj = new ObjectWithNullLists();

            string hexResult = null;

            var ex = Record.Exception(() => {
                hexResult = Helper.Write(obj);
            });

            Assert.NotEmpty(hexResult);
            Assert.Null(ex);
            Assert.Equal("A1654974656D73F6", hexResult);

        }

		[Fact]
        public void TestNullReferenceArray()
        {

            var obj = new ObjectWithNullArray();

            string hexResult = null;

            var ex = Record.Exception(() => {
                hexResult = Helper.Write(obj);
            });

            Assert.NotEmpty(hexResult);
            Assert.Null(ex);
            Assert.Equal("A1654279746573F6", hexResult);
        }


		[Fact]
		public void TestNullReferenceDictionary()
		{
			var obj = new ObjectWithNullDictionary();

			string hexResult = null;

			var ex = Record.Exception(() => {
				hexResult = Helper.Write(obj);
			});

			Assert.NotEmpty(hexResult);
			Assert.Null(ex);
			Assert.Equal("A1654974656D73F6", hexResult);

		}
	}
}
