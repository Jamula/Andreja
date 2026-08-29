---
id: d084a307-9448-4e54-bee4-41522014a102
class: DECISION
loadGuidance: [ALWAYS]
title: "Issue-drain prompt revision scope"
author: "Cyrus Jamula via Squad Coordinator"
createdAt: 2026-08-29T19:03:24.797Z
metadata: {}
---

### 2026-08-29: Issue-drain prompt revision scope
**By:** Cyrus Jamula
**Decision:** Update the Squad issue-drain prompt to implement five-child batches, ten-second spawn spacing, and explicit ACK from every child before batch advancement. Also fix Ralph's high-confidence P0/P1 prompt contradictions where current repository/runtime support exists. Where atomic repository-scoped leases or conditional-create/CAS state operations are unavailable, fail closed rather than relying on prose-only concurrency guarantees. Do not expand this change into unsupported runtime implementation unless separately approved.
