# Dahomey.Cbor
High-performance CBOR serialization framework for .Net (C#)

## Features
* Serialization/Deserialization from/to Streams, byte buffer
* Object Model
* Mapping to any .Net class
* Extensible Polymorphism support based on discriminator conventions
* Extensible Naming conventions
* Custom converters for not supported types
* [.Net standard 2.0](https://github.com/dotnet/standard/blob/master/docs/versions/netstandard2.0.md) support
* Asp.net core 2.2 CBOR formatters


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
## Asp.Net Core 2.2 Support
You can enable Dahomey.Cbor as a CBOR formatter in ASP.NET Core 2.2 by using the Nuget package [Dahomey.Cbor.AspNetCore](https://www.nuget.org/packages/Dahomey.Cbor.AspNetCore/). To enable it, add the extension method ``AddDahomeyCbor()`` to the ``AddMvc()`` call in ``ConfigureServices``

```csharp
// This method gets called by the runtime. Use this method to add services to the container.
public void ConfigureServices(IServiceCollection services)
{
    services.AddMvc().AddDahomeyCbor().SetCompatibilityVersion(CompatibilityVersion.Version_2_2);
}
```
If an incoming HTTP request holds the following headers:
* ``Content-Type`` with the value ``application/cbor``: the Request body will be deserilized in CBOR.
* ``Accept`` with the value ``application/cbor``: the Response body will be serialized in CBOR.

If the headers are missing, the default JSON formatters will be used.
