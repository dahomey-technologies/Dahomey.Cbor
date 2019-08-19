# Dahomey.Cbor
High-performance CBOR serialization framework for .Net (C#)

![Nuget (with prereleases)](https://img.shields.io/nuget/vpre/Dahomey.Cbor)
![CircleCI](https://img.shields.io/circleci/build/github/dahomey-technologies/Dahomey.Cbor/master)

## Features
* Serialization/Deserialization from/to Streams, byte buffer
* Object Model
* Mapping to any .Net class
* Extensible Polymorphism support based on discriminator conventions
* Extensible Naming conventions
* Custom converters for not supported types
* [.Net standard 2.0](https://github.com/dotnet/standard/blob/master/docs/versions/netstandard2.0.md) support
* Asp.net core 2.1 and 2.2 CBOR formatters


## Installation
### NuGet
https://www.nuget.org/packages/Dahomey.Cbor/

`Install-Package Dahomey.Cbor`

https://www.nuget.org/packages/Dahomey.Cbor.AspNetCore/

`Install-Package Dahomey.Cbor.AspNetCore`

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
CborConverter.Register(typeof(CustomObject), new CustomObjectConverter());
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
  
## Asp.Net Core Support
You can enable Dahomey.Cbor as a CBOR formatter in ASP.NET Core 2.1 or 2.2 by using the Nuget package [Dahomey.Cbor.AspNetCore](https://www.nuget.org/packages/Dahomey.Cbor.AspNetCore/). To enable it, add the extension method ``AddDahomeyCbor()`` to the ``AddMvc()`` call in ``ConfigureServices``

```csharp
// This method gets called by the runtime. Use this method to add services to the container.
public void ConfigureServices(IServiceCollection services)
{
    services.AddMvc()
      .AddDahomeyCbor();
}
```
If an incoming HTTP request holds the following headers:
* ``Content-Type`` with the value ``application/cbor``: the Request body will be deserilized in CBOR.
* ``Accept`` with the value ``application/cbor``: the Response body will be serialized in CBOR.

If the headers are missing, the default JSON formatters will be used.
