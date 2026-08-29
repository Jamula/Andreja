# Self-host operations runbook

This runbook implements the local-only Phase 1A contract in
[ADR 0005](../adr/0005-phase-1a-self-host-operations.md). It provisions no cloud
resource. The composition includes the PostgreSQL identity/Open Loops migration,
authenticated task API, and responsive task UI.

The root `LICENSE` currently contains Apache License 2.0, so the local OCI build
records `Apache-2.0` as repository license metadata. That factual label is not
qualified-counsel review, outbound-strategy approval, or permission to publish an
image. The no-publication gate in
[`docs/legal/license-evaluation.md`](../legal/license-evaluation.md) remains
controlling until Cyrus and qualified counsel explicitly decide otherwise.

## Host and trust prerequisites

- An OCI runtime with Compose support. Docker Compose v5 syntax is statically
  validated; runtime portability to Podman or another implementation remains an
  evidence decision.
- At least 2 CPU cores, 3 GiB free memory, and durable local storage.
- A trusted HTTPS endpoint whose exact origin is configured in
  `Andreja__Identity__AllowedOrigins__0` and whose host is within
  `Andreja__Identity__RelyingPartyId`. The bundle exposes plain HTTP only on
  `127.0.0.1` for an operator-managed same-host TLS reverse proxy. Set
  `ANDREJA_TRUSTED_PROXY_IP` to the one exact source IP Kestrel observes for that
  proxy. Bootstrap, sign-in, registration, and recovery reject HTTP, a missing
  `Origin`, an untrusted forwarder, a forwarded origin/host the application has not
  been configured to trust, and RP/origin/port mismatch.
- PostgreSQL 17 client tools for the host-side logical backup scripts.
- An operator-approved encrypted, access-controlled destination for recovery sets.
- Docker Buildx plus the exact Syft, Grype, Trivy, and Cosign images pinned in
  `supply-chain-policy.json`. Scanner or advisory-database unavailability is a hard
  failure; do not substitute a different tag or skip a scan.
- For local operator evidence, a Cosign public key obtained through a separately
  authenticated operator channel. The corresponding password-protected private
  key remains outside the repository, image, evidence bundle, backups, and
  runtime host whenever possible. Hosted release evidence instead requires the
  retained Sigstore bundle plus independently acquired root and policy from
  ADR 0010.

Copy `.env.example` to `.env`. Create the PostgreSQL password and one-time identity
bootstrap token files without placing either value in `.env`, command arguments,
source control, logs, or Compose environment:

```powershell
New-Item -ItemType Directory -Force deploy\secrets | Out-Null
[Convert]::ToBase64String(
  [Security.Cryptography.RandomNumberGenerator]::GetBytes(48)
) | Set-Content -NoNewline deploy\secrets\postgres_password
[Convert]::ToBase64String(
  [Security.Cryptography.RandomNumberGenerator]::GetBytes(32)
) | Set-Content -NoNewline deploy\secrets\bootstrap_token
```

Restrict the password file to the accounts running the runtime. The app container
runs as numeric UID/GID `1654:1654`, and file-backed Compose secrets preserve host
ownership. On a Linux host, provision the bootstrap token for that exact container
identity:

```bash
sudo chown 1654:1654 deploy/secrets/bootstrap_token
sudo chmod 0400 deploy/secrets/bootstrap_token
test "$(stat -c '%u:%g:%a' deploy/secrets/bootstrap_token)" = "1654:1654:400"
```

Do not leave the file owned only by the interactive host operator: Kestrel's
non-root user could not read it. On Docker Desktop, stage the file in the Linux/WSL
filesystem with numeric owner `1654:1654`; an NTFS ACL/read-only attribute alone
does not establish the in-container Unix ownership/mode contract. Stop if the
runtime presents any mode other than `0400`. The application verifies exact
owner-read permission before reading. The files, `.env`, backup directory, and
runtime state are ignored by Git.

## Same-host TLS reverse proxy

Kestrel intentionally listens on plain HTTP inside the loopback-published container
port. It trusts `X-Forwarded-For`, `X-Forwarded-Proto`, and `X-Forwarded-Host` only
when the immediate source is an exact IP in
`Andreja:Identity:TrustedProxyAddresses`. No network range or trust-all fallback is
configured. The middleware requires all three headers, processes one nearest-proxy
entry, and accepts forwarded hosts only from the configured WebAuthn origins.

