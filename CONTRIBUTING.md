# Contributing to Dahomey.Cbor

Thank you for helping. Dahomey.Cbor is maintained on limited time, and every issue and pull
request is read and reviewed by a person. The rules below exist to keep that review sustainable;
contributions that follow them are merged much faster.

## Before you start

- **Bug fixes**: open a pull request directly, or an issue first if the fix is not obvious.
- **New features, new converters, public API or behaviour changes**: open an issue and wait for the
  maintainer's agreement before writing the code. A pull request for an undiscussed feature may be
  closed even if it is correct.
- **Search first**: an existing issue may already cover the problem. Add to it rather than opening
  a new one.

## Issues

- One problem per issue, with a minimal reproduction: the type, the input bytes in hex or the value
  written, the actual and the expected result, and the target framework.
- Audit or fuzzing results that find several problems go in **one** issue with a checklist. While a
  checklist is still open, add new findings to it as new lines instead of opening another issue.
- Rank what you report. Data that is silently wrong, and inputs that hang, crash or allocate
  without bound, come first. Exceptions of the wrong type come next. Missing support for a type is
  a feature request (see above).

## Pull requests

- **At most three open pull requests per contributor.** Wait for one to be merged or closed before
  opening the next.
- **One concern per pull request.** A fix that ticks one checklist item is easier to review than
  one that ticks five. Reference the issue with `Closes #N`, or `Part of #N` for a checklist item.
- **Keep it small.** If the change touches many files or needs a long explanation, split it, or
  discuss it in an issue first.
- **Tests in proportion to the fix.** Add the tests that fail without the change and the cases that
  guard what must not change. A few hundred lines of tests for a ten-line fix make the review
  slower, not safer.
- **Describe the behaviour change**, if there is one, in the pull request description, so that it
  can go into the release notes.
- **Respond to review** by pushing to the same branch. Do not open a new pull request for the same
  change.

## Source generator

The source generator (`src/Dahomey.Cbor.Generator`) must produce exactly the bytes, and the
behaviour, of the reflection path. When it cannot support a shape, it must report a compile-time
diagnostic (`CBORxxxx`); it must never emit code that fails to compile, nor silently diverge from
reflection. A change to one path usually needs a test that compares both.

## Building and testing

The test projects target .NET 8, 9 and 10. Before pushing, run what CI runs:

```sh
dotnet build -c Release src/Dahomey.Cbor
CDDL_REQUIRED=1 dotnet test -c Release src/Dahomey.Cbor.Tests
dotnet test -c Release src/Dahomey.Cbor.Generator.Tests
```

The CDDL schema tests need the reference `cddl` Ruby gem (`gem install cddl`). Without it they
skip themselves; `CDDL_REQUIRED=1` turns a missing gem into a failure, as in CI.

The Release build must have no warnings.

## Code style

Follow the style of the surrounding code. Comments and documentation are written in English.

## License

By contributing, you agree that your contributions are licensed under the [MIT License](LICENSE).
