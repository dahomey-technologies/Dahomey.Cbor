# Dates and times

[← back to the README](../README.md)

`DateTime`, `DateOnly` and `TimeOnly` each have a converter. Which encoding they write is chosen by one
option, `CborOptions.DateTimeFormat`, so a document keeps one shape throughout.

| `DateTimeFormat` | `DateTime` | `DateOnly` | `TimeOnly` |
|---|---|---|---|
| `ISO8601` (default) | tag 0 over an RFC 3339 date-time | tag 1004 over an RFC 3339 `full-date` | an RFC 3339 `partial-time`, untagged |
| `Unix` | tag 1 over seconds since the epoch | tag 100 over days since 1970-01-01 | seconds since midnight, untagged |
| `UnixMilliseconds` | tag 1 over fractional seconds | tag 100 over days since 1970-01-01 | fractional seconds since midnight, untagged |

Reading ignores the setting: every form above is accepted whichever one is configured, tagged or not.
The option describes what this end emits, and a peer's choice is not yours to make.

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

## `TimeOnly`

```csharp
// 01:02:03     -> 68 30313A30323A3033         ("01:02:03")
// 01:02:03.841 -> 6C 30313A30323A30332E383431 ("01:02:03.841")
// 01:02:03     -> 19 0E8B                     (3723 seconds since midnight)
```

Alone among these types, `TimeOnly` is written **untagged**. The CBOR tag registry has nothing for a time
of day — tags 0, 1, 4 and 5 are whole instants, and 1002 and 1003 are a duration and a period — so there
is no number to use, and occupying an unassigned one would produce documents another decoder is entitled
to reject.

The fraction is written only when there is one, at the width it needs, and a fraction finer than 100ns in
an incoming document is truncated rather than refused: a peer whose clock is more precise than `TimeOnly`
is interoperating correctly.

`DateTimeFormat.Unix` writes whole seconds, so a `TimeOnly` carrying a fraction does not survive a round
trip through that setting. This is the same narrowing `Unix` already applies to a `DateTime`.

A `DateTime` read from a string carrying a numeric offset is converted to UTC, and the offset itself is
not retained — `DateTime` has nowhere to put it, only a `DateTimeKind`. A string with no offset and no
`Z` is given the kind named by `CborOptions.UnqualifiedTimeZoneDateTimeKind`.

## What has no converter

`System.DateTimeOffset` and `System.TimeSpan` have none. Both are structs with public properties, so the
reflection path maps them as objects and writes those properties as a map: valid CBOR, but large — a
`TimeSpan` takes 259 bytes — and a `DateTimeOffset` does not read back at all, arriving as `default`.

A source-generated context refuses `DateTimeOffset` at build time with `CBOR1002`. It does not refuse
`TimeSpan`, which is collected as an object and generates that same map.

Write a [custom converter](../README.md#custom-converters) for either if you need one.
