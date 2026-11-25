---
id: a9d94928-3e92-4c0c-9aa1-41eb37d85896
class: LOCAL
loadGuidance: [ON-DEMAND]
title: "Docs-CI, upgrade backup, and milestone guardrails"
author: "Cyrus Jamula (via Seven)"
createdAt: 2026-11-25T10:34:43.386Z
metadata: {}
---

### 2026-11-24T23:13:45.733-08:00: Documentation CI, Chesterton's Fence, and milestone guardrails
**By:** Seven (Audit reflection)
**What:** Documentation consistency checks enforce SHA-256 hashes on `docs/plan.md` (`ADR 0000` + latest amendment row), `ADRs 0001-0005`, and all 12 Phase 0 status artifacts, and require `README.md` to match `charter...pending` without `charter...ratified` while `ADR 0006` is `Proposed`. Never commit `*.local-backup` files after `squad upgrade`; never delete `.squad/templates/ralph-triage.js` without updating `squad-heartbeat.yml` and `docs-consistency.yml`; keep 100% of open GitHub issues assigned to one of the 15 `Phase 0-12` milestones; keep `docs/research/test-suite-current-inventory-112.json` synced when `.NET` tests are added/removed in inventoried suites.
**Why:** Prevents CI breaks and accidental deletion of active runtime dependencies.
