using Xunit;

namespace Dahomey.Cbor.Tests.Issues
{
    public class Issue0035
    {
        public class Result<T>
        {
            public T Data { get; set; }

            public Result(T data)
            {
                Data = data;
            }
        }

        [Fact]
        void TestRead()
        {
            const string hexBuffer = "A164446174616474657374";
            Result<string> result = Helper.Read<Result<string>>(hexBuffer);

            Assert.NotNull(result);
            Assert.Equal("test", result.Data);
        }

        [Fact]
        void TestWrite()
        {
            Result<string> result = new Result<string>("test");
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