Determine the address that the app container actually observes for the same-host
proxy (for example, the exact Compose edge-network gateway) and put only that
address in `ANDREJA_TRUSTED_PROXY_IP`. Do not use `0.0.0.0`, `::`, a CIDR, a Docker
address pool, or a client-controlled value. If the proxy source address changes,
Kestrel ignores the forwarded headers and passkey requests fail closed as HTTP.

The proxy must **replace**, not append to, all three headers. For nginx at the
configured public HTTPS virtual host:

```nginx
location / {
    proxy_pass http://127.0.0.1:8080;
    proxy_set_header Host $http_host;
    proxy_set_header X-Forwarded-Host $http_host;
    proxy_set_header X-Forwarded-Proto https;
    proxy_set_header X-Forwarded-For $remote_addr;
}
```

Use `$host` instead of `$http_host` only for the default HTTPS port. Set
`ANDREJA_HOSTNAME` to the exact public host (without a port) and keep the allowed
origin, TLS virtual host, and `AllowedHosts` aligned. Do not enable framework
environment switches that trust all forwarded headers.

By default Compose derives the allowed HTTPS origin from `ANDREJA_HOSTNAME`.
Leave `ANDREJA_PUBLIC_ORIGIN` unset unless an explicit port is required. If it
is set, its host must still equal the RP ID or be within that RP domain; startup
validation fails closed on divergence.

The repository evidence profile supplies a pinned Caddy reverse proxy, a fixed
evidence-only network, loopback-only TLS port `8443`, and exact source address
`172.30.44.10`:

```powershell
docker compose -f compose.yaml -f deploy\compose.evidence.yaml config --quiet
docker compose --profile evidence -f compose.yaml `
  -f deploy\compose.evidence.yaml up --detach
curl.exe --fail --cacert .andreja\localhost.pem `
  https://localhost:8443/health/ready
```

Generate the short-lived certificate and private key outside Git. The browser
evidence script pins the exact public-key hash rather than disabling certificate
validation. `deploy/compose.evidence.yaml` is a local evidence fixture, not a
production certificate, hostname, address-pool, or trust-policy recommendation.
On Docker Desktop, place the bootstrap value in an ignored source file, copy it
into the `andreja_evidence-bootstrap` volume, and set owner/mode to
`1654:1654/0400`; the application check is not relaxed for NTFS.

Before bootstrap, send a request through the proxy and confirm the application
accepts the configured HTTPS origin. A direct request to
`http://127.0.0.1:8080`, an untrusted source with spoofed forwarding headers, or a
wrong host/origin must not start a passkey ceremony. A spoofed earlier chain entry
must not override the single nearest value Kestrel processes.

## Acquire and verify images

The Dockerfile build and runtime bases, PostgreSQL, collector, and optional
Prometheus backend are pinned by multi-platform manifest digest. An
operator-controlled `ANDREJA_IMAGE` must be a verified
`name@sha256:<64 hex>` reference. `supply-chain-policy.json` forbids High and
Critical dependency, final-image, container, and IaC findings and permits no
waivers or scanner fallback. Record the source/tree, base and final image digests,
tool versions, both SBOM checksums, scanner reports, provenance, signature trust
material, and migration notes in the recovery inventory. These local
evidence instructions do not authorize publication.

Generate an operator-held key outside the checkout once, while the pinned Cosign
image is available. Supply the password through `COSIGN_PASSWORD`; never place it
on the command line or in the evidence directory:

```powershell
$cosign = (Get-Content supply-chain-policy.json -Raw | ConvertFrom-Json).tools.cosign.image
New-Item -ItemType Directory -Force $HOME\.andreja-signing | Out-Null
docker pull $cosign
docker run --rm --network none --env COSIGN_PASSWORD `
  --mount "type=bind,source=$HOME\.andreja-signing,target=/keys" `
  $cosign generate-key-pair --output-key-prefix /keys/andreja
```

