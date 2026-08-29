---
id: adbb49d2-15b4-4314-af2e-b027c2331c87
class: DECISION
loadGuidance: [ALWAYS]
title: "GitHub Actions keyless signing identity"
author: "Cyrus Jamula via Squad Coordinator"
createdAt: 2026-08-29T06:53:40.911Z
metadata: {}
---

### 2026-08-28T23:22:18.320-07:00: GitHub Actions keyless signing identity
**By:** Cyrus Jamula
**Decision:** Implement Phase 1A keyless Sigstore signing with GitHub-hosted Actions workload identity. Verification policy must exact-match the GitHub OIDC issuer plus repository, protected workflow identity, workflow revision, and permitted tag/ref; actor identity alone is insufficient.
**Disclosure accepted:** Signing creates permanent external Fulcio/certificate-transparency/Rekor records containing repository/workflow metadata and artifact identity. Do not publish personal email identity.
**Boundaries:** No new cloud infrastructure resource, paid service, production resource, trial, or external spend is authorized. Retain bundles and trusted roots for network-independent verification. This requires a dedicated implementation issue and reviewed policy/tool upgrade.
