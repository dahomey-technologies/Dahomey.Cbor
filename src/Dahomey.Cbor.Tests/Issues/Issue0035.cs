using Xunit;

namespace Dahomey.Cbor.Tests.Issues
{
    public class Issue0033
    {
        public class Result<T>
        {
            public T Data { get; set; }
            //public Result()
            //{
            //    // This is required now. Should not be
            //}

            public Result(T data)
            {
                Data = data;
            }
        }


        [Fact]
        public void TestNonRequiredEmptyConstructor()
        {
            var result = new Result<string>("test");
            string hexEncoded = null;
            var ex = Record.Exception(() =>
            {
                hexEncoded = Helper.Write(result);
            });
            Assert.Null(ex);
            Assert.NotNull(hexEncoded);
            Assert.Equal("A164446174616474657374", hexEncoded);
        }
    }
}
