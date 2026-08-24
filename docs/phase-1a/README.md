# Phase 1A decision packet

- **Status:** Proposed; Cyrus approval required
- **Issue:** [#9](https://github.com/cyrusjamula/Andreja/issues/9)
- **Scope:** Independent self-hosted assistant walking skeleton

This packet narrows the ratified [platform plan](../plan.md) into decisions that
are needed before Phase 1A implementation. It does not amend the plan or approve
production launch.

## Packet

1. [ADR 0001 — modular boundaries and typed client boundary](../adr/0001-phase-1a-modular-boundaries.md)
2. [ADR 0002 — identity and tenant isolation](../adr/0002-phase-1a-identity-tenancy.md)
3. [ADR 0003 — PostgreSQL persistence and data portability](../adr/0003-phase-1a-persistence-portability.md)
4. [ADR 0004 — assistant, skill, channel, and control-plane contracts](../adr/0004-phase-1a-assistant-skill-channel-contracts.md)
5. [ADR 0005 — independent self-host operations](../adr/0005-phase-1a-self-host-operations.md)
6. [Threat, privacy, cost, and test gates](evidence-gates.md)
7. [Exit and decision checklist](exit-checklist.md)

## Decision boundary

The packet selects only the smallest reversible Phase 1A seams:

- a modular .NET monolith with Clean/Onion dependency direction;
- PostgreSQL for the independent self-hosted data plane;
- a typed HTTP boundary for the Blazor client;
- local passkey identity, local BYOK assistant access, and an Open Loops task
  slice;
- one OCI application artifact, a Compose contract, and local OpenTelemetry.

It deliberately **does not select** a cloud runtime, managed database, CIAM
provider, graph database, Kubernetes distribution, public connector, or managed
control plane. It authorizes no provisioning, account creation, subscription,
free tier, trial, package installation, or live paid model call.

## Proposed inputs, not accepted dependencies

The packet was checked against ADR 0000, the plan, the static Squad directives,
the Spock charter, issue #9, and these available Phase 0 drafts:

- [PR #19 — catalog and launch frameworks](https://github.com/cyrusjamula/Andreja/pull/19)
- [PR #20 — burn and sponsorship controls](https://github.com/cyrusjamula/Andreja/pull/20)
- [PR #21 — regulatory applicability](https://github.com/cyrusjamula/Andreja/pull/21)
- [PR #25 — Phase 1A semantic graph contract](https://github.com/cyrusjamula/Andreja/pull/25)

Draft material is informative until separately reviewed and merged. In
particular, this packet follows PR #25's proposed relational-source-of-truth
boundary without making that draft authoritative.

## Human decisions still required

Cyrus must explicitly decide:

1. whether to accept ADRs 0001–0005 and the residual risks in this packet;
2. the supported WebAuthn relying-party origin/host matrix and final recovery
   factors, device limits, and break-glass policy;
3. the supported local OCI runtime/host matrix, TLS onboarding experience, key
   custody UX, backup destination, and update/signing channel;
4. the first OpenAI-compatible BYOK compatibility profile, credential storage
   UX, allowed endpoints/models, and a numeric live-model spend envelope;
5. the provisional internal SLO targets, evaluation windows, retention
   defaults, and model-exposure consent defaults;
6. the portable export v1 encoding and compatibility support window.

Cloud runtime, managed database, CIAM, and graph database decisions are
explicitly deferred and are not hidden prerequisites for Phase 1A.

## Evidence gaps

No implementation evidence exists yet for WebAuthn bootstrap/recovery, database
constraints, migration rollback, dump/restore, portable import, key restoration,
offline startup, provider failure, container restart/update, local OTel queries,
content suppression, mobile viewport behavior, or production-auth isolation.
The exact gaps and required proof are enumerated in
[the evidence gates](evidence-gates.md) and block Phase 1A exit, not packet
review.
