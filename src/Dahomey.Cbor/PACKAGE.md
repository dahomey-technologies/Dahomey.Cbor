# Dahomey.Cbor

High-performance [CBOR](https://cbor.io/) serialization framework for .NET: .NET Standard 2.0, .NET 8.0,
.NET 9.0 and .NET 10.0.

- Mapping to any .NET class, struct, record or anonymous type, with polymorphism through discriminators
- An object model (`CborObject`, `CborArray`, `CborValue`) for documents without a type to map them onto
- Native AOT and trimming support through a source generator shipped in this package
- RFC 8949 deterministic encoding, RFC 8746 typed arrays, RFC 8610 CDDL schema generation
- Failures name the byte offset and the path in the model they were reached from

## Usage

```csharp
await Cbor.SerializeAsync(customObject, stream);
CustomObject customObject = await Cbor.DeserializeAsync<CustomObject>(stream);
```

Behaviour is configured through a `CborOptions` passed to either call:

```csharp
CborOptions options = new CborOptions { Deterministic = true };
await Cbor.SerializeAsync(customObject, stream, options);
```

## Documentation

The [README](https://github.com/dahomey-technologies/Dahomey.Cbor#readme) and the
[guides](https://github.com/dahomey-technologies/Dahomey.Cbor/tree/master/docs) cover custom
converters, polymorphism, object formats, numbers, dates and times, and source generation. Behaviour
changes between versions are listed in the
[releases](https://github.com/dahomey-technologies/Dahomey.Cbor/releases).

## License

[MIT](https://github.com/dahomey-technologies/Dahomey.Cbor/blob/master/LICENSE)
