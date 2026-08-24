# ADR 0003: Phase 1A PostgreSQL persistence and portability

- **Status:** Proposed
- **Date:** 2026-08-23
- **Issue:** [#9](https://github.com/Jamula/Andreja/issues/9)
- **Governing:** [Platform plan](../plan.md#deployment-data-ownership-hosting-and-scale),
  [company charter](../charter.md#commitments), and
  [ADR 0000](0000-plan-ratification.md)
- **Proposed by:** Spock and Jett Reno
- **Decision owner:** Cyrus

## Context

Operational recovery, PostgreSQL migration, and user-controlled portability solve
different problems. Calling all three "backup" would create unsafe expectations.

## Decision

Use PostgreSQL as the Phase 1A self-host relational store. This is not a managed
database selection. EF Core mapping and PostgreSQL behavior stay in the adapter;
domain and application code use provider-neutral concepts.

Use one database with module-owned schemas and migration history. Migrations are
reviewed, deterministic, forward-only artifacts run explicitly before the new app
revision becomes ready. The web process never migrates on startup. Prefer
expand/migrate/contract changes; rollback means reverting the app and restoring a
pre-migration backup when compatibility cannot be preserved.

| Mechanism | Purpose | Includes | Excludes | Phase 1A proof |
|---|---|---|---|---|
| PostgreSQL logical dump/restore | Operator recovery and migration between compatible PostgreSQL instances | All database schemas and durable rows | Host keys, attachments outside DB, runtime config | Encrypted `pg_dump` artifact restored into a clean compatible instance |
| Andreja application export/import | User-owned, cloud-neutral portability | Versioned manifest, checksums, supported user data, grants, audits, settings, provenance, supported attachments | Password/passkey material, recovery secrets, provider/BYOK tokens, Data Protection keys, caches, unsupported derived data | Export, inspect, import into a clean instance, and compare user-visible records |
| Operational backup/PITR | Point-in-time disaster recovery | Database/WAL plus required operator-managed state | Portable cross-provider contract | Interface and runbook only; no managed/PITR system is selected in Phase 1A |

The application export is a versioned archive with a canonical manifest and
checksums. Every record identifies schema/protocol version and tenant ownership.
Import validates all checksums and references before committing, is idempotent for
a declared import ID, reports exclusions, and requires provider reauthorization.
The v1 encoding and compatibility window require human approval.

### Application export v1 content

| Data area | Included | Excluded or restored separately |
|---|---|---|
| Assistant sessions and content | User-owned session metadata, retained prompts/responses, proposal provenance, and content the user elects to export under the approved retention/model-exposure policy | Provider runtime/session handles, transient streaming state, caches, hidden provider metadata, and any content already deleted or never retained |
| Channel and connector state | User-visible non-secret configuration, account labels, declared scopes, grant references, provenance, and portable cursors only when the channel contract declares them portable | OAuth/access/refresh tokens, provider credentials, webhook secrets, provider-side subscriptions, nonportable delivery leases, and caches; import marks the channel disconnected pending reauthorization |
| Grants, consent, and share audit | Active, expired, and revoked `Grant` records, bilateral `ConsentRecord` decision history, and content-minimized `ShareAuditEntry` records needed to explain prior access | Signing private keys, peer trust-store secrets, live transport/replay caches, and authority to resume sharing; imported grants remain inactive until identities are resolved and consent is revalidated |
| Import instructions | Human-readable and machine-readable prerequisites, archive/schema versions, checksums, exclusions, conflict policy, dry-run steps, reauthorization/key-restoration steps, and post-import verification | Host-specific commands presented as universally portable or any instruction that silently recreates credentials or external authority |

Grant/consent/share and channel collections are present in the archive schema only
when an approved active slice has durable records. Phase 1A conformance fixture
archives may prove their shape, but the production export is empty for those
inactive capabilities and does not justify persistence migrations.

Import first verifies the archive and emits a dry-run report without writes. The
operator resolves tenant/identity mapping and declared conflicts, restores identity
keys through the separate recovery runbook, commits once under the import ID, then
reauthorizes providers/channels and verifies counts, grants, exclusions, audit
continuity, and user-visible records. Import never activates a sharing grant merely
because it existed in the source archive.

Logical backups and key/config backups are separately encrypted and inventoried but
restored as one tested recovery set. Backup success is not inferred from command
exit; a clean-instance restore, identity-key recovery, integrity queries, and a
sign-in/task read prove recoverability.

## Migration rules

- CI applies migrations to an empty database and a supported prior-version fixture.
- Constraint, tenant-isolation, export/import, and downgrade/restore tests gate
  release.
- No provider-specific SQL appears in domain/application code.
- A provider conformance suite is retained for any future relational adapter; it
  does not imply a second provider in Phase 1A.
- Retention deletion and export schema changes require privacy and compatibility
  review.
- Phase 1A grant/consent/share and peer-envelope conformance fixtures are in-memory
  contract data only. They create no inactive tables, migration history, transport
  state, or production records.

## Consequences

`pg_dump` is not a user portability promise, application export is not a full
disaster-recovery image, and PITR remains an operator capability. Database and key
loss remain independent failure modes that recovery drills must combine.

## Alternatives considered

- **Treat `pg_dump` as user portability:** rejected because it exposes an
  implementation-specific recovery image rather than a versioned, inspectable user
  contract.
- **Run migrations during web startup:** rejected because readiness would perform an
  implicit, hard-to-observe destructive operation.
- **Persist future grant/channel/federation schemas now:** rejected because inactive
  tables and migrations create compatibility burden without a Phase 1A user outcome;
  only contract fixtures are required.

## Deferred

Managed PostgreSQL, Azure SQL, other managed databases, PITR tooling, object
storage, graph databases, and cross-provider migration are not selected.
