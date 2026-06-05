# Provenance — `tests/fixtures/vital-status/`

This directory vendors a FHIRconnect v1.0.0 mapping bundle for the
`EVALUATION.vital_status.v1` archetype plus hand-authored canonical
openEHR and FHIR R4 samples used by the test suite. Sources are
Apache-2.0; license attribution lives in the repo-root `NOTICE`.

## Mapping files

Origin repository: <https://github.com/SevKohler/FHIRconnect-mapping-lib>
Local working copy at vendor time: `C:\ai\git\FHIRconnect-mapping-lib`
Origin commit SHA: `ff4631f4309e248dea24972415663f5ee242fe53`
Origin license: Apache-2.0
Date vendored: 2026-06-03

| Vendored path | Origin path | Vendored shape |
|---|---|---|
| `model/vital_status.v1.yml` | `model/evaluation/org.openehr/vital_status.v1.yml` | verbatim |
| `project/KDS_Vitalstatus.context.yaml` | `projects/org.highmed/KDS/vitalstatus/KDS_Vitalstatus.context.yaml` | verbatim |
| `project/KDS_composition.yml` | `projects/org.highmed/KDS/vitalstatus/KDS_composition.yml` | **patched** (see below) |
| `project/KDS_vitalsigns.yml` | `projects/org.highmed/KDS/vitalstatus/KDS_vitalsigns.yml` | **patched** (see below) |

### Patch: `encounter` extension verb in `KDS_composition.yml`

Upstream the `encounter` rule (line 13) carries:

```yaml
extension: "overwrite"
```

The vital_status model mapping has no rule with the matching
composite key
`(name=encounter, fhir=$resource.encounter.ofType(Reference).identifier, disambiguator=CLUSTER.case_identification.v0)`,
so the merge layer (`EffectiveMapping.Build`, plan slot `0605-02`
phase 6) throws `FhirConnectFormatException` per its
overwrite-with-no-match semantics. The vendored copy degrades the
verb to `extension: "add"`, which is the actual bundle-author
intent and resolves the conflict:

```diff
-    extension: "overwrite"
+    extension: "add"
```

**Upstream TODO:** file an issue / PR against
`SevKohler/FHIRconnect-mapping-lib` to fix the verb at source.
Tracked alongside the trailing-quote typo below.

### Patch: trailing-quote typo in `KDS_vitalsigns.yml`

Upstream line 19 contains:

```yaml
    slotArchetype: "COMPOSITION.report.v1.Observation""
```

— with a stray trailing double-quote that any strict YAML 1.2 parser
rejects with an "unexpected end of stream" or "found character that
cannot start any token" error. The vendored copy removes the extra
quote:

```diff
-    slotArchetype: "COMPOSITION.report.v1.Observation""
+    slotArchetype: "COMPOSITION.report.v1.Observation"
```

**Upstream TODO:** file an issue / PR against
`SevKohler/FHIRconnect-mapping-lib` to fix the typo at source.
Tracked in the draft at
`scratch/0527-01/upstream-fhirconnect-mapping-lib-vitalsigns-quote.md`
(filed by the user — `dev-do` does not open external issues).

## Hand-authored samples

| Path | Shape | Author | Date |
|---|---|---|---|
| `samples/vital-status.composition.canonical.json` | hand-authored | dev-do (Phase 2) | 2026-06-03 |
| `samples/vital-status.observation.r4.json` | hand-authored | dev-do (Phase 2) | 2026-06-03 |

Both samples are written from scratch against the FHIRconnect mapping
in this directory and the openEHR/FHIR specs at
`C:\ai\support\openEHR\` and `C:\ai\support\fhir-r4\`. They are
**not** copies of openFHIR's `core/src/test/resources/kds/vitalstatus/`
test data (which is Apache-2.0 and inspectable, but used here only
for structural reference — every field, value, and identifier in the
vendored samples is independent).

The pair is intended to round-trip equivalently under the vendored
mapping bundle. Until Phase 6a wires up the generator, the two files
are kept in lock-step by hand; once the engine is real,
`gen-fixtures.csx` regenerates the Observation from the Composition
so drift cannot creep in silently.

## Generator script

`gen-fixtures.csx` is retired. The fixture generator now lives as a
console app at `tools/GenFixtures/`. To refresh the Observation
fixture after a mapping change, run from the repo root:

```pwsh
dotnet run --project tools/GenFixtures
```

CI does not invoke the generator — it is a human-run helper.

