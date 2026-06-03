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
| `model-mapping.schema.json` | `modules/ROOT/attachments/model-mapping.schema.json` | **patched** (×2) |
| `contextual-mapping.schema.json` | `modules/ROOT/attachments/contextual-mapping.schema.json` | **patched** (×1) |

## Patches

### 1. Widen `spec.version` enum

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

### 2. Add `link` to the model `mapping` definition

The model schema's `$defs.mapping` block declares
`additionalProperties: false` but does not list `link` as one of the
allowed rule keys, so any vendored mapping that uses a `link:` rule
(e.g. `EVALUATION.vital_status.v1.yml` — five `partOfReference`
variants) is rejected. The spec body and real-world mappings both
expect `link`. The vendored model schema adds:

```diff
+        "link": {
+          "type": ["object", "null"],
+          "additionalProperties": false,
+          "properties": {
+            "meaning": { "type": "string" },
+            "type":    { "type": "string" }
+          },
+          "required": ["meaning", "type"]
+        }
```

### Notes

`additionalProperties` defaults are otherwise left untouched (no
widening required at any other node — verified by inspection of both
schemas).

The patched schemas remain valid against draft-07
(`$schema: http://json-schema.org/draft-07/schema#`); both changes
are purely additive.

**Upstream TODO:** file both patches as a combined PR against
`SevKohler/FHIRconnect-spec`. Tracked in
`scratch/0527-01/upstream-fhirconnect-spec-version-enum.md`
(filed by the user — `dev-do` does not open external issues).
