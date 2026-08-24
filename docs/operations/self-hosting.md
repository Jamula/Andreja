# Self-host operations runbook

This runbook implements the local-only Phase 1A contract in
[ADR 0005](../adr/0005-phase-1a-self-host-operations.md). It provisions no cloud
resource. The composition includes the PostgreSQL identity/Open Loops migration,
authenticated task API, and responsive task UI.

## Host and trust prerequisites

- An OCI runtime with Compose support. Docker Compose v5 syntax is statically
  validated; runtime portability to Podman or another implementation remains an
  evidence decision.
- At least 2 CPU cores, 3 GiB free memory, and durable local storage.
- A trusted HTTPS reverse proxy for any binding other than loopback. The bundle
  deliberately exposes plain HTTP on `127.0.0.1` by default and does not create or
  trust a development certificate. Passkey onboarding is owned by the identity
  slice and is not implemented here.
- PostgreSQL 17 client tools for the host-side logical backup scripts.
- An operator-approved encrypted, access-controlled destination for recovery sets.

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

Restrict both secret files to the account running the runtime. On Unix, use mode
`0600`. The files, `.env`, backup directory, and runtime state are ignored by Git.

## Acquire and verify images

The Dockerfile build and runtime bases, PostgreSQL, collector, and optional
Prometheus backend are pinned by multi-platform manifest digest. A released
`ANDREJA_IMAGE` must also be a trusted `name@sha256:<64 hex>` reference. Record the
source revision, image reference, resolved platform digest, acquisition time, and
release/migration notes in the recovery inventory.

For a locally built image:

```powershell
docker build --pull=false `
  --build-arg SOURCE_REVISION=$(git rev-parse HEAD) `
  --tag andreja:local .
$env:ANDREJA_IMAGE = docker image inspect andreja:local --format '{{.Id}}'
```

A local image ID (`sha256:<64 hex>`) is also immutable for that host. Published
bundles should use the repository digest. Never substitute `latest`.

For offline transfer, acquire everything before disconnecting:

```powershell
$env:ANDREJA_IMAGE = "registry.example/andreja@sha256:<digest>"
docker compose --profile evidence pull
$images = docker compose --profile evidence config --images
docker image save --output andreja-images.tar $images
Get-FileHash andreja-images.tar -Algorithm SHA256
```

Transfer `andreja-images.tar`, its independently recorded checksum, this checkout
or release bundle, and the release signature/provenance through the approved
channel. On the destination, verify the checksum, run `docker image load`, confirm
every configured reference resolves locally, then disable networking before the
offline startup/restart proof. Source builds performed offline also require a
previously acquired NuGet package cache; the normal proof starts after image
acquisition.

## Start, stop, and inspect

Validate interpolation and immutable references before every start:

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
database are enabled, the single Phase 1A application instance applies pending
identity/Open Loops EF migrations before accepting traffic. A migration failure
blocks startup; do not bypass it.

The application and PostgreSQL run as non-root users with dropped capabilities,
read-only application filesystems, bounded process/memory/CPU settings, and named
durable volumes. The internal network does not expose PostgreSQL or OTLP to the
host. The application port binds to loopback unless explicitly changed.

Use `docker compose restart app` for a process restart and prove both endpoints
again. Use `docker compose down` to stop while retaining named volumes. **Do not**
use `down --volumes` during normal operations.

After local identity bootstrap and sign-in, the Open Loops page supports assistant
proposal, exact review, confirmation, list, complete, JSON export, and explicit
two-step deletion. See [Open Loops help](../help/open-loops.md). The API resolves
the authenticated Identity user to exactly one active tenant membership and
principal (or validates server-issued explicit context claims), then requires
antiforgery on every mutation. No development or test authentication handler is
present in the application image.

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

## Update and rollback

1. Record the current app/database/collector digests, config checksum, schema
   version, volume inventory, and successful readiness result.
2. Produce and verify a fresh logical dump and key/config recovery set. Restore it
   into a clean instance before approving the update.
3. Acquire the proposed digest and inspect signatures, SBOM/vulnerability results,
   release notes, and migration notes without changing the running reference.
4. Stop writes. Run the separately reviewed, explicit migration artifact. The web
   process must never migrate on startup.
5. Set the new digest, run static validation, start, and verify readiness, sign-in,
   persisted data, audit continuity, and telemetry canaries.
6. Retain the prior image and recovery set until the rollback window closes.

If schemas are compatible, restore the prior app digest and restart. Otherwise stop
the new revision and restore the complete pre-update database **and keys/config**
into a clean instance. Never run a down-migration implicitly. A failed checksum,
signature, migration, readiness check, key read, tenant integrity check, or
telemetry canary is a stop condition.

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
