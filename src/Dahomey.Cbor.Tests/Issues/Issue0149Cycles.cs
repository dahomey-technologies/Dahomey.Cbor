using Dahomey.Cbor.Tests.Extensions;
using Xunit;

namespace Dahomey.Cbor.Tests.Issues
{
    /// <summary>
    /// Issue #149, converter-construction side: two types that reference each other must be
    /// serializable.
    /// </summary>
    /// <remarks>
    /// A converter resolves its members' converters while it is being constructed, and it is not in
    /// the registry until its constructor returns. Two mutually referencing types therefore ask the
    /// registry for each other indefinitely, and the process dies on an uncatchable
    /// <see cref="System.StackOverflowException"/> before any CBOR is written — a different failure
    /// from the deep-nesting one <c>CborOptions.MaxDepth</c> bounds, which happens during a write on
    /// a converter graph that built fine.
    /// <para>
    /// The single-type case (<c>A.Property</c> of type <c>A</c>) is covered by
    /// <see cref="Issue0149"/>; these are the cases that need more than the parent-converter reuse.
    /// </para>
    /// </remarks>
    public class Issue0149Cycles
    {
        public class Parent
        {
            public string Name { get; set; }
            public Child Only { get; set; }
        }

        public class Child
        {
            public int Age { get; set; }
            public Parent Owner { get; set; }
        }

        public class RingA
        {
            public int Id { get; set; }
            public RingB Next { get; set; }
        }

        public class RingB
        {
            public int Id { get; set; }
            public RingC Next { get; set; }
        }

        public class RingC
        {
            public int Id { get; set; }
            public RingA Next { get; set; }
        }

        /// <summary>
        /// Two types referencing each other — the back-reference shape that any parent/child model
        /// has.
        /// </summary>
        [Fact]
        public void MutuallyReferencingTypesCanBeSerialized()
        {
            Parent parent = new Parent
            {
                Name = "root",
                Only = new Child { Age = 3 },
            };

            // a2                        map(2)
            //    644e616d65             "Name"
            //    64726f6f74             "root"
            //    644f6e6c79             "Only"
            //    a2                     map(2)
            //       63416765            "Age"
            //       03                  3
            //       654f776e6572        "Owner"
            //       f6                  null
            Helper.TestWrite(parent, "A2644E616D6564726F6F74644F6E6C79A26341676503654F776E6572F6");
        }

        [Fact]
        public void MutuallyReferencingTypesCanBeDeserialized()
        {
            const string hexBuffer = "A2644E616D6564726F6F74644F6E6C79A26341676503654F776E6572F6";

            Parent parent = Cbor.Deserialize<Parent>(hexBuffer.HexToBytes());

            Assert.Equal("root", parent.Name);
            Assert.Equal(3, parent.Only.Age);
            Assert.Null(parent.Only.Owner);
        }

        /// <summary>
        /// A cycle longer than two types: nothing in the break may depend on the pair being adjacent.
        /// </summary>
        [Fact]
        public void ThreeTypeCycleCanBeSerialized()
        {
            RingA a = new RingA
            {
                Id = 1,
                Next = new RingB { Id = 2, Next = new RingC { Id = 3 } },
            };

            // a2                     map(2)
            //    624964              "Id"
            //    01                  1
            //    644e657874          "Next"
            //    a2                  map(2)
            //       624964           "Id"
            //       02               2
            //       644e657874       "Next"
            //       a2               map(2)
            //          624964        "Id"
            //          03            3
            //          644e657874    "Next"
            //          f6            null
            Helper.TestWrite(a, "A262496401644E657874A262496402644E657874A262496403644E657874F6");
        }

        /// <summary>
        /// An actual object graph cycle is still a write-side infinite recursion, bounded by
        /// <see cref="CborOptions.MaxDepth"/> rather than by anything here. Building the converters
        /// is what must not overflow; writing a cycle is a caller error and stays a
        /// <see cref="CborException"/>.
        /// </summary>
        [Fact]
        public void AnActualObjectCycleIsStillBoundedByMaxDepth()
        {
            Parent parent = new Parent { Name = "root" };
            parent.Only = new Child { Age = 3, Owner = parent };

            Assert.Throws<CborException>(() => Helper.Write(parent));
        }
    }
}
