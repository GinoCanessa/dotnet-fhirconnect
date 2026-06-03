#!/usr/bin/env dotnet script
#nullable enable
// vital-status fixture generator (stub).
//
// Intent: re-derive samples/vital-status.observation.r4.json from
// samples/vital-status.composition.canonical.json by walking the
// FHIRconnect mapping bundle the engine walks. Keeps the two
// fixtures honest and prevents silent drift across edits.
//
// Status: STUB. The real implementation lands in Phase 6a, once
// FhirConnectEngine is wired up. For Phase 2 this file exists so the
// generator path is on the radar and provenance is captured.
//
// Usage (once implemented):
//   dotnet script tests/fixtures/vital-status/gen-fixtures.csx
//
// CI does not run this script; humans run it after a mapping change
// to refresh the Observation fixture.

throw new System.NotImplementedException(
    "gen-fixtures.csx is a Phase 2 stub. The real implementation lands in Phase 6a "
    + "(dev-do plan scratch/0527-01/plan.md), which uses FhirConnectEngine to "
    + "regenerate vital-status.observation.r4.json from the canonical composition.");
