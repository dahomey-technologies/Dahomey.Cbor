# Dahomey.Cbor
High-performance [CBOR](https://cbor.io/) serialization framework for .Net (C#)

[![Nuget (with prereleases)](https://img.shields.io/nuget/vpre/Dahomey.Cbor)](https://www.nuget.org/packages/Dahomey.Cbor)
[![](https://github.com/dahomey-technologies/Dahomey.Cbor/actions/workflows/BuildAndTest.yml/badge.svg)](https://github.com/dahomey-technologies/Dahomey.Cbor/actions/workflows/BuildAndTest.yml)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

* [Installation](#installation)
* [Serialization and deserialization](#serialization-and-deserialization)
* [Custom converters](#custom-converters)
* [Polymorphism](#polymorphism)
* [Documentation](#documentation)

## Supported .NET versions
* .NET Standard 2.0
* .NET 8.0
* .NET 9.0
* .NET 10.0

## Features
* Serialization/deserialization from/to streams and byte buffers
* Object model (`CborObject`, `CborArray`, `CborValue`)
* Mapping to any .Net class, struct, anonymous type, nullable or dynamic
* Support for interfaces and abstract classes, with extensible polymorphism based on discriminators
* Support for non-default constructors, factories and more advanced creator mappings
* Extensible naming conventions, custom converters, and programmatic object mapping
* Required members, conditional serialization (`ShouldSerialize[PropertyName]()`), ignored default values
* Serialization callbacks (before/after serialization/deserialization)
* Support for collection interfaces: `IList<>`, `ICollection<>`, `IEnumerable<>`, `IReadOnlyList<>`, `IReadOnlyCollection<>`
* Tuples of any arity, as one flat array, on the reflection and source-generated paths
* [RFC 8746](docs/typed-arrays.md) typed arrays, opt-in per direction
* [Arbitrary-precision numbers](docs/numbers.md): bignums (tags 2 and 3), decimal fractions (tag 4) and bigfloats (tag 5)
* [RFC 8949 §4.2 deterministic encoding](docs/deterministic-encoding.md)
* [Duplicate map keys](docs/duplicate-keys.md) rejected on every decode target, with a last-wins opt-out
* [RFC 8610 CDDL schema generation](docs/cddl.md) from a source-generated context
* Native AOT and trimming support through source generation
* Failures name [the byte offset and the path](docs/error-paths.md) they were reached from

## Installation
### NuGet
https://www.nuget.org/packages/Dahomey.Cbor/

`Install-Package Dahomey.Cbor`

### Compilation from source
  1. `dotnet restore`
  2. `dotnet pack -c Release`

## Serialization and deserialization

Any C# class can be serialized to and deserialized from a CBOR stream or buffer:

```csharp
class CustomObject
{
  ...
}

await Cbor.SerializeAsync(customObject, stream);
CustomObject customObject = await Cbor.DeserializeAsync<CustomObject>(stream);
```

Another option consists in using the Dahomey.Cbor object model, which reads any document without a
type to map it onto:

```csharp
CborObject obj = new CborObject
{
    ["string"] = "foo",
    ["number"] = 12.12,
    ["bool"] = true,
    ["null"] = null,
    ["array"] = new CborArray { 1, 2 },
    ["object"] = new CborObject { ["id"] = 1 },
};

await Cbor.SerializeAsync(obj, stream);
CborObject cborObject = await Cbor.DeserializeAsync<CborObject>(stream);
```

Behaviour is configured through a `CborOptions` passed to either call:

```csharp
CborOptions options = new CborOptions { Deterministic = true };
await Cbor.SerializeAsync(customObject, stream, options);
```

A read that fails throws a `CborException` naming both the offending byte offset and the position in
the model it was reached from — see [error paths](docs/error-paths.md):

```
[129] Expected major type TextString (3). Failed to deserialize from "$.Items[7].Name".
```

## Custom converters

To take over the encoding of a specific type, inherit `CborConverterBase<T>`. An example is
[`GuidConverter`](src/Dahomey.Cbor.Tests/GuidConverter.cs) in this repository.

A converter can then be registered in three ways.

1. Decorate the class with `CborConverterAttribute`:
```csharp
[CborConverter(typeof(CustomObjectConverter))]
class CustomObject
{
}
```

2. Register the converter manually:
```csharp
CborOptions.Default.Registry.ConverterRegistry.RegisterConverter(typeof(CustomObject), new CustomObjectConverter());
```

3. Decorate a property or a field with `CborConverterAttribute` in a class referencing your custom type:
```csharp
class CustomObject2
{
    [CborConverter(typeof(CustomObjectConverter))]
    public CustomObject CustomObject { get; set; }
}
```

The last two options are for a type you cannot decorate because you do not own it — `System.Guid` in
the example above.

Converters are what the library uses internally for standard types and for classes discovered by
reflection, so a converter of your own gets the same features and the same performance.

## Polymorphism

To deserialize a base class or interface back into the concrete type that was written, the payload
must carry a *discriminator*:

```csharp
[CborDiscriminator("circle")]      // or [CborIntDiscriminator(2)], more compact on the wire
public class Circle : Shape
{
    public double Radius { get; set; }
}
```

Writing resolves the discriminator from the runtime type automatically; reading needs the concrete
types registered up front:

```csharp
options.Registry.DiscriminatorConventionRegistry.RegisterType<Circle>();
Shape shape = Cbor.Deserialize<Shape>(buffer, options);   // yields a Circle
```

Where the discriminator is written, when it is written, and how to change its name are covered in
[the polymorphism guide](docs/polymorphism.md).

## Documentation

| Guide | What it covers |
|---|---|
| [Error paths](docs/error-paths.md) | `CborException.Path`, and contributing segments from a custom converter |
| [Object formats](docs/object-formats.md) | `StringKeyMap`, `IntKeyMap` and `Array`, and what `[CborProperty(n)]` means in each |
| [Object mapping](docs/object-mapping.md) | `ObjectMappingRegistry`, `AutoMap`, adjusting a single member |
| [Polymorphism](docs/polymorphism.md) | Discriminators, policies and conventions |
| [Typed arrays](docs/typed-arrays.md) | RFC 8746, `TypedArrayMode`, and what reads what |
| [Numbers](docs/numbers.md) | `BigInteger`, `decimal`, `CborDecimalFraction` and `CborBigFloat` |
| [Dates and times](docs/dates-and-times.md) | `DateTime`, `DateOnly`, `TimeOnly` and `DateTimeFormat` |
| [Strings](docs/strings.md) | Indefinite-length strings, and why tag 25 is not supported |
| [Tuples](docs/tuples.md) | `ValueTuple` at any arity, and the Native AOT requirement |
| [Deterministic encoding](docs/deterministic-encoding.md) | RFC 8949 §4.2, key ordering, and hashing |
| [Duplicate map keys](docs/duplicate-keys.md) | RFC 8949 §5.6, `DuplicateKeyMode`, and ambiguous mappings |
| [CDDL schemas](docs/cddl.md) | RFC 8610 schema generation from a source-generated context |

Behaviour changes between versions are in the
[GitHub releases](https://github.com/dahomey-technologies/Dahomey.Cbor/releases).

## License

[MIT](LICENSE)
