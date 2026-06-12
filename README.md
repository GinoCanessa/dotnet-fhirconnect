# dotnet-fhirconnect

> A .NET implementation of the [FHIRconnect](https://sevkohler.github.io/FHIRconnect-spec/)
> mapping specification — bidirectional transformation between openEHR
> Compositions and HL7 FHIR Resources.

[![Tests](https://github.com/GinoCanessa/dotnet-fhirconnect/actions/workflows/ci.yml/badge.svg)](https://github.com/GinoCanessa/dotnet-fhirconnect/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

## Status — pre-alpha (walking skeleton + bidirectional vital_status)

`v0.x` ships **bidirectional support** for the
[`EVALUATION.vital_status.v1`](https://github.com/SevKohler/FHIRconnect-mapping-lib/blob/main/model/evaluation/org.openehr/vital_status.v1.yml)
mapping across FHIR R4, R4B, and R5 — the engine walks an
`EffectiveMapping` (model + extension merge layer) in both
directions and the CLI's `transform --direction` accepts both
`to-fhir` and `to-openehr`. Use it to exercise the engine and the
CLI shape; do not use it in production. Broader mapping coverage
and an OPT-driven `OpenEhrPathWriter` are follow-on work.

### Bidirectional support

The vital_status mapping bundle has three field-equivalent
bidirectional leaves — `effective`, `vitalStatus`, `note` — that
round-trip Composition → Observation → Composition with pinned
comparison semantics. The following fields are
**direction-asymmetric in v0.x** and populated on `ToFhir` but
not reverse-walked on `ToOpenEhr`:

- `link`-driven references (`partOf`, `basedOn`, `focus`, `case`,
  `hasMember`) — the `ehr:///compositions/<uuid>` ↔
  `Reference("Observation/<uuid>")` rewrite is lossy on the
  inverse without a marker.
- `performer` — multiple openEHR sources (`health_care_facility`,
  `composer`, `participations`, `other_participations`) collapse
  into one FHIR array; the inverse cannot recover which slot
  each entry came from.
- The Phase 6b extension-injected `code` / `category` constants —
  the extensions deterministically re-inject them on each
  `ToFhir` hop, but no openEHR-side source exists to walk on
  `ToOpenEhr`.

## Limitations & scope

This is the **canonical** scope statement for `v0.x`. Other docs link
back here rather than restating it.

- **Direction-asymmetric fields.** Populated on `ToFhir` but not
  reverse-walked on `ToOpenEhr` (see [Bidirectional support](#bidirectional-support)
  above): `link`-driven references (`partOf`, `basedOn`, `focus`,
  `case`, `hasMember`); the `performer` collapse; and the Phase 6b
  extension-injected `code` / `category` constants.
- **Skeleton bootstrap is vital_status-only.** `ToOpenEhr(object)`
  builds a Composition skeleton only for `EVALUATION.vital_status.v1`;
  other archetypes require the internal `ToOpenEhr(object, Composition)`
  overload with a caller-supplied skeleton.
- **R5 widenings out of scope.** R5's `effective` widening into
  `Reference(MolecularSequence)` and R5's restructured `Encounter`
  fields (`class` / `reasonCode` rename / `subjectStatus`) are not
  handled; the R5 adapter mirrors R4's switch arms exactly.
- **FHIRPath normaliser is narrow.** The Phase 7 normaliser whitelists
  `.ofType(<Type>)` only; `where(...)`, `as(...)`, `extension(...)`,
  etc. throw.
- **CLI emits bare at-code element names.** Friendly openEHR
  `Element.Name` values come from an operational template through the
  **library-only** `ToOpenEhr(object, Composition)` seam
  (`IOperationalTemplate`). The CLI exposes no `--template` option, so
  `transform --direction to-openehr` output carries bare at-code
  element names.
- **Not AOT-publishable.** Firely, YamlDotNet, and the DotnetOpenEhr
  SDK each have reflection-based code paths; the library carries
  `[RequiresUnreferencedCode]` accordingly.

## Quickstart — library

Install the package for the FHIR release you target — `DotnetFhirConnect.FhirR4`
(or `.FhirR4B` / `.FhirR5`). Each binds exactly one Firely release, so you
write idiomatic Firely code with **no `extern alias`** and no MSBuild
workarounds. The release-agnostic engine, loader, and validator live in the
transitively-referenced `DotnetFhirConnect.Core`.

```bash
dotnet add package DotnetFhirConnect.FhirR4 --prerelease
```

```csharp
using System.IO;
using DotnetFhirConnect.Mappings;
using DotnetFhirConnect.Fhir.R4;
using DotnetOpenEhr.Serialization.Json;
using Hl7.Fhir.Model;
// openEHR's RM and FHIR both define `Composition`/`Observation`; alias the
// openEHR side. (This is a normal cross-domain name clash — NOT the per-release
// `extern alias` workaround the package split removed.)
using OpenEhrComposition = DotnetOpenEhr.Rm.Composition.Composition;

// 1. Load a FHIRconnect mapping bundle (directory of YAML).
MappingBundle bundle = MappingBundle.Load("mappings/vital-status/project");

// 2. Construct the typed R4 engine. Touching R4Engine loads the
//    DotnetFhirConnect.FhirR4 assembly, which self-registers its adapter.
R4Engine engine = new R4Engine(bundle);

// 3. Hand it a canonical openEHR Composition.
OpenEhrComposition composition = OpenEhrJson.ParseComposition(File.ReadAllText("input.json"))!;
Observation observation = (Observation)engine.ToFhir(composition);
```

> Reaching for the release-agnostic `FhirConnectEngine` directly (instead of
> a typed `R4Engine` / `R4BEngine` / `R5Engine` facade)? Call
> `R4FhirSupport.Register()` (or the R4B / R5 equivalent) first so the
> adapter for your `spec.version` is registered.

## Quickstart — CLI

```bash
# Install (from local pack output)
dotnet pack src/DotnetFhirConnect.Cli -c Release
dotnet tool install --add-source ./src/DotnetFhirConnect.Cli/bin/Release --global DotnetFhirConnect.Cli

# Validate a mapping bundle
dotnet fhirconnect validate --mapping tests/fixtures/vital-status

# Transform a canonical openEHR Composition to a FHIR R4 Observation
dotnet fhirconnect transform \
    --direction to-fhir \
    --mapping tests/fixtures/vital-status \
    --input tests/fixtures/vital-status/samples/vital-status.composition.canonical.json \
    --output -
```

## Repository layout

```text
dotnet-fhirconnect/
├── src/
│   ├── DotnetFhirConnect.Core/   # release-agnostic engine/loader/validator (NuGet: DotnetFhirConnect.Core; Hl7.Fhir.Base only)
│   ├── DotnetFhirConnect.FhirR4/ # R4 binding (NuGet: DotnetFhirConnect.FhirR4; Core + Hl7.Fhir.R4)
│   ├── DotnetFhirConnect.FhirR4B/# R4B binding (NuGet: DotnetFhirConnect.FhirR4B; Core + Hl7.Fhir.R4B)
│   ├── DotnetFhirConnect.FhirR5/ # R5 binding (NuGet: DotnetFhirConnect.FhirR5; Core + Hl7.Fhir.R5)
│   └── DotnetFhirConnect.Cli/    # CLI tool (NuGet: DotnetFhirConnect.Cli; dotnet fhirconnect ...)
├── tests/
│   ├── DotnetFhirConnect.Tests/
│   ├── DotnetFhirConnect.Cli.Tests/
│   └── fixtures/
│       ├── vital-status/         # vendored mapping bundle + sample composition + observation
│       └── spec-schemas/         # FHIRconnect v1.0.0 JSON schemas (patched — see PROVENANCE)
└── docs/                         # getting-started + architecture notes
```

See [`docs/getting-started.md`](docs/getting-started.md) for a longer
walkthrough, [`docs/cli.md`](docs/cli.md) for the CLI reference, and
[`docs/architecture.md`](docs/architecture.md) for the internal seams.

## Packages

`v0.x` ships four NuGet packages (all at the same lockstep version):

| Package | Role | Pulls in |
|-|-|-|
| `DotnetFhirConnect.Core` | Release-agnostic engine, mapping loader, validator, translators. No FHIR model package. | `Hl7.Fhir.Base` |
| `DotnetFhirConnect.FhirR4` | R4 adapter + typed `R4Engine`. | `Core` + `Hl7.Fhir.R4` |
| `DotnetFhirConnect.FhirR4B` | R4B adapter + typed `R4BEngine`. | `Core` + `Hl7.Fhir.R4B` |
| `DotnetFhirConnect.FhirR5` | R5 adapter + typed `R5Engine`. | `Core` + `Hl7.Fhir.R5` |

Reference a single per-release package and you get exactly one Firely model
assembly transitively — so `Observation`, `Composition`, etc. resolve with no
`extern alias`. Multi-release hosts reference more than one package and call
`R{4,4B,5}FhirSupport.Register()` for order-independent `spec.version`
selection.

> The pre-`0.x` monolithic `DotnetFhirConnect` package id (which referenced
> all three Firely releases at once and leaked an aliasing requirement onto
> consumers) is **retired**. There is no compatibility/meta package; depend on
> `DotnetFhirConnect.Core` plus the per-release binding(s) you need.

## Dependencies

- **[DotnetOpenEhr](https://www.nuget.org/packages/DotnetOpenEhr)** —
  typed openEHR Reference Model, canonical / flat JSON, AQL path
  resolver. Floats to latest beta in the `2026.*-*` line via
  `Directory.Packages.props`.
- **[Hl7.Fhir.Base](https://www.nuget.org/packages/Hl7.Fhir.Base)** —
  release-agnostic Firely layer used by `Core`.
- **[Hl7.Fhir.R4](https://www.nuget.org/packages/Hl7.Fhir.R4)**,
  **[Hl7.Fhir.R4B](https://www.nuget.org/packages/Hl7.Fhir.R4B)**,
  **[Hl7.Fhir.R5](https://www.nuget.org/packages/Hl7.Fhir.R5)** —
  Firely .NET SDK; each flows transitively through exactly one
  per-release package, so consumers need no `extern alias`.
- **YamlDotNet**, **JsonSchema.Net**, **System.CommandLine**.

## License

[MIT](LICENSE). Vendored upstream sources (FHIRconnect-spec schemas,
FHIRconnect-mapping-lib vital_status bundle) are Apache-2.0 with
attribution in [`NOTICE`](NOTICE) and per-fixture `PROVENANCE.md`.
