# Dates and times

[← back to the README](../README.md)

`DateTime`, `DateTimeOffset`, `DateOnly` and `TimeOnly` each have a converter. Which encoding they write is chosen by one
option, `CborOptions.DateTimeFormat`, so a document keeps one shape throughout. `TimeSpan` has a converter
on request, through its own option, `CborOptions.TimeSpanFormat`.

| `DateTimeFormat` | `DateTime` | `DateOnly` |
|---|---|---|
| `ISO8601` (default) | tag 0 over an RFC 3339 date-time | tag 1004 over an RFC 3339 `full-date` |
| `Unix` | tag 1 over seconds since the epoch | tag 100 over days since 1970-01-01 |
| `UnixMilliseconds` | tag 1 over fractional seconds | tag 100 over days since 1970-01-01 |

`TimeOnly` is not in the table because it has one encoding whatever the setting says: an untagged RFC 3339
`partial-time` string. `DateTimeFormat` chooses between representations of an instant, and a time of day
is not one.

Reading ignores the setting: every form above is accepted whichever one is configured, tagged or not.
The option describes what this end emits, and a peer's choice is not yours to make.

Tag 1 over a float is read to the nearest tick, and a float that is the nearest one to a whole
millisecond, which is what `UnixMilliseconds` writes, is read as that millisecond. A double near today's
1.7e9 seconds resolves only about 2.4 ticks, so the exact millisecond is not in the bytes and could not
be recovered by rounding to the tick alone. NaN and the infinities name no instant and are refused.

## `DateTime`

A `DateTime` read from a string carrying a numeric offset is converted to UTC, and the offset itself is
not retained — `DateTime` has nowhere to put it, only a `DateTimeKind`. A string with no offset and no
`Z` is given the kind named by `CborOptions.UnqualifiedTimeZoneDateTimeKind`.

## `DateOnly` (RFC 8943)

```csharp
public class Invoice
{
    public DateOnly Issued { get; set; }
}
```

```csharp
// 2026-08-17 -> D9 03EC 6A 323032362D30382D3137   (tag 1004, "2026-08-17")
// 2026-08-17 -> D8 64 19 50CA                     (tag 100, 20682 days)
```

