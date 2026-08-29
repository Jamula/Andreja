---
id: 1337a563-5ddd-4da1-a420-944aff2434b3
class: DECISION
loadGuidance: [ALWAYS]
title: "Phase 1A keyless signing direction"
author: "Cyrus Jamula via Squad Coordinator"
createdAt: 2026-08-29T06:37:27.222Z
metadata: {}
---

### 2026-08-28T23:22:18.320-07:00: Phase 1A keyless signing direction
**By:** Cyrus Jamula
**Related evidence:** former issue #62; ongoing Phase 1A evidence gates
**Decision:** Use external keyless Sigstore/OIDC for Phase 1A signing evidence instead of a local file-backed signing key and independent public-key custodian.
**Boundary change:** This supersedes the prior local-only execution boundary for signing identity and transparency services only. It does not authorize paid services, production resources, cloud infrastructure provisioning, or external spend.
**Open design choice:** Select the approved OIDC identity/workload path and document privacy, availability, transparency-log, and verification consequences before implementation.
