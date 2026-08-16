# Typed arrays (RFC 8746)

[← back to the README](../README.md)

A numeric array can be encoded as an [RFC 8746](https://www.rfc-editor.org/rfc/rfc8746) typed array: one
semantic tag and one byte string holding the whole array, instead of a headered item per element.

```csharp
CborOptions options = new CborOptions { TypedArrayMode = TypedArrayMode.ReadWriteLittleEndian };
await Cbor.SerializeAsync(new[] { 1.5f, 2.5f }, stream, options);
// writes D8 55 48 00 00 C0 3F 00 00 20 40
```

Supported element types, with their little-endian tag: `sbyte` (72), `ushort` (69), `short` (77),
`uint` (70), `int` (78), `ulong` (71), `long` (79), `Half` (84), `float` (85) and `double` (86).
`byte[]` is deliberately not included: a plain CBOR byte string is shorter and is what the format
already uses; reading tags 64 and 68 into a `byte[]` still works.

On a `float[1000]` of realistic sample data this writes 4005 bytes against 4923 for the plain form, a
saving of 918. The 5 bytes of overhead are `D8 55` (tag 85) plus `59 0F A0` (byte string, length 4000).

## Reading and writing are separate

`TypedArrayMode` is a flags enum, and the default `Never` is a true no-op — upgrading to a version that
has typed arrays changes nothing for a caller who does not ask for them.

| Value | Effect |
|---|---|
| `Never` (default) | Neither read nor written. A tag in 64–87 is skipped like any other unrecognised tag. |
| `Read` | Typed arrays are read, in either byte order. Nothing is written as one. |
| `WriteLittleEndian` | Numeric arrays are written as little-endian typed arrays. |
| `ReadWriteLittleEndian` | Both. |

`Read` on its own is the interop case: accept a peer's typed arrays without changing the bytes this side
produces.

## What reads what

Writing typed arrays applies to `T[]`. Reading one fills any of the shapes that are interchangeable with
`T[]` — `List<T>`, `IList<T>`, `ICollection<T>`, `IEnumerable<T>`, `IReadOnlyList<T>`, `HashSet<T>`,
`ImmutableArray<T>` and the rest — so a document written from a `float[]` member is readable by a
consumer that declares that member as a `List<float>`.

A tag whose element type does not match the target is an error rather than something to ignore: reading
tag 86 (binary64) into a `float[]` throws, which catches a mismatch that would otherwise silently
reinterpret the bytes.

## Two things to know

`ArrayLengthMode` has no effect on an array actually written as a typed array — a typed array is a single
definite-length byte string, so there is no array header to make indefinite. It applies as usual to every
array not written as one.

The object model does not model typed arrays. A `CborValue` holding one is a `ByteString` with the tag in
`CborValue.SemanticTag`, so DOM code sees opaque bytes rather than numbers. The tag itself survives a
read-then-write, so the document still describes a typed array afterwards.

One tag, not a chain. `CborValue.SemanticTag` is a single `ulong?`, so a nested `C1 C2 01` keeps the outer
tag and drops the inner. That is a limit of the object model rather than of the writer.
