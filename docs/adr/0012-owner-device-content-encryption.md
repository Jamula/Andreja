# ADR 0012: Proposed owner-device content encryption

- **Status:** Proposed; not ratified
- **Date proposed:** 2026-10-11
- **Tracking issue:** [#2](https://github.com/Jamula/Andreja/issues/2)
- **Decision owner:** Cyrus Jamula
- **Proposed by:** T'Pol, Domain/Application Lead
- **Governing:** [ADR 0000](0000-plan-ratification.md) and the proposed
  [plan amendment](../plan.md#proposed-owner-device-content-encryption-amendment--2026-10-11)
- **Related:** [ADR 0002](0002-phase-1a-identity-tenancy.md),
  [ADR 0003](0003-phase-1a-persistence-portability.md),
  [ADR 0004](0004-phase-1a-assistant-skill-channel-contracts.md),
  [ADR 0005](0005-phase-1a-self-host-operations.md),
  [privacy baseline](../privacy.md#proposed-owner-device-content-encryption-amendment-not-ratified),
  [threat model](../threat-model.md#proposed-owner-device-content-encryption-amendment-not-ratified),
  [impact assessment](../impact-assessment.md#proposed-owner-device-content-encryption-amendment-not-ratified)

## Authority and status

This is a design proposal only. The plan's current accepted hash and every
accepted ADR remain unchanged. The trust-boundary and data-ownership change
described here requires Cyrus's explicit re-ratification of an exact proposed
plan hash under ADR 0000, plus any applicable independent reviews and evidence.
This proposal authorizes no implementation, data migration, use of real user
content, client distribution, provider call, telemetry change, risk acceptance,
or gate closure. A merge or review comment is not ratification.

If ratified, this ADR would govern only the content-encryption boundary and
would supersede conflicting content-custody portions of ADRs 0002, 0003, 0004,
and 0005 as identified below. It would not revise their unaffected identity,
tenant-isolation, persistence, portability, provider-contract, or operations
decisions. Their exact accepted versions remain historical baselines; no bytes
in those files are reclassified as newly accepted by this proposal.

## Context

The current Phase 1A application stores Open Loops task `Title` and `Details`
as PostgreSQL strings. The assistant service passes user content into the
configured provider adapter, and the portability CLI decrypts and handles
content server-side using an archive key supplied through an environment
variable. Host-level Data Protection keys and operator-controlled database,
backup, and telemetry custody do not provide owner-only content confidentiality.
This proposal changes the owner/operator data boundary; it does not describe
current implementation or claim that current content is encrypted end-to-end.

## Proposed decision

Adopt a single-owner, owner-device content boundary for user content in any
future ratified slice:

1. A separately distributed owner client performs content encryption and
   decryption, semantic processing, search/indexing, assistant preparation and
   proposal review, and plaintext export/import. Plaintext is not sent to
   Andreja's server for content processing. Search indexes and other
   content-derived state remain on the owner device and are encrypted at rest
   when persisted.
2. The client encrypts content before network submission and authenticates it
   with a standard, independently reviewed authenticated-encryption
   construction. The proposed key hierarchy uses a random owner content root
   key and per-record data keys; the server never receives a decryption key,
   recovery secret, or key-encryption key. Algorithm parameters, nonce
   discipline, serialization, key rotation, and cryptographic library are
   implementation prerequisites for independent security review, not choices
   ratified here. No custom cryptography is permitted.
3. A device receives access only through an owner-authorized device enrollment
   or owner-held recovery material. Device-local key protection must use a
   trusted client/device keystore where available. Authentication passkeys,
   identity recovery codes, and host Data Protection keys are not content keys
   and do not recover them.
4. The server stores authenticated ciphertext and the minimum service metadata
   required for identity, tenant authorization, versioning, routing, and
   durability. Tenant/account identifiers, opaque object identifiers,
   ciphertext sizes, record counts, access timing, network metadata, and
   availability patterns may remain observable unless separately minimized;
   encryption is not a claim of metadata invisibility. Server policy may
   authorize storage and retrieval but cannot inspect plaintext to filter,
   rank, search, classify, summarize, export, or enforce semantic rules.
5. User content is not sent in plaintext to an external AI provider, connector,
   or other recipient. There is no live sharing with another human under this
   proposal. ADR 0004's `Grant`, `ConsentRecord`, `DisclosureLevel`, and peer
   envelope shapes remain contract-only and cannot activate a grant, disclosure,
   or live data exchange. External model exposure requires a separate future
   decision that is consistent with the owner-only boundary; no such exception
   is proposed here.
6. The owner creates and retains a recovery package and separate high-entropy
   recovery secret outside Andreja's server and operator custody. An owner-
   authorized client can use both to restore the content key hierarchy. No
   operator escrow, support-assisted decryption, administrator reset, or
   break-glass recovery is allowed. If every usable device key and the complete
   owner-held recovery material are lost, content is permanently unrecoverable.
   Identity/account recovery remains a separate process and cannot recreate
   content keys.
7. The trusted client must be independently verifiable before it receives
   plaintext or keys: immutable signed release artifacts, an independently
   authenticated verification root, verification before execution, and
   tamper/failure behavior are required. Code served solely by the same
   self-hosted server that stores ciphertext is not an independent trust
   anchor. Accepted ADR 0010 governs OCI signing only and does not by itself
   establish trust in browser or owner-client code. A compromised client,
   browser/OS, extension, device, release-signing chain, or unlocked/coerced
   owner device can expose plaintext or keys and is not prevented by storage
   encryption.

### Migration and lifecycle boundary

Existing plaintext database rows, application archives, backups, logs, and
provider copies do not become protected retroactively. A future migration must
demonstrate owner-client encryption of retained records, disposition of
plaintext copies under an approved retention policy, and a cutover that does
not silently fall back to plaintext. Until then, the current application,
operator CLI, and recovery procedures retain their separately documented
behavior; this proposal makes no assertion that they satisfy this architecture.

Server-side restoration may restore ciphertext and service metadata but cannot
verify that an owner can decrypt content. A valid restore therefore requires
owner-controlled key/recovery material and independent client verification.
Device revocation can deny future server access and remove locally stored keys
on a cooperative device; it cannot recall copied ciphertext, plaintext, or a
key from a compromised or offline device.

## Proposed scope of changes to accepted ADRs

Only if the plan amendment is explicitly re-ratified:

- **ADR 0002:** Identity and tenant authorization remain; separate
  operator-held host/Data Protection key-history recovery from the owner-only
  content key hierarchy. The unapproved operator break-glass path remains
  forbidden and is not repurposed as content recovery.
- **ADR 0003:** PostgreSQL continues as the Phase 1A reference store, but
  human-content-bearing rows and server-side plaintext export/import are
  replaced by opaque ciphertext storage and owner-client portability. The
  current operator-key CLI is not a conforming owner-content path. Backup and
  application portability remain distinct; restore requires owner key
  availability and content recovery evidence.
- **ADR 0004:** Provider and skill contracts remain only where content is
  processed on the owner client. No plaintext external call or live
  other-human sharing is permitted. Grant/consent and peer contracts remain
  non-activating fixtures unless a separate decision explicitly amends this
  boundary.
- **ADR 0005:** Operational hosting, PostgreSQL, TLS, image, and telemetry
  boundaries remain. Operators may restore, withhold, corrupt, or delete
  ciphertext and observe service metadata, but cannot recover user content
  from server-side key custody because no such custody exists.

## Required evidence before any implementation or activation proposal

All evidence is a future prerequisite, not supplied or claimed by this ADR.
Use synthetic fixtures only; no production/user content, real recovery secrets,
or live provider calls are authorized.

- Independent architecture, cryptography/security, privacy, operations, and
  accessibility review of the exact client/key protocol, recovery UX, metadata
  map, and migration.
- Negative synthetic tests proving that database rows/dumps, backups, server
  APIs/responses, application logs, exception paths, telemetry, and support
  artifacts contain neither plaintext content nor decryption/recovery keys.
- Client tests proving only a verified, untampered release decrypts; an
  altered, unsigned, downgraded, or unverifiable artifact fails closed before
  receiving key material; test evidence binds the verification root and exact
  release digest independently of the server.
- Synthetic owner-only decrypt, new-device enrollment, device revocation,
  offline recovery, key rotation, backup/restore, encrypted export/import,
  ciphertext corruption/replay, and total-key-loss scenarios. Demonstrate
  that identity recovery and operator credentials cannot recover content and
  that total key/recovery loss is unrecoverable.
- Content-flow negatives proving all search, filtering, assistant preparation,
  proposal review, and export occur locally; no external plaintext call or
  other-human grant can be activated; current server-side assistant and
  portability paths cannot silently handle plaintext after a proposed cutover.
- A record-by-record inventory of metadata visible to the server/operator,
  leakage through timing/size/counts and network logs, client caches, legacy
  backups, deletion/revocation limits, migration rollback behavior, and
  retention disposition.
- Independent exact-revision review and separate human approval of the
  proposed plan re-ratification, recovery boundary, and remaining risks. Existing
  High (15) coercion/custody and High (12) telemetry/support/reviewer concerns
  remain unaccepted until their own evidence and decision gates are satisfied.

## Consequences and open decisions

This architecture can reduce server/operator access to stored content, while
moving confidentiality, search, recovery, and availability responsibility to
the owner device and owner-held recovery material. It adds client distribution,
integrity verification, local storage, key lifecycle, migration, deletion, and
accessibility requirements. It does not protect against compromised owner
devices, active coercion, hostile verified code, denial of service, ciphertext
loss, or metadata inference. The concrete cryptographic profile, release-root
custody, device enrollment UX, metadata budget, retention policy, and migration
sequence all remain open for the required reviews.
