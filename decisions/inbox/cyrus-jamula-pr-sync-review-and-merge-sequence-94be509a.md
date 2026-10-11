---
id: 94be509a-796e-419b-ba52-98833a1bfe82
class: DECISION
loadGuidance: [ALWAYS]
title: "PR sync review and merge sequence"
author: "Cyrus Jamula"
createdAt: 2026-10-11T06:25:44.102Z
metadata: {}
---

User directive: "when reviewing active work list the active PRs as well; start by rebasing to main, follow up by addressing the PR feedback; then merge to main". Include active PRs in work reviews. For authorized active scoped PRs, sync/rebase against current main, address feedback and validate exact new head, then merge only after applicable required checks/reviews/thread resolution and explicit policy gates are satisfied. Preserve human policy acceptance scope: merging Proposed packages does not accept or activate them. Never bypass protections or reviewer rejection lockouts; exact approved content changes require renewed approval. Archive completed eligible sessions after merge/no remaining work.
