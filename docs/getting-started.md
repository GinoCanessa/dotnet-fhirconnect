# Getting started

This guide walks through using `dotnet-fhirconnect` to load a vendored
FHIRconnect mapping bundle, validate it, and run it through the
walking-skeleton engine to produce a FHIR R4 Observation from an
openEHR Composition.

## Prerequisites

- **.NET 10 SDK** (`global.json` pins `10.0.108`, rolls forward to
  the latest installed `10.x.y`).
- An openEHR Composition in canonical JSON form (the repo ships one
  at [`tests/fixtures/vital-status/samples/vital-status.composition.canonical.json`](../tests/fixtures/vital-status/samples/vital-status.composition.canonical.json)).
- The corresponding FHIRconnect mapping bundle ([`tests/fixtures/vital-status/`](../tests/fixtures/vital-status/) here).

## Walkthrough

### 1. Load the mapping bundle

The vital_status bundle lives in two directories under the fixture
root: `model/` for the archetype mapping, and `project/` for the
context bundle plus extensions. The convenience `R4Engine` constructor
expects a single `MappingBundle`, so callers merge the two:

```csharp
ModelMapping vitalStatus = (ModelMapping)FhirConnectMapping.Load(
    "tests/fixtures/vital-status/model/vital_status.v1.yml");

MappingBundle project = (MappingBundle)FhirConnectMapping.Load(
    "tests/fixtures/vital-status/project");

Dictionary<string, ModelMapping> models =
    new Dictionary<string, ModelMapping>(project.Models)
    {
        [vitalStatus.Metadata.Name] = vitalStatus,
    };
MappingBundle bundle = new MappingBundle(project.Context, models, project.Extensions);
```

The CLI's `transform --mapping <dir>` does this auto-merge for you when
the directory contains `model/` + `project/` siblings.

### 2. Validate

```csharp
ValidationReport report = FhirConnectValidator.Validate(
    "tests/fixtures/vital-status/project");
if (!report.IsValid)
{
    foreach (ValidationIssue issue in report.Issues)
    {
        Console.WriteLine($"[{issue.Severity} {issue.Code}] {issue.Pointer}: {issue.Message}");
    }
    return;
}
```

Same idea from the command line:

```bash
dotnet fhirconnect validate --mapping tests/fixtures/vital-status --format text
```

### 3. Transform

```csharp
string compositionJson = File.ReadAllText(
    "tests/fixtures/vital-status/samples/vital-status.composition.canonical.json");
Composition composition = OpenEhrJson.ParseComposition(compositionJson)!;

R4Engine engine = new R4Engine(bundle);
Observation observation = (Observation)engine.ToFhir(composition);

Console.WriteLine($"effective:  {observation.Effective}");
Console.WriteLine($"value:      {((CodeableConcept)observation.Value).Coding.First().Code}");
Console.WriteLine($"performers: {observation.Performer.Count}");
```

Same from the CLI, writing the serialised Observation to stdout:

```bash
dotnet fhirconnect transform \
    --direction to-fhir \
    --mapping tests/fixtures/vital-status \
    --input tests/fixtures/vital-status/samples/vital-status.composition.canonical.json \
    --output -
```

## Adding your own mapping

1. Drop the FHIRconnect YAML files (`<name>.context.yaml`, model
   mapping, any extensions) under a single directory tree.
2. Run `dotnet fhirconnect validate` against the directory — fix any
   issues the validator surfaces (it understands the FHIRconnect
   v1.0.0 JSON schemas plus a small set of semantic rules; see
   [`docs/architecture.md`](architecture.md)).
3. Wire up your input composition shape — typed `Composition` from
   `DotnetOpenEhr.Rm`, canonical JSON via `OpenEhrJson.ParseComposition`,
   or flat JSON via `OpenEhrFlatJson.ParseComposition`.
4. Pass everything to `R4Engine.ToFhir`.

If your model file uses rule kinds beyond what `v0.x` supports today
(see the engine notes in [architecture.md](architecture.md)), the
engine silently skips the unsupported rules — file an issue or extend
the rule executor.

## Known v0.x limitations

- **FHIR → openEHR is not implemented.** `ToOpenEhr` throws
  `NotSupportedException`; `transform --direction to-openehr`
  returns exit code 3 with an explanatory message.
- **Extension rules** (`reference`, `slotArchetype`, `extension: add|
  overwrite|remove`) are loaded but not applied. This means Observations
  produced from the vital_status mapping lack the `code` /
  `category` fields that the `KDS_vital_status` extension would
  otherwise inject. Round-trip semantics depend on Phase 6b.
- **R4B / R5 adapters are pending stubs.** Constructing one for a
  bundle whose `spec.version` is `R4B` or `R5` succeeds; calling its
  methods throws `NotImplementedException` with a release-tagged
  message.
- **AOT publish is not supported.** Firely, YamlDotNet, and the
  DotnetOpenEhr SDK each have reflection-based code paths; the
  library carries `[RequiresUnreferencedCode]` accordingly.
