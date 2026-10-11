---
id: 93211d3a-d813-4e82-ae57-1b124e0b8686
class: DECISION
loadGuidance: [ALWAYS]
title: "Repair CI failures on active PRs"
author: "Cyrus Jamula"
createdAt: 2026-10-11T06:26:37.427Z
metadata: {}
---

User directive: "when there is a failing CI check please try to fix it as part of the work on the current PR, even when it not necessarily caused by the change in the PR". Investigate and repair failing CI within the active PR, including pre-existing failures, using precise root-cause fixes and targeted validation. Coordinate shared-file ownership. Do not hide failures, disable checks/tests, weaken protections/security/privacy, introduce secrets or authorize infrastructure/spend; escalate genuine external/permission/policy blockers.
