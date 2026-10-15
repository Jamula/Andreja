# Feedback operator runbooks

**Version:** feedback-runbooks-v1; **owner:** Guinan

**Applicability:** proposed feedback capability; **document review:** 2026-10-15

**Approval and implemented procedure bindings:** NOT RECORDED

**Last successful exercise, every runbook:** NOT RUN

These are executable handling sequences once their prerequisites and concrete
procedure bindings exist; they do not claim that consoles, endpoints, jobs or
controls are implemented. Use the [readiness packet](../frameworks/feedback-readiness.md)
and [scenario plan](../frameworks/feedback-readiness-scenarios.md). Never invent
a command, access grant, address, target, cadence or legal rule to fill a gap.

## Common preflight, evidence and stop contract

Applies to **each** runbook below:

1. Confirm the exact source decisions and outstanding prerequisites in the
   readiness packet. Bind each action to an implemented, reviewed procedure/
   admin operation, expected observation and regression test at the release
   commit/immutable artifact/fixture/configuration digest. Missing binding
   blocks execution; local service-free planning remains allowed.
2. Confirm an unexpired, unrevoked #158 Evidence Authorization for synthetic
   non-production work, or the distinct applicable later stage authorization.
   Compare artifact and private inventory to the grant. No real reports,
   public collection, provider activity or sends are authorized by this document.
3. Appoint the proposed accountable role and approver below through #158;
   verify operator identity, purpose-bound least privilege and audited access.
   A role label is not an appointment. Ordinary triage has no direct database,
   queue, contact-vault, verifier or incident-store browsing privilege.
4. Supply approved numeric drill cadence/unit, next due time and acknowledgment/
   update/escalation/aging targets from measured capacity. Drill before proposed
   launch, after relevant change/failure and at that supplied cadence. An unset
   cadence or unavailable coverage blocks operation, not authoring.
5. Record run ID, manifest digests, trigger/time, authorized actor, action/
   observation, result, safe correlation/reason codes, counts, scoped receipts,
   evidence digest/custodian/restricted reference and reviewer. Never attach
   case text, addresses, diagnostics, secrets, proof or public investigation.
6. If identity, authorization, confidentiality, custody, cap, coverage or
   acceptance fails: stop affected intake/publication/sends, deny optional
   processing, notify the named escalation owner privately and mark FAILED/
   BLOCKED. Jett/Tuvok/Deanna may contain immediately; Cyrus may revoke.
   Do not destroy documented binding-duty evidence or guess a retention rule.
7. Recovery requires reconciled state, corrected cause, repeat negative tests,
   owner and approver review, and valid stage authorization. Changed artifact/
   inventory/recipient/expiry needs NEW authorization. Never restore an unsafe
   grant, withdrawn consent, revoked receipt or deleted record as rollback.

## Proposed accountability, access and drill register

All rows require the common preflight, approved cadence and safe evidence above.
Approvers review procedure/recovery within their domain; they do not approve
launch, legal advice or risk acceptance on Cyrus's behalf.

