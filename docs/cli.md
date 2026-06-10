# CLI reference

`dotnet fhirconnect` is the command-line front end for
`dotnet-fhirconnect`. It is built on `System.CommandLine` and is a thin
wrapper around the library — every flag maps directly to a library
call. Install it from local pack output the same way as the
[README quickstart](../README.md#quickstart--cli):

```bash
dotnet pack src/DotnetFhirConnect.Cli -c Release
dotnet tool install --add-source ./src/DotnetFhirConnect.Cli/bin/Release --global DotnetFhirConnect.Cli
```

Two verbs are available: [`validate`](#validate) and
[`transform`](#transform). `--help` is auto-generated for the root
command and each verb.

## `validate`

Validate a FHIRconnect mapping file or bundle directory against the
embedded FHIRconnect v1.0.0 JSON schemas plus the semantic rules
(`FCV001`–`FCV900`).

| Flag | Alias | Required | Description |
|------|-------|----------|-------------|
| `--mapping` | `-m` | yes | Path to a FHIRconnect mapping file or bundle directory. |
| `--format` | | no | Output format: `text` (default) or `json`. |

The **text** report prints `Valid.` when the bundle is clean, or
`Invalid — N issue(s):` followed by one line per issue
(`[<Severity> <Code>] <filePath>:<line> <pointer> — <message>`).

The **json** report emits a single object:

```json
{
  "isValid": false,
  "issues": [
    {
      "filePath": "…",
      "pointer": "/spec/version",
      "line": 3,
      "column": 12,
      "severity": "Error",
      "code": "FCV...",
      "message": "…"
    }
  ]
}
```

Example:

```bash
dotnet fhirconnect validate --mapping tests/fixtures/vital-status --format text
```

## `transform`

Transform between an openEHR Composition and a FHIR resource in either
direction via a FHIRconnect mapping bundle.

| Flag | Alias | Required | Description |
|------|-------|----------|-------------|
| `--direction` | `-d` | yes | `to-fhir` or `to-openehr`. |
| `--mapping` | `-m` | yes | Mapping bundle root. A directory with `model/` + `project/` siblings is auto-merged. |
| `--input` | `-i` | yes | Input file: openEHR canonical JSON for `to-fhir`, FHIR JSON for `to-openehr`. |
| `--output` | `-o` | yes | Output file path, or `-` to write to stdout. |

### openEHR → FHIR

```bash
dotnet fhirconnect transform \
    --direction to-fhir \
    --mapping tests/fixtures/vital-status \
    --input tests/fixtures/vital-status/samples/vital-status.composition.canonical.json \
    --output -
```

### FHIR → openEHR

```bash
dotnet fhirconnect transform \
    --direction to-openehr \
    --mapping tests/fixtures/vital-status \
    --input tests/fixtures/vital-status/samples/vital-status.observation.r4.parseable.json \
    --output -
```

> The `to-openehr` input must be `vital-status.observation.r4.parseable.json`
> (it carries `Observation.status: "final"`), **not** the engine-emitted
> `vital-status.observation.r4.json`, which Firely rejects on the inbound
> parse (exit code `2`).

## Exit codes

Exit codes follow `sysexits.h` conventions where possible.

| Code | Meaning |
|------|---------|
| `0` | Success. |
| `1` | Validation failed — the report has at least one error. |
| `2` | I/O or parse error (file missing, malformed YAML/JSON). |
| `3` | Transform / runtime error inside the engine. |
| `64` | Usage error — bad arguments, unknown verb, unknown direction. |

## OPT / element names

The CLI exposes **no** `--template` option, so `transform --direction
to-openehr` output carries bare at-code element names. Friendly openEHR
element names require the library-only `ToOpenEhr(object, Composition)`
seam. See [Limitations & scope](../README.md#limitations--scope).
