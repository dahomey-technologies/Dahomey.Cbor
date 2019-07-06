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


## Installation
### NuGet
https://www.nuget.org/packages/Dahomey.Cbor/

`Install-Package Dahomey.Cbor`

### Compilation from source
  1. `dotnet restore`
  2. `dotnet pack -c Release`