| Runbook | Proposed accountable owner / approver | Trigger | Minimum purpose-bound access | Cadence input / last successful exercise |
| --- | --- | --- | --- | --- |
| R01 intake health/recovery | Jett / Tuvok and Data | Intake health/backlog/cap or persistence signal | Boundary-scoped health/counters; separately authorized recovery job, no case-body browse | REQUIRED / NOT RUN |
| R02 privacy screen | Guinan / Deanna Troi | New or changed private submission | Screened triage view; restricted reviewer alone sees suspect scope | REQUIRED / NOT RUN |
| R03 private handoff | Tuvok or Deanna Troi for incident class / appropriate independent privacy/security reviewer | Vulnerability, secret, data incident or conflict | Boundary-specific restricted incident role; ordinary staff only opaque reference | REQUIRED / NOT RUN |
| R04 dedupe/split | Guinan / Data | Sanitized candidate match or incorrect merge | Screened case links and authorized transition operation | REQUIRED / NOT RUN |
| R05 publication | Guinan / Deanna Troi and Tuvok | Optional consented draft, grant failure, withdrawal or unsafe publication | Draft/consent review; app-owned scoped publisher; removal only by authorized responder | REQUIRED / NOT RUN |
| R06 tracking custody | Tuvok / Data and Jett | Issuance, lost receipt, rotation, revocation, replay or enumeration signal | Boundary/record-scoped receipt operation; custodians manage verifiers, never retrieve secrets | REQUIRED / NOT RUN |
| R07 return/email | Jett / Guinan and Deanna Troi | Verify/change/revoke contact; delivery failure/bounce/complaint | Masked return-channel view; dedicated per-boundary sender/event roles | REQUIRED / NOT RUN |
| R08 rights/holds | Deanna Troi / Sarek with qualified counsel where required | Access/correction/deletion/objection/withdrawal/conflict/hold trigger | Restricted rights view, scoped proof result, authorized propagation jobs | REQUIRED / NOT RUN |
| R09 abuse/appeal/cost | Tuvok / Jett, Quark and Deanna Troi | Limit, suspected abuse, challenge failure or appeal | Short-lived approved abuse signals, no content or fingerprint profiling | REQUIRED / NOT RUN |
| R10 lifecycle/handoff | Guinan / Data and delivery owner | Aging, absent owner, transfer, delivery or reopen request | Screened case state and authorized transitions | REQUIRED / NOT RUN |
| R11 metric privacy | Guinan/Data / Deanna Troi and Tuvok | Generation, threshold failure, export/access review, purge or misuse | Approved aggregate interface; separately scoped catalog/source-event custodian | REQUIRED / NOT RUN |

## R01 Intake health, backpressure and recovery

**Prerequisites:** approved per-boundary persistence, queue/backlog/byte limits,
retry/dead-letter TTL, cost cap, outage response, recovery binding and coverage.

1. Check content-free liveness/readiness, durable-acceptance counters, queue age/
   depth/bytes and reserved versus actual cost against supplied thresholds.
   An HTTP processing response is not proof a case was durably accepted.
2. If persistence or authorization cannot be checked, deny new acceptance;
   use the approved uniform non-enumerating degraded response. Never claim a
   receipt/case exists if persistence failed.
3. Apply approved backpressure before bounds are exceeded. Pause workers/sends
   as necessary; separate privacy quarantine from queue dead-letter metadata.
   Do not inspect body-bearing queues or move content into operational logs.
4. Reconcile accepted durable IDs with queued/completed/idempotency outcomes
   through the scoped recovery operation. Replay only unprocessed authorized
   IDs within retry/cost/TTL bounds; demonstrate a second replay changes nothing.
5. Verify no accepted record is lost and each has its independent safe status;
   reconcile dead-letter/purge receipts and cost before resuming.

**Evidence:** before/after counts/ages, rejection class, durable-versus-processed
reconciliation and replay transition counts; no payloads. **Fail/stop:** cap
breach, loss, cross-boundary job or unresolved discrepancy. Escalate Jett/Data,
Tuvok for isolation and Quark for cost. **Recovery:** repair persistence/control,
repeat outage/replay/cap negatives, then common recovery; never bypass quarantine.

## R02 Intake privacy screening and safe redaction

**Prerequisites:** approved inventory/notice/consent versions, prohibited-class
rules, restricted route, sanitization and enforced classification operations.

1. Keep the submission private and diagnostics/attachments empty unless
   separately approved and individually consented. Compare source boundary;
   public intake must reject even null/empty tenant fields.
2. Before ordinary access or dedupe, classify incident/conflict triggers.
   Restrict uncertain/high-risk material and use R03, not ordinary investigation.
3. Validate each diagnostic's allow-listed name, exact preview/purpose,
   affirmative selection and receipt version/digest. Reject stale, missing or
   changed consent; do not silently repair or broaden it.
4. Use the restricted redaction operation to remove/restrict prohibited classes;
   create a minimal sanitized summary. Never copy suspected secrets, prompts,
   task/file/connector payloads, identities, sensitive context or raw logs.
5. Enforce classification/access and record controlled screening reason only;
   acknowledge safely using R07/R10 and release only screened scope to triage.

