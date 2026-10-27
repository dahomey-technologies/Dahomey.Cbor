; Diagnostics added since the last released version. Add a row here for every new
; DiagnosticDescriptor in Diagnostics.cs -- RS2008 fails the build otherwise -- and move the table
; into AnalyzerReleases.Shipped.md under a '## Release <version>' heading when a version ships.

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------------------------------------------------------------------
CBOR1015 | Dahomey.Cbor.Generator | Error | Creator constructor is not accessible to the generated context
CBOR1016 | Dahomey.Cbor.Generator | Error | Constructor mapping cannot be reproduced
