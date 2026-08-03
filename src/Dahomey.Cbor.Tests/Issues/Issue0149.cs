#nullable enable
using System.Buffers;
using Xunit;

namespace Dahomey.Cbor.Tests.Issues
{
    /// <summary>
    /// Issue #149 — "Stack Overflow": serializing a class with a nullable property whose type is the
    /// class itself overflowed the stack.
    /// </summary>
    /// <remarks>
    /// Already fixed by the recursion work in #147 (reuse the converter under construction when a
    /// member's type matches it) and #151 (lazy converter resolution). These tests pin the behaviour
    /// so it cannot silently regress — a regression here is a stack overflow, which kills the test
    /// host rather than failing a test, so it is worth keeping cheap and explicit.
    /// </remarks>
    public class Issue0149
    {
        public class TestObject
        {
            public TestObject? TestProperty { get; set; } = null;
        }

        [Fact]
        public void SerializeSelfReferentialNullablePropertyThatIsNull()
        {
            ArrayBufferWriter<byte> buffer = new ArrayBufferWriter<byte>();
            Cbor.Serialize(new TestObject(), buffer);

            // {"TestProperty": null}
            Assert.Equal("A16C5465737450726F7065727479F6", Helper.Write(new TestObject()));
        }

        [Fact]
        public void SerializeSelfReferentialNullablePropertyThatIsSet()
        {
            TestObject obj = new TestObject { TestProperty = new TestObject() };

            // {"TestProperty": {"TestProperty": null}}
            Assert.Equal(
                "A16C5465737450726F7065727479A16C5465737450726F7065727479F6",
                Helper.Write(obj));
        }

        [Fact]
        public void RoundTripSelfReferentialChain()
        {
            TestObject obj = new TestObject
            {
                TestProperty = new TestObject
                {
                    TestProperty = new TestObject(),
                },
            };

            string hexBuffer = Helper.Write(obj);
            TestObject rehydrated = Cbor.Deserialize<TestObject>(
                Extensions.StringExtensions.HexToBytes(hexBuffer));

            Assert.NotNull(rehydrated.TestProperty);
            Assert.NotNull(rehydrated.TestProperty!.TestProperty);
            Assert.Null(rehydrated.TestProperty!.TestProperty!.TestProperty);
        }

        // NOTE: a genuine reference cycle (obj.TestProperty = obj) is deliberately NOT tested here.
        // CBOR has no back-references, and the writer has no depth limit, so it recurses until the
        // stack is exhausted. A StackOverflowException cannot be caught, so such a test aborts the
        // whole test host rather than failing. Bounding recursion depth is tracked separately; see
        // the workspace AOT-PLAN/CLAUDE notes.

    }
}
