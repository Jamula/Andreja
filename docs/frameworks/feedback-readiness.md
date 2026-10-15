# Feedback readiness execution packet

**Version:** feedback-readiness-v1

**Accountable role:** Guinan; quality co-lead/evidence custodian: Data

**Applicability:** proposed feedback capability only; no running service

**Document review date:** 2026-10-15; specialist approval: NOT RECORDED

**Execution status:** NOT RUN; feedback remains inactive

This is the entry point for the non-activating documentation slice of
[#158](https://github.com/Jamula/Andreja/issues/158), not a policy amendment,
runtime implementation, authorization, successful exercise, or launch packet
with completed evidence. It implements the operator handoff in the existing
[framework](feedback-support.md). Start with:

1. This packet: decision provenance, required inputs, authority and execution records.
2. [Operator runbooks](../runbooks/feedback-operations.md): ordered handling,
   containment, recovery and drill instructions.
3. [Canonical feedback help](../help/feedback.md): versioned feedback-specific
   help source, not a claim that a support channel exists.
4. [Exact 14-scenario execution plan](feedback-readiness-scenarios.md): setup,
   actions, observable assertions and failure evidence.

Website integration belongs to [#93](https://github.com/Jamula/Andreja/issues/93);
identity/PostgreSQL evidence belongs to the existing delivery owners, and
operations evidence tooling to Jett Reno. This slice changes none of them.

## Decision provenance and precedence

Checked against the issue bodies and latest Cyrus comments on 2026-10-15.
The older source documents contain October 10/11 status snapshots; their claims
that #155–#157 are open are historical, not current issue status. We leave
hash-governed source documents unchanged. A conflict in substantive requirements
still blocks execution; this status note does not amend them.

| Record | Current administrative/specification result | Still required before execution |
| --- | --- | --- |
| [#155](https://github.com/Jamula/Andreja/issues/155), [exact specification decision](https://github.com/Jamula/Andreja/issues/155#issuecomment-6138983913), [administrative completion](https://github.com/Jamula/Andreja/issues/155#issuecomment-6139419194) | CLOSED; specification `feedback-privacy-v1.1` / `feedback-notice-v1.1` approved at `142f227e8457a573010a3c333e94e0eda0a8d6ac`, LF SHA-256 `d50dbd302c7ed139ee2acbf6e7923e72198c3f217dc46abc00c81818c49bb34e`; closure explicitly skipped evidence | Qualified-counsel determination and joint Cyrus/counsel executable scope-specific disposition; applicable reviews, implementation and propagation evidence. No counsel signoff inferred. |
| [#156](https://github.com/Jamula/Andreja/issues/156), [final specification decision](https://github.com/Jamula/Andreja/issues/156#issuecomment-6198438831) | CLOSED; latest final Cyrus decision approves the exact security document set at `d9d7d89c9d52d6d6fc2a7240d14a522476a2f2c2` only | Exact-version eligible role reviews, measured controls, tests and runtime evidence remain missing. |
| [#157](https://github.com/Jamula/Andreja/issues/157), [specification decision](https://github.com/Jamula/Andreja/issues/157#issuecomment-6198529157) | CLOSED; latest Cyrus decision approves specification only at `d9d7d89c9d52d6d6fc2a7240d14a522476a2f2c2`, LF SHA-256 `b3bf9d5960c2ff7234365d90b4bcd260e55dcadec6252dc7fdec05ddce6c0275` | All eight evidence areas remain unverified, including provider/residency/sender choices, priced Quark recommendation and required verdicts. No effective provider or spend authorization. |
| [#158](https://github.com/Jamula/Andreja/issues/158) | OPEN; execution/authorization record | Separate Evidence Authorization, actual capacity/help/test/drill/teardown evidence, recommendations, reviews and separate final decision. |

Security source LF SHA-256 values at that approved commit are
`405e42445816bd7890318a9df124d36e2f94003a0689910781fb637a1066d918`
for [the security package](feedback-security-abuse-incident-routing.md) and
`f64b5f82f91d0ae303b00d11604cf0139c9573c7ecaf506ae2d427d3bebc7b62`
for [its supplement](feedback-security-156-decision-supplement.md).
Use the exact document set, not the supplement's historical candidate-version
label alone. Privacy requirements come from
[the privacy package](../legal/feedback-privacy-retention-dsr.md); platform
requirements from [the platform package](feedback-platform-decision.md).

Specification approval is a prerequisite, not proof of its implementation,
operational/counsel review, exact-head ordinary-code sign-off, or authority to
activate. No administrative closure, merged PR, favorable specialist comment,
blank form or document check supplies those things.

## Operator-supplied configuration and decision matrix

These are required **record fields**, not implemented environment variables or
activation switches. Values are UNSET unless an authorized record actually
supplies them. All execution defaults are **disabled/off/no-collection/deny**.
Unknown is not approved, zero-cap is not a free-tier exception, and an omitted
value cannot inherit a provider default. Missing substantive values block
execution, not document authoring or service-free synthetic fixture planning.
No policy or binding duty can be bypassed by a runtime parameter.

For each row the operator records value, unit/scope, source decision URL,
version/digest, proposing role, approving authority, expiry/review trigger and
validation evidence. Cross-reference restricted evidence instead of copying
private inventory, recipients, proof or counsel advice to GitHub.

| Required input group | Proposed accountable roles / approval boundary | Validation before use; safe unset behavior |
| --- | --- | --- |
| Mode and channel enablement | Guinan/Jett; Cyrus stage decision | Local authoring only; collection, publication, sends, provisioning, analytics and attachments off. Every boundary independently denied until expressly authorized. |
| Roles, purposes, inventory, basis, notices and granular diagnostic/publication consent | Deanna Troi/Sarek; #155 and applicable qualified counsel | Match every field/derived class to purpose, recipients, classification, versioned notice/receipt, lawful basis and withdrawal. Reject unknown diagnostic, class or purpose. |
| Jurisdiction, retention, proof, conflict/appeal, holds and lawful disposition | Deanna Troi/Sarek/qualified counsel; scoped Cyrus/counsel record under #155 | Validate scope, authority, original trigger/time zone, duration/end condition, decision deadline, owner, propagation and backup duties. Include the supplement's proposed receipt/stub and no-return appeal classes in the approved #155 inventory; their purpose, retention and proof cannot be assumed. Protect documented binding preservation duties. No universally lawful purge fallback, silent extension, clock reset or indefinite frozen-content workaround. Missing disposition disables the affected route before it operates. |
| Security/control register and private routes | Tuvok/Jett; #156 requirements and exact-version reviewers | Supply numerical size/rate/concurrency/queue/retry/throttle/TTL limits, nonce/idempotency windows, verifier encoding/custody, key roles, isolation and degraded-mode rules. Bind each control to appeal, accessible alternative, cost/retention and test. Validate direct private incident/contact and conflict-class independent recipient; unavailable is not a staffed route. |
| Providers, regions, terms and resources | Jett/Quark with Spock, Tuvok, Deanna Troi, Sarek, Guinan, Data; #157 | Enumerate topology, stores/replicas/queues/backups/keys, processors/transfers, sender/DNS, callback and exit paths. Validate terms/date, residency, least privilege and deletion against #155/#156. No provider, region or account selected here. |
| Numeric resources/messages/cost bounds | Jett/Quark; priced #157 scope plus Cyrus #158 authorization | Quoted units, fixed/variable/ancillary costs, reserve-before-action hard stops, alert threshold, cancellation/renewal triggers and reconciliation. Caps must fit #157, never exceed its proposed ceilings without a new applicable decision; no usable USD cap exists in this packet. All spend/sends denied when unset. |
| Capacity and service targets | Guinan/Data/Jett/Quark/Sarek; Cyrus #158 decision | Supported hours with time zone/calendar, languages, named assigned staff/backup/on-call, restricted-incident coverage, handoff overlap, backlog maximum and measured throughput. Supply achievable numeric acknowledgment, update, escalation, aging, verification, closure and DSR targets with clock start/pause/end rules, exception coverage and source. No public time commitment until capacity and lawful deadlines are validated. |
| Commitment catalog | Guinan with Data, Jett, Quark and Sarek reviews; Cyrus | Every proposed external commitment names exact wording/scope, accountable owner, capacity/cost/legal dependency, measurement source, numerator/denominator where relevant, breach detector/escalation and de-scope/stop action. Required reviews are individual recorded verdicts, not appointments by this document. |
| Restricted metrics catalog | Guinan/Data; Deanna Troi/Tuvok purpose/access review and #158 approval | Complete the catalog below; analytics off, exports denied and small groups suppressed until approved and tested. No product or financial joins. |
| Runbook access, approval and cadence | Each runbook's proposed owner/approver; #158 | Appoint actual eligible operators, verify least privilege and fallback coverage, bind implemented procedure, numeric drill cadence/unit and revalidation triggers. No numeric cadence is invented here. Last successful exercise: NOT RUN. |
| Help inventory and accessibility | Guinan/Jadzia Dax/Data; privacy/security and claims reviewers as applicable | Bind version/owner/review date/review-expiry, supported languages, route/link evidence, WCAG 2.2 AA keyboard/screen-reader/zoom/error recovery and plain-text equivalents. Unverified routes remain unavailable. |
| Evidence identity and custody | Data/Jett; Cyrus Evidence Authorization | Release commit, immutable artifact digest, fixture digest, complete private inventory, operators/custodian/reviewers/approvers, validity and revocation controls. Drift denies execution and invalidates affected results. |

### Metrics catalog and prohibited-use tests

Create one approved row for **each** enabled measure in the framework's allowed
support-quality list, not a generic permission to count everything. Each row
must contain:

- Metric ID/version, owner, purpose and operational decision it supports.
- Controlled source fields/events; definition; numerator/denominator;
  inclusion/exclusion rules and zero-denominator treatment.
- Aggregation dimensions and time bucket, minimum privacy/group threshold,
  freshness and reconciliation checks, and suppression representation
  (suppressed is neither zero nor an exact count).
- Classification, restricted reader/export roles, retention trigger/duration,
  purge/recompute/deletion/backup behavior and approval reference.
- Explicit prohibited joins and uses and test IDs covering each restriction.

Data constructs synthetic events with independently known totals; include a
below-threshold group, boundary/time-bucket edge, zero denominator, revoked/
deleted/restricted source and an unauthorized reader/exporter. Reconcile
permitted totals; inspect emitted fields and rejected interface operations.
Reject text, raw diagnostics, contact, IP, direct or pseudonymous identifiers,
tracking material and user timelines. Attempt joins to task/profile, prompts,
connector data, advertising IDs, unrestricted telemetry and company accounting;
all must deny. Attempt ranking, targeting, pricing, moderation and personalization
use; all must deny through the supported interface and access policy. Keep
financial accounting separate; cost reconciliation does not permit case-level
financial joins. A missing source/access/use test blocks that metric.

## Separate Evidence Authorization

**Current record: MISSING. No exercise is authorized.**

Before requesting authorization, Guinan/Data verify explicit specification
decisions **and** resolve the outstanding prerequisites above, including counsel
disposition and required reviews. Jett prepares a private inventory; public
#158 receives only a safe immutable reference/digest. Cyrus must explicitly
approve/amend/reject that exact packet **in #158**. Suggested record shape:

| Field | Required supplied value (currently UNSET) |
| --- | --- |
| Artifact identity | Release commit; immutable built-artifact digest; fixture manifest and digest; approved document/config digests |
| Private allowlist | Exact non-production accounts/providers/resources/regions, queues, stores including quarantine/dead-letter/incident/callback stores, backups, keys and custody, DNS records, sender identities, verifier-controlled recipients, operators, Data custodian, reviewers and approvers |
| Exclusions | Synthetic-only; no real user, tenant, contact, support, security, incident or DSR records; no production credentials/tenants; no public intake, indexing, generally reachable launch endpoint or general email |
| Validity | Issuance/start/end timestamps with zone; expiry no later than 14 days after issuance; automatic deny after expiry; Cyrus revocation path and proof it works |
| Limits | Exact priced #157-bounded USD cap plus hard resource/message/record/byte/concurrency caps; allowlisted outbound sender and recipients only; cost reservation and stop evidence |
| Custody/observability | Boundary-specific least privilege, credential custodians, content-free observations, retention/access/purge; no secrets or recipient identifiers in public evidence |
| Stop authority | Cyrus may revoke at any time; Tuvok, Deanna Troi and Jett may stop/contain security, privacy, scope or cost failure immediately |
| Exit contract | Deny ingress/sends; revoke keys, grants, sessions and receipt verifiers; purge queue/store/event/backup scope lawfully; remove DNS/sender; cancel/disable providers; reconcile cost and obtain receipts |

An extension, expired window, resource/provider/region substitution, recipient
expansion, changed artifact/configuration scope or other inventory drift requires
**NEW explicit authorization**, never an operator edit. New evidence cannot be
collected under a stale grant. A local fake is useful for planning, not a
substitute for controlled-live or provider evidence.

## Ordered execution and evidence custody

1. Guinan/Data check prerequisites, actual eligible role assignments/reviews and
   every required input; record missing values under existing #155–#158 owners.
   Do not operate a route to discover its legal disposition.
2. Data snapshots release commit, immutable artifact digest, fixture digest,
   configuration/authorization digest and document versions into the restricted
   execution manifest. Jett independently compares deployed identity and
   inventory. Any mismatch stops before the first action.
3. Jett proves deny-before-start, deny-after-expiry, revocation, recipient/
   resource/cost caps and isolation using only authorized synthetic resources.
   Data records observations, not just configuration screenshots.
4. Execute all [14 scenarios](feedback-readiness-scenarios.md) and all
   [runbook drills](../runbooks/feedback-operations.md), including their negatives.
   Store content-free outcomes and restricted evidence references per run.
5. On a failure, stop affected processing, contain with the runbook owner,
   preserve only authorized minimized evidence, and record FAILED or BLOCKED.
   Accepted records must not disappear; reconcile before any replay. No
   suppressed error, skip or unavailable dependency becomes PASS.
6. Exercise expiry/revocation **and teardown**: compare every inventory object
   to disablement/deletion/cancellation receipts; verify receipt/session/key
   invalidity and absence of sender/DNS, queue/store/event/backup scope.
   Apply authorized lawful hold exceptions, record residuals, and do not claim
   full purge if anything remains. Reconcile reservations, actual metering and
   provider invoice/statement; unresolved charges or objects block completion.
7. Data signs custody and coverage against the exact manifest. Guinan checks
   capacity, help, commitments, metrics, drills and residual-risk records.
   Required reviewers inspect the same artifact set and record actual verdicts.
8. Only then present the separate final decision below. Document creation,
   static checks, specification approval and authorization are never pass evidence.

Each execution record contains run ID, authorized scope/time, actor role,
commit/artifact/fixture/config digests, scenario or drill ID, command/procedure
binding and version, expected/observed result, start/end, status, evidence
digest/custodian/restricted reference, reviewer and any failure/decision issue.
Evidence excludes content, contacts, proof, raw/recoverable secrets and private
investigation. A digest proves identity, not safety or success. Custody failure,
fixture drift or rebuilt artifacts require affected evidence to be rerun under
the appropriate new authorization.

## Acceptance and final decision record

All twelve #158 acceptance areas remain **UNSATISFIED BY TEMPLATES ALONE**:

| #158 AC area | Execution evidence needed | Current result |
| --- | --- | --- |
| 1: prerequisite approvals | Exact decisions plus unresolved counsel/review/control/provider evidence carried to execution | MISSING evidence |
| 2: discrete Evidence Authorization | Explicit Cyrus decision on the exact complete authorization | MISSING |
| 3: exact authorized exercise scope | Identity/inventory/fixture comparisons and negative drift observations | NOT RUN |
| 4: expiry/revocation/caps/teardown | Enforced denial and complete reconciled exit receipts | NOT RUN |
| 5: capacity and numeric targets | Measured coverage/throughput and approved targets | MISSING |
| 6: external commitments | Complete catalog and Guinan/Data/Jett/Quark/Sarek verdicts | MISSING |
| 7: all 14 exact-release scenarios | Actual automated plus controlled-live observations | NOT RUN |
| 8: runbooks | Appointments, approvals, cadence and successful drills for all 11 topics | NOT RUN |
| 9: canonical help | Actual link/accessibility/ownership/version/staleness and route checks | NOT RUN |
| 10: restricted metrics | Approved catalog and reconciliation/privacy/prohibited-use evidence | NOT RUN |
| 11: residual risks/blockers | Complete scoped risk record, owners, decisions and failure disposition | MISSING |
| 12: final recommendation/decision | Guinan/Data recommendations, all required verdicts and separate Cyrus decision | MISSING |

In #158 maintain execution records, not a competing backlog:

- **Commitment record:** catalog above, measured result/breach, reviewer verdicts.
- **Exercise record:** manifest, each scenario/drill result, teardown and custody.
- **Risk record:** ID, evidence, affected scope, likelihood/impact rationale,
  accountable proposed role, mitigation, trigger, stop/recovery, unresolved input,
  existing owning issue and decision reference. Unknown stays MISSING, not accepted.

Known blockers already belong to #155 (counsel/proof/retention/disposition),
#156 (reviews/control/isolation/private-route proof), #157 (priced vendor/
residency/sender/cap/exit evidence), #158 (capacity, metrics, authorization,
tests, drills, risks and launch) and #93 (website/help integration). Searching
open and closed feedback/decision issues found no need for a duplicate gate.
An actually new actionable decision is proposed to the coordinator with
scope, owner, safe development path and required runtime input; do not open
issues merely for blank execution fields.

The final record must identify the exact release/artifact/fixture and complete
evidence set, Guinan/Data recommendations, Picard, Tuvok, Deanna Troi, Sarek,
Jett Reno, Quark, Jadzia Dax and applicable delivery-owner verdicts, unresolved
risks, commitments and rollback/stop authority. Reviewer eligibility and
lockout remain applicable; no author self-approves a rejected artifact.

Cyrus separately records **proceed / extend-learning / de-scope / stop** in
#158. Proceed requires explicit scoped residual-risk acceptance and the actual
capacity/help/14 tests/drills/teardown/reviews, not these forms. Extend-learning
needs a new Evidence Authorization for any further exercise. De-scope requires
consistent revised help/capacity/tests and applicable decisions, not waiver of a
binding duty. Stop leaves collection/publication/sends disabled and executes
authorized containment/exit. Even proceed does not bypass separate deployment
or change controls.

## Document validation and revalidation

Run the repository documentation checks and the packet-specific contract tests:

```powershell
python .github\scripts\check_docs_consistency.py
python -m unittest discover -s tests -p test_feedback_readiness_docs.py
git diff --check
```

These check documentation only. They do not run the 14 runtime scenarios.
Revalidate this packet at each proposed release and any decision, route,
provider, consent, retention, capacity, accessibility or artifact change.
The operator must supply an approved numeric review interval/expiry; none is
silently assumed. Last successful operational exercise: **NOT RUN**.
