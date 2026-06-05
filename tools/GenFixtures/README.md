# GenFixtures

Regenerates the canonical R4 Observation fixture
(`tests/fixtures/vital-status/samples/vital-status.observation.r4.json`)
from the canonical Composition + project bundle by running the
`R4Engine.ToFhir` pipeline against the vendored mapping. Use this
after any mapping change that would alter the produced Observation;
the regenerated file is committed and pinned so test assertions
can compare against a known-good byte image.

## Usage

```pwsh
dotnet run --project tools/GenFixtures
```

CI does **not** run this — it is an authoring tool. Review the diff
before committing the regenerated fixture.
