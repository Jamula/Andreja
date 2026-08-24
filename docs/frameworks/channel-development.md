# Channel Development Framework

## Owner and status

Jett Reno owns the Channel Development Framework end to end, per
[plan §Phase 0 deliverables](../plan.md#phase-0---govern-and-decide) ("Assign
Jett Reno as Channel Framework owner"). This document is the ratified,
authoritative expansion of
[plan §Channel Development Framework](../plan.md#channel-development-framework);
the plan section becomes a summary pointer once this file merges. Every
connector in [`docs/roadmap/channel-connectors.md`](../roadmap/channel-connectors.md)
must progress through the stages below, and the catalog's Status column must
match a stage name here.

## Definition

A channel is a typed, permissioned connection between Andreja and an external
identity, assistant provider, content source, communication surface,
publication target, notification destination, support system, or approved
business service. Channels and skills are separate artifacts: a channel
manifest describes the external provider binding, grants, and operating
contract; a skill manifest declares dependencies on stable channel
IDs/capabilities. Both are enforced by the same policy evaluator
([plan §Connector platform](../plan.md#connector-platform)).

One provider may expose multiple isolated channels/grants. GitHub assistant
auth, GitHub content, GitHub identity, GitHub feedback publishing, and GitHub
support/project each carry a distinct token, scope, consent, storage, and
revocation path — they are never collapsed into one grant.

## Phase 0 boundary — $0 no-cloud rule

Every stage below that a channel reaches during Phase 0 (charter, provider
qualification, manifest definition, threat/privacy design) is **local and
paper research only**. Per [ADR 0000](../adr/0000-plan-ratification.md) and
the [Prioritization and Launch Framework](prioritization-launch.md#phase-0-0-no-cloud-rule):

- No cloud accounts, subscriptions, free tiers, or trials — including
  provider developer-portal trial tiers that can convert to billable
  resources.
- No sandbox/test provider account is created, and no OAuth app is registered
  against a live provider console, until the channel has cleared stage 2
  (provider qualification) **and** a Phase 1A/1B budget decision approves the
  spend.
- AI-credit and professional-services envelopes (for example, provider
  developer-program fees or paid API tiers) are tracked and approved
  separately from the $0 cloud-infrastructure cap.
- Any exception (for example, the uncapped `fleet-research` run) is recorded
  explicitly in the relevant ADR, never assumed.

A channel cannot move past stage 6 (Validate) while Phase 0's $0 rule is in
effect, because validation requires a sandbox account. Stage 2–5 artifacts
(qualification research, manifest, threat model, adapter design) are the
correct Phase 0 output; implementation and validation wait for the
budget-approved phase named in the connector catalog.

## Lifecycle stages

### 1. Charter the user outcome

- Audience, jobs-to-be-done, read/write/publish behavior, success evidence,
  non-goals, and manual fallback.
- Recorded as a GitHub issue using the "Feature, skill, or channel proposal"
  form (`Channel or connector` type), linked to the relevant milestone.
- Exit evidence: issue merged with problem/outcome, scope, data/permissions,
  and evidence sections filled in.

### 2. Qualify the provider

- Official API and account types, terms, app review, OAuth
  verification/security assessment, pricing, limits, regions, support,
  deprecation policy, and business viability.
- Research current product/API status before promising integrations —
  provider terms change frequently, especially for social/professional APIs.
- Exit evidence: a qualification note (in the issue or a linked ADR) covering
  every bullet above, with sources and dates.

### 3. Define the channel manifest

- Stable ID/version, category, provider, account type, capabilities, OAuth
  scopes, data classes, query/sync/publish modes, webhook/change-feed
  support, retention/cache, costs, and minimum platform version.
- Add a provider delivery-topology ADR: polling/manual, provider watch
  renewal, webhook, Pub/Sub/event bus, gateway/socket, public callback,
  NAT/egress, optional Andreja relay, exposed metadata/content,
  reconciliation, and cost.
- Do not assume every self-host connector can or must expose a public
  callback. Offer a documented polling/manual fallback where provider terms
  and freshness requirements permit; any managed relay is a separate opt-in
  with explicit data disclosure.
- Exit evidence: manifest committed under `docs/skills/` or a channel-specific
  doc; delivery-topology ADR merged.

### 4. Threat/privacy design

- Least privilege, token isolation, consent preview, provenance, untrusted-
  content handling, data-flow/retention map, model exposure, abuse/rate
  limits, disconnect/purge, and user export.
- Tuvok challenges the security artifact; Deanna Troi challenges the privacy
  artifact; Cyrus approves both.
- Exit evidence: threat-model and privacy-classification entries added to
  `docs/threat-model.md` and `docs/privacy.md` (or their Phase 0 equivalents).

### 5. Implement the adapter

- Typed capability interface, provider mapping, idempotency, delta
  reconciliation, retries, backoff, circuit breaking, health, tracing,
  cost/usage events, and no provider types in domain/application code.
- Exit evidence: adapter merged behind the channel-host contract with
  architecture tests proving no provider SDK types leak into domain/UI.

### 6. Validate

- Sandbox/test account, contract/conformance tests, permission-negative
  tests, failure/rate-limit/replay tests, telemetry-redaction tests, E2E user
  scenarios, provider-review evidence, help content, and runbook.
- This is the first stage requiring a live provider account — it cannot start
  before Phase 0's $0 rule is lifted for this channel (see boundary above).
- Exit evidence: test suite green in CI; runbook and help content merged.

### 7. Dogfood read-only/draft first

- Query and draft/export before writes or publishing; require
  proposal/preview/confirmation for side effects.
- Exit evidence: Cyrus (Customer Zero) uses the channel for real personal
  work in read-only/draft mode; feedback captured through Guinan's workflow
  (issue #10).

### 8. Release by stage

- Internal dogfood, invite alpha, private beta, public beta, and GA gates
  with explicit supported account types and limitations at each stage.
- Each stage maps to the launch stages and evidence gates in
  [`prioritization-launch.md`](prioritization-launch.md#launch-stages-and-evidence-gates).
- Exit evidence: a milestone/exit-gate issue records the stage, supported
  scope, known limits, and the decision to proceed, extend, de-scope, or
  stop.

### 9. Operate

- SLOs, freshness/quality, token expiry, webhook/delta reconciliation,
  provider incidents, cost, support ownership, API/version monitoring, and
  user-facing status.
- Guinan receives channel feedback/support trends; the channel owner (Jett
  Reno, plus the domain specialist named in the catalog) remains accountable
  for technical resolution and user-visible status.
- Exit evidence: dashboards, alerts, and a named SLO owner exist per
  [plan §OpenTelemetry and operability](../plan.md#opentelemetry-and-operability).

### 10. Change or retire safely

- Compatibility window, migration/export, re-consent for scope expansion,
  advance deprecation notice, token revocation, cache purge, and tested
  shutdown.
- Exit evidence: a retirement or scope-change issue records the compatibility
  window, notice sent, and purge/revocation test evidence. Status moves to
  `Deprecated` in the connector catalog only after this evidence exists.

## Non-negotiable rules

- Channel writes, sends, shares, publishes, deletes, or financial actions
  always require the capability and confirmation tier defined in the
  manifest and policy evaluator — no channel is exempt because it ships
  first-party.
- Every connector issue records provider terms, supported account types,
  required verification/security assessment, scopes, rate limits,
  webhook/change-feed behavior, data residency/retention, cost, test tenant,
  support burden, and exit strategy
  ([plan §Connector platform](../plan.md#connector-platform)).
- A channel's stage never advances in this framework without matching a
  status transition in
  [`docs/roadmap/channel-connectors.md`](../roadmap/channel-connectors.md) in
  the same pull request.

## Retirement triggers

A channel enters stage 10 (Change or retire safely) when any of the
following occurs, and the trigger is recorded in the retirement issue:

- The provider deprecates the API/scope the manifest depends on.
- A security or privacy artifact identifies an unmitigated risk that the
  provider cannot address.
- Cost or support burden exceeds the envelope Quark approved for the channel.
- Usage/evidence shows the channel does not serve the user outcome in its
  charter (Prioritization and Launch Framework stop criteria, see
  [`prioritization-launch.md`](prioritization-launch.md#program-stop-and-de-scope-rules)).
- A regulated-data channel (rows 18–22 of the connector catalog) loses its
  legal/regulatory basis per Sarek's applicability matrix.

## Cross-references

- Catalog of record: [`docs/roadmap/channel-connectors.md`](../roadmap/channel-connectors.md)
- Sibling framework: [`docs/frameworks/skill-development.md`](skill-development.md)
- Prioritization and launch gates: [`docs/frameworks/prioritization-launch.md`](prioritization-launch.md)
- Feedback lifecycle consumed by "Operate" (stage 9): issue #10 and its
  companion `docs/frameworks/feedback-support.md` (not duplicated here).
