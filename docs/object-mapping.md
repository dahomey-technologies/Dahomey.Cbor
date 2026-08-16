# Object mapping

[← back to the README](../README.md)

A type's mapping — which members are serialized, under which names, with which requirement policy —
comes from the conventions and the attributes by default. ``ObjectMappingRegistry`` configures it
programmatically instead, for types you do not own or settings no attribute expresses.

```csharp
options.Registry.ObjectMappingRegistry.Register<Foo>(om =>
{
    om.AutoMap();
    om.MapMember(o => o.A).SetMemberName("a").SetRequired(RequirementPolicy.Always);
});
```

## Adjusting a single member

Taking the whole mapping over is not needed to change one member: ``MapMember`` over a member the
mapping already covers returns that member's mapping rather than adding a second one, so ``AutoMap``
then ``MapMember`` reads as it looks — take the conventions, then change this one member.

The member is identified by its ``MemberInfo`` — the declaration, so a member inherited from a base
type is recognized as the one the conventions already mapped — and the lambda and reflection overloads
reach the same mapping. This is what ``MongoDB.Bson``'s ``BsonClassMap.MapMember``, which this API
takes its shape from, does with the same input.

``AutoMap`` reaches its own members the same way, so the order does not matter: called after a member
was mapped by hand, it configures that mapping from the attributes rather than adding a second one,
and the two settings live together — the caller's name, the attribute's requirement policy. Two
``AutoMap`` calls likewise leave one mapping per member. Where the two do say something about the same
setting, the attribute wins, since that is the call being made.

A convention of your own that builds ``MemberMapping<T>`` itself and passes them to
``AddMemberMappings`` appends what it is given, unchanged.

## Two members under one name

A mapping that puts two members under the same CBOR name is refused — see
[duplicate map keys](duplicate-keys.md#ambiguous-mappings-are-refused-before-the-decode).
``ClearMemberMappings()`` followed by explicit ``MapMember`` calls decides the whole mapping by hand
where the collision comes from a base type you do not own.
