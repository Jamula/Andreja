# ADR 0003: Phase 1A PostgreSQL persistence and portability

- **Status:** Proposed
- **Issue:** [#9](https://github.com/cyrusjamula/Andreja/issues/9)
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

## Consequences

`pg_dump` is not a user portability promise, application export is not a full
disaster-recovery image, and PITR remains an operator capability. Database and key
loss remain independent failure modes that recovery drills must combine.

## Deferred

Managed PostgreSQL, Azure SQL, other managed databases, PITR tooling, object
storage, graph databases, and cross-provider migration are not selected.
