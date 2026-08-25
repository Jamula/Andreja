# Application export and import contract

Andreja application portability is separate from PostgreSQL recovery. Version 1 is
defined by
[`application-export-v1.schema.json`](application-export-v1.schema.json) and the
framework-neutral types in
`src/Andreja.Platform.Contracts/Portability/ApplicationExportContract.cs`.

## Archive layout

An export is an operator-encrypted archive containing `manifest.json` plus only the
relative artifact paths declared by that manifest. Producers serialize the
manifest as UTF-8 JSON with camel-case property names and string enum values.
Artifact checksums are lowercase SHA-256 of the exact stored bytes. Paths use `/`,
are case-insensitively unique and relative, and may not contain `\`, `:`, `.`, or
`..` segments.

Every manifest identifies archive/schema/application versions, export ID, UTC
creation time, opaque tenant ownership, byte/record counts, checksums, exclusions,
and provider reauthorization steps. Even an empty area has a zero-record artifact,
so these six areas are explicit and inspectable:

- records;
- attachments and their metadata;
- grants (imported inactive);
- content-minimized audit history;
- non-secret settings; and
- proposal/source provenance.

The manifest must explicitly report exclusion of credentials, passkeys, recovery
secrets, provider access/refresh/BYOK tokens, Data Protection or other host keys,
and caches. Provider runtime handles, hidden metadata, leases, subscriptions,
transient streams, unsupported derived data, and already-deleted/non-retained
content are also excluded when applicable. Exclusion is not silent data loss.

## Operator CLI

Application portability is available only as the local
`src/Andreja.Portability.Cli` operator command. It is deliberately not an HTTP
endpoint. The PostgreSQL connection string and a base64-encoded random 32-byte
archive key are read from environment variables, never command-line arguments:

```powershell
$env:ANDREJA_PORTABILITY_POSTGRES = Get-Content .andreja\portability-postgres.txt
$env:ANDREJA_PORTABILITY_KEY = Get-Content .andreja\portability-key.txt
dotnet run --project src\Andreja.Portability.Cli -- export `
  --tenant 00000000-0000-0000-0000-000000000000 `
  --output D:\protected\tenant.andreja
dotnet run --project src\Andreja.Portability.Cli -- import `
  --archive D:\protected\tenant.andreja --dry-run
dotnet run --project src\Andreja.Portability.Cli -- import `
  --archive D:\protected\tenant.andreja --commit `
  --approve-export 00000000-0000-0000-0000-000000000000
```

Use a fresh random archive key, deliver it separately, and keep it in
operator-controlled secret storage. The archive is an authenticated AES-256-GCM
envelope; the key is zeroed from process memory after use. The command writes a
same-directory partial file with write-through semantics, atomically renames it,
never overwrites a destination, removes partial files on cancellation/failure, and
logs only IDs, opaque tenant references, counts, and lengths.

## Export sequence

1. Authorize the export in the application and select the intended tenant/data.
2. Materialize a tenant-scoped PostgreSQL repeatable-read snapshot without logging
   record content.
3. Write each versioned artifact, then its length/count and SHA-256 to the manifest.
4. Emit all exclusions and required channel/provider reauthorization actions.
5. Canonicalize every JSON object, store entries in stable order, and validate exact
   counts, lengths, paths, contract versions, and SHA-256 digests.
6. Encrypt the in-memory bounded ZIP to a user-controlled destination; no plaintext
   staging file is created.

The current persistent slice exports tenant, AppUser, external identity mapping,
principal, membership, contact, task, task receipt, proposal, proposal receipt,
task/proposal audit, tenant settings, and proposal/source provenance. Attachments,
persisted sharing grants/consent, and persisted semantic assertions are not
implemented in Phase 1A, so their required artifacts are explicit zero-record
files. The exporter does not invent persistence to fill the contract.

## Dry-run and clean-instance import

Import is deny-by-default:

1. Authenticate/decrypt the bounded archive in memory. Reject the archive before
   parsing on envelope authentication failure.
2. Parse `manifest.json` with duplicate/unknown-property rejection and validate the
   v1 schema.
3. Reject missing/extra/duplicate entries and JSON properties, unsafe paths,
   symbolic links, unsupported versions/types, oversized files/counts, compression
   bombs, checksum/length/count mismatch, cross-tenant rows, and foreign lineage.
4. Produce a dry-run report with tenant/identity mapping, conflicts, exclusions,
   counts, inactive grants, and reauthorization actions. Perform no writes.
5. Apply the `ApplicationPortability` migration explicitly. Import first acquires
   one database-scoped, session-level PostgreSQL advisory lock on a dedicated
   non-pooled connection. Only after the lock is held does it open the serializable
   transaction and obtain a fresh snapshot, re-read the import ledger, and prove
   every application/identity/security table empty. This prevents two waiting
   importers from retaining pre-lock snapshots. The single lock key serializes all
   imports for that database, is acquired before any transaction or other lock, and
   therefore has no lock-order cycle.
6. Lock acquisition honors caller cancellation and has a 30-second default timeout.
   Success, validation/transaction failure, injected process-failure checkpoints,
   and cancellation all attempt `pg_advisory_unlock` without the cancelled token.
   The dedicated connection has pooling and multiplexing disabled and is always
   closed/disposed, so a broken explicit unlock cannot leak a session lock into the
   pool; PostgreSQL releases it when the session closes.
7. Commit requires `--approve-export` with the dry-run export ID. It commits all rows
   plus its digest ledger in one serializable transaction. A byte-identical retry is
   idempotent; a different or conflicting import is rejected after acquiring the
   same lock and starting a fresh transaction.
8. Restore identity and host keys only through their separate recovery procedure.
   Reauthorize every provider/channel; never recreate a credential from the
   archive.
9. No credential user, passkey, recovery code, recent-auth grant, provider token,
   Data Protection/TLS/signing/bootstrap key, cache, telemetry, or runtime secret is
   imported. AppUser and Principal IDs remain stable, but operators create new local
   credential mappings and complete every reported provider reauthorization.
10. Compare counts, attachments, audit continuity, provenance, settings, exclusions,
   user-visible records, and delete behavior on the clean instance.

A valid archive never authorizes overwrite. A target containing any application or
identity row (including excluded credential/security rows) is dirty. Migration
history is the sole permitted pre-import state. Failure, cancellation, process loss
before commit, or a constraint error rolls back the transaction; no partial import
is retained.

## Compatibility and security

Archive v1 and schema `1.0.0` acceptance is exact. A future reader may add an explicit compatibility
window only after human review; it must never guess how to reinterpret an unknown
schema. Verify release provenance before parsing. Current hard limits are 64 MiB
encrypted/compressed, 128 MiB expanded, 32 MiB per artifact, 100,000 records per
artifact, and 100:1 per-entry expansion. Avoid following links. Checksums detect
corruption but do not authenticate the exporter, so distribution signature policy
is a separate operator decision.

Application exports contain user data and audit history. Protect them as sensitive
data, never attach them to issues or telemetry, and test with synthetic fixtures
only.
