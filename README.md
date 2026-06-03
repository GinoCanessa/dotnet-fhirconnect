# dotnet-fhirconnect

> A .NET implementation of the [FHIRconnect](https://sevkohler.github.io/FHIRconnect-spec/)
> mapping specification — bidirectional transformation between openEHR
> Compositions and HL7 FHIR Resources.

[![Tests](https://github.com/ginoc/dotnet-fhirconnect/actions/workflows/ci.yml/badge.svg)](https://github.com/ginoc/dotnet-fhirconnect/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

## Status — pre-alpha (walking skeleton)

`v0.x` ships **one direction only** — openEHR Composition → FHIR
Resource — for the
[`EVALUATION.vital_status.v1`](https://github.com/SevKohler/FHIRconnect-mapping-lib/blob/main/model/evaluation/org.openehr/vital_status.v1.yml)
mapping against FHIR R4 `Observation`. Use it to exercise the engine
and the CLI shape; do not use it in production.

The reverse direction (FHIR → openEHR), the R4B / R5 adapters, the
extension-aware engine (Phase 6b), and broader mapping coverage are
follow-on work. See [`scratch/0527-01/plan.md`](scratch/0527-01/plan.md)
for the roadmap and open deviations.

## Quickstart — library

```csharp
using DotnetFhirConnect.Mappings;
using DotnetFhirConnect.Fhir.R4;
using DotnetOpenEhr.Rm.Composition;
using DotnetOpenEhr.Serialization.Json;
using Hl7.Fhir.Model;

// 1. Load a FHIRconnect mapping bundle (directory of YAML).
MappingBundle bundle = (MappingBundle)FhirConnectMapping.Load("mappings/vital-status/project");

// 2. Construct the typed R4 engine.
R4Engine engine = new R4Engine(bundle);

// 3. Hand it a canonical openEHR Composition.
Composition composition = OpenEhrJson.ParseComposition(File.ReadAllText("input.json"))!;
Observation observation = (Observation)engine.ToFhir(composition);
```

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
│   ├── DotnetFhirConnect/        # core library (NuGet: DotnetFhirConnect)
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
walkthrough and [`docs/architecture.md`](docs/architecture.md) for the
internal seams.

## Dependencies

- **[DotnetOpenEhr](https://www.nuget.org/packages/DotnetOpenEhr)** —
  typed openEHR Reference Model, canonical / flat JSON, AQL path
  resolver. Floats to latest beta in the `2026.*-*` line via
  `Directory.Packages.props`.
- **[Hl7.Fhir.R4](https://www.nuget.org/packages/Hl7.Fhir.R4)** —
  Firely .NET SDK for FHIR R4 (R4B / R5 packages re-added when the
  pending adapters land).
- **YamlDotNet**, **JsonSchema.Net**, **System.CommandLine**.

## License

[MIT](LICENSE). Vendored upstream sources (FHIRconnect-spec schemas,
FHIRconnect-mapping-lib vital_status bundle) are Apache-2.0 with
attribution in [`NOTICE`](NOTICE) and per-fixture `PROVENANCE.md`.
