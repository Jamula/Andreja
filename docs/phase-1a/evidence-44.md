# Phase 1A evidence run 44

- **Status:** Blocking evidence run; Phase 1A exit is **not** claimed
- **Issues:** [#44](https://github.com/Jamula/Andreja/issues/44),
  [#62](https://github.com/Jamula/Andreja/issues/62), and blocking defect
  [#87](https://github.com/Jamula/Andreja/issues/87)
- **Execution window:** 2026-08-24 15:41–17:14 PDT
- **Evidence coordinator:** Data
- **Operations owner:** Jett Reno
- **Security/privacy challenge:** Tuvok and Deanna Troi
- **Final exit and residual-risk authority:** Cyrus

This record contains only synthetic local evidence. No cloud resource, account,
subscription, public deployment, paid model, user data, reusable credential,
cookie, recovery code, private key, database dump, or private OCI layer is
committed or attached. Ignored runtime artifacts were destroyed after recording
the bounded results below.

## Provenance and versions

| Item | Recorded value |
| --- | --- |
| Live `main` and branch base | `4cf532ef763a1709589ac8d0cf004b7404b8b5b4` |
| Audited implementation commit | `6bfddbc1556e7dc587e44a77a10278382a1e45b0` |
| Audited source tree | `65f3a0e468b0de593f4c62e939583715b21914f4` |
| OCI platform | `linux/arm64` |
| OCI manifest | `sha256:c222231aee9ba30208be27c024f8f534ae998066392f129dc9676302cdfac079` |
| OCI config | `sha256:ca311e7267f5e3a767769d8b665836c7971adb85d0f465e191ec30818fcb2360` |
| Reproducibility | two no-cache builds produced the same manifest digest |
| SPDX 2.3 | `sha256:4e7bf1bb5ad2f44ea54cfa2b375f2845c691ffad2ffa1ff2d55a828ce060c515` |
| CycloneDX 1.6 | `sha256:dcf259b0177c84e38ee7eed1cc10c8df0582dee4037b97f72045881f8ad8cc04` |
| Dependency/final-image/IaC scans | Grype/Grype/Trivy: zero forbidden High/Critical findings |
| SDK/runtime | .NET SDK `10.0.301`; Docker `29.7.2`; Buildx `0.36.1`; Compose `5.4.0` |
| Scanners | Syft `1.28.0`; Grype `0.100.0`; Trivy `0.67.2`; Cosign `2.6.1` |
| Browser harness | Node `22.23.2`; Edge `152.0.4191.41`; CDP virtual CTAP2 authenticator |
| PostgreSQL | `17.6-bookworm@sha256:f3bd19c606e442c3d7bdfa8002e03fe260a1023351e0ea4598032022b68dd6e3` |
| TLS proxy | `caddy@sha256:4c6e91c6ed0e2fa03efd5b44747b625fec79bc9cd06ac5235a779726618e530d` |

The OCI bundle is **unsigned hosted-style evidence**. Local
`Test-OciEvidence.ps1` correctly rejected `-AllowUnsignedHostedEvidence` because
this was not a hosted CI run. No operator trust anchor was available, and the
agent did not generate or self-approve one. The image is audited and
reproducible, but it is not authorized for release, update, or production
startup.

The executed configuration hashes were:

| Configuration | SHA-256 |
| --- | --- |
| `compose.yaml` | `1d798c23f121b399e344bc1c3e832020042f2b907d0b9502d1083e75006fa023` |
| `deploy/compose.evidence.yaml` | `f06b21d940f54d04f531e96a3b40737ebb0e3ddcea127cb94db431ed9ed3e25e` |
| `deploy/compose.offline-evidence.yaml` | `58120166a3ce19ebb57421717300589cfa1e98764e7fe0cb7b682ea7d147f1f6` |
| `deploy/Caddyfile.evidence` | `de8eff3c1e1f574a6b950e2b3afd003af346483f00f7fff32b6f49586e259f6d` |
| `deploy/otel-collector.yaml` | `0f3c290142ec39c1fedd931992edf6d2bc907c84c8229243f5682946699fdb93` |
| Reviewed migration SQL | `4573a249a23f384cb58f9693cc06eebbffec0f4913f0f0fcef1fb76d1a800b4f` |

The local certificate's public trust-anchor SHA-256 was
`b53d79bb145f22df1e23bd5c192de7d31eafaf681becfe6947b628cc0d7b08c3`.
The browser trusted only its exact SPKI; `curl --cacert` trusted only that public
certificate. The private key stayed ignored and was deleted.

## Reproduction boundary

Create random PostgreSQL/bootstrap values and a short-lived localhost
certificate outside Git. On Docker Desktop, stage the bootstrap token into a
volume with the required Linux identity and mode; do not weaken the application
check:

```powershell
docker volume create `
  --label com.docker.compose.project=andreja `
  --label com.docker.compose.volume=evidence-bootstrap `
  andreja_evidence-bootstrap
$source = (Resolve-Path .andreja\bootstrap_token_source).Path
docker run --rm --network none --user 0 --entrypoint /bin/sh `
  --mount type=volume,source=andreja_evidence-bootstrap,target=/secure `
  --mount "type=bind,source=$source,target=/source,readonly" `
  caddy@sha256:4c6e91c6ed0e2fa03efd5b44747b625fec79bc9cd06ac5235a779726618e530d `
  -c 'cp /source /secure/bootstrap_token &&
      chown 1654:1654 /secure/bootstrap_token &&
      chmod 0400 /secure/bootstrap_token'
```

With `.env` pointing only to ignored files and immutable images:

```powershell
docker compose -f compose.yaml -f deploy\compose.evidence.yaml config --quiet
docker compose up --detach postgres
dotnet ef migrations script --idempotent `
  --project src\Adapters\Andreja.Adapters\Andreja.Adapters.csproj `
  --context AndrejaIdentityDbContext `
  --output .andreja\reviewed-migration.sql
$approvedMigrations = @(
  '20260824031732_InitialIdentityTenancy'
  '20260824043341_Phase1AOpenLoopsTasks'
  '20260824075115_ProductionPasskeyIdentity'
  '20260824102012_DurableRecentAuthenticationGrants'
  '20260824154149_DurableProposalConfirmation'
)
& scripts\operations\migrate-database.ps1 `
  -BackupDumpPath backups\postgres\andreja-pre-migration.dump `
  -ReviewedMigrationScriptPath .andreja\reviewed-migration.sql `
  -DatabaseName andreja `
  -ApprovedMigrations $approvedMigrations `
  -ConfirmBackupRestoreAndMigrationReview
docker compose --profile evidence -f compose.yaml `
  -f deploy\compose.evidence.yaml up --detach
curl.exe --fail --cacert .andreja\localhost.pem `
  https://localhost:8443/health/ready
$env:ANDREJA_BOOTSTRAP_TOKEN_FILE =
  (Resolve-Path .andreja\bootstrap_token_source).Path
pwsh -NoProfile -File scripts\evidence\Test-TelemetryEvidence.ps1
```

The migration command applied exactly five approved migrations. It emitted a
known chiseled-image warning that `libgssapi_krb5.so.2` was absent; PostgreSQL
password authentication and all migrations still completed. This warning was
not represented as a clean log.

The audited OCI command was:

```powershell
pwsh -NoProfile -File scripts\supply-chain\New-OciEvidence.ps1 `
  -OutputDirectory artifacts\supply-chain-e44-r3 `
  -Platform linux/arm64 -HostedUnsigned
```

The resulting ignored bundle and reports are not distributable evidence. A
separately trusted operator must rerun without `-HostedUnsigned` and supply the
approved external signing key and trust anchor.

### Exact offline reproduction

Acquire and verify every signed artifact first. The local run used the audited
but unsigned bundle only as non-release evidence; that does not authorize
startup. Preload the exact archive and all four pinned support images while
networking is available, then set `.env` to the audited immutable reference:

```powershell
docker load --input artifacts\supply-chain-e44-r3\image.oci.tar
$env:ANDREJA_IMAGE =
  'andreja-local@sha256:c222231aee9ba30208be27c024f8f534ae998066392f129dc9676302cdfac079'
# Persist the same value in the ignored .env used by Compose.

$files = @(
  'compose.yaml'
  'deploy\compose.evidence.yaml'
  'deploy\compose.offline-evidence.yaml'
)
docker compose --profile evidence $(
  $files | ForEach-Object { '--file'; $_ }
) config --images | ForEach-Object { docker image inspect $_ *> $null }

pwsh -NoProfile -File scripts\evidence\Test-OfflineEvidence.ps1 `
  -AuditedAppImage $env:ANDREJA_IMAGE `
  -DestroySyntheticVolumes
```

The script uses those three Compose files, `up --detach --pull never`, and no
pull or registry fallback. It requires both `${COMPOSE_PROJECT_NAME:-andreja}_edge`
and `${COMPOSE_PROJECT_NAME:-andreja}_backend` to report `Internal=true`,
requires live/ready before and after `docker compose restart app`, and runs a
preloaded pinned Caddy helper in the app network namespace. The helper must
reach both loopback health endpoints and must fail to connect to reserved
TEST-NET address `192.0.2.1`. It always stops the stack; the switch above also
removes the synthetic volumes.

Expected single-line result after cleanup:

```json
{"status":"passed","imagesPreloaded":5,"auditedAppImage":"andreja-local@sha256:c222231aee9ba30208be27c024f8f534ae998066392f129dc9676302cdfac079","pullPolicy":"never","edgeInternal":true,"backendInternal":true,"startupHealthy":true,"restartHealthy":true,"testNetEgressBlocked":true}
```

## Pass/fail matrix

| Gate | Result | Evidence and exclusion |
| --- | --- | --- |
| Clean/live-main precondition | **PASS** | Worktree started clean at live `main` `4cf532e`; no Squad runtime was invoked. |
| Unit and architecture | **PASS** | Release: 242 unit + 18 architecture; Debug: 246 unit + 18 architecture; zero failed/skipped. |
| PostgreSQL integration | **PASS** | Pinned 17.6 disposable database: 16/16, including tenant, concurrency, crash, restart, and idempotency vectors. |
| OCI reproducible build and scans | **PASS (audit only)** | Two equal ARM64 manifests; both SBOMs; dependency, image, and IaC scans reported zero forbidden findings. |
| Trusted OCI authorization | **BLOCKED** | Unsigned local hosted-style evidence was rejected. No separately trusted operator key was available. |
| Explicit migration and readiness | **PASS** | Empty schema, reviewed SQL digest, exact five-ID approval, no startup migration; direct and TLS `/health/live` and `/health/ready` returned 200. |
| Local TLS/reverse proxy | **PASS** | Exact `172.30.44.10` trusted proxy, exact `https://localhost:8443` origin, scoped public trust anchor, replacement forwarded headers, no trust-all callback. |
| Passkey bootstrap/sign-in/recovery | **PASS** | Real Edge CDP virtual CTAP2 authenticator: one-time bootstrap, sign-out/sign-in, lost-credential recovery, replacement sign-in, and used-code replay rejection. Recovery values were held only in process memory. |
| Open Loops/BYOK seam | **PASS within local scope** | Deterministic browser path completed proposal, confirmation, persistence, list, completion, JSON export, and two-step delete. Unit conformance covers local OpenAI-compatible timeout/cancel/malformed/error/revoke/budget cases. No external provider or paid call ran. |
| Restart/reconnect persistence | **PASS** | Browser observed reconnect UI; completed task, identity cookie, Data Protection state, database state, audit, and subsequent deletion survived app restart. |
| Phone/tablet/desktop and keyboard | **PASS (basic)** | 320/768/1280 had no horizontal overflow, a labelled main region, and zero unnamed interactive controls; six real Tab events reached at least three distinct controls. No human assistive-technology study was performed. |
| PostgreSQL dump/restore | **PARTIAL / BLOCKED for exit** | Pinned `pg_dump`/`pg_restore` clean-instance rehearsal restored 5 migrations, 1 synthetic identity, 1 passkey, 4 security-audit rows, and 0 deleted tasks. The plaintext synthetic dump was checksum-verified and destroyed, but encrypted recovery-set custody plus restored app sign-in with restored keys was not run. |
| Data Protection persistence/at-rest | **PARTIAL / BLOCKED for exit** | One mode-`0600` key file persisted; browser sign-in survived app restart. Host-volume encryption and an encrypted, separately held recovery copy were not independently demonstrated. |
| Application export/import | **PASS (supplemental #87)** | Production local CLI emitted a canonical authenticated archive from a repeatable-read tenant snapshot; exact-schema dry-run and serializable clean-instance commit passed against two disposable PostgreSQL 17 databases. Source/target portable counts matched `1|1|1|1|1|1|1|1|1`; the user-visible task matched; excluded credential/passkey/recovery/grant/token/login/bootstrap rows totaled zero; identical retry was idempotent. No real data or archive was retained. |
| Offline startup/no egress | **PASS for local evidence** | `Test-OfflineEvidence.ps1` verified all 5 immutable images were preloaded, used `--pull never`, required both networks internal, passed audited-image live/ready before and after restart, and rejected a TEST-NET `192.0.2.1` connection from the app network namespace. |
| OTel metrics/traces/logs | **PASS** | Counters were sampled before and strictly after the complete browser flow plus app restart. Accepted spans increased `1 -> 188` (delta `187`, versus the required non-health threshold `10`), metric points `0 -> 119`, and fixed-content logs `1 -> 2`. Prometheus reported policy checks `55`, suppressed attributes `181`, and `sum(violations) or vector(0) = 0`. Unit canaries prove task/prompt/response/token/recovery/raw-ID attributes are removed. |
| Update/rollback | **BLOCKED** | The audited digest started against preserved state, but no second separately approved and signed revision existed. Restart or replacing an unaudited test image is not represented as an update/rollback rehearsal. |
| SLO/RPO/RTO and cost approval | **BLOCKED** | Nonzero telemetry baselines exist, but numeric SLO/RPO/RTO, retention, and external model-spend envelopes still require Cyrus approval. |

## Commands also passed

- `dotnet format Andreja.slnx --verify-no-changes --no-restore`
- PostgreSQL-project format verification
- NuGet direct/transitive vulnerability scan
- supply-chain pin policy
- all 20 fail-closed supply-chain negative cases
- evidence Compose interpolation
- `node --check scripts\evidence\browser-e2e.mjs`
- strict post-browser `Test-TelemetryEvidence.ps1`
- exact preloaded-image `Test-OfflineEvidence.ps1`

## Decision

Issue #62's tooling premise is resolved: local Linux ARM64 Docker, PostgreSQL,
and a real-browser virtual authenticator are available and reproducible. Phase
1A exit remains blocked by trusted OCI signing, encrypted database-plus-key
recovery with restored sign-in, a genuine
approved update/rollback pair, and human approval of SLO/RPO/RTO, retention,
cost, and residual risk. None of those gates is waived by the passing rows.

## Supplemental application-portability evidence (#87)

The #87 fallback began from clean live `main`
`27a702c9a6308aa7be41ec9930829b417a6d5e3e`. It used only synthetic fixed
records and a locally generated one-run archive key. PostgreSQL `17-alpine` ran
locally in Docker; two explicitly migrated disposable databases were used.

- solution build: zero warnings/errors;
- unit/WAF operations contract: duplicate-area and no-web-mutation negatives;
- live integration:
  `ApplicationPortabilityTests.ExportDryRunAndAtomicImportRoundTripPortableDataOnly`;
- concurrency regression: four fresh-database races between distinct export IDs
  each produced exactly one commit; the waiting importer began a fresh serializable
  transaction and rejected the committed ledger/dirty state. Exact replay remained
  idempotent. Cancellation, bounded lock timeout, injected pre-transaction failure,
  and invalid archive validation all released or avoided the dedicated non-pooled
  session lock and a following import succeeded;
- operator rehearsal: dry-run, approved atomic commit, source/target count and
  visible-task comparison, excluded-table zero check, and idempotent retry;
- authenticated-envelope tamper was rejected before any mutation;
- temporary archive, key, SQL fixtures, databases, and container were destroyed.

This closes only #87's implementation/evidence gap. It does not claim Phase 1A
exit, release authorization, trusted OCI provenance, recovery custody, or
update/rollback approval.
