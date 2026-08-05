using Dahomey.Cbor.Tests.Extensions;
using System;
using System.Collections.Generic;
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

        public class Team
        {
            public string Name { get; set; }
            public List<Member> Members { get; set; }
        }

        public class Member
        {
            public string Name { get; set; }
            public Team Team { get; set; }
        }

        public struct Slot
        {
            public Node Node { get; set; }
        }

        public class Boxed
        {
            public int Id { get; set; }
            public ValueTuple<Boxed, int> Pair { get; set; }
        }

        public class Node
        {
            public int Id { get; set; }
            public Slot Slot { get; set; }
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

        [Fact]
        public void ThreeTypeCycleCanBeDeserialized()
        {
            const string hexBuffer = "A262496401644E657874A262496402644E657874A262496403644E657874F6";

            RingA a = Cbor.Deserialize<RingA>(hexBuffer.HexToBytes());

            Assert.Equal(1, a.Id);
            Assert.Equal(2, a.Next.Id);
            Assert.Equal(3, a.Next.Next.Id);
            Assert.Null(a.Next.Next.Next);
        }

        /// <summary>
        /// The cycle most real models actually have: the back-reference is reached through a
        /// collection rather than directly.
        /// </summary>
        /// <remarks>
        /// This shape survives on <c>master</c> already, because <c>AbstractCollectionConverter</c>
        /// resolves its item converter lazily. Pinning it keeps a future change there from
        /// reintroducing the overflow through a path nothing else covers.
        /// </remarks>
        [Fact]
        public void CycleThroughACollectionCanBeRoundTripped()
        {
            Team team = new Team
            {
                Name = "a",
                Members = new List<Member> { new Member { Name = "b" } },
            };

            // a2                           map(2)
            //    644e616d65                "Name"
            //    6161                      "a"
            //    674d656d62657273          "Members"
            //    81                        array(1)
            //       a2                     map(2)
            //          644e616d65          "Name"
            //          6162                "b"
            //          645465616d          "Team"
            //          f6                  null
            const string hexBuffer = "A2644E616D656161674D656D6265727381A2644E616D656162645465616DF6";

            Helper.TestWrite(team, hexBuffer);

            Team read = Cbor.Deserialize<Team>(hexBuffer.HexToBytes());

            Assert.Equal("a", read.Name);
            Assert.Single(read.Members);
            Assert.Equal("b", read.Members[0].Name);
            Assert.Null(read.Members[0].Team);
        }

        /// <summary>
        /// A cycle whose second leg goes through <c>StructMemberConverter</c>. Struct-to-struct
        /// cycles are impossible in C#, but a struct holding a class that holds the struct is a
        /// genuine mutual reference and is the only shape that reaches that converter.
        /// </summary>
        [Fact]
        public void CycleThroughAStructMemberCanBeRoundTripped()
        {
            Node node = new Node { Id = 7 };

            // a2                     map(2)
            //    624964              "Id"
            //    07                  7
            //    64536c6f74          "Slot"
            //    a1                  map(1)
            //       644e6f6465       "Node"
            //       f6               null
            const string hexBuffer = "A26249640764536C6F74A1644E6F6465F6";

            Helper.TestWrite(node, hexBuffer);

            Node read = Cbor.Deserialize<Node>(hexBuffer.HexToBytes());

            Assert.Equal(7, read.Id);
            Assert.Null(read.Slot.Node);
        }

        /// <summary>
        /// A cycle whose second leg goes through a converter that resolves eagerly.
        /// </summary>
        /// <remarks>
        /// The tuple and nullable converters look their item types up from their own constructors,
        /// unlike the collection and dictionary converters. That stays safe only because the member
        /// converter no longer resolves during construction: <c>Boxed</c> reaches the registry before
        /// its <c>Pair</c> member is looked at, so <c>Tuple2Converter</c>'s eager <c>Lookup</c> finds
        /// it rather than re-entering its construction. This is the one shape that covers those
        /// eager lookups, and it overflows the stack without the change.
        /// </remarks>
        [Fact]
        public void CycleThroughATupleCanBeSerialized()
        {
            Boxed boxed = new Boxed { Id = 1, Pair = new ValueTuple<Boxed, int>(new Boxed { Id = 2 }, 9) };

            // a2                        map(2)
            //    624964                 "Id"
            //    01                     1
            //    6450616972             "Pair"
            //    82                     array(2)
            //       a2                  map(2)
            //          624964           "Id"
            //          02               2
            //          6450616972       "Pair"
            //          82               array(2)
            //             f6            null
            //             00            0
            //       09                  9
            Helper.TestWrite(boxed, "A262496401645061697282A262496402645061697282F60009");
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
