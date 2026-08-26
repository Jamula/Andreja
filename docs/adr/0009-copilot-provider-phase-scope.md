# ADR 0009: Qualify the Copilot SDK before a limited Phase 1B provider

- **Status:** Proposed
- **Date:** 2026-08-26
- **Issue:** [#74](https://github.com/Jamula/Andreja/issues/74)
- **Governing:** [Platform plan](../plan.md#assistant-and-ai-architecture),
  [ADR 0000](0000-plan-ratification.md), and
  [ADR 0004](0004-phase-1a-assistant-skill-channel-contracts.md)
- **Decision owner:** Cyrus
- **Approvals required:** Cyrus after named architecture, privacy, security,
  FinOps/operations, and qualified legal review

## Status and decision

This ADR is the explicit **recommended resolution** of #74, not an accepted
decision and not evidence of Cyrus approval. Merge records reviewed proposal
text only. Activation remains prohibited until Cyrus accepts this ADR and every
applicable entry gate below has current evidence.

The recommendation is:

1. **Phase 1A has no real Copilot provider.** Its runtime providers remain the
   deterministic fake and the optional Andreja-native OpenAI-compatible BYOK
   adapter. The fake remains the offline/default CI path. A local
   OpenAI-compatible endpoint may satisfy the independent path; an external
   endpoint additionally requires the existing disclosure and model-spend
   gates.
2. **The Copilot SDK is integrated into the pre-runtime toolchain in Phase 1A
   only.** This means a pinned, non-shipping compile-time spike in an isolated
   development/test project; a conformance mapping from SDK DTO/events into
   `IAssistantProvider`/`IAssistantSession`; schema/version-drift and dependency
   vulnerability/license checks; and developer commands that can build the
   spike without credentials. It may use recorded synthetic fixtures. It is
   excluded from the app's production dependency graph, container, service
   registration, configuration, UI, deployment manifests, startup, network
   paths, and release claims. It must not authenticate, start a Copilot runtime,
   send content, invoke a model, provision an account, or incur usage.
   The spike's build job must scrub Copilot/GitHub credential environment
   variables and stores, make Copilot/GitHub executables unavailable, deny
   egress, prohibit client-start/session APIs, and verify the production graph
   and published artifact contain no Copilot dependency. “Credential-free” is a
   tested property, not an instruction to developers.
3. **A limited real Copilot provider is Phase 1B at the earliest.** It begins
   only after all entry gates below pass. Start with synthetic content, one
   consenting authorized adult/test identity, an explicit typed-tool allowlist,
   hard budget, short retention/cleanup window, and a reversible canary. It is
   not the default provider and does not replace BYOK or the deterministic fake.
4. **Defer beyond Phase 1B when a gate is unanswered or fails.** Phase 1B may
   continue with BYOK/fake where its separately approved outcomes permit.
   Schedule pressure, SDK availability, a successful compile, documentation, or
   an existing Copilot subscription is not a waiver.

## Why this scope

### Repository evidence

- `IAssistantProvider` and `IAssistantSession` are application-owned contracts;
  their tenant, application-user, principal, purpose, tool, cancellation, result,
  and content-free usage shapes contain no provider SDK types.
- The deterministic provider validates the session tool allowlist and supports
  cancellation/disposal. It is the tested offline fallback.
- The OpenAI-compatible adapter is present and has local conformance coverage for
  endpoint validation, typed-tool parsing, credential rotation/revocation,
  bounded responses, retry/cancellation, content-free metrics, and a
  zero-by-default external-unit budget. Provider pause/kill remains a required
  operational gate rather than implemented adapter evidence.
- No `GitHub.Copilot.SDK` package reference, Copilot adapter, runtime
  registration, token store, or release configuration exists. Adding one to the
  shipping graph would create new auth, process, filesystem, egress, retention,
  support, and commercial boundaries rather than complete the current Phase 1A
  seam.
- ADR 0004 is Proposed. It requires a deterministic default and independent BYOK
  path, keeps SDK types outside application contracts, and describes Copilot as
  a future optional implementation. This ADR narrows provider timing only; it
  does not accept ADR 0004.

### Current external evidence

The following sources were rechecked on 2026-08-26:

- GitHub announced the Copilot SDK as generally available on 2026-06-02 with a
  stable API and production support. The configured NuGet feed currently
  exposes `GitHub.Copilot.SDK` 1.0.11 as its latest package.
- GitHub's OAuth guide says user OAuth requests use each user's Copilot
  subscription, each user needs an active subscription, and the application
  owns token storage, refresh, and expiry. GitHub's authentication guide also
  documents organization-attributed server-to-server and BYOK paths; those are
  distinct commercial/authentication choices and are not authorization for
  Andreja.
- GitHub's multi-tenancy guide requires `mode: "empty"` for a shared server,
  explicit tools, per-session credentials, application authorization around
  resume/delete, and deliberate filesystem/session-state isolation. GitHub's
  scaling guide characterizes shared-runtime isolation as logical and
  per-user-runtime isolation as stronger.
- The backend guide describes a separately operated headless CLI, no official
  prebuilt CLI container, no built-in SDK-to-CLI authentication, local
  session-state storage, health/cleanup duties, and a single-runtime failure
  domain. These are operator responsibilities, not guarantees inherited by
  using the SDK.
- The persistence guide says persisted sessions can contain conversation
  history, tool results, planning state, and artifacts. Disconnect preserves
  them; explicit deletion removes them. Provider keys are not persisted there.
- Usage events and RPCs expose useful token/credit/quota signals, but some
  aggregate/context RPCs are experimental and ephemeral events are not replayed.
  Provider usage values therefore do not substitute for invoice reconciliation
  or an Andreja hard budget.
- GitHub's model-hosting disclosure varies by model, host, feature maturity,
  account type, and user setting. Some features/models have exceptions to
  zero-data-retention arrangements, and individual-plan interaction data may be
  used for model improvement subject to current policy/settings. No universal
  Andreja retention, residency, training, or abuse-monitoring claim is safe.
- Copilot use is governed by the then-current account agreement, GitHub
  additional/product terms, acceptable-use rules, privacy disclosures, and
  provider/model terms. Documentation is technical evidence, not counsel's
  approval of redistribution, funded allowances, or a customer-facing service.

These facts establish feasibility and uncertainty. They do not establish that
Andreja's intended use is entitled, isolated, private, affordable, lawful, or
operable.

## Provider entry gates

Every row is blocking before user/content activation. A synthetic runtime canary
is itself real-provider activation, but it generates rather than precedes some
live evidence. Before that canary, Cyrus must accept this ADR and explicitly
authorize only the synthetic canary after the SDK/support, entitlement, synthetic
data-flow/recipient, isolation design, runtime-channel, auth-custody,
retention/residency/training/abuse terms, hard-budget, offline/fallback,
deterministic-test, legal/privacy/security, and rollback preconditions below are
documented. No user content or user/cohort identity is permitted until the
synthetic canary passes and every row is complete. Evidence must identify the SDK
and CLI/runtime versions, account/plan, model, authentication path, topology,
region, and review date.

| Gate | Required evidence before any real provider activation | Stop / rollback trigger |
|---|---|---|
| SDK/version/support | Pin compatible GA SDK and CLI/runtime versions; compile the adapter; archive generated-schema/conformance results; record support channel, deprecation/update policy, dependency license and vulnerabilities; prohibit experimental APIs from release-critical accounting unless separately accepted. | Unsupported/incompatible version, schema drift, unresolved critical dependency issue, or no support path |
| Entitlement and billing | Written mapping for per-user subscription, organization server-to-server, or BYOK; confirm who is licensed, attributed, charged, rate-limited, and permitted to serve the scenario; no pooled or silently transferred user entitlement. | Unknown entitlement, attribution, invoice owner, quota behavior, or redistribution right |
| Tenant/user isolation | Adversarial two-tenant proof for credentials, model cache, session ID/state, tools, filesystem, concurrency, resume/list/delete, logs, and cleanup. Use `mode: "empty"`. Prefer per-user runtime initially; a shared runtime requires measured evidence and explicit risk acceptance. | Cross-tenant read/write/infer/enumerate, shared ambient tool/host access, stale credentials, or unowned session |
| Prompt/tool/data exposure | Versioned data-flow map of every prompt, system instruction, typed-tool schema/arguments/results, artifact, metadata, filter, model host, subprocessor, telemetry path, and support path; data minimization and injection/adverse-tool tests. | Undeclared recipient/egress, tool widening, prohibited content, or inability to explain exposure |
| Retention, residency, training, and abuse monitoring | Account-, model-, feature-, host-, and region-specific current terms; GitHub/runtime local persistence map; numeric Andreja TTL; delete/purge verification; training/opt-out and abuse/safety-review disclosure. Do not generalize one model's terms. | Unknown or changed terms, unavailable required region/deletion, inaccurate disclosure, or retention beyond approved TTL |
| Authentication and credential custody | Separate GitHub assistant grant from app identity, GitHub content, and feedback/publishing; least scopes; encrypted token/refresh-token custody; no CLI-login fallback; rotation, revocation, expiry, reauthentication, incident, and break-glass tests. | Token/session crossover, plaintext or logged secret, fallback to another identity, failed revocation, or unclear custodian |
| Runtime control channel | Keep SDK-to-CLI traffic on loopback or an isolated same-pod/sidecar boundary. Any cross-process/container-host path requires mutually authenticated encryption, strict workload/network policy, connection-secret rotation, and a negative test proving unauthorized workloads cannot reach, observe, resume, delete, or invoke the runtime. Never expose the headless RPC listener publicly. | Unauthenticated or cleartext reachable RPC, failed workload identity, unexpected listener, or unauthorized session/tool operation |
| Budgets and cost | Approved numeric per-session/user/tenant/day/spike limits; pre-call reservation and hard stop; quota/premium-request behavior; content-free usage ledger; anomaly alert; funded-versus-user-funded disclosure. Reconcile Andreja/organization-funded use to its provider statement/invoice. For user-funded subscriptions, reconcile Andreja session metrics to account quota/attribution and explicitly record that Andreja has no currency invoice; do not collect a personal billing statement without a separate purpose/privacy approval. | Unmeasured usage, unenforceable stop, experimental-only accounting, unexpected charge, missing funding-mode evidence, or envelope exhaustion |
| Availability, fallback, and offline | Timeouts, cancellation, backpressure, rate-limit/outage behavior, health/readiness, bounded retries, cleanup, capacity, support/runbook, and SLO evidence. Fake and BYOK remain independently selectable; offline startup makes no Copilot call. | Startup dependency, runaway retry, leaked partial work, unavailable kill, or fallback mutates semantics/data unsafely |
| Consent and disclosure | Before enablement, identify recipient(s), account/billing owner, model, purpose, data classes, tools, persistence/retention, training/abuse handling, cost, limitations, and disconnect/delete effects. Require affirmative opt-in and provider-specific re-consent on material change. | Missing/stale/inaccurate disclosure, coerced/default consent, or affected person lacks authority |
| Audit and provenance | Content-minimized records bind tenant, principal, purpose, provider/model/version, auth mode, policy/tool set, request/result IDs, usage, consent/disclosure version, timestamps, deletion, and rollback without storing prompts, responses, tokens, or tool arguments in the usage ledger. | Cannot attribute a call/action/cost, audit leaks content, or provenance can be forged/crossed |
| Tests and canaries | Before a synthetic canary: deterministic contract, SDK fixture/schema, no-egress/tool-deny, and shipping-graph exclusion tests. Before any user/content activation: the capped synthetic live canary plus prompt-injection, cancellation, timeout, quota, token-expiry, retention/delete, crash/restart, unauthorized-runtime-access, and two-tenant tests. Non-deterministic live smoke is supplemental only. | Canary leakage/crossing, flaky live test becomes sole gate, missing negative test, or cleanup cannot be proved |
| Legal, privacy, security, and abuse approval | Qualified counsel/vendor review of then-current agreements, commercial/customer-facing use, acceptable use, IP/content, funded allowance, subprocessors and notices; named privacy/security/abuse review; classification/impact assessment; Cyrus accepts documented residual risk. | Reviewer no-go, unresolved high/critical issue, terms change, prohibited use, or approval/evidence expires |
| Rollback and exit | Feature flag default off; provider pause/kill; revoke credentials; stop runtime/egress; drain/cancel; delete approved local/provider state where supported; preserve minimized audit; switch to fake/BYOK; notify affected users; rehearse rollback and define owner/time objective. | Kill or deletion fails, provider remains reachable, data/action diverges, or fallback/notification is unavailable |

## Qualification sequence

1. **Toolchain spike, no runtime:** pin SDK/CLI, compile an isolated adapter against
   synthetic fixtures, map only through provider-neutral contracts, generate
   schema-drift evidence, scrub ambient credentials/stores, remove Copilot/GitHub
   executables, deny egress, statically prohibit runtime-start/session APIs, and
   prove the shipping dependency graph/artifact remains Copilot-free.
2. **Gate packet:** produce the entitlement/terms, data-flow, privacy/security,
   threat, FinOps, operations, test, support, and rollback evidence above. Record
   named reviewer verdicts and expiry/re-review triggers.
3. **Local synthetic runtime canary:** only after explicit pre-canary
   authorization as defined above, use synthetic data, one dedicated test
   identity, no ambient tools, explicit session storage, zero shared-user state,
   short TTL, and a hard budget. Its evidence completes the remaining live gates
   before any user/content activation.
4. **Limited Phase 1B user canary:** only after the local canary passes, obtain
   informed opt-in, run the Open Loops proposal-only scenario, reconcile usage,
   verify purge/revocation, and exercise rollback.
5. **Topology decision:** shared runtime remains disallowed unless its adversarial
   evidence is at least as strong as the approved isolation target. Otherwise
   retain per-user runtime isolation or defer.

## Approver record required for acceptance

Acceptance requires a dated record containing:

- Cyrus's explicit decision and residual-risk acceptance;
- architecture verdict on provider-neutral contracts and topology;
- privacy verdict on purpose, recipients, retention/residency/training,
  consent/disclosure, deletion, and the classification/impact assessment;
- security/abuse verdict on auth custody, tenant/session/tool/filesystem
  isolation, prompt injection, monitoring, incident response, and rollback;
- FinOps/operations verdict on entitlement, quotas, hard budgets,
  reconciliation, SLO/support, capacity, fallback, and kill/cleanup;
- qualified legal verdict on the then-current applicable agreements and intended
  personal, invite-cohort, funded, and customer-facing uses; and
- exact evidence links, versions, expiry/re-review triggers, accepted exceptions,
  and owners.

Silence, issue closure, PR merge, package availability, a successful spike, or
current personal Copilot access is not acceptance.

## Consequences

- Phase 1A remains independently useful, self-hostable, provider-neutral, and
  testable offline without GitHub, Andreja cloud, or a model account.
- The requested Copilot SDK toolchain objective gets early
  compile/schema/adapter feedback without creating runtime, entitlement,
  privacy, spend, or release claims.
- Phase 1B gets a reversible path to real Copilot evidence without making it a
  release dependency or hiding uncertainty behind a broad “supported” claim.
- BYOK is not reimplemented on the Copilot SDK in Phase 1A. The existing
  Andreja-native adapter remains the independent provider path; an SDK-based BYOK
  experiment, if useful later, is still governed by the same gates.
- Provider deferral cannot block offline startup or remove deterministic
  conformance. A failed Copilot gate narrows the phase rather than weakening the
  boundary.

## Cost delta

- The proposal authorizes **$0 runtime/model/provider spend** and no account,
  subscription, trial, cloud resource, or live canary.
- The Phase 1A toolchain spike consumes only separately approved development
  engineering/AI credits, existing CI/package-feed capacity, dependency review,
  and maintenance time. Before implementation, its issue must estimate those
  units, name the development envelope, set a time/credit/CI-minute stop
  threshold, and name an owner. Unestimated work is no-go.
- A real Phase 1B canary requires a separate product-provider/model allowance
  distinct from the development-AI, Phase 1A model-spend, professional-services,
  and cloud-infrastructure envelopes. It records the funding mode, billable
  units, hard stops, statement/invoice or user-quota reconciliation source,
  cancellation path, and lower-cost fake/BYOK fallback.
- This ADR provides no price or unit-cost estimate because no account, plan,
  funding mode, model, or topology is selected. That absence is itself a
  blocking FinOps gate, not an assumption of zero future cost.

## Alternatives considered

### Real Copilot in Phase 1A

Rejected as the recommendation. It would add a real account/token, SDK/CLI
process, filesystem state, model/subprocessor exposure, terms, support, cost, and
retention boundaries before Phase 1A's independent recovery, SLO, retention,
spend, and residual-risk exits are complete. It supplies little evidence beyond
what the provider-neutral contract and BYOK conformance already prove.

### Keep phase optional with no minimum

Rejected. “Optional” leaves acceptance and budget claims ambiguous and permits a
provider to become a de facto release dependency without passing a named gate.

### Defer all Copilot work until after Phase 1B

Not selected. A bounded non-runtime compile/conformance spike is reversible
evidence that can reduce SDK drift risk and satisfy the toolchain requirement
without activation, but its development/CI cost must be estimated and capped as
described above. If the spike cannot remain isolated from shipping or
network/auth behavior, defer it too.

## Primary sources

- [Copilot SDK general availability](https://github.blog/changelog/2026-06-02-copilot-sdk-is-now-generally-available/)
- [Copilot SDK authentication](https://docs.github.com/en/copilot/how-tos/copilot-sdk/auth/authenticate)
- [GitHub OAuth setup](https://docs.github.com/en/copilot/how-tos/copilot-sdk/setup/github-oauth)
- [Multi-tenancy and server deployments](https://docs.github.com/en/copilot/how-tos/copilot-sdk/setup/multi-tenancy)
- [Scaling and multi-tenancy](https://docs.github.com/en/copilot/how-tos/copilot-sdk/setup/scaling)
- [Backend services setup](https://docs.github.com/en/copilot/how-tos/copilot-sdk/setup/backend-services)
- [Session resume and persistence](https://docs.github.com/en/copilot/how-tos/copilot-sdk/features/session-persistence)
- [Usage and billing metrics](https://docs.github.com/en/copilot/how-tos/copilot-sdk/features/usage-and-billing)
- [Hosting of models for GitHub Copilot](https://docs.github.com/en/copilot/reference/ai-models/model-hosting)
- [GitHub Acceptable Use Policies](https://docs.github.com/en/site-policy/acceptable-use-policies/github-acceptable-use-policies)
- [GitHub Terms for Additional Products and Features](https://docs.github.com/en/site-policy/github-terms/github-terms-for-additional-products-and-features)
- [`GitHub.Copilot.SDK` NuGet package](https://www.nuget.org/packages/GitHub.Copilot.SDK/)
