# Object formats

[← back to the README](../README.md)

`CborObjectFormat`, set on a type with `[CborObjectFormat(...)]`, decides how a class or struct is laid
out on the wire:

| Format | Layout |
|---|---|
| `StringKeyMap` (default) | a map keyed by member name |
| `IntKeyMap` | a map keyed by the member's `[CborProperty(n)]` index |
| `Array` | an array, members written by position, no keys at all |

Both keyless-ish formats compress a document of repeated keys hard, in plain RFC 8949 — usually harder
than the string references this library [does not support](strings.md#string-references-tag-25--not-supported).

## `IntKeyMap` addresses, `Array` orders

The two read `[CborProperty(n)]` differently, and the difference is worth being explicit about.
`IntKeyMap` writes `n` into the document as the member's key, so it addresses the member: gaps are
free, and a member keeps its meaning wherever it moves in the type. `Array` writes no keys at all, so
`n` only *orders* the members — they are written in ascending index order and read back by position.
Gaps and negative indexes are allowed and change nothing on the wire, so these two types produce
identical bytes:

```csharp
[CborObjectFormat(CborObjectFormat.Array)]
public class Row { [CborProperty(0)] public int Id { get; set; } [CborProperty(1)] public string Name { get; set; } }

[CborObjectFormat(CborObjectFormat.Array)]
public class SameRow { [CborProperty(5)] public int Id { get; set; } [CborProperty(9)] public string Name { get; set; } }

// both write 82 02 63 726F77  --  [2, "row"]
```

What that costs is that an `Array` type's wire format depends on its member *set*, not only on the
indexes: inserting a member with an index that sorts between two existing ones shifts everything after
it by one position, where `IntKeyMap` would not move. Use `IntKeyMap` where indexes need to be stable
addresses across versions.

## `Array` writes every member

For the same reason, `Array` writes every member of the type on every document. A member omitted by
`[CborIgnoreIfDefault]` or by a `ShouldSerializeXyz()` method would leave no trace in a keyless
format, so everything after it would shift a position earlier and read back onto the wrong member.
Those declarations are therefore ignored in `Array`, and the member is written with the value it
holds — `null` or the type's default in the case they exist to catch. They work as declared in
`StringKeyMap` and `IntKeyMap`, where each value travels with the key that says which member it
belongs to.

One combination becomes visible that way: a member declaring both `[CborIgnoreIfDefault]` and
`[CborRequired]` — with a policy of `DisallowNull` or `Always` — throws when it is null. The two
declarations contradict each other in a format that cannot omit anything, and the document such a
write would produce could not be read back. Drop one of the two, or use `IntKeyMap`.

A member keyed by an index has no name to put in such a message, so it is reported as its index:
`Property 'index 2' cannot be null.` That also applies to `IntKeyMap`.

## The discriminator

The discriminator is not a member and holds no position — in `Array` it is written under a semantic tag
and recognised by it — so `CborDiscriminatorPolicy` decides whether it appears in an `Array` exactly as
it does in a map. See [polymorphism](polymorphism.md#where-the-discriminator-is-written) for where each
format puts it.
