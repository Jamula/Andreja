# ADR 0002: Phase 1A identity and tenant isolation

- **Status:** Proposed
- **Issue:** [#9](https://github.com/cyrusjamula/Andreja/issues/9)
- **Decision owner:** Cyrus

## Context

The first installation has one human, but later tenancy cannot be retrofitted
safely. Local sign-in and recovery must work without Andreja or GitHub cloud.

## Decision

Use internal GUIDv7 IDs for `Tenant`, `AppUser`, `Principal`,
`TenantMembership`, `Contact`, and tenant resources. External authentication is
a replaceable adapter; provider subjects are never domain foreign keys.

Every tenant-owned table contains `TenantId`. The database enforces:

- primary or alternate uniqueness for `(TenantId, Id)`;
- child foreign keys `(TenantId, ParentId)` referencing the parent's
  `(TenantId, Id)`;
- tenant-scoped natural uniqueness such as `(TenantId, NormalizedName)`;
- global uniqueness for external identity `(Issuer, Subject)`;
- uniqueness and validity for the selected primary identity link;
- no foreign key that can connect tenant-owned rows by bare resource ID.

Request middleware resolves an authenticated user, membership, tenant, and
principal into immutable scoped context. Application policy checks and
access-scoped projections are authoritative; query filters are defense in depth,
not the only isolation control. Background work carries the same explicit
tenant/principal/purpose context. Two-tenant negative tests begin with the first
persistence release.

### Local passkey bootstrap

ASP.NET Core Identity is an outer adapter. Bootstrap is available only when no
tenant/admin exists, HTTPS and WebAuthn RP origin checks pass, and a 256-bit
single-use bootstrap token is supplied from a read-only host-mounted file. One
transaction creates the tenant, user, principal, owner membership, passkey, and
audit event, then permanently consumes bootstrap state. The token is never stored
in source, image, logs, telemetry, database backup, or application export.

### Recovery and linking

- Enrollment produces single-use, hashed recovery codes and requires the user to
  confirm offline storage; a second passkey is strongly encouraged.
- Recovery invalidates sessions, is rate-limited and audited, and requires new
  passkey enrollment. Recovery codes are rotated after use.
- A separately stored local operator recovery key may enable an offline
  break-glass command only after Cyrus approves its custody UX. The command must
  require filesystem access, disable network use, emit an audit receipt, and
  never reveal existing credentials.
- Identity/Data Protection and envelope-encryption key history are backed up and
  restored separately from ordinary application export.
- External identity linking requires recent authentication plus proof from the
  new provider. Matching email never links accounts. Collisions fail closed.
- Unlinking cannot remove the last usable sign-in/recovery path.

Passkeys are not automatically treated as attested hardware or as MFA. Optional
bring-your-own OIDC remains behind the identity port. No CIAM provider is selected.

## Consequences

Cross-tenant references fail in PostgreSQL even if application filtering regresses.
Restores are incomplete until key and identity recovery drills pass. The first
admin UX is intentionally unavailable on an already initialized database.

## Human decision

Cyrus must approve RP IDs/origins, recovery-code count/lifetime, passkey device
limits, operator break-glass custody, and which optional OIDC profile Phase 1A
supports.
