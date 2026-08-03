using System.Collections.Generic;
using Xunit;

namespace Dahomey.Cbor.Tests.Issues
{
    /// <summary>
    /// Issue #133 — "Collection Expression Serialization": a single-element C# collection expression
    /// failed to serialize with
    /// "GenericArguments[0], '&lt;&gt;z__ReadOnlySingleElementList`1[...]' ... violates the constraint
    /// of type 'TC'", while <c>new[] { 42 }</c> worked.
    /// </summary>
    /// <remarks>
    /// The compiler lowers a single-element collection expression to a synthesized
    /// <c>&lt;&gt;z__ReadOnlySingleElementList&lt;T&gt;</c>, which has no public parameterless
    /// constructor and so cannot satisfy <c>CollectionConverter&lt;TC, TI&gt;</c>'s
    /// <c>where TC : class, ICollection&lt;TI&gt;, new()</c>.
    ///
    /// Already fixed by #141, which routes types with a null namespace (the synthesized ones live in
    /// the global namespace) to <c>InterfaceCollectionConverter</c> with <c>List&lt;T&gt;</c> as the
    /// concrete collection instead. These tests pin that behaviour, including the multi-element
    /// synthesized types, which lower to different classes.
    /// </remarks>
    public class Issue0133
    {
        public class Model
        {
            public int X { get; set; }
        }

        [Fact]
        public void SingleElementCollectionExpressionMatchesArraySyntax()
        {
            int[] viaArraySyntax = new[] { 42 };
            IReadOnlyList<int> viaCollectionExpression = [42];

            // The synthesized type is the whole point of the issue; assert we really are on that path.
            Assert.StartsWith("<>z__ReadOnlySingleElementList", viaCollectionExpression.GetType().Name);

            const string hexBuffer = "81182A"; // [42]
            Assert.Equal(hexBuffer, Helper.Write(viaArraySyntax));
            Assert.Equal(hexBuffer, Helper.Write(viaCollectionExpression));
        }

        [Fact]
        public void SingleElementCollectionExpressionOfObjects()
        {
            IEnumerable<Model> models = [new Model { X = 1 }];

            Assert.StartsWith("<>z__ReadOnlySingleElementList", models.GetType().Name);

            Assert.Equal("81A1615801", Helper.Write(models)); // [{"X": 1}]
        }

        /// <summary>
        /// Two or more elements lower to a different synthesized type again
        /// (<c>&lt;&gt;z__ReadOnlyArray</c>), so cover it separately.
        /// </summary>
        [Fact]
        public void MultiElementCollectionExpression()
        {
            IReadOnlyList<int> values = [1, 2, 3];

            Assert.Equal("83010203", Helper.Write(values)); // [1, 2, 3]
        }

        [Fact]
        public void EmptyCollectionExpression()
        {
            IReadOnlyList<int> values = [];

            Assert.Equal("80", Helper.Write(values)); // []
        }

        [Fact]
        public void CollectionExpressionAsMemberValue()
        {
            ModelHolder holder = new ModelHolder { Values = [7] };

            Assert.Equal("A16656616C7565738107", Helper.Write(holder)); // {"Values": [7]}
        }

        public class ModelHolder
        {
            public IReadOnlyList<int> Values { get; set; }
        }
    }
}
