# OCI Dependency Gate Historical Finding Diagnosis (#211)

- **Tracking issue:** [#211](https://github.com/Jamula/Andreja/issues/211) (`[M02.01.01]`)
- **Status:** Diagnostic evidence recorded and fail-closed scanner provenance hardened; issue `#211` remains open pending independent security review and human-gated `v*` keyless Sigstore release verification (`ADR 0010`).

## 1. Historical Failure Context

- **Observed failure:** GitHub Actions run [`38788424764` / job `116606516741`](https://github.com/Jamula/Andreja/actions/runs/38788424764/job/116606516741) on `2026-10-15T18:52:16Z` (synthetic merge of PR head `e9e87d03d38f8208f83138fa0bea1ef5030441ad` into base `d9d7d89c9d52d6d6fc2a7240d14a522476a2f2c2`) failed closed with:
  `Dependency scan contains 12 forbidden finding(s).`
- **Source diff boundary:** `git diff d9d7d89c9d52d6d6fc2a7240d14a522476a2f2c2 e9e87d03d38f8208f83138fa0bea1ef5030441ad` changed only `docs/phase-1a/evidence-44.md`. Both commits shared identical `Dockerfile`, `supply-chain-policy.json` (`mcr.microsoft.com/dotnet/sdk:10.0.301-noble`, `mcr.microsoft.com/dotnet/aspnet:10.0.12-noble-chiseled`), and `Directory.Packages.props` (`10.0.12` ASP.NET Core / EF Core packages).
- **Historical evidence gap:** Run `38788424764` retained zero uploaded artifacts and its failure exception emitted only the integer count (`12`) without package/CVE identifiers or Grype advisory-database build/checksum metadata. The exact October 15 advisory-database snapshot and its 12-row inventory therefore cannot be reconstructed retroactively and are not claimed.

## 2. Matched Isolated `linux/amd64` Reproduction (`2026-11-14`)

On an isolated `linux/amd64` runner ([`#211` comment `6874369718`](https://github.com/Jamula/Andreja/issues/211#issuecomment-6874369718)), both historical base `d9d7d89c9d52d6d6fc2a7240d14a522476a2f2c2` and PR head `e9e87d03d38f8208f83138fa0bea1ef5030441ad` were built with Buildx `0.39.0` / BuildKit `0.34.2` and scanned offline against one frozen Grype database snapshot:

- **Syft:** `1.28.0` (`anchore/syft@sha256:bc71d110d271c823b3e3c58702aa8ad6bf06e2abd3c1ff7c8966420a9a57dc00`)
- **Grype:** `0.100.0` (`anchore/grype@sha256:dcf0168c9b275c6dbcbff5ceeecfac29a6ccf138a87f0adc9ce5bd69fe6146f3`)
- **Frozen Grype DB:** schema `v6.1.10`, built `2026-11-14T06:19:25Z`, SHA-256 `ad4ef5a2d7ec9ee89b5571018ff6c444fc269d3f576d2f4d2f32d3433d12f413`
- **Matched result:** Identical `15` forbidden matches (`13` `HIGH`, `2` `CRITICAL`) across both commits, all concentrated in the historical `.NET` / ASP.NET Core `10.0.12` packages, confirming deterministic parity between historical base and PR head under controlled scanner/DB inputs.

## 3. Remediation & Diagnostic Hardening

1. **Runtime & NuGet baseline upgrade (`PR #296`, `#291`–`#293`):** Upgraded `global.json` and `Dockerfile` / `supply-chain-policy.json` to `.NET SDK 10.0.404` (`mcr.microsoft.com/dotnet/sdk:10.0.404-noble@sha256:fe0fd4e93bd2fbb47c883b7a6369e5c8d3db8bf01cdc5981f6ee35fbdf3ff4fb`), `mcr.microsoft.com/dotnet/aspnet:10.0.15-noble-chiseled@sha256:7c625e44e5d588b4d7b2a913e94ca4bd5098ce47442da404829e4c7936a764e1`, and `10.0.15` NuGet packages. Hosted unsigned OCI runs (`43695921787` and subsequent PR checks) report zero forbidden findings (`HIGH` / `CRITICAL` thresholds preserved without exception).
2. **Sanitized scanner & advisory-DB provenance on failure (`scripts/supply-chain/SupplyChain.Common.psm1`, `New-OciEvidence.ps1`):**
   - `Assert-NoForbiddenGrypeFinding` emits escaped, content-free `(package, advisory, version, severity)` entries **plus** sanitized Grype database provenance (`scannerVersion`, `dbSchemaVersion`, `dbBuilt`, `dbChecksum`) from `descriptor.db` while omitting filesystem locations and raw match details.
   - `Assert-NoForbiddenTrivyFinding` emits escaped `(id, type, severity)` summaries for Container/IaC violations while omitting file paths and raw config snippets.
   - Verified fail-closed by `scripts/supply-chain/Invoke-NegativeTests.ps1`.

## 4. Remaining Human & Release Boundary

- Passing `hosted-unsigned-validation` checks do **not** authorize production startup, update, or release, and do not close `#211` on their own.
- Closure of `#211` remains gated on independent security review and human-authorized `refs/tags/v*` keyless Sigstore signing and offline verification (`Test-OciEvidence.ps1 -ExpectedSigningMode keyless-sigstore`).