**Evidence:** synthetic prohibited-class rejection/redaction counts, consent
mismatch results, role-denial observations and zero content-canary leakage
across errors/logs/traces/metrics/alerts/queues. **Fail/stop:** ordinary exposure,
unknown purpose/class or unavailable restricted owner; Deanna/Tuvok contain.
**Recovery:** correct screening/access and repeat canary/consent tests; never
lower classification merely to drain backlog.

## R03 Private vulnerability and personal-data-incident handoff

**Prerequisites:** appointed direct private recipients, tested routing and
restricted storage/notification/access; valid route/contact evidence including
`security.txt` before public intake. No placeholder contact is usable.

1. Stop ordinary triage, dedupe, publication and optional messages for the
   affected scope. Classify as a handoff, not a public spam investigation.
2. For ordinary non-conflict security reports, use the approved direct Tuvok
   path; for suspected personal-data incidents, Deanna Troi/Tuvok. Jett contains
   service exposure. Route counsel/notification questions privately.
3. If Cyrus is named or has an actual/potential/perceived conflict, bypass
   Cyrus-mediated intake entirely. Use only appointed qualified external
   counsel/independent reviewer with verified direct contact and their chosen
   specialist participation. Cyrus has no contents/findings/remediation access.
   If that recipient is absent, the capability is unavailable: do not accept
   or process through ordinary intake or claim that it works.
4. Transfer only the authorized restricted reference/source class to the
   responder; keep investigation in its approved boundary. Confirm responder
   acceptance/coverage without exposing details to ordinary staff.
5. Give only the common safe acknowledgment/status permitted by policy; record
   a content-free handoff receipt. Do not promise a timeline or public update.

**Evidence:** destination/role authorization result, handoff acknowledgment,
ordinary/public denial and safe response; synthetic fixtures only. **Fail/stop:**
wrong recipient, unavailable coverage, conflict or public disclosure; notify
Tuvok/Deanna or the independent recipient, never disclose conflict content to
Cyrus. **Recovery:** correct and re-evidence route/coverage; ordinary operations
resume only for safe unrelated scope, never by moving incident content back.

## R04 Dedupe, split and incorrect-merge recovery

**Prerequisites:** R02 complete, approved sanitized key inputs, private link/
unlink operations, lifecycle history and safe status per submitter.

1. Compute/search only category, surface, compatible version, sanitized
   outcome and consented bounded error code. Exclude contacts, tenants, IP and
   raw content. Guinan confirms similarity; automation alone cannot merge.
2. Link to canonical work **before** drafting a GitHub issue. Preserve each
   receipt, follow-up choice, consent, verification and reopen independently.
3. For distinct actionable outcomes, create authorized private children and
   parent links with minimized scope; never copy private material to a public
   issue. Retain entitlement per reporter, not shared credentials.
4. For incorrect merge, freeze affected link updates, snapshot content-free
   transition versions, unlink and retriage. Reassign canonical mappings and
   correct each safe status without exposing other reporters or counts.
5. Reconcile all subscriptions/history and notify only entitled safe channels;
   test canonical issue change/closure does not verify or close every reporter.

**Evidence:** before/after link counts/versions, independent status/reopen/
verification outcomes and denial tests. **Fail/stop:** lost subscription,
cross-entitlement, consent inheritance or unrepairable history; Guinan/Data,
Deanna for disclosure. **Recovery:** repair links from authorized history,
retest split/unlink/retry; never restore withdrawn consent or merge identities.

## R05 GitHub preview, consent, failure, withdrawal and removal

**Prerequisites:** R02–R04, configured approved repository (framework canonical
target `Jamula/Andreja` plus equivalent-control override), app-owned least-
privilege grant, exact consent binding and authorized remediation operation.
Publication defaults off; a synthetic evidence grant is not public publication
permission.

1. Prepare the exact sanitized title/body/labels/acceptance evidence/safe
   source reference and repository/visibility warning. Preview all fields;
   explain copies/notifications/forks cannot reliably be recalled.
2. Obtain separate affirmative versioned publication consent, not diagnostic/
   contact consent. Immediately revalidate digest, scope and withdrawal state
   before a single idempotent publish action. Material edit, target or warning
   change invalidates consent and requires a fresh preview/receipt.
