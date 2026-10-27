# Arbitrary-precision numbers

[← back to the README](../README.md)

Three encodings beyond the basic integers and floats — bignums (tags 2 and 3), decimal fractions
(tag 4) and bigfloats (tag 5) — and one at the other end of the range, half-precision floats.

## Bignums (RFC 8949 §3.4.3)

A member typed `System.Numerics.BigInteger` reads and writes integers of any width. Values that fit in 64
bits use a basic integer, which §3.4.3 makes the preferred serialization; anything larger uses tag 2
(unsigned) or tag 3 (negative) over a big-endian magnitude.

```csharp
public class Account
{
    public BigInteger Balance { get; set; }
}
```

```csharp
// 12                    -> 0C                      (basic integer)
// 18446744073709551616  -> C2 49 010000000000000000 (tag 2, 2^64)
// -18446744073709551617 -> C3 49 010000000000000000 (tag 3)
```

Because the tag appears only where it carries information, a `BigInteger` member is byte-identical to the
same value typed as an `int` or a `ulong` for every value those types can hold — swapping one for the other
does not change the documents a service already emits.

Reading accepts either form, so a `BigInteger` member reads a document whose producer always tags. It also
reads the whole of major type 1, which reaches -2^64: no `long` holds that, so `ReadInt64` rejects it as an
invalid signed integer where a `BigInteger` member decodes it.

A bignum under an outer tag (`C1 C2 …`) decodes, as does one under a whole stack of them: the reader reads
through the stack and the innermost bignum tag decides. A text string, on the other hand, is rejected
rather than parsed — there is no span overload of `BigInteger.Parse` on netstandard2.0, so accepting one
would mean picking an encoding on a path nothing asks for.

Tags 4 and 5 reach a member typed as one of them, which the two sections below are about: tag 4 also
reads into a `decimal`, and both read into a type of their own for values no `decimal` holds. Neither
has an object-model type, so a DOM read still gives a tagged `CborArray`.

## Decimals (RFC 8949 §3.4.4)

A `decimal` has two encodings, and which one is written is a setting:

```csharp
CborOptions options = new CborOptions { DecimalFormat = DecimalFormat.DecimalFraction };
await Cbor.SerializeAsync(273.15m, stream, options);
// writes C4 82 21 19 6AB3 -- tag 4, [-2, 27315]
```

| Value | What is written |
|---|---|
| `DecimalFloat` (default) | `FC` plus the sixteen raw bytes of the value: `FC 0000000000006AB3 0000000000020000`. |
| `DecimalFraction` | Tag 4 over `[exponent, mantissa]`, the RFC 8949 §3.4.4 form. |

**Prefer `DecimalFraction` for anything read outside this library.** RFC 8949 §3.3 lists additional
information 28–30 in major type 7 as *reserved*, so the default form occupies a slot the format has not
assigned: it round-trips here and no other decoder reads it — `System.Formats.Cbor` throws on it in
both its lax and strict modes. It remains the default because changing it would move the bytes of every
document with a `decimal` in it.

The conversion carries every value the type holds, with no range or precision policy to pick: a
`decimal` is a sign, a 96-bit mantissa and a scale of 0 to 28, which is exactly the decimal fraction
`[-scale, mantissa]`. The scale is part of what is written, so `0.00m` and `0m` stay distinguishable —
`C4 82 21 00` against `C4 82 00 00` — as they are in the default form. A mantissa past 2^64 goes out
under the bignum tag (`decimal.MaxValue` writes as `C4 82 00 C2 4C FFFFFFFFFFFFFFFFFFFFFFFF`), which
is the preferred serialization rather than a special case.

The one thing tag 4 does not carry is a signed zero, which it has no room for: `-0.00m` reads back as
`0.00m`, equal by every comparison the language offers and distinguishable only by `decimal.GetBits` or
by rendering it. The default form stores the sign bit as it stands and keeps it.

### Reading takes both, always

There is nothing to opt into on the read side: a `decimal` member reads either form whatever
`DecimalFormat` says, so turning the setting on does not stop a service reading the documents it wrote
before, and leaving it off still lets it read a peer's tag 4.

Reading is deliberately the more generous of the two: an unnormalised mantissa is accepted rather than
refused, so `[-30, 100]` reads as `1E-28`. What a `decimal` genuinely cannot hold — a mantissa wider
than 96 bits, or a scale that cannot be reduced to 28 — is a `CborException` rather than a rounded
value.

Two things do not follow the setting. `CborWriter.WriteDecimal(value)` writes the default form, since a
`CborWriter` holds no options; pass the format explicitly
(`WriteDecimal(value, DecimalFormat.DecimalFraction)`) from a custom converter. And the object model
has no decimal fraction of its own: a `CborDecimal` *writes* as tag 4 like any other decimal, but
reading those bytes back gives a `CborArray` tagged 4 rather than a `CborDecimal`. What is lost is the
node's type and nothing else — such a value writes back byte-identically, tag included, so DOM code
still carries a peer's decimals faithfully. It also carries the ones no `decimal` can hold, which is
why narrowing tag 4 onto the type here would cost more than it gives. A value that needs more than a
`decimal` has a type of its own — the next section.

