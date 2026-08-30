---
id: 34207416-60d2-48ff-9ce3-d6a13af24cb0
class: DECISION
loadGuidance: [ALWAYS]
title: "Simplify issue admission ownership"
author: "Cyrus Jamula"
createdAt: 2026-08-30T02:49:18.543Z
metadata: {}
---

2026-08-29T19:48:48.237-07:00 — User directive: Remove the repository-scoped atomic CAS/lease requirement from issue admission and replace it with a simpler single-coordinator guard. Preserve fail-safe duplicate detection and do not weaken unrelated issue, worktree, validation, privacy, or merge gates.
