#!/usr/bin/env dotnet script
#nullable enable
// vital-status fixture generator (trampoline).
//
// The real generator now lives in `tools/GenFixtures/` as a console
// app so it does not depend on the `dotnet script` global tool. Run
//
//     dotnet run --project tools/GenFixtures
//
// from the repo root. This file is kept only so the discovery path
// from `tests/fixtures/vital-status/` remains obvious.

throw new System.NotImplementedException(
    "gen-fixtures.csx is retired. Run `dotnet run --project tools/GenFixtures` "
    + "from the repo root instead. See tools/GenFixtures/README.md.");

