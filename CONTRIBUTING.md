# Contributing to Andreja

`Jamula/Andreja` is public so operators, reviewers, and future users can inspect
its source code, architecture, supply-chain evidence, and governance records.

## External contribution status (Gate B — paused)

External pull requests are **currently paused** under Gate B in
[issue #6](https://github.com/Jamula/Andreja/issues/6)
([`docs/legal/license-evaluation.md`](docs/legal/license-evaluation.md) and
[`docs/legal/licensing-decision-packet.md`](docs/legal/licensing-decision-packet.md)).

- Until inbound contributor terms (CLA/DCO, patent grant alignment, and
  chain-of-title verification) are formally ratified and closed in
  [issue #6](https://github.com/Jamula/Andreja/issues/6), unsolicited external
  pull requests are closed without code review so unreviewed third-party IP does
  not enter the codebase.
- Public issue reports for reproducible defects or documentation drift are
  welcome only if they contain **zero** personal data, credentials, `.env`
  values, bootstrap tokens, recovery codes, passkey ceremony payloads, database
  dumps, third-party connector payloads, or private task/prompt content.
- **Never report security vulnerabilities in public issues or pull requests.**
  Follow [`SECURITY.md`](SECURITY.md) to submit a private GitHub Security
  Advisory (`https://github.com/Jamula/Andreja/security/advisories/new`).

## Authorized maintainer and `.squad/` workflow

Authorized changes by repository owner Cyrus Jamula and the repository's
[`.squad/`](.squad) specialist team ([`meet-the-squad.md`](meet-the-squad.md))
follow [`docs/operating-model.md`](docs/operating-model.md) and strict
governance and verification gates:

1. **Roadmap milestones and issue tracking:**
   Work is organized across the 15 roadmap milestones defined in
   [`docs/plan.md`](docs/plan.md) and sequenced in
   [`docs/roadmap/full-vision-completion-roadmap.md`](docs/roadmap/full-vision-completion-roadmap.md)
   (`Phase 0`, `Phase 1A`, `Phase 1B`, `Phase 2`, `Phase 3A`, `Phase 3B`, and
   `Phase 4` through `Phase 12`). Lifecycle labels (`status:*`, `blocked:*`,
   `blocks:*`) are maintained manually, while `squad` / `squad:*` specialist
   routing labels are managed as documented in
   [`docs/help/issue-status.md`](docs/help/issue-status.md).
2. **Human gates and `$0` cloud rule:**
   `Phase 0` provisions no cloud accounts, paid services, free tiers, or trials
   without explicit human approval from Cyrus Jamula
   ([`docs/cost-model.md`](docs/cost-model.md)). Agents never self-approve human
   gates, activate live external providers, or treat issue closure as
   release/ratification authority. ADRs marked `Proposed` (for example
   [ADR 0006](docs/adr/0006-charter-ratification.md),
   [ADR 0011](docs/adr/0011-default-branch-required-gates.md), and
   [ADR 0012](docs/adr/0012-owner-device-content-encryption.md)) are design
   proposals only and do not take normative effect unless explicitly accepted.
3. **Independent reviewer lockout:**
   Security, privacy, legal, supply-chain, and governance artifacts require
   independent specialist review under
   [`.github/skills/reviewer-protocol/SKILL.md`](.github/skills/reviewer-protocol/SKILL.md)
   before squash-merging into protected `main`. When a reviewer rejects an
   artifact, the original author is locked out from revising that artifact and a
   different specialist must own the revision.
4. **SHA-256 hash-locked governance baselines:**
   [`docs/plan.md`](docs/plan.md) (hash-locked in
   [`docs/adr/0000-plan-ratification.md`](docs/adr/0000-plan-ratification.md)),
   the 12 Phase 0 status artifacts listed in `docs/plan.md`, and accepted
   Phase 1A ADRs (`0001`–`0005` in
   [`docs/phase-1a/packet-decision-66.md`](docs/phase-1a/packet-decision-66.md))
   are verified by
   [`.github/scripts/check_docs_consistency.py`](.github/scripts/check_docs_consistency.py).
   Do not modify hash-locked governance files without an authorized decision and
   matching hash/amendment update.
5. **Pull request template and evidence:**
   Every pull request must use
   [`.github/pull_request_template.md`](.github/pull_request_template.md),
   preserving its charter-impact state marker, gate checklist, and exact
   validation commands with pass/fail results.
6. **Zero secrets or personal data in Git or `.squad/`:**
   Never read live `.env*` files or record secrets, tokens, emails, or personal
   data in source files, tests, commits, or `.squad/` state
   ([`.github/skills/secret-handling/SKILL.md`](.github/skills/secret-handling/SKILL.md)).

## Engineering, C#, and testing standards

- **SDK and build baseline:**
  `.NET 10` SDK `10.0.404` is pinned in [`global.json`](global.json) with
  `rollForward: disable`. [`Directory.Build.props`](Directory.Build.props)
  enforces `Nullable = enable`, `ImplicitUsings = enable`,
  `TreatWarningsAsErrors = true`, `AnalysisLevel = latest-recommended`,
  `AnalysisMode = All`, `EnforceCodeStyleInBuild = true`, and deterministic
  builds; package versions are centrally pinned in
  [`Directory.Packages.props`](Directory.Packages.props).
- **C# and Clean/Onion boundary conventions:**
  Enforced via [`Directory.Build.props`](Directory.Build.props) and
  `dotnet format`: file-scoped namespaces, `sealed` on concrete non-abstract
  types unless designed for inheritance, 4-space indentation, and Allman
  braces. Preserve inward architectural dependencies
  (`Andreja.Platform.Contracts` / `Andreja.Api.Contracts` ← `Andreja.Modules` ←
  `Andreja.Adapters` ← `Andreja.AppHost` / `Andreja.Portability.Cli`), enforced
  by [`tests/Andreja.ArchitectureTests`](tests/Andreja.ArchitectureTests), and
  keep domain and skill contracts free of external provider SDK types, ambient
  credentials, and unconfirmed side effects.
- **Microsoft Testing Platform (`MTP`) and test inventory parity:**
  All four `.NET` test projects use `MSTest.Sdk` `4.5.1` and
  `Microsoft.Testing.Extensions.TrxReport` `2.5.2` on
  `Microsoft.Testing.Platform` (`global.json` test runner). Invoke `dotnet test`
  with `--solution`, `--project`, or `--test-modules`, and emit TRX reports with
  `--report-trx` (never VSTest `--logger`). Adding, removing, or renaming tests
  requires keeping
  [`docs/research/test-suite-current-inventory-112.json`](docs/research/test-suite-current-inventory-112.json)
  in parity via
  [`.github/scripts/test-dotnet-test-parity.ps1`](.github/scripts/test-dotnet-test-parity.ps1)
  (while [`docs/research/test-suite-baseline-112.json`](docs/research/test-suite-baseline-112.json)
  remains immutable historical evidence). Live database integration tests stay
  in [`tests/Andreja.PostgreSqlIntegrationTests`](tests/Andreja.PostgreSqlIntegrationTests)
  outside [`Andreja.slnx`](Andreja.slnx) and run only against disposable
  `andreja_test_*` databases.
- **Automation languages:**
  Prefer `.NET` (`C#`), `Node.js` (`JavaScript` / `TypeScript`), or
  `PowerShell 7` for repository tooling. Do not introduce new Python scripts;
  preserve existing repository-owned documentation checks until migrated through
  a governed PR.

## Local pre-PR validation

Run the service-free validation suite from the repository root before opening or
updating an authorized pull request (see [`docs/development.md`](docs/development.md)
and [`docs/testing-matrix.md`](docs/testing-matrix.md) for live PostgreSQL, OCI,
and browser/offline evidence layers):

```powershell
dotnet restore Andreja.slnx
dotnet restore tests\Andreja.PostgreSqlIntegrationTests\Andreja.PostgreSqlIntegrationTests.csproj

dotnet format Andreja.slnx --verify-no-changes --no-restore
dotnet format tests\Andreja.PostgreSqlIntegrationTests\Andreja.PostgreSqlIntegrationTests.csproj `
  --verify-no-changes --no-restore

dotnet build Andreja.slnx --configuration Debug --no-restore
dotnet build Andreja.slnx --configuration Release --no-restore
dotnet build tests\Andreja.PostgreSqlIntegrationTests\Andreja.PostgreSqlIntegrationTests.csproj `
  --configuration Debug --no-restore
dotnet build tests\Andreja.PostgreSqlIntegrationTests\Andreja.PostgreSqlIntegrationTests.csproj `
  --configuration Release --no-restore

dotnet test --solution Andreja.slnx --configuration Debug --no-build

dotnet test --test-modules tests\Andreja.UnitTests\bin\Release\net10.0\Andreja.UnitTests.dll `
  --no-build `
  --report-trx `
  --report-trx-filename Andreja.UnitTests-Release.trx `
  --results-directory artifacts\Release\test-results\Andreja.UnitTests
dotnet test --test-modules tests\Andreja.ArchitectureTests\bin\Release\net10.0\Andreja.ArchitectureTests.dll `
  --no-build `
  --report-trx `
  --report-trx-filename Andreja.ArchitectureTests-Release.trx `
  --results-directory artifacts\Release\test-results\Andreja.ArchitectureTests
dotnet test --test-modules tests\Andreja.PublicSiteTests\bin\Release\net10.0\Andreja.PublicSiteTests.dll `
  --no-build `
  --report-trx `
  --report-trx-filename Andreja.PublicSiteTests-Release.trx `
  --results-directory artifacts\Release\test-results\Andreja.PublicSiteTests

pwsh -NoProfile -File .github\scripts\test-dotnet-test-parity.ps1 `
  -Configuration Release `
  -ResultsRoot artifacts\Release\test-results `
  -InventoryPath docs\research\test-suite-current-inventory-112.json `
  -OutputPath artifacts\Release\test-inventory-parity.json

python .github\scripts\check_docs_consistency.py
python .github\scripts\check_docs_consistency.test.py
python -m unittest discover -s tests -p "test_*.py"
node --test .github\scripts\squad-readiness.test.js
pwsh -NoProfile -File scripts\operations\validate-contract.ps1
pwsh -NoProfile -File scripts\supply-chain\Test-SupplyChainPolicy.ps1
```
