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
