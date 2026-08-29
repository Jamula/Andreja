# Team Decisions

### 2026-08-28T19:22:58.476-07:00: Design Review disposition for issues #108 and #102
**By:** Picard (Design Review)

**What:** Issue #108 may proceed after live-base verification. Issue #102 remains blocked because its feasibility window terminated without spend; it requires a natural candidate plus a newly approved budget and feasibility window before proceeding.

**Why:** The Design Review confirmed #108 has a viable path subject to fresh-base validation, while #102 no longer has an active authorized feasibility window or budget basis.

### 2026-08-28T21:17:54.580-07:00: Weekly retrospective enforcement (consolidated)
**By:** Picard (Retrospective with Enforcement)

**What:** Queue admission fails closed when no completed weekly retrospective log exists within seven days; Ralph must not admit additional queue work until the ceremony completes. Completion requires review of current GitHub evidence, governed ceremony logging, recording required team decisions, and filing every genuinely new concrete improvement as a non-duplicate GitHub issue labeled `retro-action`. Existing issues remain the source of truth for already-tracked improvements and must not be duplicated.

**Why:** The retrospective was overdue, enforcement automation was unavailable, and readiness was obscured by acceptance-gate and blocker-state drift. The team reviewed 30 closed issues and 56 merged PRs, found 27 open issues and PR #50, and filed tracking action Jamula/Andreja#122 for the genuinely new improvement.

### 2026-08-28T21:17:54.580-07:00: Privacy/consent approval for feedback framework
**By:** Deanna Troi (Privacy and Consent Lead)

**What:** APPROVE the privacy and consent design in `docs/frameworks/feedback-support.md` and its Phase 1B acceptance path for Jamula/Andreja#10. This approval does not claim implementation or authorize collection, GitHub publication, external email, deployment, or launch.

**Remaining gates:** Approved controller/legal-basis/notice and retention/DSR decisions; tenant-less isolation/encryption/access/residency/subprocessor and tracking-secret decisions; transactional-email provider/sender/consent/delivery-event controls; classification/impact assessment; threat/abuse and data-flow artifacts; exercised runbooks and acceptance tests; controlled live exercise; and Cyrus residual-risk/launch approval.

**Evidence:** https://github.com/Jamula/Andreja/issues/10#issuecomment-5460345080

### 2026-08-29T04-36-52: Security gate for issue #10: APPROVE docs/frameworks/feedback-support.md as a planning/non-authorizing artifact; Phase 1B implementation, acceptance, and blocking-artifact-dependent closure remain BLOCKED.
**By:** Tuvok
**What:** Security gate for issue #10: APPROVE docs/frameworks/feedback-support.md as a planning/non-authorizing artifact; Phase 1B implementation, acceptance, and blocking-artifact-dependent closure remain BLOCKED.
**References:** issue #10, PR #16, PR #117, docs/frameworks/feedback-support.md, docs/threat-model.md, docs/privacy.md, Guinan, Deanna Troi, Cyrus Jamula
**Why:** ## Decision

**Verdict: APPROVE (planning artifact only)** — Tuvok (Security Engineer) approves the security/documentation gate for `docs/frameworks/feedback-support.md`, explicitly scoped as a planning, non-authorizing artifact.

**BLOCKED:** Phase 1B implementation/acceptance and any closure of issue #10 that depends on the named blocking security artifacts.

Requested by Cyrus Jamula through Ralph. Posted at https://github.com/Jamula/Andreja/issues/10#issuecomment-5460364032

## Rationale

No public intake implementation exists. The framework documents requirements and plans, not implementation evidence. All controls below are labeled **planned** unless noted otherwise.

