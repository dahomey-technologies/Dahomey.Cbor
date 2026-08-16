# Deterministic encoding (RFC 8949 §4.2)

[← back to the README](../README.md)

```csharp
CborOptions options = new CborOptions { Deterministic = true };
await Cbor.SerializeAsync(customObject, stream, options);
```

Guarantees the four core requirements of RFC 8949 §4.2.1: shortest-form arguments for integers,
lengths and tags; preferred float serialization; definite lengths; and map keys sorted bytewise on
their encoded form. The same value always produces the same bytes, which is what makes hashing and
deduplication meaningful. This is the §4.2.1 ordering rule, not the deprecated length-first variant
from §4.2.3, which is not implemented.

## What gets ordered

Key ordering applies to ``StringKeyMap`` and ``IntKeyMap`` objects, ``Dictionary<K,V>``, and
``CborObject``/``CborValue`` maps. ``CborObjectFormat.Array`` writes its members positionally and has
no map keys at all, so its bytes are identical with and without ``Deterministic``.

Because ordering is on the *encoded* key, a shorter key always sorts before a longer one, so ``"z"``
sorts before ``"aa"``. Keys of different CBOR major types order by major type first — unsigned
integer, then negative integer, then byte string, then text string — which is why negative integer
keys sort after all non-negative ones.

Any key type whose converter can write it can be ordered, because each key is ordered on the bytes
that converter produces — strings, chars, byte strings, every integral type, enums, floating point,
booleans and ``DateTime`` alike. A custom key converter needs nothing added here to participate. The
same holds for ``CborObject``, whose keys may be of any kind and may mix kinds freely within one map,
including nulls, arrays and nested maps; that is what lets a document read off the wire be re-encoded
deterministically whatever it contains.

**An enum key whose underlying type is wider than 32 bits is written truncated to 32 bits**, and is
therefore ordered that way too. Two enum values that differ only above bit 31 collide into the same
map key, producing a map with duplicate keys that is neither deterministic nor valid to hash. This is
how ``EnumConverter`` writes enums with or without ``Deterministic``; if your enum is backed by
``long``/``ulong`` and its values exceed 32 bits, key the map by the underlying integer instead.

## The discriminator is not the first key

Sorted bytewise, the discriminator takes whatever position its encoded key earns like any other:
``"_t"`` encodes to ``0x62 0x5F 0x74``, which places it after every one-character member name and after
every two-character name starting below ``0x5F`` — on a PascalCase model, somewhere in the middle of
the map.

This is what §4.2.1 requires; it grants a discriminator no exemption. Two consequences are worth
knowing:

* **Reads of polymorphic types get slower.** Deserialization bookmarks the reader, scans the map for
  the discriminator, then rewinds and re-reads, so it may scan the whole map first. Correctness is
  unaffected — Dahomey.Cbor does not care where the discriminator sits — but the cost is real.
* **Strict readers elsewhere may reject the document.** Several polymorphic deserializers in other
  ecosystems require the type tag in first position. A document this library round-trips happily may
  not be readable by one of those once sorted.

Both are avoidable without giving up compliance, and both are free:

* ``CborObjectFormat.IntKeyMap`` — the discriminator's index is 0, encoding to the single byte
  ``0x00``. That is the smallest encoding in the lowest major type, so nothing can sort before it.
  This is the recommendation.
* ``StringKeyMap`` with a one-character discriminator name, which
  ``DefaultDiscriminatorConvention`` takes as a constructor argument. A one-character name encodes in
  two bytes and so beats every name of two characters or more.

## When the setting is read

``Deterministic`` may be set at any point in an options object's life, including on the long-lived
``CborOptions.Default`` — but not while a write using those options is in flight. It is read when a
write starts, not when converters are built, so it takes effect on the very next write; a change made
*during* a write (from a property getter, a custom converter, or another thread) is picked up by the
write after it, and the write in progress finishes on the ordering it started with.

Deterministic mode also refuses any setting that would admit more than one encoding of the same
value. An indefinite-length map or array throws a ``CborException`` at the point the header would be
written, whichever of the three sources asked for it: ``CborOptions.ArrayLengthMode``,
``CborOptions.MapLengthMode``, or a ``CborLengthMode`` attribute on a type or member — the last of
which takes priority over both options, and is therefore the case most likely to surprise. The
refusal is at the writer rather than at the property setters so that no combination can slip past by
being configured in the wrong order.

**When verifying an integrity hash, hash the bytes you received, never a re-serialization.** If
``UnhandledNameMode.Silent`` is in effect, decoding a document written by a newer version silently
drops members this version doesn't know about, and re-encoding then produces different — and
differently hashing — bytes than what was received. Hash the wire bytes directly.