From a clean reviewed commit, build twice without cache, compare the OCI/config
digests, create SPDX 2.3 and CycloneDX SBOMs, run all scanners, generate SLSA
in-toto provenance, and sign it locally:

```powershell
$env:COSIGN_PASSWORD = Read-Host 'Cosign key password' -AsSecureString |
  ConvertFrom-SecureString -AsPlainText
pwsh -NoProfile -File scripts\supply-chain\New-OciEvidence.ps1 `
  -OutputDirectory artifacts\supply-chain `
  -SigningKeyPath $HOME\.andreja-signing\andreja.key `
  -TrustedPublicKeyPath $HOME\.andreja-signing\andreja.pub
Remove-Item Env:\COSIGN_PASSWORD
```

The local artifact directory is ignored by Git. It contains private image layers
and detailed reports: keep it access-controlled and never upload it as a GitHub
artifact, attach it to an issue/PR, or send it to a public registry. Publication,
release, and license decisions remain in issues #6 and #65.

For offline transfer of local operator evidence, acquire the evidence directory,
independently trusted public key, verification script, and exact pinned Cosign
image before disconnecting. Transfer them only through the approved private
operator channel. At the destination, disconnect networking and run:

```powershell
pwsh -NoProfile -File scripts\supply-chain\Test-OciEvidence.ps1 `
  -BundleDirectory artifacts\supply-chain `
  -TrustedPublicKeyPath D:\trusted\andreja.pub `
  -ExpectedSigningMode operator-held-key
$evidence = Get-Content artifacts\supply-chain\evidence.json -Raw | ConvertFrom-Json
$env:ANDREJA_IMAGE = $evidence.image.immutableReference
```

Verification runs Cosign with `--network none`, validates the externally trusted
key fingerprint and signature, checks the schema and exact flat artifact inventory
(including hidden entries and rejecting directories, links, and reparse points),
recomputes every checksum, parses both SBOMs and all scanner reports against the
signed severity policy, binds provenance to the commit/tree/base/image/tool
digests and approved platform, requires commit-specific migration notes, checks the
single-platform OCI manifest/config OS and architecture, loads the archive, and
requires the loaded image platform to match. Missing or extra files,
invalid/unsigned provenance, an untrusted key, forbidden findings, policy,
platform or checksum drift, unavailable tools, or an unresolved digest blocks startup.
There is no unsigned, tag-based, online, or scanner fallback.

For accepted ADR 0010 keyless evidence, acquire the complete retained evidence
directory, independently authenticated and pre-positioned copies of the Sigstore
TUF trusted root and `supply-chain-policy.json`, and the preloaded pinned Cosign
image. Keep both trust inputs outside the evidence directory. With networking
unavailable, run:

```powershell
pwsh -NoProfile -File scripts\supply-chain\Test-OciEvidence.ps1 `
  -BundleDirectory artifacts\supply-chain `
  -TrustedPolicyPath D:\trusted\supply-chain-policy.json `
  -TrustedRootPath D:\trusted\sigstore-trusted-root.json `
  -ExpectedSigningMode keyless-sigstore
```

The hosted workflow builds and scans with read-only permissions. Only its
version-tag signing job has `id-token: write`. That job requires the tag commit
on protected `main`, exact-matches repository/workflow/revision/ref policy,
creates the public Fulcio/CT/Rekor record, retains the standardized bundle and a
copy of the pre-positioned TUF root, and verifies against only the independent
root path with `--network none`. Pull-request and ordinary
`main` runs remain unsigned validation evidence and cannot authorize startup or
update. The local operator-key mode cannot substitute for hosted release
evidence.

Evidence schema 1.1 carries the keyless-only fields. The retained evidence v1
schema and policy 1.0 operator path remain verifiable; policy 1.1 keyless fields
are required only for keyless mode.

## Start, stop, and inspect

Verify the signed bundle as above, then validate interpolation and immutable
references before every start or update:

```powershell
pwsh -NoProfile -File scripts\operations\validate-contract.ps1
docker compose config --images
docker compose up --detach
docker compose ps
Invoke-WebRequest http://127.0.0.1:8080/health/live
Invoke-WebRequest http://127.0.0.1:8080/health/ready
```