Design assessment by dimension:
- **Abuse/unsafe content** — adequate as design (planned). Layered rate/size/concurrency limits, schema and type allow lists, archive rejection, secret and high-risk screening, backpressure/quarantine/dead-letter/cost caps, accessible challenge only on evidence, no cross-site profiling, no downgrade of suspected vulnerabilities into spam (feedback-support.md L257-278).
- **Encryption/access** — adequate as design, not decided. Separately encrypted contact destinations, masked triage display, least-privilege role separation, content absent from logs/traces/metrics/queue metadata, classification inherited by backups/replicas/quarantines (L181-225). Encryption/key custody, rotation, residency, subprocessors, and admin access remain deferred Phase 0 decisions (L641-662).
- **Private security/privacy reporting** — adequate as design, partially current. `SECURITY.md` and `.github/ISSUE_TEMPLATE/config.yml` route vulnerability and data-incident reports privately today; `S0 Restricted` stops ordinary triage. `security.txt` and the public-facing private-report surface are not in place.
- **Public-to-private boundary** — strong as design (planned). Separately deployed service and queue with no trust path to an Andreja data plane; no tenant identifier accepted, generated, inferred, or used as a partition key; content-free correlation metadata on the queue; uniform non-revealing responses (L181-225). Threat model still lists support intake and public/help site as future/gated (threat-model.md L97, L310-318; L184 contract-only).
- **Diagnostic consent/preview** — strong as design (planned). Diagnostics default empty and require field-level consent receipts; publication requires a dedicated versioned consent receipt on the exact GitHub preview, revalidated before creation, with no substitution from diagnostic/contact/terms consent, an app-owned least-privilege GitHub grant, and no feedbackId/tracking secret/contact/tenant reference in the published issue (L399-432).
- **Deduplication** — adequate as design (planned). Runs after privacy screening and before drafting, using sanitized fields and short-lived protected abuse signals rather than durable fingerprinting; failure messages must not confirm existence of duplicates, persons, addresses, tracking references, or incidents (L257-278). Dedupe-privacy negative tests not yet written.
- **Phase 1B email** — adequate as design, not decided. Outbound-only minimized email, channel revocation, bounce/complaint suppression, approved essential-notification rules, required non-email status/access/delete path. Provider, sender identity/domain, delivery-event retention, consent posture, subprocessors, and cost cap undecided (L641-662); SPF/DKIM/DMARC evidence outstanding.

Privacy baseline independently records feedback/support submission as future/gated with no intake deployed (privacy.md L93), consistent with this verdict.

## Remaining security gates (blocking Phase 1B)

1. Intake-specific threat/abuse model — current canonical threat model excludes/defers support intake (threat-model.md L97). Dedicated STRIDE/LINDDUN pass required on tenant-less intake, queue, triage view, and publisher.
2. Anti-abuse decision — thresholds, control set, evidence retention, appeal path, degraded mode that cannot silently lose accepted records.
3. Isolation/encryption decision — tenant-less queue/store topology, HTTPS/security-header baseline, encryption at rest and in transit, key custody and rotation, residency, backups, restricted access.
4. Tracking credential lifecycle — secret generation/storage/expiry, recovery, DSR proof with throttling and enumeration resistance (L227-255).
5. Negative test matrix — enumeration, replay, prompt/content injection reaching GitHub triage or the publisher, prohibited-default regressions, dedupe-privacy leakage.
6. Private vulnerability reporting surface — `security.txt` and public-facing private-report route live before any public intake opens.
7. Attachments stay closed until scanning, quarantine, type/size, and retention controls are approved (L641-662).
8. Phase 1B email decisions — provider, sender identity, consent/essential-notification posture, SPF/DKIM/DMARC alignment evidence.

No collection, GitHub publication, or outbound email may activate before gates 1-8 pass and Cyrus records residual-risk approval.

## Evidence

- https://github.com/Jamula/Andreja/blob/main/docs/frameworks/feedback-support.md#L181-L225
- https://github.com/Jamula/Andreja/blob/main/docs/frameworks/feedback-support.md#L257-L278
- https://github.com/Jamula/Andreja/blob/main/docs/frameworks/feedback-support.md#L399-L432
- https://github.com/Jamula/Andreja/blob/main/docs/frameworks/feedback-support.md#L641-L662
- https://github.com/Jamula/Andreja/blob/main/docs/threat-model.md#L97
- https://github.com/Jamula/Andreja/blob/main/docs/threat-model.md#L184
- https://github.com/Jamula/Andreja/blob/main/docs/threat-model.md#L310-L318
- https://github.com/Jamula/Andreja/blob/main/docs/privacy.md#L93

## Boundaries

This is a security verdict only. It does not declare legal compliance and does not replace privacy review (Deanna Troi / Sarek) or Cyrus's residual-risk approval.