## Decimal fractions and bigfloats (RFC 8949 §3.4.4)

A member typed `CborDecimalFraction` reads and writes tag 4, and one typed `CborBigFloat` reads and writes
tag 5. Both are `readonly struct`s holding a `BigInteger` mantissa and an `int` exponent — the value is
`Mantissa × 10^Exponent` and `Mantissa × 2^Exponent` respectively.

```csharp
public class Reading
{
    public CborDecimalFraction Value { get; set; }   // tag 4
    public CborBigFloat Scale { get; set; }          // tag 5
}
```

```csharp
// new CborDecimalFraction(27315, -2)  -> C4 82 21 19 6AB3  (273.15, the §3.4.4 example)
// new CborBigFloat(3, -1)             -> C5 82 20 03       (1.5, likewise)
// mantissa past a basic integer       -> C4 82 20 C2 49 010000000000000000
```

**These types and `decimal` divide the work rather than competing.** A `decimal` covers everything a
`decimal` holds, in either encoding, and that is what most documents want — declare `decimal` and set
`DecimalFormat`. These cover what it cannot: a mantissa wider than 96 bits, a scale past 28, and tag 5,
for which `decimal` has no encoding at all. Holding the whole of what each tag can express is what makes
read and write symmetric here and leaves no range policy to pick.

The two overlap deliberately and harmlessly. Under `DecimalFormat.DecimalFraction` a `decimal` member and
a `CborDecimalFraction` member write **the same tag 4 bytes** for the same value, and each reads what the
other wrote as far as its own type reaches. Which converter runs is settled by the declared type, so
neither shadows the other. **Nothing about what `double`, `float` or `Half` read or write changes.**

The mantissa goes through the same writer as a `BigInteger`, so it takes a bignum tag only where it does
not fit a basic integer. The tag itself is unconditional in both directions: it is the only thing
separating either value from the plain two-element array it is encoded as, so an untagged array is refused,
and where tags 4 and 5 are stacked the innermost decides. A foreign tag anywhere in the stack is skipped,
as elsewhere. An indefinite-length content array is accepted; any length other than two is a
`CborException`.

**The exponent is narrower than the format allows.** §3.4.4 requires a basic integer, which reaches ±2^64,
where these types hold an `int`: a conforming document with an exponent beyond ±2^31 is refused with a
`CborException` rather than read. That is deliberate — such an exponent describes a number with more digits
than there is memory to render it in.

Conversions are explicit and never silent:

```csharp
decimal exact = new CborDecimalFraction(27315, -2).ToDecimal();   // 273.15m, or OverflowException
double near  = new CborDecimalFraction(27315, -2).ToDouble();     // 273.15,  rounds
var fraction = (CborDecimalFraction)273.15m;                      // exact
var bigfloat = (CborBigFloat)1.5;                                 // exact, [-1, 3]
```

`ToDecimal` is exact or throws — it never rounds, since a silent rounding is what these types exist to
avoid. `ToDouble` rounds, and throws only when the magnitude is past what a `double` holds. Every finite
`double` converts exactly into either type, because `2^-n = 5^n × 10^-n`; **there is no conversion from
`decimal` to `CborBigFloat`**, because one tenth is not any integer over a power of two, so the operator
could only round.

Equality is structural over the pair as encoded, so `10e0` and `1e1` are the same number and are *not*
equal, and neither is normalised on the way out — a document round-trips byte for byte. Two encodings of
one number are therefore two distinct dictionary keys, and under `CborOptions.Deterministic` they sort as
the different byte strings they are.

## Half-precision floats (RFC 8949 §3.3)

A member typed `System.Half` reads and writes binary16 — major type 7, additional information 25.

```csharp
public class Reading
{
    public Half Temperature { get; set; }
}
```

```csharp
// (Half)1.5     -> F9 3E00
// Half.MaxValue -> F9 7BFF   (65504)
// Half.Epsilon  -> F9 0001   (the smallest subnormal)
```

**The writer emits binary16 and nothing wider**, where `float` and `double` members emit the shortest
form that round-trips and so may go out as any of the three widths. That is what lets a generated CDDL
schema describe a `Half` as `float16` where a `float` needs the prelude's looser `float`. A NaN of any
payload is written as the canonical `F9 7E00`, so a deterministic encoding admits one spelling of NaN
rather than 2046.

Reading is as tolerant as a `float` member's, because it is the same reader: an integer, a text string,
or any of the three float widths all decode. A value past binary16's range saturates to infinity, which
is what the IEEE conversion does; it is not an error.

`Half` is also the tenth [typed-array](typed-arrays.md) element type, tag 84 little-endian.

> **The encoding changed.** Before `Half` had a converter it fell through to the object mapper and was
> written as the struct's own members — a document no other decoder reads, and write-only besides, since
> those members are computed or read-only and a read returned `default`. Such a document is now refused
> with `Invalid major type Map` rather than silently yielding zero. Nothing that round-tripped before
> round-trips differently now, because nothing round-tripped.
