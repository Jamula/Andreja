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