### 2026-08-28T22:04:41.627-07:00: Phase 1A evidence governance path
**By:** Cyrus Jamula
**Issue:** Jamula/Andreja#62
**Decision:** Update the Phase 1A evidence packet to match current repository reality, provisionally accept the updated packet as the governing work target, and then complete the missing evidence. Do not claim Phase 1A exit/completion and do not authorize external spend through this decision.
**Remaining gates:** trusted operator signing; encrypted PostgreSQL-and-key recovery with restored passkey sign-in; separately signed update/rollback proof; numeric service, recovery, retention, and model-spend limits; specialist reviews; final residual-risk acceptance by Cyrus.
**Disposition:** Keep #62 provisional while amendment and follow-up evidence work proceed.

### 2026-08-29T00:19:56.420-07:00: Authorize bounded default-branch negative exercise
**By:** Cyrus Jamula (via Squad coordinator)
**What:** Run exactly one bounded live negative enforcement exercise for issue #67: create a disposable PR with one intentionally failing required check, verify GitHub reports it blocked, issue no merge command, make no repository-setting change, then close the PR and remove its branch after recording evidence.
**Limits:** No ruleset, merge-queue, CODEOWNERS, approval, secret-scanning, billing, subscription, trial, or infrastructure change is authorized.
**Why:** Validate enabled controls before deciding any repository-setting mutation.

### 2026-08-29T00:39:35.749-07:00: Pause issue #67 while Actions restriction is privately resolved
**By:** Cyrus Jamula (via Squad coordinator)
**What:** Preserve draft PR #130, commit 66705f1c817a16857bf282b44bb836efd3003f2a, branch, and worktree unchanged while Cyrus privately resolves the systemic hosted GitHub Actions execution restriction.
**Limits:** No reruns, edits, pushes, settings/billing/runner changes, public diagnostics, PR close/delete/merge/queue/bypass, or alternate evidence method without a new explicit decision.
**Why:** Failures occur before runner allocation and cannot prove isolated required-check enforcement.

### 2026-08-28T23:22:18.320-07:00: Phase 1A keyless signing direction
**By:** Cyrus Jamula
**Decision:** Use external keyless Sigstore/OIDC for Phase 1A signing evidence instead of a local file-backed signing key and independent public-key custodian.
**Boundary change:** Supersedes the local-only boundary for signing identity and transparency services only; no paid services, production resources, cloud infrastructure provisioning, or external spend.

### 2026-08-28T23:22:18.320-07:00: GitHub Actions keyless signing identity
**By:** Cyrus Jamula
**Decision:** Implement Phase 1A keyless Sigstore signing with GitHub-hosted Actions workload identity. Verification must exact-match the GitHub OIDC issuer, repository, protected workflow identity, workflow revision, and permitted tag/ref; actor identity alone is insufficient.
**Disclosure accepted:** Permanent external Fulcio/certificate-transparency/Rekor records may contain repository/workflow metadata and artifact identity; do not publish personal email identity.
**Boundaries:** No new cloud infrastructure, paid service, production resource, trial, or external spend. Retain bundles and trusted roots for network-independent verification. Use a dedicated implementation issue and reviewed policy/tool upgrade.

### 2026-08-28T22:04:41.627-07:00: Phase 1A local evidence execution
**By:** Cyrus Jamula
**Issue:** Jamula/Andreja#62
**Decision:** Restore required tooling locally and run every Phase 1A evidence gate locally, without cloud/production provisioning, external spend, or waived gates.

### 2026-08-28T22:04:41.627-07:00: Windows ARM64 evidence runtime
**By:** Cyrus Jamula
**Issue:** Jamula/Andreja#62
**Decision:** Authorize elevated WSL2 enablement, one reboot, Docker Desktop ARM64 installation through WinGet, acceptance of applicable Docker terms, and starting Docker Desktop for local Phase 1A evidence. No cloud resources or external spend; applicable free-use licensing must remain satisfied.

