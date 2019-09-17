using System.Collections.Generic;
using System.Diagnostics;
using Dahomey.Cbor.Attributes;
using Newtonsoft.Json;
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
        public void MajorTextTypeExpectedMajorTypeTextString3Read()
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
            var hexData = "A166726573756C7481A16C7061727469636970616E747383A262696418476475736572A2626964636A3166646E616D65674AC3B87267656EA262696418486475736572A2626964636E3268646E616D65664A65726F6D79A262696418496475736572A26269646371336B646E616D65664D6167676965";
            var ex = Record.Exception(() =>
            {
                var obj = Helper.Read<WebserviceResponse<List<Conversation>>>
                    (hexData, CborOptions.Default);
            });

            Assert.Null(ex);

            return;
        }

        [Fact]
        public void MajorTextTypeExpectedMajorTypeTextString3Write()
        {
            var json = @"{""result"":[{""participants"":[{""id"":71,""user"":{""id"":""j1f"",""name"":""Jørgen""}},{""id"":72,""user"":{""id"":""n2h"",""name"":""Jeromy""}},{""id"":73,""user"":{""id"":""q3k"",""name"":""Maggie""}}]}]}";
            var ws = JsonConvert.DeserializeObject<WebserviceResponse<List<Conversation>>>(json);
            
            var actual = Helper.Write(ws, CborOptions.Default);

            var ex = Record.Exception(() =>
            {
                var decoded = Helper.Read<WebserviceResponse<List<Conversation>>>(actual, CborOptions.Default);
            });
            Assert.Null(ex);

            var expected = "A166726573756C7481A16C7061727469636970616E747383A262696418476475736572A2626964636A3166646E616D65674AC3B87267656EA262696418486475736572A2626964636E3268646E616D65664A65726F6D79A262696418496475736572A26269646371336B646E616D65664D6167676965";

            Assert.Equal(expected, actual);

        }
    }
}