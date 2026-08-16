; Diagnostics carried by a released version of the generator, which shipped for the first time in
; 1.27.0. Entries move here from AnalyzerReleases.Unshipped.md when a version ships, under a heading
; naming that version.

## Release 1.27.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------------------------------------------------------------------
CBOR1001 | Dahomey.Cbor.Generator | Error | CBOR serializer context must be partial
CBOR1002 | Dahomey.Cbor.Generator | Error | Type is not supported by CBOR source generation
CBOR1003 | Dahomey.Cbor.Generator | Error | Naming convention is not supported by CBOR source generation
CBOR1004 | Dahomey.Cbor.Generator | Error | Type has conflicting discriminator attributes
CBOR1005 | Dahomey.Cbor.Generator | Error | Member needs an explicit index
CBOR1006 | Dahomey.Cbor.Generator | Warning | Member cannot be deserialized
CBOR1007 | Dahomey.Cbor.Generator | Error | CBOR feature is not supported by source generation
CBOR1008 | Dahomey.Cbor.Generator | Error | Non-public member cannot be source-generated
CBOR1009 | Dahomey.Cbor.Generator | Error | Discriminated subtype is not declared on any context
CBOR1010 | Dahomey.Cbor.Generator | Error | Type has no accessible parameterless constructor
CBOR1011 | Dahomey.Cbor.Generator | Error | Type has no CDDL representation
CBOR1012 | Dahomey.Cbor.Generator | Error | Polymorphic schema is incomplete
CBOR1013 | Dahomey.Cbor.Generator | Error | Two members map to one CBOR name
CBOR1014 | Dahomey.Cbor.Generator | Error | Two members map to one CBOR index
