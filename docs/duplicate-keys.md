# Duplicate map keys (RFC 8949 §5.6)

[← back to the README](../README.md)

A CBOR map carrying the same key twice is rejected with a ``CborException`` naming the key, the byte
it was read at, and the path it sits at:

```
[6] Duplicate map key: A. Failed to deserialize from "$.A".
```

The object model is the one target that reports no path segment for it — ``CborValue``/``CborObject``
maps are not members, so the path of a duplicate at the root of one stays ``$``. The offset and the
key are given whatever the target.

This applies to every decode target — the ``CborValue`` object model, ``Dictionary<K,V>`` and the
other dictionary types, and mapped classes with or without a creator mapping. §5.6 requires a
protocol to define what happens on repeated keys and leaves rejecting, first-wins and last-wins all
open to the decoder, so this is a library policy rather than a conformance result. Rejecting is the
default because silently keeping one of two values for the same key is the failure mode nobody
notices, which is the wrong default for anything decoding untrusted frames.

For a protocol that does define last-wins, ask for it:

```csharp
CborOptions options = new CborOptions { DuplicateKeyMode = DuplicateKeyMode.LastWins };
```

That applies to every target too. A mode that reached only some of them would leave the policy
depending on what a document is being read into, which is the problem it exists to remove. First-wins
is not offered.

A repeated key that matches no member is not a duplicate member — what happens to an unknown name is
``UnhandledNameMode``'s question, and repeating one does not change the answer. Neither is a null map
key, which is refused in both modes: there is no earlier occurrence for a later one to win over.

The **discriminator** is refused when repeated, even though it is a key of the map rather than a
member of the type: two readers disagreeing about which occurrence names the type is how one document
comes to mean two things.

## Ambiguous mappings are refused before the decode

One case is settled earlier than the decode, because no answer at decode time is right: **a type
mapping two members to the same CBOR name** would write a document with a repeated key that it then
could not read back. The mapping itself is refused, the first time the type is serialized or
deserialized, naming the type and the colliding name:

```
class/struct Aliased maps several fields/properties to the member name 'X'
```

The mapping is ambiguous in both directions — only one of the two members can ever be read from key
``X``, and writing both is not representable — so there is nothing for ``DuplicateKeyMode`` to decide.
``[CborProperty("X")]`` twice is the visible way in. Three others reach the same place with nothing in
the source looking wrong:

* a **naming convention** that folds two member names into one — ``Id`` and ``ID`` under
  ``LowerCaseNamingConvention``;
* a **mapping API call** that renames a member onto a name another member already holds —
  ``SetMemberName("X")`` where ``X`` is already mapped, whether by an attribute, by the conventions or
  by an earlier call;
* a member that **hides a base member** of the same name with ``new`` rather than ``override``, which
  reports both declarations and so maps both, under the one name. Where the hiding member is a
  **field** this always happens, whatever the two types are: field lookup folds nothing, so ``int``
  over ``int`` is two members. Where it is a **property**, the pair is folded only when the two are
  identical *as declared* — which is narrower than it sounds, since a generic base declares
  ``T Value`` and a derived ``new int Value`` over a ``Base<int>`` differs from it as declared and so
  collides, despite reading as the same type at the source. ``override`` never collides, and neither
  does a hierarchy of interfaces, whose members are reported one interface at a time.

Give the two members distinct names, or drop one. Where the collision comes from a base type you do
not own, ``[CborIgnore]`` on the member you do own removes it from the mapping, and
``ClearMemberMappings()`` followed by explicit ``MapMember`` calls decides the whole mapping by hand —
see [object mapping](object-mapping.md).