3. If the grant/provider fails, pause publication and reconcile the operation
   marker with repository state before any bounded retry. Safe status remains
   usable without GitHub; never ask for a user's token or create duplicates.
4. Pre-publication withdrawal cancels pending work and prevents stale jobs
   publishing. Post-publication correction/removal uses R08 and the approved
   controlled-artifact procedure, not a promise to erase every copy.
5. For accidental/unauthorized publication, immediately disable publisher grant,
   retries and affected route. Authorized responders remove/redact controlled
   issue/comment/attachment content without quoting it in an edit or incident.
6. Preserve only minimized private object references/times/categories/digests;
   use R03 for incident/notification/legal assessment. Seek available cache/
   index removal; record unretractable notifications/copies/forks privately.

**Evidence:** preview/receipt digests (not bodies), stale-consent denial,
idempotency and repository reconciliation, removal receipts, incident handoff
and residual-copy category. **Fail/stop:** unsafe output, stale consent,
unknown creation result or overbroad grant; Guinan/Deanna/Tuvok contain.
**Recovery:** fix binding/grant, rotate compromised credentials, repeat edited/
withdrawn/retry/removal negatives and obtain owner/approver review; a former
consent never authorizes republishing removed content.

## R06 Opaque tracking custody, recovery, rotation and suspected enumeration

**Prerequisites:** approved #155 proof/stub/retention policy, #156 verifier/
generation/nonce/idempotency binding, source-reviewed RNG and cryptography,
boundary-specific custody and atomic operations; no operator secret retrieval.

1. Check issuer generates a separate non-semantic unique display reference
   and CSPRNG secret (framework minimum 128 bits; approved supplement specifies
   32 random bytes). Only its one-way domain-separated verifier and approved
   metadata persist. Initial secret is shown once in non-cacheable HTTPS output.
2. Verify secret/candidate transport uses protected POST bodies only; no URL
   path/query/fragment, history, referrer, browser persistent storage, logs,
   traces, metrics, alerts, queue, backup, export, screenshot or provider capture.
   Ordinary support cannot retrieve, resend or reconstruct it.
3. For status require valid record/boundary/generation/expiry/purpose proof or
   approved verified return route. Display reference, ordinary session or an
   operator assertion alone cannot authorize a read or mutation.
4. For rotation/recovery, require current authority or approved fresh recovery
   grant. Client holds the new CSPRNG candidate in memory and a scoped
   idempotency key. One transaction/CAS consumes nonce/grant, checks generation,
   installs only new verifier, invalidates old sessions and writes the marker.
5. Inject pre-commit failure: previous validity is unchanged (zero stays zero
   if recovery began with no valid credential). After commit exactly one
   candidate is valid. For lost acknowledgment retry with same candidate/key,
   authenticate candidate and marker; no second swap. Different candidate/
   actor/action/record/boundary retry denies; concurrent swaps have one winner.
6. Client candidate loss/marker expiry requires fresh approved recovery, never
   rollback to an old secret. Lost receipt **and** no verified return route
   can use only the approved separate private appeal when available. Appeal
   submission grants no access, existence disclosure or recovery shortcut.
7. Exercise revocation/deletion/expiry invalidating every read/session/mutation.
   Test CSRF, exact origin, nonce/single-use replay and idempotency on verify,
   reopen, recovery, rotation, revocation and other mutations.
8. On enumeration/isolation signals, apply approved shared abuse limits and
   uniform response/timing/length/header classes, not attacker-induced permanent
   victim lockout. Test two records, two tenants, tenant-less/authenticated
   boundary, guessed/missing/stale references and identifier collisions.
9. For custody/key maintenance, compare boundary/data-class key IDs/versions,
   runtime/admin privileges and primary/recovery custodian to the authorized
   inventory; keep contact-vault custody distinct. Use the reviewed key rotation
   operation to preserve only approved retained ciphertext readability without
   extending retention. Retire old decrypt keys only after authorized
   re-encryption/backup expiry and recovery proof. An encryption-key rotation
   must not rotate every receipt secret. Restore only in isolation; reconcile
   authoritative revocation generations, deletion tombstones, consent withdrawal,
   contact revocation, expiry and scoped holds before serving status or publishing.
   Unknown freshness keeps those routes off; compromise uses R03 immediately.

