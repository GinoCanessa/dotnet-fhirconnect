# Security Policy

## Supported versions

`dotnet-fhirconnect` is **pre-alpha (v0.x)** — there is no
guaranteed supported release line. Stability and security are
explicitly best-effort until a `1.0.0` is tagged.

## Reporting a vulnerability

Please use GitHub's
[private vulnerability reporting](https://docs.github.com/en/code-security/security-advisories/guidance-on-reporting-and-writing-information-about-vulnerabilities/privately-reporting-a-security-vulnerability)
on this repository:

1. Open the **Security** tab on
   <https://github.com/ginoc/dotnet-fhirconnect>.
2. Click **Report a vulnerability** and fill in the form with as
   much repro detail as you can share safely.

If you cannot use that flow, open a regular issue tagged
`security` and we will follow up with a private channel.

## Scope reminder

This project transforms data between openEHR and FHIR — it does not
run as a service, perform authentication, or persist data. Most
classes of vulnerability that apply to hosted services do not apply
here. Issues we are interested in:

- Memory safety / parse panics in the YAML or JSON intake paths
  (`MappingYamlReader`, `YamlJsonBridge`, the R4 adapter parsers).
- Validator bypasses that mark genuinely malformed mappings as
  valid.
- Path-traversal in the CLI's `--mapping` / `--input` / `--output`
  arguments.

Things we are not in scope for in v0.x:

- AOT publish failures (the library is not AOT-publishable; see
  `docs/architecture.md`).
- FHIR ↔ openEHR semantic-equivalence bugs (the engine is a
  walking skeleton and known to be partial).
