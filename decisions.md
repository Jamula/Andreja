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
