using System.Collections.Generic;
using Dahomey.Cbor.Attributes;
using Xunit;

namespace Dahomey.Cbor.Tests.Issues
{
    public class Issue0034
    {
        public class WebserviceResponse<T>
        {
            [CborProperty("result")]
            public T Result { get; set; }
        }
        public class Conversation
        {
            [CborProperty("id")]
            public int id { get; set; }
            [CborProperty("participants")]
            public List<Participant> participants { get; set; }
        }

        public class Participant
        {
            [CborProperty("id")]
            public int Id { get; set; }
            [CborProperty("user")]
            public User User { get; set; }
        }

        public class User
        {
            [CborProperty("id")]
            public string Id { get; set; }
            [CborProperty("name")]
            public string Name { get; set; }
        }

        [Fact]
        public void MajorTextTypeExpectedMajorTypeTextString3()
        {
            /*
             * In JSON
            {"result":[{
                "participants":[
                    {"id":71,"user":{"id":"j1f","name":"Jørgen"}},
                    {"id":72,"user":{"id":"n2h","name":"Jeromy"}},
                    {"id":73,"user":{"id":"q3k","name":"Maggie"}}
                ]}
            ]}
             */

            // Hex Encoded CBOR data encoded by
            // await Cbor.SerializeAsync(ws, stream2, CborOptions.Default);
            var hexData = "A266726573756C7481A362696400646B657973F66C7061727469636970616E747383A262696418476475736572A9624964636A3166684C656E677468434D006647656E64657200644E616D65664AC3B87267656E6443697479F6675461674C696E65F666417661746172F6634167650069497343686172697479F4A262696418486475736572A9624964636E3268684C656E677468434D006647656E64657200644E616D65664A65726F6D796443697479F6675461674C696E65F666417661746172F6634167650069497343686172697479F4A262696418496475736572A96249646371336B684C656E677468434D006647656E64657200644E616D65664D61676769656443697479F6675461674C696E65F666417661746172F6634167650069497343686172697479F46673746174757318C8";

            var ex = Record.Exception(() => {
                var obj = Helper.Read<WebserviceResponse<List<Conversation>>>
                    (hexData, CborOptions.Default);
            });

            Assert.Null(ex);

            return;
        }
    }
}
