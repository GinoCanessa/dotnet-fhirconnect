# Provenance — `tests/fixtures/spec-schemas/`

This directory vendors FHIRconnect v1.0.0 JSON-Schema files used by
the library validator. Sources are Apache-2.0; license attribution
lives in the repo-root `NOTICE`.

Origin repository: <https://github.com/SevKohler/FHIRconnect-spec>
Local working copy at vendor time: `C:\ai\git\FHIRconnect-spec`
Origin commit SHA: `bab767161190c1acf01493b0a7d4c71588927ac5`
Origin license: Apache-2.0
Date vendored: 2026-06-03

| Vendored path | Origin path | Vendored shape |
|---|---|---|
| `model-mapping.schema.json` | `modules/ROOT/attachments/model-mapping.schema.json` | **patched** |
| `contextual-mapping.schema.json` | `modules/ROOT/attachments/contextual-mapping.schema.json` | **patched** |

## Patch: widen `spec.version` enum

Both schemas declare `properties.spec.properties.version.enum =
["R4"]`. The FHIRconnect v1.0.0 specification body does **not**
restrict mappings to FHIR R4 — the schema lags. The vendored copies
widen the enum to `["R4", "R4B", "R5"]`, matching the supported FHIR
releases locked in `featurerequest.md` (Decisions, 2026-05-27):

```diff
-          "type": "string",
-          "enum": [
-            "R4"
-          ]
+          "type": "string",
+          "enum": [
+            "R4",
+            "R4B",
+            "R5"
+          ]
```

`additionalProperties` defaults are left untouched (no widening
required at any node — verified by inspection of both schemas).

The patched schemas remain valid against draft-07
(`$schema: http://json-schema.org/draft-07/schema#`); the change is
purely additive (extends an enum).

**Upstream TODO:** file the same widening as a PR against
`SevKohler/FHIRconnect-spec`. Tracked in
`scratch/0527-01/upstream-fhirconnect-spec-version-enum.md`
(filed by the user — `dev-do` does not open external issues).