`/health/live` proves the process can serve. `/health/ready` proves the Data
Protection directory is writable, the password secret is mounted, and the
PostgreSQL and enabled local OTLP sockets are reachable. When Open Loops and the
database are enabled, startup queries EF migration state and refuses to serve when
any migration is pending; readiness repeats that check to detect drift. Neither
path applies a migration, and neither may be bypassed.

The application and PostgreSQL run as non-root users with dropped capabilities,
read-only application filesystems, bounded process/memory/CPU settings, and named
durable volumes. The internal network does not expose PostgreSQL or OTLP to the
host. The application port binds to loopback unless explicitly changed.

Use `docker compose restart app` for a process restart and prove both endpoints
again. Use `docker compose down` to stop while retaining named volumes. **Do not**
use `down --volumes` during normal operations.

The Open Loops page is implemented for assistant proposal, exact review,
confirmation, list, complete, JSON export, and explicit two-step deletion. See
[Open Loops help](../help/open-loops.md) and
[local identity help](../help/local-identity.md). Production identity uses only
ASP.NET Core Identity passkeys and hashed, single-use recovery codes. No password,
header, operator, test, or cloud authentication path is available. The separate
fixed development helper is compiled only in Debug and mapped only in Development.

The API resolves the authenticated Identity user to exactly one active tenant
membership and principal (or validates server-issued explicit context claims), then
requires antiforgery on every mutation. No development sign-in endpoint is mapped in
Production, and no header-based or automatic fake-auth handler exists.

Interactive Server API calls do not forward browser cookies from `HttpContext`.
The circuit-scoped typed client reads its own authentication state and attaches a
fresh, short-lived, one-time, Data Protection-protected delegation token accepted
only by the internal Open Loops audience. The pooled HTTP handler pipeline is
stateless and contains no user/circuit-scoped dependency. External API requests
continue to require the approved Identity cookie/passkey path. Delegation tokens,
cookies, and authorization headers must never be logged, exported, or copied into
support evidence.

`ANDREJA_ASSISTANT_PROVIDER` selects `deterministic`, the local offline and CI
default. To opt into BYOK, use the separate override:

```powershell
New-Item -ItemType Directory -Force deploy\secrets | Out-Null
# Write the operator-owned API key to deploy\secrets\assistant_api_key without
# echoing it, passing it as an argument, or placing it in .env.
docker compose -f compose.yaml -f deploy\compose.byok.yaml config
docker compose -f compose.yaml -f deploy\compose.byok.yaml up --detach
```

Set the non-secret values documented in `.env.example`: one exact base endpoint,
model, provider/content disclosure, retention disclosure, response and unit limits,
and the credential **file path**. The credential value itself belongs only in the
read-only file. On Linux its mode must be exactly `0400` for the application UID. On
Windows direct development the file must have the read-only attribute; Docker Desktop
operators must stage it with the same Linux ownership/mode contract as the bootstrap
secret. Do not place the value in JSON, `.env`, Compose environment, source, logs,
support evidence, exports, or skill configuration.

The configured endpoint must exactly match an entry in `AllowedEndpoints`. Andreja
appends `/chat/completions`, rejects user-info/query/fragment values and redirects,
and permits plain HTTP only when the URI is loopback. A model on the Docker host is
not loopback from inside the app container: expose it through operator-managed HTTPS
with a system-trusted certificate, or run both in an explicitly reviewed same-network-
namespace arrangement. HTTPS always uses normal platform certificate and hostname
validation; there is no trust-all callback.

`ANDREJA_ASSISTANT_APPROVED_EXTERNAL_TOTAL_UNITS` defaults to `0`, so an HTTPS
profile can be inspected but no external request can run until Cyrus approves and
records a numeric envelope. Each external attempt reserves the configured maximum
input plus maximum output units before sending. Loopback conformance/local use does
not consume that external envelope. This is a stop control, not currency conversion
or a claim that provider token accounting is exact.

The transport applies one overall timeout, caller/session cancellation, at most the
configured retry count for HTTP 408, 429, 5xx, and network/read failures, and a
bounded response body. HTTP redirects, other 4xx responses, malformed JSON, unknown
or schema-invalid tools, and limit failures are not retried. Provider error bodies
are discarded and never shown or logged. The Open Loops skill receives only validated
typed arguments and can create a proposal only; task mutation still requires the
separate confirmation path.