Both tags are registered by [RFC 8943](https://www.rfc-editor.org/rfc/rfc8943.html) for exactly this: a
date with no time of day and no time zone. A date before the epoch is a negative day count, which that
definition allows.

Both numeric formats write the day count. A date has no time of day to carry milliseconds, so there is
nothing for `UnixMilliseconds` to make finer.

### Documents written before `DateOnly` had a converter

Before it had a converter a `DateOnly` was written as a map of its public properties (`Year`, `Month`,
`Day`, `DayOfWeek`, `DayOfYear`, `DayNumber`). That form is still **read**, whatever the setting, and is
never written. `DayNumber` decides when present, and `Year`, `Month` and `Day` otherwise; a map with
neither is refused. Keys match without regard to case, so a naming convention does not matter, and a
repeated key follows `CborOptions.DuplicateKeyMode` like any other map. A key that is not text is
refused. Only the string-keyed map is recognised, which loses nothing: neither type's properties carry a member index, so
`IntKeyMap` and `Array` refused to write either of them (`expecting all fields/properties to get a member
index`) and no document was ever stored that way.

## `TimeOnly`

```csharp
// 01:02:03     -> 68 30313A30323A3033         ("01:02:03")
// 01:02:03.841 -> 6C 30313A30323A30332E383431 ("01:02:03.841")
```

Alone among these types, `TimeOnly` is written **untagged**. The CBOR tag registry has nothing for a time
of day — tags 0, 1, 4 and 5 are whole instants, and 1002 and 1003 are a duration and a period — so there
is no number to use, and occupying an unassigned one would produce documents another decoder is entitled
to reject.

The fraction is written only when there is one, at the width it needs, and a fraction finer than 100ns in
an incoming document is truncated rather than refused: a peer whose clock is more precise than `TimeOnly`
is interoperating correctly.

`DateTimeFormat` has no effect on a `TimeOnly`: with `Unix` or `UnixMilliseconds` set it is still the
string above, fraction included.

### Documents written before `TimeOnly` had a converter

A `TimeOnly` used to be written as a map of its public properties (`Hour`, `Minute`, `Second`,
`Millisecond`, `Microsecond`, `Nanosecond`, `Ticks`). That map is still **read** and never written.
`Ticks` carries the whole value, so seconds and fractions are recovered from it; without `Ticks`,
`Hour` and `Minute` are required and `Second`, `Millisecond` and `Microsecond` are used when present.
The same applies as for `DateOnly`: only the string-keyed map ever existed.

## `DateTimeOffset`

`DateTimeOffset` keeps the offset it carries under `ISO8601` (tag 0). Tag 1 names an instant and has
nowhere to put an offset, so `Unix` and `UnixMilliseconds` write the right instant and read it back at
offset zero.

## `TimeSpan` (RFC 9581)

By default a `TimeSpan` has no converter: the reflection path maps it as an object and writes its public
properties as a map — valid CBOR, but large (259 bytes) and meaningful only to a decoder that knows the CLR
type's shape. That form is unchanged. `CborOptions.TimeSpanFormat` opts in to the duration
[RFC 9581](https://www.rfc-editor.org/rfc/rfc9581.html#section-4) registers for exactly this, tag 1002:

```csharp
var options = new CborOptions { TimeSpanFormat = TimeSpanFormat.Duration };
```

```csharp
// 01:02:03     -> D9 03EA A1 01 19 0E8B                 {1: 3723}
// 01:02:03.500 -> D9 03EA A2 01 190E8B 28 1A1DCD6500    {1: 3723, -9: 500000000}
// -1.5 s       -> D9 03EA A2 01 21 28 1A1DCD6500        {1: -2, -9: 500000000}
```

Key 1 is the whole seconds and key -9 a fraction in nanoseconds, written only when the value has one.
RFC 9581 §3.3 makes the fraction an **unsigned** count *added* to the base, so a negative duration writes
the floored base: -1.5 s is -2 + 0.5. `TimeSpan.MinValue` and `MaxValue` round-trip.

This setting governs reading as well as writing: under the default there is no converter, so tag 1002 is
not read as a `TimeSpan`, and under `Duration` the property-map form is not.

A duration is read by these rules:

- Key 1 is an integer or a float. A float is rounded to the nearest tick, and is refused beside a
  fraction key.
- The fraction keys -3, -6, -9, -12, -15 and -18 are unsigned integers, at most one per duration. A
  `TimeSpan` resolves to 100ns, so a fraction finer than that is accepted only when it is a whole number of
  ticks, and is otherwise refused rather than truncated. A fraction of any size is added to the base
  with checked arithmetic.
- Keys -1, -13 and 13 are the timescale, at most one of them: 0 (UTC) and 1 (TAI) are accepted, since a
  length in SI seconds is the same on both. Any other value is skipped under -1 or -13 and refused under the
  critical key 13.
- An unsigned key that is not understood, key 0 included, is refused, as are a base under key 4 or 5 and a
  duration with no key 1. A negative or text key that is not understood is skipped with its value.
- A plain number is read as seconds.
- A value outside the range of a `TimeSpan` is a `CborException`.

The tag itself is not checked on read, as for every other converter here.

In a source-generated context the setting has to be stated on the attribute, because it decides whether
`TimeSpan` is collected as an object or resolved to a converter:

```csharp
[CborSerializable(typeof(Job))]
[CborSourceGenerationOptions(TimeSpanFormat = TimeSpanFormat.Duration)]
public partial class JobContext : CborSerializerContext { }
```

A schema-generating context describes a `TimeSpan` under `Duration` as
`#6.1002({1 => int, ? -9 => uint})`; the fraction is `uint` because the reader accepts a count larger than
one second and folds it into the base.

## What has no converter

`System.TimeSpan` has none under the default `TimeSpanFormat.Members`. It is a struct with public
properties, so the reflection path maps it as an object and writes those properties as a map. A
source-generated context does not refuse it; it collects `TimeSpan` as an object and generates that same
map. Set `TimeSpanFormat.Duration` (above) for the compact form.

Write a [custom converter](../README.md#custom-converters) for any other shape.

## Compatibility

Data written by this version cannot be read by older versions. A `DateOnly` is now a tagged string or
number and a `TimeOnly` an untagged string, where an older version expected (or, for a `DateOnly` on
current runtimes, failed to write) a map of the type's public properties, and refuses anything else.
This version reads that map, so upgrade readers before writers.

A `TimeSpan` written under `TimeSpanFormat.Duration` is a tagged map that an older version, which maps a
`TimeSpan` by its members, cannot read. The default is unchanged, so nothing changes until the option is set.
