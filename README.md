# Dahomey.Cbor
High-performance [CBOR](https://cbor.io/) serialization framework for .Net (C#)

[![Nuget (with prereleases)](https://img.shields.io/nuget/vpre/Dahomey.Cbor)](https://www.nuget.org/packages/Dahomey.Cbor)
[![](https://github.com/dahomey-technologies/Dahomey.Cbor/actions/workflows/BuildAndTest.yml/badge.svg)](https://github.com/dahomey-technologies/Dahomey.Cbor/actions/workflows/BuildAndTest.yml)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

## Supported .NET versions
* .NET Standard 2.0
* .NET 6.0
* .NET 7.0
* .NET 8.0
* .NET 9.0

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

