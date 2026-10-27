# Polymorphism

[← back to the README](../README.md)

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

## Where the discriminator is written

The location depends on [`CborObjectFormat`](object-formats.md):

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

## Controlling when the discriminator is written

`CborDiscriminatorPolicy` (per type via the attribute's `Policy` property, or globally via
`CborOptions.DiscriminatorPolicy`):

* `Auto` (the effective default) — written only when the declared type differs from the actual type
* `Always` — always written, even when serializing the concrete type directly
* `Never` — never written

## Using a different member name

Both discriminator kinds default to the `"_t"` member name. Register the convention explicitly to
change it:

```csharp
DiscriminatorConventionRegistry registry = options.Registry.DiscriminatorConventionRegistry;
registry.ClearConventions();
registry.RegisterConvention(new DefaultDiscriminatorConvention<int>(options.Registry, "t"));
```

`ClearConventions` also forgets which subtypes were registered, so register each one again with
`RegisterType<T>()` afterwards. Reading the abstract base does not do it for you: the first read of a
document fails with `no type is registered for the discriminator …` until the subtype is registered.
Converters that already exist notice the change and use the new convention, so the call is safe after
(de)serialization has started. Call `RegisterConvention` straight after `ClearConventions`, though: a
type first used (serialized, deserialized or looked up) while no convention is registered is mapped
without a discriminator, and keeps writing none once a convention is registered again. Configure the
registry before the first use of the types it covers.

For a discriminator that is neither a plain string nor a plain int, implement `IDiscriminatorConvention`
and register it the same way.

> **Note:** the registry caches one convention per declared type, so a single hierarchy cannot mix
> string- and int-keyed discriminators. Independent hierarchies may each use their own kind within the
> same `CborOptions`.
