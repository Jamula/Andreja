---
id: 20496137-2ccb-44c5-8dde-655523baa701
class: POLICY
loadGuidance: [ALWAYS]
title: "Issue-drain five-child ACK batches"
author: "Cyrus Jamula via Squad Coordinator"
createdAt: 2026-08-29T18:59:59.143Z
metadata: {}
---

### 2026-08-29: Issue-drain admission policy
**By:** Cyrus Jamula
**Directive:** Admit up to five child issue sessions per batch. Space spawn attempts ten seconds apart. After the batch is spawned, require an explicit ownership/base/duplicate-check ACK from every child before advancing the batch or admitting more work.
**Safety boundaries:** Respect any lower verified platform capacity. Continue one issue/owner/session/branch/worktree/PR ownership, pre-spawn duplicate and collision checks, definitive-non-creation fallback, and no replacement while creation outcome is uncertain. A missing or blocked ACK prevents batch advancement.
