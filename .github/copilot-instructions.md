# Copilot Instructions

## Subagent Model Configuration

Any subagents must use the same model and configuration as the spawning
agent (e.g., if the user has specified `claude-opus-4.6 (high)`, all
subagents must also use that configuration).

## General Preferences

- **Language / framework:** C# 14 / .NET 10 for the library and the CLI.
- **Style:**
  - Use **explicit types** instead of `var` (enforced by `.editorconfig`).
  - Use `[]` for empty / simple collection initializers (instead of
    `new List<T>()` or `new()`).
  - File-scoped namespaces, expression-bodied members where natural.
- **No HTTP / TLS plumbing here.** This repo ships a library + CLI;
  the "HTTP-only, no UseHttpsRedirection/UseHsts" preference applies
  when (and only when) a hosted service is added.
- **Commits:** conventional commits (`feat`, `fix`, `refactor`, `test`,
  `chore`, `docs`, `build`, `ci`, `perf`). Scope optional but
  encouraged. Always include the repo co-author trailer:
  `Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`
- **Tests:** xUnit (v3). Always run `dotnet test DotnetFhirConnect.slnx`
  after changes. Tests live under `tests/`.
- **Build:** `dotnet build DotnetFhirConnect.slnx -c Release`.
  `TreatWarningsAsErrors=true` is on; treat AOT/trim warnings as
  build-breaking. Escape hatch (for SDK-induced warnings only) is
  `[RequiresUnreferencedCode]` on the touching method, never a global
  TWAE flip.

## Repo-Specific Pointers

- **Plan / source-of-truth for in-flight work:** `scratch/0527-01/plan.md`.
- **Local spec mirrors (prefer over web fetches):**
  - openEHR: `C:\ai\support\openEHR\`
  - FHIR: `C:\ai\support\fhir-r4\`, `C:\ai\support\fhir-r4b\`,
    `C:\ai\support\fhir-r5\`
  If a needed openEHR spec file is missing locally, prompt the user
  to add it rather than silently fetching from the web. For FHIR the
  local mirrors are expected to be complete — flag missing files so
  the local copy can be refreshed.
- **Reference repos (read-only, not dependencies):**
  - `C:\ai\git\FHIRconnect-spec`
  - `C:\ai\git\openfhir`
  - `C:\ai\git\FHIRconnect-mapping-lib`
- **Sister .NET repo for layout conventions:**
  `C:\ai\git\dotnet-openehr-sdk`.

## DotnetOpenEhr Dependency Posture

- **Float to latest.** `DotnetOpenEhr` is pinned in
  `Directory.Packages.props` to a floating range (currently
  `2026.*-*`). Do **not** introduce a `packages.lock.json` or run
  restore in locked mode — the floating range would silently freeze.
- **Umbrella package only.** Reference `DotnetOpenEhr`, not the
  sub-packages. Trade-off explicitly accepted in `featurerequest.md`.
- **No internal seam over the SDK.** Call typed RM, AQL,
  serialization, and template APIs directly throughout.

## Solution / NuGet Conventions

- Solution file is `DotnetFhirConnect.slnx` (slnx format).
- Shipping projects under `src/` set `_IsShippingProject=true` so
  `Directory.Build.props` flips on AOT/trim flags and package
  metadata.
- Central package management is on; **never** put `Version` on a
  `PackageReference` — bump the version in `Directory.Packages.props`.
- Test projects set `<IsTestProject>true</IsTestProject>` and inherit
  fixture copy from `Directory.Build.targets`.
