---
id: b41604a9-180d-4c7d-83c7-ca7cdf67d2b7
class: DECISION
loadGuidance: [ALWAYS]
title: "Phase 1A issue 44 decision bundle"
author: "Cyrus Jamula"
createdAt: 2026-08-30T17:53:03.585Z
metadata: {}
---

### 2026-08-30T10:52:40.063-07:00: Phase 1A evidence-gate decisions accepted

**By:** Cyrus Jamula
**References:** Jamula/Andreja#44, Jamula/Andreja#38

**Decision:** Accept the complete decision set presented for issue #44:

1. **Phase 1A model usage:** Keep live external model calls at a $0 spend limit; use deterministic fake or local providers for Phase 1A evidence. This does not authorize paid calls.
2. **Identity scope:** Require recovery codes plus a second sign-in path. Defer operator break-glass recovery and external OIDC from Phase 1A.
3. **Recovery custody:** Before the combined database/key recovery drill, explicitly select and record an encrypted backup destination, custodian, retention period, and restore cadence. This accepts the requirement and sequencing; it does not invent or preselect those concrete values.
4. **Release trust:** Name an independent trusted-root holder, require protected branch/tag evidence, and define separate approval evidence for the update and rollback revisions before the release-trust gate may pass.
5. **Evidence closure contract:** Require one final cumulative evidence run at the exact signed commit SHA; retain the signed artifact bundle and verification roots. Explicitly record the Phase 1A scope disposition for ARM64-only execution, local-only PostgreSQL integration testing, and automated-versus-human accessibility evidence before closure. This acceptance does not silently choose unspecified scope values.
6. **Numeric limits:** Collect a local baseline first, then explicitly approve numeric SLO, RPO/RTO, retention, and hard usage limits. Candidate values remain unapproved until separately recorded.
7. **Privacy assessment:** Require a dedicated, proportionate privacy impact assessment and explicit numeric retention/deletion rules before privacy sign-off.
8. **Final governance:** After all nine named specialist verdicts are recorded, Cyrus will accept, amend, or reject ADRs 0001-0005; residual-risk acceptance follows only after all blocking evidence closes; the final Phase 1A disposition is then recorded as proceed, extend learning, de-scope, or stop.

**Already-settled boundaries reaffirmed:** local-only evidence execution; no cloud provisioning or paid external model calls; keyless Sigstore/GitHub OIDC signing; no user data in telemetry, logs, tests, fixtures, GitHub, or Squad state; failures block and receive no success-shaped fallback.

**Execution gates, not additional policy decisions:** the reviewed hosted signing run, combined encrypted PostgreSQL/Data Protection key restore with restored passkey sign-in, separately signed update/rollback exercise, final cumulative exact-SHA evidence rerun, and nine specialist reviews.

**Routing:** Data owns the issue #44 evidence packet; Picard facilitates the final Phase Gate Review. No milestone exit, release, cloud provisioning, paid spend, or residual-risk acceptance is authorized by this decision alone.