**Evidence:** RNG source/failure review, verifier-only storage/capture scans,
generation/active-verifier counts, transaction/fault observations, session
denials, response-class comparisons and scoped custody audit. No raw candidate
in evidence. **Fail/stop:** leakage, two valid credentials, zero after committed
success, unauthorized read, replay or missing proof policy; Tuvok/Jett/Data.
**Recovery:** contain/revoke scoped authority, repair transactional controls,
use fresh proof and rerun negatives. Read-only degraded access is allowed only
if current revocation/expiry/authorization can still be enforced.

## R07 Return-channel verification, revocation and email suppression

**Prerequisites:** approved contact/essential-notification policy, non-email
status/rights route, separated encrypted contact vault, exact authorized sender/
recipient list, DNS/alignment, callback auth, retention and hard send/cost caps.

1. Honor no-follow-up. For opt-in verify the destination with approved
   purpose-bound single-use proof; no existence or masked-address hint before
   proof. Changes require approved proof, not possession of a display reference.
2. Use only safe tracking reference/status/action/public help links, no case
   body, diagnostics, classification, tenant or private issue URL. Review
   plain-text/HTML equivalence, locale, keyboard/screen-reader rendering,
   headers/tags/provider metadata and absence of tracking pixels.
3. Before every send, check revocation/suppression, authorization expiry,
   recipient allowlist, nonce/idempotency marker, reserved message/cost capacity
   and aligned dedicated sender. No general email or reminders.
4. Authenticate/replay-check provider callbacks within their boundary; dedupe
   events and apply bounce/block/complaint suppression. Stop queued optional
   sends immediately on revocation or suppression, including retry workers.
5. On provider timeout/lost acknowledgment reconcile message marker/events
   before bounded retry. Preserve safe non-email status; do not expose provider
   error bodies, expand recipients or substitute a sender.
6. Purge/reconcile permitted contact/delivery/suppression records under approved
   scope, including R08 deletion and provider exit. A suppressed address is not
   permission for a permanent unrestricted contact record.

**Evidence:** aligned-identity result, template/accessibility verdict, single
send counts, revoked/complaint/callback replay denials, cap/outage observations
and scoped purge receipts. **Fail/stop:** unauthorized recipient, duplicate
send, leak, unsafe contact, alignment/cap failure; Jett/Guinan/Deanna, Quark for
cost. **Recovery:** disable sender, reconcile queues/provider state, repair/
rotate scoped grants and retest; no bypass of suppression for convenience.

## R08 Access, correction, deletion, objection, withdrawal and holds

**Prerequisites:** approved #155 scope-specific roles, proof/conflict policy,
retention schedule/propagation inventory and qualified-counsel/joint executable
disposition. Binding duties remain protected; a timer or checkbox is not a
lawful purge-versus-preserve decision.

1. Acknowledge privately with a random request reference and uniform response,
   without account or existence confirmation. Classify right and affected
   fields/systems; use the least intrusive approved proof and record level/
   result, not raw proof. Never require government ID by default.
2. For a non-user subject without account/receipt/return channel, use approved
   proportionate identity/relationship corroboration. Do not demand submitter
   identity or receipt, disclose another person or treat content fragments as
   disclosure authority. Failure gets safe denial and independent appeal.
3. Atomically restrict disputed processing/publication/dedupe/optional messaging/
   metrics. Separate uncontested scope. Deanna/Sarek/counsel determine conflict,
   another person's rights, incident evidence and holds; never default to
   submitter preference or unlock disputed fields on timeout.
4. For access, return only verified entitled fields in a private portable
   human-readable result; redact others/security/privileged scope and explain
   withheld categories safely. For correction, update source/derived data or
   approved factual dispute annotation.
5. For deletion/objection/diagnostic or publication consent withdrawal, execute
   the approved scoped action; cancel pending publication/sends, invalidate
   covered receipts, purge/disassociate source/contact/diagnostics/search/dedupe/
   cache/queue/dead-letter/delivery and derived copies as applicable. Remove/
   redact controlled public artifacts using R05; do not create a public DSR.
