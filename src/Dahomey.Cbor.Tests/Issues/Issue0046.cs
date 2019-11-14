using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Serialization.Conventions;
using System;
using System.Linq;
using System.Text;
using Xunit;

namespace Dahomey.Cbor.Tests.Issues
{
    public class Issue0046
    {
        [CborDiscriminator("atom", Policy = CborDiscriminatorPolicy.Always)]
        public class Atom
        {
            public int Z { get; set; }
            public int A { get; set; }
        }

        [Fact]
        public void Test()
        {
            CborOptions options = new CborOptions();
            options.Registry.DiscriminatorConventionRegistry.RegisterConvention(new DefaultDiscriminatorConvention(options.Registry, "serializer"));
            options.Registry.ObjectMappingRegistry.Register<Atom>(objectMapping =>
            {
                objectMapping.AutoMap();
                objectMapping.SetOrderBy(m => m.MemberName);
            });

            Atom obj = new Atom
            {
                A = 10,
                Z = 10
            };

            const string hexBuffer = "A361410A6A73657269616C697A65726461746F6D615A0A";
            Helper.TestWrite(obj, hexBuffer, null, options);
        }
    }
}
