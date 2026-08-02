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

Supported dictionary key types are ``string``, ``char``, ``byte[]``/``ReadOnlyMemory<byte>``, every
integral type (``byte``, ``sbyte``, ``short``, ``ushort``, ``int``, ``uint``, ``long``, ``ulong``)
and enums — each ordered as its own converter writes it. For an enum that means: with
``EnumFormat.WriteToString`` a value that *has* a name is written and ordered as that name, and a
value that has none (a combination of flags, or a cast from an unlisted number) falls back to the
integer form in both. ``CborObject`` keys may be text strings, byte strings and integers, mixed
freely within one map, which is what lets a document read off the wire be re-encoded
deterministically. Any other key type throws a ``CborException`` rather than silently emitting
unsorted output.

**An enum key whose underlying type is wider than 32 bits is written truncated to 32 bits**, and is
therefore ordered that way too — the ordering follows the bytes actually emitted. Two enum values
that differ only above bit 31 collide into the same map key, producing a map with duplicate keys that
is neither deterministic nor valid to hash. This is how enums are written with or without
``Deterministic``; if your enum is backed by ``long``/``ulong`` and its values exceed 32 bits, key
the map by the underlying integer instead.

``Deterministic`` may be set at any point in an options object's life, including on the long-lived
``CborOptions.Default`` — but not while a write using those options is in flight. It is read when a
write starts, not when converters are built, so it takes effect on the very next write; a change made
*during* a write (from a property getter, a custom converter, or another thread) is picked up by the
write after it, and the write in progress finishes on the ordering it started with.

Deterministic mode also rejects any setting that would admit more than one encoding of the same
value: setting ``ArrayLengthMode`` or ``MapLengthMode`` to ``LengthMode.IndefiniteLength`` while
``Deterministic`` is enabled throws a ``CborException``.

**When verifying an integrity hash, hash the bytes you received, never a re-serialization.** If
``UnhandledNameMode.Silent`` is in effect, decoding a document written by a newer version silently
drops members this version doesn't know about, and re-encoding then produces different — and
differently hashing — bytes than what was received. Hash the wire bytes directly.

