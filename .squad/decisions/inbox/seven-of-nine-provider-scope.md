### 2026-09-07: Resolve assistant-provider phase scope
**By:** Seven of Nine
**What:** Phase 1A runtime is limited to the deterministic fake and optional
Andreja-native OpenAI-compatible BYOK provider. Phase 1A may contain only a
credential-free, non-shipping Copilot SDK compile/conformance spike. A real
Copilot provider begins no earlier than Phase 1B after every ADR 0009 gate and
separate activation approval.
**Why:** This preserves the independent self-hosted and offline Phase 1A
critical path while advancing SDK compatibility evidence without creating
entitlement, isolation, retention, credential, operational, or cost exposure.
