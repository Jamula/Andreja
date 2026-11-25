---
id: 7c3c0ee8-b00f-4363-802f-8ee2eeff1870
class: POLICY
loadGuidance: [ALWAYS]
title: "No-Python tooling policy and .NET 10 migration target"
author: "Cyrus Jamula (via Seven)"
createdAt: 2026-11-25T10:34:31.309Z
metadata: {}
---

### 2026-11-24T23:13:45.733-08:00: Tooling and scripting language policy
**By:** Cyrus Jamula (via Seven)
**What:** Never author new Python scripts or interactive Python helpers in repository workflows or agent sessions; use `.NET 10` (`C#` + `MSTest`), `Node.js` (`JS`/`TS`), `Rust`, or `PowerShell 7`. Migrate legacy repository Python scripts (`check_docs_consistency.py`, `generate_architecture_diagram.py`, and Python doc tests) to `.NET 10` (`C#` + `MSTest`).
**Why:** User cross-repository engineering preference and elimination of 5-language tooling fragmentation.
