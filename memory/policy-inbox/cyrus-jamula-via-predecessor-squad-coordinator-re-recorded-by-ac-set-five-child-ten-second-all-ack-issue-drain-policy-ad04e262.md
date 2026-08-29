---
id: ad04e262-984c-472f-8029-1d7843d91a10
class: POLICY
loadGuidance: [ALWAYS]
title: "Set five-child ten-second all-ACK issue-drain policy"
author: "Cyrus Jamula via predecessor Squad coordinator, re-recorded by active coordinator"
createdAt: 2026-08-29T19:00:40.639Z
metadata: {}
---

### 2026-08-29T11:59:59.134-07:00: Set issue-drain batch pacing and fail-closed advancement policy
**By:** Cyrus Jamula (relayed by predecessor Squad coordinator; re-recorded by active coordinator)
**What:** Admit up to five child issue sessions per batch, space spawn attempts at least 10 seconds apart, and require an explicit valid ACK from every child before the batch advances.
**Overrides:** Lower verified platform capacity and all retrospective, duplicate, collision, dirty-state, remote-drift, uncertain-creation, lease, privacy, and validation gates override the target.
**Safety hold:** Do not admit new queue work until the reported P0 issue-drain contract findings are addressed and independently reviewed.
**Why:** Preserve higher safe parallelism while making batch advancement explicitly fail-closed.
