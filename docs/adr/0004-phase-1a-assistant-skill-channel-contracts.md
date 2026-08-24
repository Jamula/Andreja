# ADR 0004: Phase 1A assistant, skill, channel, and control-plane contracts

- **Status:** Proposed
- **Date:** 2026-08-23
- **Issue:** [#9](https://github.com/Jamula/Andreja/issues/9)
- **Governing:** [Platform plan](../plan.md#sharing-consent-and-federation-foundations),
  [company charter](../charter.md#decision-and-launch-enforcement), and
  [ADR 0000](0000-plan-ratification.md)
- **Proposed by:** Seven of Nine
- **Decision owner:** Cyrus

## Context

The Open Loops task slice must prove provider replacement, permissioned skill
execution, user-confirmed writes, and independent operation without building a
marketplace or connector platform.

## Decision

Define application-owned contracts with no provider SDK types:

- `IAssistantProvider` negotiates capabilities and creates `IAssistantSession`;
- `IAssistantSession` accepts a content/policy envelope and an exact typed-tool
  allowlist, streams structured events, reports content-free usage, and supports
  cancellation and cleanup;
- `ISkillHost` resolves a versioned manifest, evaluates tenant/principal/purpose/
  grant/capability context, validates typed input, invokes a use case, and returns a
  typed result or proposal;
- `IChannelHost` resolves a versioned channel manifest and brokers typed external
  capabilities without exposing provider credentials to skills;
- the policy evaluator intersects principal permissions, user grants, skill/channel
  capabilities, purpose, resource sensitivity, and confirmation tier.

Skills receive neither `DbContext`, secrets, unrestricted network/filesystem access,
nor `IServiceProvider`. Channel adapters own encrypted token handles; identity,
assistant, content, feedback, and publishing grants remain distinct even for one
provider.

### Grant, consent, disclosure, audit, and peer-envelope contracts

Approval of this ADR ratifies the following Phase 1A application-owned contract
shapes from the governing plan. They are versioned DTO/policy seams with no provider,
transport, cryptography-library, or persistence types:

- `Grant` contains grant/version IDs, owner tenant, resource/scope reference, grantee
  principal, purpose, disclosure level, allowed operations, validity/expiry,
  revocation state/time, and the governing consent reference.
- `ConsentRecord` contains consent/version IDs, grant, both principals, the offered
  terms, and a timestamped immutable decision timeline whose states are `Offered`,
  `Accepted`, `Active`, `Rejected`, `Expired`, or `Revoked`. Both parties and the
  currently effective terms must be explicit.
- `ShareAuditEntry` is append-only and contains audit/version IDs, tenant, actor/peer,
  grant/consent references, resource/scope, purpose, disclosure level, operation,
  outcome, envelope/payload digest when applicable, and occurrence time. It contains
  no shared content or credentials.
- `DisclosureLevel` is an ordered, fail-closed ladder: `Existence` exposes only
  availability/duration; `Timing` adds the title, time, and location required for
  plans; `Summary` adds outcome, status, and participants; `Full` adds notes, amounts,
  attachments, and provenance only when explicitly granted. Policy may reduce but
  never widen the requested level.
- `IPeerChannel` validates and exchanges a `SignedPeerEnvelope`. The immutable,
  canonical envelope binds protocol version, envelope ID, sender, recipient, grant,
  purpose, nonce, idempotency key, issue time, expiry, payload type, payload digest,
  signing algorithm/key ID, and signature. It exposes no discovery, trust bootstrap,
  relay, or provider credential.

The policy evaluator authorizes the intersection of tenant residency, principal,
active bilateral consent, grant, purpose, operation, requested disclosure, and
resource sensitivity. Unknown enum values, versions, payload types, or consent states
fail closed.

### Phase 1A conformance proof

Deterministic in-memory fixtures provide two peer endpoints and test:

1. exact serialization/version vectors and rejection of unknown incompatible shapes;
2. consent state transitions, expiry/revocation, grant/purpose/operation intersection,
   and inability to escalate the disclosure ladder;
3. append-only, content-minimized share audit for allow and deny outcomes;
4. canonical signature verification plus rejection of payload/header tampering,
   unknown keys/algorithms, wrong sender/recipient, expired/future envelopes,
   grant/purpose mismatch, nonce replay, and conflicting idempotency reuse; and
5. retry of the same valid idempotency key without duplicate effect or audit loss.

These tests create no grant, consent, share-audit, channel, or federation database
tables or migrations. Phase 1A runs no peer listener, discovery, relay, remote trust
exchange, or live federation traffic.

### Implemented contract mapping

The Phase 1A implementation keeps `TenantId`, authoritative `AppUserId`, and
`PrincipalId` distinct on assistant, skill, and channel execution boundaries.
`SkillManifest` and `ChannelManifest` use semantic artifact versions plus explicit
schema versions and describe lifecycle stage, publisher, purpose/capability/data-class
permissions, disclosure ceiling, execution mode, retention, help/support,
compatibility, integrity/provenance, and explicit reasons for every non-applicable
field.

One application-owned evaluator is used by both in-memory hosts. It denies unless the
invocation, user policy, active grant, active bilateral consent, declared capability,
operation, data class, purpose, time window, revocation state, and ordered disclosure
ceiling intersect. Manifest/schema/version mismatches and pre-policy denials use the
same content-minimized audit shape. The channel fixture is deterministic and local:
provider, account, OAuth, query/sync/publish, webhook/change-feed, cache, cost, and
delivery-topology fields are explicitly non-applicable. This mapping introduces no
connector, provider credential, network execution, marketplace publication,
federation traffic, or persistence migration.

### Independent BYOK path

The required provider adapter is an OpenAI-compatible BYOK profile selected by the
operator/user. Endpoint, model, content-retention disclosure, timeout, and spending
policy are explicit configuration. Credentials are encrypted using keys outside the
database and never enter manifests, prompts, usage records, logs, traces, exports,
or skill calls. A deterministic fake provider is the default test path. GitHub
Copilot and local-model adapters are optional future implementations; neither is
required for offline-from-Andreja-cloud startup.

### Open Loops vertical slice

1. The user asks the assistant to create a task.
2. The provider can invoke only the typed `open-loops.propose-task` tool.
3. `ISkillHost` validates schema and policy and returns an exact proposed change,
   provenance, and expiry; it does not write.
4. The typed API presents the proposal. User confirmation binds proposal version,
   actor, tenant, and idempotency key.
5. The Open Loops use case re-evaluates policy, persists the task, and records an
   audit event transactionally.
6. List, complete, export, and delete use access-scoped contracts. Assistant-originated
   writes always follow proposal/confirmation; direct UI writes remain server
   authorized and audited.

### Local control plane

The Phase 1A self-host app exposes authenticated, typed local APIs/UI only for the
surfaces proven by its walking slice: the selected BYOK profile and encrypted
credential handle, the first-party Open Loops manifest, task proposal/audit history,
usage/budget state, health, and provider pause/kill. A general policy editor, channel
administration, peer/federation administration, fleet management, and remote control
plane are not Phase 1A surfaces. Any future remote client is disabled by default,
declares every outbound call, and cannot receive user content, credentials, or
authority to run skills.

## Consequences

Phase 1A implements only the first-party Open Loops skill and BYOK adapter. Channel
host/manifests are contract-tested seams, not permission to ship public connectors.
Third-party executable skills, remote UI, marketplace, live federation transport,
internet discovery, external peer interoperability, and managed relay remain out of
scope. The grant/consent/share and signed-envelope contracts and local conformance
fixtures above are required Phase 1A scope; calling live federation out of scope does
not defer those seams.

## Alternatives considered

- **Expose provider SDK/session and generic plugin types:** rejected because it leaks
  credentials and provider behavior across the application boundary.
- **Authorize directly from skill/channel manifests:** rejected because manifests
  declare capability but cannot create consent or grant themselves authority.
- **Build live federation or persist future peer state now:** rejected because Phase
  1A needs compatibility and authorization proof, not inactive migrations or an
  operational network.

## Human decision

Cyrus must approve the initial BYOK compatibility profile, endpoint/model allowlist,
credential custody UX, proposal expiry/confirmation tiers, and live-model budget.
