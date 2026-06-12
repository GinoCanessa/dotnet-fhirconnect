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
- **R4 / R4B / R5 adapters** built on Firely's
  `BaseFhirJsonDeserializer` / `BaseFhirJsonSerializer`,
  disambiguated via an `extern alias` scheme stamped by an
  MSBuild target onto the per-release `ReferencePath` items.
- **Engine** (`FhirConnectEngine` + typed `R4Engine` facade) that
  walks an `EffectiveMapping` (model + extension merge layer) in
  both directions: openEHR Composition → FHIR R4 `Observation`
  and FHIR → openEHR for the `EVALUATION.vital_status.v1`
  scope.
- **`FhirConnectEngine.ToOpenEhr`** implemented for vital_status —
  builds a Composition skeleton via `SkeletonBuilder` and
  delegates to an internal overload that runs the model rules in
  reverse via `FhirToOpenEhrTranslator` + `OpenEhrPathWriter`.
- **Phase 6b extension dispatch** — `reference` / `slotArchetype` /
  `extension: add | overwrite | remove` are now consumed by
  `EffectiveMapping.Build` at engine construction so the produced
  Observation carries extension-injected fields (LOINC `code`
  `67162-8`, `category: survey`, encounter-identifier wiring).
  Workaround: patched `KDS_composition.encounter` from
  `extension: overwrite` to `extension: add`; upstream spec error
  pending fix (see `tests/fixtures/vital-status/PROVENANCE.md`).
- **CLI** with `validate` and `transform` verbs; `transform
  --direction to-openehr` now wired end-to-end.

### Changed

- **OPT-driven element names.** `OpenEhrPathWriter` now stamps openEHR
  `Element.Name` from an optional operational template
  (`IOperationalTemplate`, loaded via the internal
  `OperationalTemplateLoader` over the SDK `Opt14XmlParser`) instead of a
  hand-coded at-code→name table. Names resolve from the active component
  archetype's terminology (then the root terminology); when no template
  is supplied the bare at-code is used. The vendored German
  `KDS_Vitalstatus.opt` and the canonical composition fixture use the
  OPT's German literals (`Vitalstatus` / `Kommentar` / `Zeitpunkt der
  Feststellung`). Element names are not mapped into FHIR, so emitted R4
  output is unchanged.

### Known limitations

- Skeleton bootstrap inside `FhirConnectEngine.ToOpenEhr(object)`
  is vital_status-only; other archetypes need a caller-supplied
  Composition via the internal `ToOpenEhr(object, Composition)`
  overload.
- Direction-asymmetric fields (link-driven `partOf` / `basedOn` /
  `focus` / `case` / `hasMember`, the `performer` collapse, and
  the Phase 6b extension-injected `code` / `category` constants)
  are populated on ToFhir but are not reverse-walked on
  ToOpenEhr.
- R5's `effective` widening into `Reference(MolecularSequence)`
  and R5's restructured `Encounter` fields (`class` /
  `reasonCode` rename / `subjectStatus`) are out of scope; the
  R5 adapter mirrors R4's switch arms exactly.
- The Phase 7 FHIRPath normaliser whitelists `.ofType(<Type>)`
  only; `where(...)`, `as(...)`, `extension(...)`, etc. throw.
- Library is not AOT-publishable.

[Unreleased]: https://github.com/GinoCanessa/dotnet-fhirconnect/compare/main...HEAD