6. Collect per-destination idempotent receipts, backup expiry/tombstones and
   restore-before-availability deletion replay. Recompute/delete affected
   identifiable aggregates or suppress broken thresholds. Unresolved destination
   goes to restricted exception reconciliation, not a completion claim.
7. For a hold, require separate minimized metadata (authority, exact scope,
   basis, owner, duration/end condition, review/expiry and propagation). Isolate
   held content from ordinary reuse; preserve documented binding scope only.
   At original expiry or metadata purge trigger record original time/zone and
   applicable deadline. No reset on detection/reopen; no silent renewal.
8. Apply only the preauthorized Cyrus/qualified-counsel scope-specific lawful
   disposition, including the no-fresh-decision case. The source's single
   nonrenewable seven-calendar-day proposal is not authority to store or destroy.
   Missing/expired/wrong-scope disposition blocks the affected route before
   operation and escalates privately to Deanna, Sarek, Cyrus and qualified
   counsel (except conflict-class routing follows R03). No indefinite freeze
   or automatic purge fallback; documented binding duties remain protected.
9. Return safe completed/partial/denied/delayed result with controlling reason,
   appeal and applicable approved legal deadline; distinguish deleted,
   disassociated, redacted, scoped exception, pending backup expiry and outside
   control. Do not promise numeric DSR performance before lawful targets and
   actual capacity. Record private appeal/independent review.

**Evidence:** proof-level result, restriction/role denials, system receipts,
original-trigger/deadline checks, partial/appeal result, tombstone/restore and
public-removal evidence, no claimant or case content. **Fail/stop:** unresolved
lawful disposition, over-disclosure, missed deadline, unpropagated deletion or
restore resurrection; Deanna/Sarek/counsel and Jett/Data. **Recovery:** repair
scope/propagation and re-evidence under authorized disposition; do not undo
deletion or restore old receipts as rollback.

## R09 Abuse, appeal, cost and accessible recovery

**Prerequisites:** approved measured controls, short-lived evidence/retention,
backpressure/cost reservation, accessible challenge alternative and separate
private appeal policy; attachments stay disabled absent separate approval.

1. Compare schema/body/rate/concurrency/burst/replay/queue/byte/cost counters
   with approved thresholds; apply bounded rejection/quarantine without echo.
   Screen incident triggers with R03, not spam disposal.
2. Block injection, archives/unsafe attachments and secret/high-risk classes.
   Never retain a body just because it failed validation or fingerprint a user.
3. Offer only an evidenced accessible alternative; test keyboard/screen-reader,
   shared-device and challenge-error recovery without requiring a public post,
   account or tracking receipt. If unavailable, block activation and say so.
4. Private appeal is a separate minimized intake, not record search/recovery;
   uniform pre-proof response and rate accounting cannot expose existence or
   cause per-victim lockout. Independent authorized reviewer checks the reason
   using least-intrusive proof; reply only through approved verified means.
5. Reserve resources/message/cost before work. On threshold/cap/expiry breach
   deny further work, stop affected ingress/sends, notify Jett/Quark/Tuvok and
   reconcile accepted records under R01. No autoscaling, quota increase or
   lower-control provider substitution.
6. Purge approved abuse signals/appeal evidence at the supplied schedule and
   record false-positive correction without widening collection.

**Evidence:** at/below/above-limit results, accessible alternative observations,
uniform appeal output, false-positive review result, cost-reservation/hard-stop
and purge receipts. **Fail/stop:** inaccessible path, unbounded costs/queues,
profiling, unsafe echo or incident treated as spam. **Recovery:** correct rule/
coverage, retest boundary conditions and reconcile accepted work; loosening
thresholds or extending authorization requires applicable decisions.

## R10 Aging, handoff, decline, delivery, verification, closure and reopen

**Prerequisites:** authorized state machine, actual assigned owner/backup,
numeric clocks/targets/aging and follow-up window from capacity, approved safe
reason codes, independent status/return channel and release/help evidence.

