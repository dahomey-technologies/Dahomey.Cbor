# Dahomey.Cbor
High-performance [CBOR](https://cbor.io/) serialization framework for .Net (C#)

[![Nuget (with prereleases)](https://img.shields.io/nuget/vpre/Dahomey.Cbor)](https://www.nuget.org/packages/Dahomey.Cbor)
[![](https://github.com/dahomey-technologies/Dahomey.Cbor/actions/workflows/BuildAndTest.yml/badge.svg)](https://github.com/dahomey-technologies/Dahomey.Cbor/actions/workflows/BuildAndTest.yml)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

## Supported .NET versions
* .NET Standard 2.0
* .NET 8.0
* .NET 9.0
* .NET 10.0

## Features
* Serialization/Deserialization from/to Streams, byte buffer
* Object Model
* Mapping to any .Net class
* Extensible Polymorphism support based on discriminator conventions
* Extensible Naming conventions
* Custom converters for not supported types
* Can require properties or fields with different policies (CborRequiredAttribute)
* Conditional Property Serialization support based on the existence of a method ShouldSerialize\[PropertyName\]()
* Support for interfaces and abstract classes
* Support for non default constructors, factories and more advanced creator mappings
* Can ignore default values
* Object mapping to programmatically configure features on a class
* Support for serialization callbacks (before/after serialization/deserialization)
* Support for anonymous types
* Support for Nullables
* Support for collection interfaces: IList<>, ICollection<>, IEnumerable<>, IReadOnlyList<>, IReadOnlyCollection<>
* Support for dynamics
* Support for structs
* Support for RFC 8746 typed arrays, opt-in per direction (CborOptions.TypedArrayMode)
* Support for RFC 8949 §3.4.3 bignums (tags 2 and 3) as System.Numerics.BigInteger
* Duplicate map keys rejected on every decode target, with a last-wins opt-out (CborOptions.DuplicateKeyMode)
* Reads RFC 8949 indefinite-length (chunked) byte and text strings; writes definite-length only
* Ambiguous mappings — two members of a type under one CBOR name — refused when the mapping is built

## Installation
### NuGet
https://www.nuget.org/packages/Dahomey.Cbor/

`Install-Package Dahomey.Cbor`

### Compilation from source
  1. `dotnet restore`
  2. `dotnet pack -c Release`
  
## How to use Dahomey.Cbor
### Deserialization

Any C# class be deserialized from a CBOR buffer Stream:

```csharp
class CustomObject
{
  ...
}

CustomObject customObject = await Cbor.DeserializeAsync<CustomObject>(stream);
```

Another option consists in using Dahomey.Cbor object model to deserialize the buffer in a more generic ``CborObject`` object:

```csharp
CborObject cborObject = await Cbor.DeserializeAsync<CborObject>(stream);
```

#### Where a read failed

A read that fails throws a ``CborException`` naming both the offending byte offset and the position in
the model it was reached from:

```
[129] Expected major type TextString (3). Failed to deserialize from "$.Items[7].Name".
```

The same path is available on its own, in the notation ``System.Text.Json`` uses, for code that needs
to act on it rather than log it:

```csharp
try
{
    return Cbor.Deserialize<CustomObject>(buffer);
}
catch (CborException exception)
{
    logger.LogWarning("rejected at {Path}", exception.Path);   // $.Items[7].Name
    throw;
}
```

Every failure raised while deserializing has a path, down to ``$`` for a document that contradicts the
requested type outright — so ``Path`` being ``null`` means the exception did not come from a read at
all, such as a serialization failure or a mapping the registry refused to build.

The path grows as the exception travels back up the stack, so read it once the read has failed rather
than part of the way out of it: an intercepting ``catch`` that logs ``Message`` and rethrows sees only
the path as far as it is known at that point.

A path is as precise as the converters it passed through. A converter you register yourself is already
named by the object holding it, and one that delegates to other converters inherits whatever they
contribute — so most custom converters need do nothing. A converter that decodes a structure of its
own can name positions inside it by adding a segment on the way out:

```csharp
public override Payload Read(ref CborReader reader)
{
    try
    {
        return ReadBody(ref reader);
    }
    catch (CborException exception)
    {
        exception.PrependPathMember("Body");   // or PrependPathIndex(i) inside a sequence
        throw;                                 // rethrow the same exception, do not wrap it
    }
}
```

Each frame adds only its own segment, outermost last, and nothing is built until something has
actually failed.

Member names and map keys read the same way — ``$.Map.key`` — and are bracketed and escaped when they
would otherwise be ambiguous: a member genuinely named ``a.b`` is written ``$['a.b']``. Text taken from
the document is also truncated, so a message's length follows the shape of a document rather than the
size of the values in it.

### Serialization

Any C# class can be serialized to CBOR buffer Stream:

```csharp
CustomObject customObject = new CustomObject
{
  ...
};

await Cbor.SerializeAsync(customObject, stream);
```

As for deserialization a more generic solution consists in using ``CborObject`` object:

```csharp
CborObject obj = new CborObject
{
    ["string"] = "foo",
    ["number"] = 12.12,
    ["bool"] = true,
    ["null"] = null,
    ["array"] = new CborArray {1, 2},
    ["object"] = new CborObject { [ "id" ] = 1 },
};

await Cbor.SerializeAsync(cborObject, stream);

```

### Typed arrays (RFC 8746)

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

#### Reading and writing are separate

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

#### What reads what

Writing typed arrays applies to `T[]`. Reading one fills any of the shapes that are interchangeable with
`T[]` — `List<T>`, `IList<T>`, `ICollection<T>`, `IEnumerable<T>`, `IReadOnlyList<T>`, `HashSet<T>`,
`ImmutableArray<T>` and the rest — so a document written from a `float[]` member is readable by a
consumer that declares that member as a `List<float>`.

A tag whose element type does not match the target is an error rather than something to ignore: reading
tag 86 (binary64) into a `float[]` throws, which catches a mismatch that would otherwise silently
reinterpret the bytes.

#### Two things to know

`ArrayLengthMode` has no effect on an array actually written as a typed array — a typed array is a single
definite-length byte string, so there is no array header to make indefinite. It applies as usual to every
array not written as one.

The object model does not model typed arrays. A `CborValue` holding one is a `ByteString` with the tag in
`CborValue.SemanticTag`, so DOM code sees opaque bytes rather than numbers. The tag itself survives a
read-then-write, so the document still describes a typed array afterwards.

One tag, not a chain. `CborValue.SemanticTag` is a single `ulong?`, so a nested `C1 C2 01` keeps the outer
tag and drops the inner. That is a limit of the object model rather than of the writer.

### Indefinite-length strings (RFC 8949 §3.2.3)

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

### Bignums (RFC 8949 §3.4.3)

A member typed `System.Numerics.BigInteger` reads and writes integers of any width. Values that fit in 64
bits use a basic integer, which §3.4.3 makes the preferred serialization; anything larger uses tag 2
(unsigned) or tag 3 (negative) over a big-endian magnitude.

```csharp
public class Account
{
    public BigInteger Balance { get; set; }
}
```

```csharp
// 12                    -> 0C                      (basic integer)
// 18446744073709551616  -> C2 49 010000000000000000 (tag 2, 2^64)
// -18446744073709551617 -> C3 49 010000000000000000 (tag 3)
```

Because the tag appears only where it carries information, a `BigInteger` member is byte-identical to the
same value typed as an `int` or a `ulong` for every value those types can hold — swapping one for the other
does not change the documents a service already emits.

Reading accepts either form, so a `BigInteger` member reads a document whose producer always tags. It also
reads the whole of major type 1, which reaches -2^64: no `long` holds that, so `ReadInt64` rejects it as an
invalid signed integer where a `BigInteger` member decodes it.

A bignum under an outer tag (`C1 C2 …`) decodes, as does one under a whole stack of them: the reader reads
through the stack and the innermost bignum tag decides. A text string, on the other hand, is rejected
rather than parsed — there is no span overload of `BigInteger.Parse` on netstandard2.0, so accepting one
would mean picking an encoding on a path nothing asks for.

Tags 4 and 5 (decimal fraction and bigfloat) are not decoded semantically and still surface as a
two-element array. Tracked as https://github.com/dahomey-technologies/Dahomey.Cbor/issues/170.

### Custom converters

If you need to write a customer converter for a specific class, you can inherit a custom converter class for CborConverterBase<T>.
An example can be found here:
https://github.com/dahomey-technologies/Dahomey.Cbor/blob/master/src/Dahomey.Cbor.Tests/GuidConverter.cs

Then you can register you custom converter in 3 ways.

1. Either you decorate your class with the CborConverterAttribute:
```csharp
[CborConverter(typeof(CustomObjectConverter))]
class CustomObject
{
}
```

2. Or you can register your custom converter manually:
```csharp
CborOptions.Default.Registry.ConverterRegistry.RegisterConverter(typeof(CustomObject), new CustomObjectConverter());
```

3. The last option is to decorate a property or a field with the CborConverterAttribute in a class referencing your custom class:
```csharp
class CustomObject2
{
    [CborConverter(typeof(CustomObjectConverter))]
    public CustomObject CustomObject { get; set; }
}
```

The last two options are useful when you write a custom cbor converter for a class you can't decorate with the CborConverterAttribute because you don't own it like the above example with System.Guid.

CborConverters are use in the heart of the library for standard types and auto discovered custom classes by reflection.
It means you will benefit of the same features and performance.

### Polymorphism

To deserialize a base class or interface back into the concrete type that was written, the payload must
carry a *discriminator*. Decorate each concrete type with a discriminator attribute:

```csharp
public abstract class Shape
{
    public int Id { get; set; }
}

[CborDiscriminator("circle")]      // string discriminator
public class Circle : Shape
{
    public double Radius { get; set; }
}

[CborIntDiscriminator(2)]          // integer discriminator — more compact on the wire
public class Square : Shape
{
    public double Side { get; set; }
}
```

A type must not carry both attributes; doing so throws a `CborException`.

Writing resolves the discriminator from the runtime type automatically. **Reading** needs the concrete
types registered up front, so that a discriminator value can be mapped back to a type:

```csharp
CborOptions options = new CborOptions();
options.Registry.DiscriminatorConventionRegistry.RegisterType<Circle>();
options.Registry.DiscriminatorConventionRegistry.RegisterType<Square>();

Shape shape = Cbor.Deserialize<Shape>(buffer, options);   // yields a Circle or a Square
```

#### Where the discriminator is written

The location depends on `CborObjectFormat`:

| Object format | Discriminator location |
|---|---|
| `StringKeyMap` (default) | under the member name `"_t"` |
| `IntKeyMap` | under key `0` |
| `Array` | first item, wrapped in the `DiscriminatorSemanticTag` (default `39`) |

```csharp
// StringKeyMap: {"_t": 2, "Side": 3.0, "Id": 1}
// IntKeyMap:    {0: 2, 1: 1, 2: 3.0}
// Array:        [39(2), 1, 3.0]
```

#### Controlling when the discriminator is written

`CborDiscriminatorPolicy` (per type via the attribute's `Policy` property, or globally via
`CborOptions.DiscriminatorPolicy`):

* `Auto` (the effective default) — written only when the declared type differs from the actual type
* `Always` — always written, even when serializing the concrete type directly
* `Never` — never written

#### Using a different member name

Both discriminator kinds default to the `"_t"` member name. Register the convention explicitly to
change it:

```csharp
DiscriminatorConventionRegistry registry = options.Registry.DiscriminatorConventionRegistry;
registry.ClearConventions();
registry.RegisterConvention(new DefaultDiscriminatorConvention<int>(options.Registry, "t"));
```

For a discriminator that is neither a plain string nor a plain int, implement `IDiscriminatorConvention`
and register it the same way.

> **Note:** the registry caches one convention per declared type, so a single hierarchy cannot mix
> string- and int-keyed discriminators. Independent hierarchies may each use their own kind within the
> same `CborOptions`.

### CDDL schemas

Add `[CborCddlSchema]` to a source-generated context and it gains a `CddlSchema` constant holding an
[RFC 8610](https://www.rfc-editor.org/rfc/rfc8610) schema for every type it declares:

```csharp
[CborSerializable(typeof(CddlPerson))]
[CborCddlSchema]
public partial class CddlTestContext : CborSerializerContext { }

File.WriteAllText("person.cddl", CddlTestContext.CddlSchema);
```

For

```csharp
public class CddlPerson
{
    public string Name { get; set; }
    public int Age { get; set; }
    public byte Rating { get; set; }
    public bool Active { get; set; }
    public double Score { get; set; }
}
```

`CddlSchema` is:

```cddl
; Generated by Dahomey.Cbor. Do not edit.
; Describes what the serializer WRITES, closed over the declared members, exact except
; where a converter's own output is not: `any` for object, the open uint/int form for a
; [Flags] enum, any length for [* X] and {* K => V}, and a member declared as a polymorphic
; base admitting every subtype the context declares. One case is narrower than the writer:
; a uint-backed [Flags] value above int.MaxValue is written as a negative integer, which
; `uint` rejects.
; Member types follow their nullable annotations. A member declared non-nullable but left
; null at run time is written as F6 and will NOT validate against this schema.

CddlPerson = {
  "Name": tstr,
  "Age": -2147483648..2147483647,
  "Rating": 0..255,
  "Active": bool,
  "Score": float,
}
```

A few things to know about what gets emitted:

* **It describes what the serializer writes, not what the reader accepts.** `UnhandledNameMode`
  defaults to `Silent`, so unknown keys are tolerated on read but never emitted — the schema is closed
  over the declared members, matching the writer, not a looser description of everything the reader
  tolerates. It is exact except where a converter's own output is not, and the header lists those
  cases: `object` is `any`, a `[Flags]` enum is the open `uint`/`int` form, `[* X]` and `{* K => V}`
  admit any length, and a member declared as a polymorphic base admits every subtype the context
  declares.
* **Member types follow their nullable annotations.** A member whose type is annotated nullable (or
  left in an unannotated context) renders as `X / nil`; a member annotated non-nullable renders as the
  bare rule `X`. The same rule reaches collection elements and dictionary values, which follow their
  own annotation rather than the member's. Dictionary *keys* never render as nilable: RFC 8610's
  `memberkey` production admits only a `type1`, so a `/` choice there is a parse error, and
  `Dictionary<TKey,TValue>` throws on a null key anyway. `byte`, `short`, `int` and their unsigned
  counterparts get their exact range rather than the prelude's unbounded `int`/`uint`; `long` and
  `ulong` get `int` and `uint`, which is already exact for them.
* **Collection, array and dictionary roots get a rule of their own.**
  `[CborSerializable(typeof(List<Person>))]` emits `ListOfPerson = [* Person]` alongside `Person`, so
  the schema describes the document actually written and not only its element type.
* **Polymorphic types get a `-poly` rule, and concrete ones a rule pair.** `X-poly` is the type
  choice `X-poly = A-poly / B-poly / ...` over the discriminated subtypes, and a member typed as the
  polymorphic base references `X-poly`, not `X`. A *concrete* base also gets the bare rule `X`,
  describing what is written when the static type at the call site is exactly `X` — the discriminator
  is suppressed there — and that bare rule joins its own choice as an arm, alongside an anonymous arm
  for the same type reached through a base of its own and therefore carrying a discriminator. An
  abstract class or an interface cannot be written as itself and correctly gets no bare rule. An
  abstract type or interface with no discriminated subtype reachable from the context is a build error
  (`CBOR1012`) — a type choice with nothing to distinguish its arms describes a document nothing can
  actually tell apart.
* **Settings that change the wire format must be declared on the context**, since the generator runs at
  compile time and cannot see run-time `CborOptions`:

  ```csharp
  [CborSourceGenerationOptions(
      EnumFormat = ValueFormat.WriteToString,
      DateTimeFormat = DateTimeFormat.Unix,
      TypedArrayMode = TypedArrayMode.ReadWriteLittleEndian)]
  ```

* **A type with no CDDL representation is a build error (`CBOR1011`)**, not a silent omission — a
  schema that quietly drops a member is worse than no schema at all.

The output uses core RFC 8610 grammar only — prelude types, arrays, maps, `*`, `/`, ranges and
`#6.n(...)` — so it can be checked with any conformant CDDL tool; this repository's own tests validate
it against the reference [`cddl`](https://rubygems.org/gems/cddl) Ruby gem, both by parsing every
emitted schema and by checking real serializer output against it.

### Deterministic encoding (RFC 8949 §4.2)

```csharp
CborOptions options = new CborOptions { Deterministic = true };
await Cbor.SerializeAsync(customObject, stream, options);
```

Guarantees the four core requirements of RFC 8949 §4.2.1: shortest-form arguments for integers,
lengths and tags; preferred float serialization; definite lengths; and map keys sorted bytewise on
their encoded form. The same value always produces the same bytes, which is what makes hashing and
deduplication meaningful. This is the §4.2.1 ordering rule, not the deprecated length-first variant
from §4.2.3, which is not implemented.

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

#### The discriminator is no longer the first key

Without ``Deterministic`` the discriminator is written first, at index 0. Sorted bytewise it takes
whatever position its encoded key earns like any other: ``"_t"`` encodes to ``0x62 0x5F 0x74``, which
places it after every one-character member name and after every two-character name starting below
``0x5F`` — on a PascalCase model, somewhere in the middle of the map.

This is what §4.2.1 requires; it grants a discriminator no exemption. Two consequences are worth
knowing:

* **Reads of polymorphic types get slower.** Deserialization bookmarks the reader, scans the map for
  the discriminator, then rewinds and re-reads. Previously it hit on the first item; now it may scan
  the whole map first. Correctness is unaffected — Dahomey.Cbor does not care where the discriminator
  sits — but the cost is real.
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


### Duplicate map keys (RFC 8949 §5.6)

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

> **Behaviour change.** A mapped class **whose members are assigned** — anything without a creator
> mapping, which is most classes — used to take the last occurrence of a repeated member silently, and
> now throws. Dictionaries, the object model, and classes with a non-default constructor already
> rejected, so this is the row that changes. If your producers emit duplicates, a deserialize that
> worked will now throw with nothing in your own code having changed; set
> ``DuplicateKeyMode.LastWins`` to keep the old behaviour.
>
> What made this worth breaking is that the old behaviour was not a choice anyone made: values for a
> type with a non-default constructor are collected in a dictionary until the constructor can be
> called, so they hit ``Add`` and were refused, while a type with a default constructor has its
> members assigned, so a repeat overwrote. The same class changed behaviour when someone added or
> removed a constructor.

A repeated key that matches no member is not a duplicate member — what happens to an unknown name is
``UnhandledNameMode``'s question, and repeating one does not change the answer. Neither is a null map
key, which is refused in both modes: there is no earlier occurrence for a later one to win over.

The **discriminator** is refused when repeated, even though it is a key of the map rather than a
member of the type: two readers disagreeing about which occurrence names the type is how one document
comes to mean two things.

One case is settled earlier than the decode, because no answer at decode time is right: **a type
mapping two members to the same CBOR name** used to write a document with a repeated key that it then
could not read back. The mapping itself is now refused, the first time the type is serialized or
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
``ClearMemberMappings()`` followed by explicit ``MapMember`` calls decides the whole mapping by hand.

> **Behaviour change.** Such a type used to serialize, so this is a new exception at first use for a
> type that "worked". What it wrote was a document whose second member was unreadable — silently
> discarded before #169, refused as a duplicate key after it — so the type never round-tripped; the
> exception names the mapping instead of leaving it to be found in the bytes. A document already
> written by one of these mappings still reads with ``DuplicateKeyMode.LastWins``, against a type
> whose mapping no longer collides.
>
> The utility type behind the member lookup, ``Dahomey.Cbor.Util.ByteBufferDictionary<T>``, is public,
> and its ``Add`` changed with it: a key already present is now refused with an ``ArgumentException``
> rather than silently replacing the entry, as ``Dictionary<TKey, TValue>.Add`` does. The type has
> neither an indexer nor a removal, so code of your own that relied on ``Add`` overwriting has to keep
> the keys it has added and build a fresh dictionary instead.

#### Adjusting a single member

Taking the whole mapping over is not needed to change one member: ``MapMember`` over a member the
mapping already covers returns that member's mapping rather than adding a second one, so ``AutoMap``
then ``MapMember`` reads as it looks — take the conventions, then change this one member.

```csharp
options.Registry.ObjectMappingRegistry.Register<Foo>(om =>
{
    om.AutoMap();
    om.MapMember(o => o.A).SetMemberName("a").SetRequired(RequirementPolicy.Always);
});
```

The member is identified by its ``MemberInfo`` — the declaration, so a member inherited from a base
type is recognized as the one the conventions already mapped — and the lambda and reflection overloads
reach the same mapping. This is what ``MongoDB.Bson``'s ``BsonClassMap.MapMember``, which this API
takes its shape from, does with the same input.

``AutoMap`` reaches its own members the same way, so the order does not matter: called after a member
was mapped by hand, it configures that mapping from the attributes rather than adding a second one,
and the two settings live together — the caller's name, the attribute's requirement policy. Two
``AutoMap`` calls likewise leave one mapping per member. Where the two do say something about the same
setting, the attribute wins, since that is the call being made.

> **Behaviour change.** ``MapMember`` used to append unconditionally, so the call above mapped ``A``
> twice: under ``A`` from the conventions and under ``a`` from the call, writing the member under both
> keys — a document that reads back without complaint, carrying a member it should not. Without the
> rename the two mappings shared a name, and the type is refused by the duplicate-name check above.
> Code that relied on the append to write one member under two keys has to declare a second member to
> carry the second key.
>
> ``AutoMap`` now goes through ``MapMember`` for each member it maps, which is what makes the other
> order behave too. A convention of your own that builds ``MemberMapping<T>`` itself and passes them to
> ``AddMemberMappings`` still appends, unchanged: that call adds what it is given.
