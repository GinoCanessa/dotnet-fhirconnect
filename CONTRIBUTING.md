# Contributing

## Build, test, pack

```bash
dotnet restore DotnetFhirConnect.slnx
dotnet build   DotnetFhirConnect.slnx -c Release --no-restore
dotnet test    DotnetFhirConnect.slnx -c Release --no-build

# Library + CLI packages
dotnet pack src/DotnetFhirConnect      -c Release --no-build
dotnet pack src/DotnetFhirConnect.Cli  -c Release --no-build
```

The CI workflow at `.github/workflows/ci.yml` runs the same three
commands on `ubuntu-latest` + `windows-latest`.

## .NET version

`global.json` pins the SDK to `10.0.108` with
`rollForward: latestMajor`. Any installed .NET 10 SDK satisfies it;
on Windows: `winget install Microsoft.DotNet.SDK.10`.

## Style

- See [`.editorconfig`](.editorconfig) and the repo conventions in
  [`.github/copilot-instructions.md`](.github/copilot-instructions.md).
- **Explicit types** (the rule is enforced as an error).
- `[]` for empty / simple collection initializers (suggestion-level
  — the analyzer false-positives on `IEnumerable` types like
  `YamlStream`).
- File-scoped namespaces.
- xUnit v3 for tests; one test class per behavioural slice.
- Conventional commits: `feat(scope):`, `fix(scope):`, `test(scope):`,
  `docs(scope):`, `chore(scope):`, `build(scope):`, `ci(scope):`,
  `refactor(scope):`, `perf(scope):`.
- All commits include the repo co-author trailer:

  ```text
  Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
  ```

## DotnetOpenEhr posture

The library floats `DotnetOpenEhr` to the latest beta in the
`2026.*-*` line via central package management
(`Directory.Packages.props`). **Do not** introduce a NuGet lock file
(`packages.lock.json`) or run restore in locked mode — both would
silently freeze the floating range to first-restore.

## Sub-directories

- `src/` — shipping projects (set `_IsShippingProject=true` via
  `src/Directory.Build.props`; AOT/trim flags and package metadata
  apply).
- `tests/` — non-shipping xUnit projects with `IsTestProject=true`;
  pick up the `tests/fixtures/` tree automatically via
  `Directory.Build.targets`.
- `tests/fixtures/` — vendored upstream sources (Apache-2.0;
  attributed in `NOTICE` and per-fixture `PROVENANCE.md`).
- `scratch/` — local-only working notes (git-ignored); home of the
  current `plan.md` and upstream-issue drafts.
- `support/` — local mirrors of openEHR / FHIR specs (git-ignored).
