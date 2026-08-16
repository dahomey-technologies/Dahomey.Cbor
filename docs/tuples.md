# Tuples

[← back to the README](../README.md)

A `ValueTuple` is one CBOR array holding its elements in order, at any arity:

```csharp
// (1, "two")                        -> 82 01 63 74776F
// (1, 2, 3, 4, 5, 6, 7, 8, 9)       -> 89 01 02 03 04 05 06 07 08 09
// fifteen elements                  -> 8F 01 02 ... 0F
```

**Past seven elements the array stays flat.** C# represents such a tuple as seven fields plus a `Rest`
holding the overflow, and nests again every seven after that, but that is a detail of the language: a
nine-element tuple is nine items, not seven and a nested pair. A one-element `ValueTuple<T>` — which C#
has no literal for — is an array of one.

Reading is exact about arity: an array of the wrong length is a `CborException` naming the arity
expected, in both the definite and indefinite-length forms, and a failure inside an element names its
flattened position, so `$[8]` rather than a path through a `Rest` the document knows nothing about.

`System.Tuple<…>` — the class, not the struct — is not a tuple to this library and is treated as an
ordinary object.

## Under Native AOT

Tuples work in a source-generated context, at every arity:

```csharp
[CborSerializable(typeof(Reading))]
public partial class MyContext : CborSerializerContext { }
```

The generated context names each converter instantiation in source — `Tuple2Converter<int, string>`, and
for a longer tuple `Tuple8Converter<…, ValueTuple<int, int>>` together with the `Rest`'s own converter —
so nothing is constructed reflectively and the bytes are identical to the reflection path's.

**Without a context, a tuple needs the reflection path, which Native AOT cannot serve.** The provider
resolves a tuple's converter through `Type.MakeGenericType`, as it does for every generic converter —
collections, dictionaries, nullables — so a trimmed or AOT-published application must declare the types
it serializes on a context. That is the same requirement as for any other generic type here, not
something specific to tuples.