The credential file is read for each attempt and is never cached in application
configuration. Replace it atomically with another correctly permissioned file to
rotate; atomically replace it with an empty correctly permissioned file to revoke.
Deleting, making writable/inaccessible, exceeding 4096 bytes, or using invalid UTF-8
instead produces the content-free `credential-unavailable` configuration failure.
All fail before provider I/O without returning the value, path, or store exception.
The provider endpoint reports configuration readiness plus the operator-authored
provider and retention disclosures without returning the handle or path.

## Data Protection key contract

The `data-protection-keys` volume is separate from the database and application
image. All historical keys needed to decrypt cookies or protected records must be:

1. inventoried with instance name, application revision, creation/activation/
   expiration dates, and checksum;
2. copied only while the application is stopped or while the volume snapshot is
   guaranteed consistent;
3. kept on encrypted host storage and encrypted again outside the bundle with an
   operator-held key;
4. stored separately from the encrypted PostgreSQL dump where practical; and
5. restored to the same mounted path and non-root ownership before application
   readiness is evaluated.

Never include Data Protection, TLS, envelope, recovery, or signing keys in an
Andreja application export. A database-only restore is not a successful recovery.
After key restore, verify prior protected state can be read and a newly generated
value survives another restart. Key rotation and backup destinations require human
approval; the bundle does not invent either policy.

## PostgreSQL logical backup and restore

`scripts/operations/backup-postgres.ps1` and `.sh` run `pg_dump` in custom format
with owner and ACL metadata removed. They emit a SHA-256 sidecar and tool/version
metadata. Credentials come only from a PostgreSQL password file (`PGPASSFILE`,
format `host:port:database:user:password`), never an argument. The database is not
host-published by the normal Compose file. For a maintenance window, bind it only
to loopback, run the script, and remove the override immediately:

```powershell
docker compose -f compose.yaml -f deploy\compose.maintenance.yaml up --detach postgres
pwsh -NoProfile -File scripts\operations\backup-postgres.ps1 `
  -HostName 127.0.0.1 -Port 5432 -Database andreja -Username andreja `
  -PasswordFile .andreja\pgpass
docker compose up --detach postgres
```

The `.andreja\pgpass` entry must match the maintenance host/port and contain the
same password as the Compose secret. The normal `docker compose up` recreates
PostgreSQL without a published port. Alternatively, run the client on an
operator-controlled network path.

The matching restore scripts verify the checksum, query the destination, refuse a
database containing any user table, and invoke `pg_restore` with
`--single-transaction --exit-on-error`. Restore only into a compatible PostgreSQL
major version. Verify schema version, row counts, tenant constraints, audit
continuity, sign-in/key recovery, and user-visible reads. Encrypt the dump,
metadata, configuration inventory, and key inventory as a recovery set after
creation; plaintext staging artifacts must be access-restricted and removed after
verification.

These are logical recovery scripts, not PITR. WAL archiving, managed backups, and
provider-specific restore are explicitly outside Phase 1A.

## Explicit database migration

Database mutation is an operator command, not application startup behavior. Before
running it:

1. stop writes and record current image, configuration, and schema versions;
2. create a fresh logical dump and verify its SHA-256 sidecar;
3. restore that dump into a clean compatible PostgreSQL instance and complete the
   documented integrity/key checks;
4. generate the idempotent SQL from the exact proposed source/image revision, review
   every statement and release note, and record its SHA-256;
5. list the exact pending EF migration IDs in application order; and
6. approve the tested rollback choice. If any artifact, checksum, pending ID, or
   rollback assumption differs, stop.

The source-side review commands are:

```powershell
dotnet ef migrations has-pending-model-changes `
  --project src\Adapters\Andreja.Adapters\Andreja.Adapters.csproj `
  --context AndrejaIdentityDbContext
dotnet ef migrations script --idempotent `
  --project src\Adapters\Andreja.Adapters\Andreja.Adapters.csproj `
  --context AndrejaIdentityDbContext `
  --output .andreja\reviewed-migration.sql
