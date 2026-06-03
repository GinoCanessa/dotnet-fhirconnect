# Changelog

All notable changes to this project are documented here, following
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- **Walking-skeleton release.** Initial skeleton of the `dotnet-fhirconnect`
  repository: library (`DotnetFhirConnect`), CLI tool
  (`DotnetFhirConnect.Cli` → `dotnet fhirconnect ...`), and xUnit
  test suite. Targets .NET 10 / C# 14.
- **FHIRconnect v1.0.0 loader** with typed record graph for model,
  context, and extension mapping files.
- **Validator** with JSON-schema evaluation against the patched
  embedded schemas plus a small set of semantic rules
  (`FCV001`–`FCV900`).
- **R4 adapter** (`R4Adapter`) backed by Firely's
  `BaseFhirJsonDeserializer` / `BaseFhirJsonSerializer`. R4B / R5
  adapter stubs ship for the seam.
- **Engine** (`FhirConnectEngine` + typed `R4Engine` facade) that
  walks the `EVALUATION.vital_status.v1` model mapping and produces
  a FHIR R4 `Observation` from a canonical openEHR Composition.
- **CLI** with `validate` and `transform` verbs over the library.

### Known limitations

- `ToOpenEhr` direction is not implemented — calls throw
  `NotSupportedException`; CLI `--direction to-openehr` returns
  exit code 3.
- Extension rules (`reference`, `slotArchetype`,
  `extension: add | overwrite | remove`) are loaded but not
  applied — Observations produced from the vital_status bundle
  lack the `code` and `category` fields that the
  `KDS_vital_status` extension would otherwise inject.
- R4B / R5 adapters are pending stubs; only R4 mappings transform
  end-to-end today.
- Library is not AOT-publishable.

[Unreleased]: https://github.com/ginoc/dotnet-fhirconnect/compare/main...HEAD