### 2026-08-29T00:45:11.771-07:00: Select Quark co-lead topology for finance-adjacent skills
**By:** Cyrus Jamula (via Squad coordinator)
**What:** Quark will co-lead Finance Administration, Trading Research and Review, Lifestyle Rewards, Miles and Points, and Employer Benefits, with explicit ledger, custody, conflict-of-interest, and decision-authority boundaries.
**Limits:** Does not authorize implementation, establish compliance, permit professional claims, or change repository artifacts.

### 2026-08-29T00:50:37.694-07:00: Require strict three-ledger separation
**By:** Cyrus Jamula (via Squad coordinator)
**What:** Maintain independent company-accounting, product-tenant-metering, and user finance/rewards ledgers, with no shared source of truth or cross-ledger write/reconciliation path.

### 2026-08-29T00:52:16.105-07:00: Set user-controlled tenant-isolated custody
**By:** Cyrus Jamula (via Squad coordinator)
**What:** User finance/rewards records remain user-controlled in the tenant-isolated product data plane. Quark has no standing access and receives only minimized, non-user consultation evidence.

### 2026-08-29T11:42:23.263-07:00: Set conflict disclosure and recommendation boundary
**By:** Cyrus Jamula (via Squad coordinator)
**What:** Quark must disclose finance-adjacent conflicts involving company accounting, budgets, vendors, sponsors, compensation, or Quark-owned FinOps metrics, but may advise. Seven of Nine alone makes the product recommendation.

### 2026-08-29T11:43:41.013-07:00: Require two-step professional and regulatory review
**By:** Cyrus Jamula (via Squad coordinator)
**What:** Before user-specific finance-adjacent guidance, Sarek must classify legal/regulatory applicability and an independent qualified human must approve the methodology and claims.
**Limits:** Does not establish compliance, appoint the reviewer, authorize implementation/launch, or approve professional claims.

### 2026-08-29T11:48:12.499-07:00: Retain sole launch authority for finance-adjacent features
**By:** Cyrus Jamula (via Squad coordinator)
**What:** Only Cyrus may authorize launch through explicit recorded approval based on Seven of Nine's recommendation and completed specialist gates. Quark is advisory without launch authority or veto.

### 2026-08-29: Issue-drain prompt revision scope
**By:** Cyrus Jamula
**Decision:** Update the issue-drain prompt for five-child batches, ten-second spawn spacing, and explicit ACK from every child before advancement. Fix Ralph's high-confidence P0/P1 contradictions where supported. Where atomic repository leases or conditional-create/CAS state are unavailable, fail closed rather than rely on prose-only concurrency guarantees. Do not expand into unsupported runtime implementation without separate approval.

### 2026-08-29T12:55:03.294-07:00: Issue-drain five-agent waves
**By:** Cyrus Jamula
**What:** For Squad issue drain, spawn sub-agents in waves of five, space spawn attempts 10 seconds apart, and wait for an ACK from every member of the wave before admitting the next wave. Apply only with confirmed platform capacity and the active issue-drain safety contract; never duplicate ownership or bypass stale-base, retrospective, dependency, collision, or ACK gates.

### 2026-08-29T20:00:26.964-07:00: Use App ownership for issue 132 recovery
**By:** Cyrus Jamula
**What:** For issue #132 recovery, do not block on a separate atomic lease/CAS probe; let the App system handle session/worktree ownership. Apply narrowly to the rejected #132 revision: reconcile first, create one isolated App worktree/session for Spock, preserve duplicate/collision checks and the explicit ACK-before-release gate, keep Jett Reno locked out, and require independent review before any readiness claim. Does not authorize merge, auto-merge, enqueue, or ordinary backlog admission.

### 2026-08-29T12:54:23.291-07:00: Issue 132 review rejection
**By:** Squad Coordinator
**What:** Data independently reviewed PR #134 / issue #132 after merge and REJECTED the artifact. Blocking findings: caller-forgeable writer authorization; bypassable pacing, capacity, and fallback; self-asserted retrospective evidence without provenance or Scribe-quiescence proof. Jett Reno authored the rejected artifact and is locked out for this revision cycle. Data recommends Spock as revision owner. Ordinary issue-drain writer admission remains frozen. The merged contract requires verified repository-scoped atomic ownership; no such capability has been verified, so no revision writer may be admitted until the runtime supplies it or Cyrus separately authorizes an allowed recovery path.
