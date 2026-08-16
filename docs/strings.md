# Strings

[← back to the README](../README.md)

## Indefinite-length strings (RFC 8949 §3.2.3)

A byte or text string may arrive as a series of chunks terminated by a break, which is what a producer
emits when it does not know the length in advance:

```
7F 62 7374 64 7265616D FF     reads as "stream"
5F 42 0102 43 030405 FF       reads as the five bytes 01 02 03 04 05
```

An indefinite-length string denotes exactly the concatenation of its chunks, so it is indistinguishable
from the definite-length string of the same content once read — there is nothing to configure and
nothing to opt into.

**Reading only.** Nothing is ever written in this form: `LengthMode.IndefiniteLength` applies to arrays
and maps, not to strings, so what this library emits is unchanged.

A chunk whose major type differs from the enclosing string, and a nested indefinite-length string, are
both malformed and raise a `CborException`.

## String references (tag 25) — not supported

String references — tag 25 over an index into a table of the strings already seen, the table scoped by a
tag 256 around the document — come from the [cbor.schmorp.de](http://cbor.schmorp.de/stringref)
specification rather than from RFC 8949, and are **not supported**. Python `cbor2` emits them on
`dumps(..., string_referencing=True)`, which is not its default; `System.Formats.Cbor` does not read them
either.

Supporting them is not a matter of decoding one more tag: the table is built from every string in
document order, including the ones a decode never materialises, so stepping over an unmapped member would
have to decode it anyway or every later index would refer to the wrong string.

Tag 25 raises a `CborException` naming stringref wherever the item it stands for is about to be used.
Tag 256 is skipped, since a namespace with no reference under it is ordinary CBOR, and so is a
reference inside a member the type does not map, since nothing needs resolving to discard it.

The refusal is in the reader, so it covers deserialization into your own types, including the members
whose readers walk the tag stack themselves — `decimal`, `BigInteger`, `CborDecimalFraction`,
`CborBigFloat`.

The object model is unchanged, and reads a reference where it can. `CborValue` carries a tag it does not
model as data — as it does for typed arrays — so a document read into one keeps tag 25 in
`CborValue.SemanticTag` over the index rather than throwing. `CborValue.SemanticTag` holds one tag, so
that applies to an outermost reference only: a tag 25 nested under another tag is read as the value
underneath, which refuses it.

If the aim is compactness rather than interoperability with a producer you do not control,
`CborObjectFormat.IntKeyMap` writes each key as a small integer and `CborObjectFormat.Array` drops the
keys altogether — see [object formats](object-formats.md). Both compress a document of repeated keys
harder than string references do, in plain RFC 8949.
