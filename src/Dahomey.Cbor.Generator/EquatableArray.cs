using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Required by <c>init</c> accessors, and absent from netstandard2.0. Declaring it here is what
    /// lets this project use records at all.
    /// </summary>
    internal static class IsExternalInit
    {
    }
}

namespace Dahomey.Cbor.Generator
{
    /// <summary>
    /// An <see cref="ImmutableArray{T}"/> that compares by content.
    /// </summary>
    /// <remarks>
    /// The incremental pipeline skips a step when its input equals the previous run's, and
    /// <see cref="ImmutableArray{T}"/> compares by reference to its backing array. A model carrying
    /// one is therefore never equal to the model the next run produces, however identical the
    /// contents, and every downstream step re-runs on every keystroke -- which is the whole failure
    /// this type exists to avoid.
    /// </remarks>
    internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IEnumerable<T>
        where T : IEquatable<T>
    {
        private readonly ImmutableArray<T> _values;

        public EquatableArray(ImmutableArray<T> values)
        {
            _values = values;
        }

        public EquatableArray(IEnumerable<T> values)
        {
            _values = ImmutableArray.CreateRange(values);
        }

        public static EquatableArray<T> Empty => new EquatableArray<T>(ImmutableArray<T>.Empty);

        public int Count => _values.IsDefault ? 0 : _values.Length;

        public T this[int index] => _values[index];

        public bool Equals(EquatableArray<T> other)
        {
            if (_values.IsDefault || other._values.IsDefault)
            {
                return _values.IsDefault && other._values.IsDefault;
            }

            if (_values.Length != other._values.Length)
            {
                return false;
            }

            for (int i = 0; i < _values.Length; i++)
            {
                if (!EqualityComparer<T>.Default.Equals(_values[i], other._values[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object? obj)
        {
            return obj is EquatableArray<T> other && Equals(other);
        }

        public override int GetHashCode()
        {
            if (_values.IsDefault)
            {
                return 0;
            }

            int hash = 17;

            foreach (T value in _values)
            {
                hash = (hash * 31) + (value?.GetHashCode() ?? 0);
            }

            return hash;
        }

        public object?[] ToObjectArray()
        {
            object?[] result = new object?[Count];

            for (int i = 0; i < result.Length; i++)
            {
                result[i] = _values[i];
            }

            return result;
        }

        public IEnumerator<T> GetEnumerator()
        {
            return _values.IsDefault
                ? ((IEnumerable<T>)Array.Empty<T>()).GetEnumerator()
                : ((IEnumerable<T>)_values).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