1. Reconcile due items and absent owners against approved hours/calendar and
   backlog. Escalate aging to Guinan/domain owner, operations/Data for coverage,
   Picard for prioritization and Cyrus for extraordinary resourcing; do not
   invent dates or silently reset clocks.
2. On transfer, record old/new owner, reason, version and explicit acceptance
   by receiving owner; preserve history and due clock. If handoff fails,
   retain accountable coverage or stop affected intake under the approved rule.
3. Enforce the framework states: Received only after durability;
   PrivacyScreened before ordinary use; Acknowledged with safe receipt;
   NeedsInformation requests minimum sanitized evidence; Triaged records
   severity/dedupe/route/owner; Planned/InProgress signal intent, not dates.
4. Declined records reason and reconsideration conditions; Duplicate retains
   independent subscription/verification. Send only authorized safe updates.
5. Delivered requires actual release/help/remediation evidence, applicability
   and known limitations. Ask for explicit outcome confirmation or use
   documented objective acceptance evidence. Delivery/email delivery/silence
   cannot set Verified.
6. Close with truthful reason: verified, declined, withdrawn/deleted, duplicate-
   following-canonical, unable-to-contact, or unverified after approved window.
   Preserve unverified label and evidence; never convert silence to success.
7. Allow NeedsInformation/Declined/Duplicate/Delivered/Closed to return to
   Triaged through proof-authorized reopen. Record new sanitized evidence,
   accept/reject with reason, assign owner and preserve age/retention history.
   Do not reset retention by reopening or demand new public disclosure.
8. Exercise unauthorized transitions, lost update, notification failure,
   owner loss, stale version, repeated verify/reopen and simultaneous requests;
   audit/idempotency/authorization must hold for every transition.

**Evidence:** controlled transition events, owner acceptance, safe updates,
delivery versus verification evidence, reasoned decline/unverified close/
reopen results, failures and clock/coverage observations. **Fail/stop:** owner
vacancy, impossible target, forced verification, inaccessible status or lost
history; Guinan/Data/domain owner. **Recovery:** correct ownership/state with
audited compensating transition, restore safe status and retest; no fabricated
verification or unsupported commitment.

## R11 Metric generation, threshold failure, access, purge and misuse

**Prerequisites:** complete approved
[restricted catalog](../frameworks/feedback-readiness.md#metrics-catalog-and-prohibited-use-tests),
controlled source events, numeric privacy/time buckets, access/export rules
and retention/deletion procedures. Public-site analytics remains off.

1. Select only approved catalog IDs and controlled event fields. Run source
   quality/reconciliation checks; reject free text, diagnostics, identifiers,
   contact, IP, secret or user-timeline fields before aggregation.
2. Compute numerator/denominator and bucketed totals against independently
   known synthetic records. Apply minimum-group/privacy thresholds; suppress
   below-threshold cells, never emit a precise count or misleading zero.
3. Verify unauthorized readers/exports deny and time-bucket/dimension changes
   cannot reconstruct small groups. Stop a metric with missing catalog fields,
   stale source or failed quality/privacy checks.
4. Attempt prohibited product/profile/prompt/connector/advertising/financial/
   telemetry joins and ranking, targeting, pricing, moderation/personalization
   operations. Supported interfaces and access policies must deny, not merely
   display a warning.
5. Purge at approved trigger/duration; propagate source deletion/restriction,
   recompute/suppress identifiable or threshold-breaking aggregates, check
   backup restoration and obtain receipts. Review access at supplied cadence.
6. On misuse, disable affected generation/export/grants, retain only authorized
   minimized private evidence, invoke R03 for suspected incident and notify
   Guinan/Data/Deanna/Tuvok. Quark receives safe cost totals, not case-level joins.

**Evidence:** catalog/version, reconciliation/suppression/access/use-test
results, emitted field inventory, purge/restore receipts and access review.
**Fail/stop:** content/identifier leak, small-group inference, unauthorized join/
use/export or missed purge. **Recovery:** remove unsafe output under approved
scope, repair catalog/interface/access and rerun all negatives; do not restore
a previously exposed metric as rollback. Last successful exercise: **NOT RUN**.
