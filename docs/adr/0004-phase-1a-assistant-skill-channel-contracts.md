# ADR 0004: Phase 1A assistant, skill, channel, and control-plane contracts

- **Status:** Proposed
- **Issue:** [#9](https://github.com/cyrusjamula/Andreja/issues/9)
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

The self-host app exposes authenticated, typed local APIs/UI for provider settings,
policies, manifests, proposal/audit history, usage/budget state, health, pause, and
kill controls. This product control plane is separate from an optional Andreja
service. Any future remote control-plane client is disabled by default, declares
every outbound call, and cannot receive user content, credentials, or authority to
run skills.

## Consequences

Phase 1A implements only the first-party Open Loops skill and BYOK adapter. Channel
host/manifests are contract-tested seams, not permission to ship public connectors.
Third-party executable skills, remote UI, marketplace, federation, and managed
relay remain out of scope.

## Human decision

Cyrus must approve the initial BYOK compatibility profile, endpoint/model allowlist,
credential custody UX, proposal expiry/confirmation tiers, and live-model budget.