Get-FileHash .andreja\reviewed-migration.sql -Algorithm SHA256
```

Keep PostgreSQL running, but do not start an unready app revision. Invoke the
checked-in wrapper with the verified dump, reviewed SQL, database name, and exact
pending migration IDs:

```powershell
$approvedMigrations = @(
  '20260824031732_InitialIdentityTenancy'
  '20260824043341_Phase1AOpenLoopsTasks'
  '20260824075115_ProductionPasskeyIdentity'
  '20260824102012_DurableRecentAuthenticationGrants'
  '20260824154149_DurableProposalConfirmation'
  '20260825004005_ApplicationPortability'
)
& scripts\operations\migrate-database.ps1 `
  -BackupDumpPath backups\postgres\andreja-<timestamp>.dump `
  -ReviewedMigrationScriptPath .andreja\reviewed-migration.sql `
  -DatabaseName andreja `
  -ApprovedMigrations $approvedMigrations `
  -ConfirmBackupRestoreAndMigrationReview
```

The wrapper rechecks the backup sidecar, hashes the reviewed SQL, emits a local
approval manifest, and starts the same pinned app image only as:
`--migrate-database --approval-file /run/andreja/migration-approval.json`.
The command rehashes both read-only mounted artifacts, requires the configured
database and actual pending migrations to match the approval exactly, applies EF
migrations, and fails if any remain. Failure or cancellation returns nonzero.
After success, start the normal app and require `/health/ready` before restoring
traffic. Preserve the approval manifest, SQL hash, backup inventory, output, and
result as the migration evidence set.

## Update and rollback

1. Record the current app/database/collector digests, config checksum, schema
   version, volume inventory, and successful readiness result.
2. Produce and verify a fresh logical dump and key/config recovery set. Restore it
   into a clean instance before approving the update.
3. Acquire the proposed digest and inspect signatures, SBOM/vulnerability results,
   release notes, and migration notes without changing the running reference. Run
   `Test-OciEvidence.ps1` offline with either the separately trusted local public
   key or, for hosted evidence, independently trusted Sigstore root and policy
   files outside the retained evidence; any failure stops the update before
   Compose or migration.
4. Stop writes. Run the exact explicit migration command above with the reviewed
   SQL, restored backup evidence, checksum approval, and pending-ID list. The web
   process never migrates on startup.
5. Set the new digest, run static validation, start, and verify readiness, sign-in,
   persisted data, audit continuity, and telemetry canaries.
6. Retain the prior image and recovery set until the rollback window closes.

If schemas are compatible, restore the prior app digest and restart. Otherwise stop
the new revision and restore the complete pre-update database **and keys/config**
into a clean instance. Never run a down-migration implicitly. A failed checksum,
signature, SBOM, scanner policy, evidence inventory, migration, readiness check,
key read, tenant integrity check, or telemetry canary is a stop condition.

## Local telemetry and evidence

The application exports only to the configured local OTLP endpoint. ASP.NET spans
pass through an exact low-cardinality attribute allowlist; all other attributes are
removed and counted as policy violations. Health spans are excluded. Metrics use
framework route templates and content-free Andreja instruments. Prompt, response,
task, token, recovery, connector content, and raw user identifiers are forbidden.

The collector applies a second deletion layer and sends traces to `nop`; it has no
payload-logging exporter. Metrics are exposed only on the internal network. Enable
the opt-in, loopback-only evidence backend with:

```powershell
docker compose --profile evidence up --detach
```

Query `andreja_telemetry_policy_checks_total` first; zero means insufficient
evidence. Then require `andreja_telemetry_policy_violation_total` to remain zero
under normal tests. Inject synthetic canaries containing each prohibited class,
exercise representative requests, and confirm no canary appears in collector
configuration, logs, Prometheus label names/values, or exported evidence. A
non-zero violation means the processor suppressed a prohibited attribute and
blocks release until the producer is corrected. Other undeclared attributes are
removed and counted by `andreja_telemetry_suppressed_attributes_total`; review
that count before extending the exact allowlist. Evidence must record sample count,
time window, query version, build digest, config checksum, owner, and exclusions.

The optional backend is local convenience, not a cloud dependency, SLA, or managed
observability choice.
