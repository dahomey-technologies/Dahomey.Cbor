# Where a read failed

[← back to the README](../README.md)

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

## Custom converters

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
