# Phase 1A evidence-gate index

- **Status:** Proposed
- **Date:** 2026-08-23
- **Issue:** [#9](https://github.com/Jamula/Andreja/issues/9)

This is a phase-scoped acceptance index, not a replacement threat model, privacy
inventory, testing matrix, or financial ledger. These are internal dogfood gates,
not public SLAs or production claims.

The current implementation run is recorded in
[`evidence-44.md`](evidence-44.md). Its blocked rows remain blocking; the record
does not approve launch or close this proposed packet.

## Canonical artifact links

| Domain | Canonical source and ownership | Phase 1A use |
|---|---|---|
| Threat | The canonical [`docs/threat-model.md`](../threat-model.md) reconciles current, contract-only, evidence-blocked, and future/gated threats and controls. Tuvok owns challenge; the ratified plan governs. The [charter](../charter.md#ethics-and-sustainability-impact-assessment) is a proposed input until explicitly ratified. | The threat rows below index Phase 1A controls and proof into that canonical model. |
| Privacy | The canonical [`docs/privacy.md`](../privacy.md) owns the inventory and handling baseline. Deanna Troi owns challenge; the ratified plan governs. The [charter](../charter.md#ethics-and-sustainability-impact-assessment) is a proposed input until explicitly ratified. | The privacy gate below is a Phase 1A field/test overlay, not a second data inventory. |
| Testing | The canonical [`docs/testing-matrix.md`](../testing-matrix.md) records current local evidence and exclusions. Data owns challenge; the plan defines the governing [validation strategy](../plan.md#validation-and-regression-strategy). | The table below supplies Phase 1A rows/test IDs to that canonical matrix. |
| Cost | The canonical [`docs/cost-model.md`](../cost-model.md) is Quark's cost-model and ledger source. It remains **Draft for ratification**, authorizes no spend, and the plan's [Cost and FinOps rules](../plan.md#cost-and-finops) remain governing. | This file only links the Phase 1A usage query and stop checks. It neither duplicates price/SKU figures nor changes the cost model's authority. |

These links are self-correcting pointers: the same change that makes a canonical
path, status, owner, or evidence mapping stale must correct this index. Conflicts
resolve in favor of the ratified plan and canonical owner. The proposed charter
does not co-govern until explicitly ratified; this index must then be corrected.

## Threat gate

| Threat | Required control and evidence |
|---|---|
| Cross-tenant ID substitution or worker confusion | Composite database constraints, scoped context, policy checks, and two-tenant read/write/enumeration negative tests |
| Bootstrap or recovery takeover | Single-use bootstrap, HTTPS/RP validation, rate limits, session invalidation, collision tests, recovery audit, and clean restore drill |
| BYOK theft or provider confusion | External key custody, encrypted credentials, endpoint allowlist, separate grants, redaction tests, revoke/rotate test |
| Prompt/tool injection | Untrusted content separation, exact typed-tool allowlist, schema validation, purpose/capability checks, proposal confirmation, adversarial tests |
| Skill/channel confused deputy | No ambient services/secrets, scoped `ISkillHost`/`IChannelHost`, distinct tenant/app-user/principal identity, complete manifest metadata, permission-negative and manifest digest/version tampering tests |
| Grant, consent, or disclosure escalation | One evaluator shared by skill/channel hosts; user policy, bilateral consent, active purpose-bound grant, capability, operation, data class and ordered disclosure intersection; expiry/revocation; content-minimized allow/deny audit; negative tests for wrong tenant/app user/principal/purpose/grant/capability/operation/data class and every attempted ladder escalation |
| Peer-envelope spoof, replay, or confused audience | Canonical signed-envelope vectors; tamper, signature/key/algorithm, sender/recipient, time, nonce replay, idempotency conflict, grant/purpose, payload type, and version rejection tests using local fixtures only |
| Malicious/failed update or restore | Reproducible OCI build from a clean commit; source/base/image/tool digests; SPDX and CycloneDX checksums; pinned dependency/image/container/IaC scans with High/Critical deny policy; signed in-toto provenance under a separately trusted operator key; exact offline inventory verification; explicit migration notes; backup-before-update; clean restore; rollback exercise |
| Telemetry leakage | Attribute allowlist, content suppression, canary-secret redaction test, bounded cardinality and local query review |
| Backup/export disclosure | Encryption, least-privilege destination, explicit exclusions, checksum validation, restore/import access tests |

Any unresolved critical/high threat, isolation failure, reusable-secret leak, or
unexplained outbound call blocks exit. Tuvok challenges the threat artifact and
Cyrus accepts residual risk.

## Privacy gate

The initial classes are identity/authentication, task content, audit/provenance,
assistant content, provider credential, usage/operational, and recovery material.
For each persisted field the implementation records purpose, sensitivity, source,
allowed model exposure, retention trigger, export status, deletion behavior, and
audit requirement.

Defaults are:

- task and assistant content remain tenant data and never enter operational
  telemetry;
- model exposure is deny-by-default per data class and is previewed before consent;
- credentials, passkeys, recovery material, and Data Protection keys are never
  application-exportable;
- usage events contain provider/model, units when reported, duration, result class,
  retry/tool counts, and phase attribution, but no prompt, response, task text, raw
  user ID, or token;
- deletion removes eligible primary and derived data and emits a content-free audit
  receipt; export reports every exclusion;
- semantic assertions distinguish user-stated or verified facts from unreviewed
  hypotheses, keep source references content-minimized, and default model exposure,
  sharing, and sensitive-inference export to denied;
- grant/consent/share-audit fixtures contain no shared payload; future persisted
  records require the canonical privacy inventory and active-slice approval;
- no cloud processor or Andreja service receives self-host content by default.

Deanna Troi challenges the data-flow/retention/export/delete matrix and Cyrus
approves residual risk before dogfood data is entered.

## Test strategy

| Layer | Blocking proof |
|---|---|
| Unit/domain | Task lifecycle, proposal expiry/idempotency, policy combinations, recovery rules |
| Architecture | Dependency direction, no framework/SDK types inward, no cross-module internals, no UI-to-handler/EF path |
| PostgreSQL integration | Migrations from empty/prior schema, composite FK/uniqueness, atomic proposal/task/audit/receipt confirmation, restart and crash recovery, concurrency, delete |
| Contract/conformance | API DTO serialization; assistant fake/failure/cancel; complete skill/channel manifest serialization, semantic/schema versions, explicit non-applicable metadata, permission negatives, digest mutation, concurrency and no-ambient-service checks; exact `Grant`, `ConsentRecord`, `ShareAuditEntry`, disclosure-ladder, and `IPeerChannel` signed-envelope vectors including consent transitions, least disclosure, tamper, audience, expiry, replay, and idempotent retry; semantic assertion and pinned JSON-LD fixtures covering typed ownership, append-only provenance, raw-lineage rejection, atomic single-winner correction/retraction, dependency invalidation, safe predicate IRIs, coexistence, least exposure, isolation/purpose denial, default-sensitive exclusion, delete/tombstone, serialized content-minimization canaries, unknown versions/classes, tamper, and concurrent updates |
| Security/privacy | Cross-tenant enumeration, bootstrap/recovery abuse, CSRF/headers, canary secrets, telemetry/export exclusions |
| End to end | Passkey bootstrap, BYOK/fake assistant proposal, confirmation, create/list/complete/export/delete |
| Operations | Offline startup after image acquisition from a preloaded/locally built image or local registry, restart, update, provider outage, dump/restore plus keys, app import, readiness |
| UX | Keyboard/accessibility basics and phone/tablet/desktop Playwright viewports, reconnect and error recovery |

Live model tests are capped, isolated smoke evidence and never the only blocking
assertion. Deterministic fakes gate normal CI.

### Production-impossible test authentication

`Andreja.TestAuth` exists only as a test project and is never referenced by
`Andreja.AppHost` or any publishable project. There is no environment variable,
header, runtime flag, or production handler that enables a fake principal.
Architecture and publish tests must prove:

1. the production dependency graph and image contain no test-auth assembly;
2. the registered production schemes are the approved passkey/OIDC adapters only;
3. common fake-auth headers and test schemes receive `401`/`403`;
4. configuration fails closed for an unknown authentication scheme.

Phase 1B may use a real dedicated test OIDC tenant only under its separate decision;
it does not weaken this rule.

## Provisional internal SLOs and evidence queries

Metric names are contracts exported over OTLP. A local Prometheus-compatible
evidence profile may evaluate these queries. Targets require Cyrus approval after a
local baseline and are not external commitments.

| Internal objective | Provisional target | Evidence query |
|---|---:|---|
| Confirmed task capture succeeds | >= 99% over 7 days | `sum(increase(andreja_task_capture_total{outcome="success"}[7d])) / sum(increase(andreja_task_capture_total[7d]))` |
| Confirm-to-persist latency | p95 <= 2 s over 7 days | `histogram_quantile(0.95, sum by (le) (rate(andreja_task_capture_duration_seconds_bucket[7d])))` |
| Passkey sign-in succeeds | >= 99% over 7 days | `sum(increase(andreja_signin_total{method="passkey",outcome="success"}[7d])) / sum(increase(andreja_signin_total{method="passkey"}[7d]))` |
| Assistant typed-tool call succeeds | >= 95% over 7 days | `sum(increase(andreja_tool_call_total{outcome="success"}[7d])) / sum(increase(andreja_tool_call_total[7d]))` |
| Content-policy telemetry violations | exactly 0 after >0 checks | First require `sum(increase(andreja_telemetry_policy_checks_total[7d])) > 0`; only then assert `sum(increase(andreja_telemetry_policy_violation_total[7d])) == 0` |
| Restore drill freshness | successful within approved RPO/RTO | `max(andreja_restore_drill_timestamp_seconds)` plus signed drill record |

Zero denominators or zero policy checks produce "insufficient evidence," never
success. Every evidence record includes build digest, schema version, window, sample
count, query version, owner, and known exclusions. RPO/RTO and retention windows
remain human decisions.

A content-free PostgreSQL usage-ledger query supports cost review:

```sql
SELECT provider, model, count(*) AS sessions,
       sum(input_units) AS input_units,
       sum(output_units) AS output_units,
       sum(duration_ms) AS duration_ms,
       sum(retry_count) AS retries
FROM assistant_usage
WHERE occurred_at >= :window_start
GROUP BY provider, model;
```

Column names are the required evidence shape, not a frozen physical schema.

## Cost index and stop gates

- Phase 0 cloud-infrastructure spend remains `$0`; this packet performs only
  local/paper analysis and selects no cloud resource.
- A real BYOK call is blocked until Quark records published-price assumptions,
  per-session/task estimates, warning/hard-stop thresholds, and Cyrus approves the
  Phase 1A model-spend envelope in the canonical cost source identified above.
- Unknown provider units remain unknown. They are not converted into invented cost.
- Unmetered spend, missing redaction evidence, envelope breach, or unreconciled
  provider usage triggers no-go and pause.
- Managed hosting cost, cloud observability, CIAM, managed database, and cloud
  provisioning belong to a separately approved Phase 1B spike.
