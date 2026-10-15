# Feedback exact-release scenario execution plan

**Version:** feedback-scenarios-v1; **owner/custodian:** Data with Guinan

**Applicability:** proposed feedback release; **document review:** 2026-10-15

**All runtime scenarios:** NOT RUN; no exercise authorization or implementation
evidence is supplied by this plan.

The numbered **Framework requirement** paragraphs below preserve the complete
14 scenarios from [Phase 1B acceptance evidence](feedback-support.md#phase-1b-acceptance-evidence).
They are not reduced to happy-path summaries. Follow the
[readiness packet](feedback-readiness.md) and
[runbooks](../runbooks/feedback-operations.md); all missing prerequisites block
execution rather than become a skip/pass.

## Shared setup, binding and result contract

Before every scenario, Data/Jett bind the exact release commit, immutable
artifact digest, fixture manifest digest, approved configuration and current
Cyrus #158 Evidence Authorization. All are **UNSET** here. Independently compare
the actual build/deployment/fixture/inventory to those identifiers before and
after execution. Artifact, fixture, authorization or inventory drift stops the
run; a rebuilt artifact is not equivalent just because its source is similar.

Use only generated synthetic records within the explicit authorization, never
real user/tenant/contact/security/incident/DSR data. Email recipients must be the
named verifier-controlled allowlist; example data cannot select recipients.
No public GitHub publication is authorized by the synthetic exercise. Bind
publication checks to a locally simulated repository boundary; any controlled
external repository exercise would require a separately explicit permitted
non-public inventory/control decision, not inference from these fixtures.
Simulation does not satisfy unavailable real integration evidence.

Supply a fixture manifest with synthetic namespaces A/B/public, source versions,
cases, consent states, forbidden-class canaries, owner roles, expected event/
transition counts, metric oracle, provider fault points and teardown scope.
Digest the canonical manifest and generated dataset; never include raw receipt
secrets or contacts in evidence. Keep secret material only in the approved
ephemeral client custody, never in committed fixtures or captured requests.
If #157's proposed record/message/resource ceilings cannot cover all fixtures,
plan approved sequential reset/purge within caps; do not raise limits.

For each action below bind the concrete implemented test/admin operation,
command, tool version and fault-injection hook in the execution record. No
runner or endpoint is invented here. The actor must have the least privilege
specified by its linked runbook; test with unauthorized roles too. Unavailable
implementation/procedure/access/counsel/coverage means BLOCKED, not PASS.

Each scenario record must include:

- Scenario/run ID, owner/operator and reviewer, start/end, exact manifest and
  artifact/configuration/fixture digests, authorization reference and validity.
- Setup/action binding; expected versus observed assertions including negatives;
  content-free trace/event/count/response-class and restricted evidence references.
- PASS only if **all** required assertions are observed, independently reviewed
  and custody intact; otherwise FAILED/BLOCKED with reason, affected scope,
  existing decision issue, proposed accountable owner and stop/recovery action.
- Sanitized failure evidence: test/assertion ID, controlled error class, count/
  generation/state delta and safe timestamp/correlation reference; no canary
  values, payloads, destinations, secrets or investigation in public records.

Failure invokes the linked runbook's containment and recovery. Re-run corrected
negative paths and affected dependent scenarios on the authorized exact artifact.
Expiry/revocation/hard-stop/teardown exercises and final cost reconciliation in
the readiness packet are additional mandatory evidence, not replaced by 14
scenario labels. Last successful exercise for every scenario: **NOT RUN**.

## S01 Tenant-less public receipt

**Framework requirement:**

> A public submitter without Andreja or GitHub credentials receives an opaque
> tracking receipt; the record exists only in the tenant-less boundary and no
> user data-plane record, cookie, token, or GitHub issue is created.

**Fixture/setup:** clean browser with no credentials; synthetic public record;
separate app-plane/repository write counters and cookie/token instrumentation.
**Action:** submit through the authorized isolated public boundary; inspect
durability and one-time receipt output; attempt public tenant-field injection.
**Observable pass assertions:** distinct non-semantic reference and secret;
only verifier/approved metadata persists; exactly one public durable case;
zero app-plane/repository writes, app cookies and user tokens. Receipt exists
only after durability; injected tenant fields, including empty/null, deny.
**Negative/edge:** fail persistence before receipt and retry; generic processing
response cannot claim acceptance or expose whether another case exists.
**Failure evidence:** boundary counts, receipt/durability ordering and
cookie/header field-presence results, never credential values.
**Owner:** Jett; Data reviews; Guinan lifecycle.
**Runbooks:** [R01](../runbooks/feedback-operations.md#r01-intake-health-backpressure-and-recovery),
[R06](../runbooks/feedback-operations.md#r06-opaque-tracking-custody-recovery-rotation-and-suspected-enumeration).
**Status:** NOT RUN.

## S02 Authenticated diagnostic consent and isolation

**Framework requirement:**

> Authenticated intake defaults every diagnostic off, previews each selected
> field and purpose, records independent consent, and proves tenant isolation.

**Fixture/setup:** two synthetic tenants A/B with distinct authorized principals;
allow-listed diagnostic candidates, empty selection and consent versions.
**Action:** inspect initial UI/payload, independently choose a field, confirm
its exact purpose/value preview and submit; repeat as tenant B and wrong role.
**Observable pass assertions:** every initial selection false and diagnostics
empty; each included field has its own matching affirmative receipt/version/
digest; tenant is server-derived and A cannot read/mutate B or public records.
**Negative/edge:** change value/purpose after preview, omit receipt, supply unknown
field, stale notice or client tenant; deny without auto-consenting or crossing scope.
**Failure evidence:** selected-field names/counts and receipt-binding/authorization
results, no values or principals.
**Owner:** Guinan; Deanna Troi/Tuvok domain review, Data evidence.
**Runbooks:** [R02](../runbooks/feedback-operations.md#r02-intake-privacy-screening-and-safe-redaction),
[R06](../runbooks/feedback-operations.md#r06-opaque-tracking-custody-recovery-rotation-and-suspected-enumeration).
**Status:** NOT RUN.

## S03 Exclusion and leakage prevention

**Framework requirement:**

> Default-exclusion tests block or restrict representative secrets, prompts,
> task/file/connector content, raw logs, and sensitive personal context
> without echoing them into logs, traces, metrics, alerts, or errors.

**Fixture/setup:** generated unique forbidden-class canaries for every named
class and identity/health/finance/family classes; capture-safe sink inspections
at proxy/application/error/queue/provider boundaries.
**Action:** submit each class via permitted test boundary; trigger validation,
screening and exception paths; inspect all authorized output/capture surfaces.
**Observable pass assertions:** blocked or restricted before ordinary access;
no canary match in logs/traces/metrics/alerts/errors/queue/provider metadata,
GitHub or support tooling. Evidence reports only zero/nonzero category counts.
**Negative/edge:** encoding, mixed safe/unsafe fields, injected header/error
and scanner outage cannot bypass minimization or echo.
**Failure evidence:** offending sink category and match count, not matched text;
invoke private containment if a canary escapes.
**Owner:** Tuvok/Deanna Troi; Guinan screening, Data evidence.
**Runbooks:** [R02](../runbooks/feedback-operations.md#r02-intake-privacy-screening-and-safe-redaction),
[R03](../runbooks/feedback-operations.md#r03-private-vulnerability-and-personal-data-incident-handoff).
**Status:** NOT RUN.

## S04 Rights, non-user subjects, conflicts and propagation

**Framework requirement:**

> Submitter and non-user-subject access, correction, deletion, objection, and
> consent-withdrawal exercises work without an Andreja or GitHub account.
> Tests cover a non-user subject with no tracking receipt or return channel,
> proportionate identity/relationship proof, conflicting instructions,
> non-disclosure of submitters and unrelated people, restriction while a
> conflict is decided, denial/partial-response appeal, and propagation to
> approved derived stores, delivery records, public-artifact
> deletion/redaction, and backup handling.

**Fixture/setup:** synthetic submitter, non-user claimant without account/receipt/
return channel, unrelated subject, disputed fields, hold scopes, every approved
derived/delivery/public-artifact/backup destination and tombstone manifest.
Approved counsel/disposition and proof policy are mandatory first.
**Action:** run each right for submitter and non-user; exercise proof fail/pass,
conflicting instructions, uncontested partial response, safe denial and
independent appeal. Run authorized disposition at original trigger/deadline,
source deletion, destination fault/retry and isolated backup restore.
**Observable pass assertions:** proportionate proof without submitter identity
or government-ID default; no pre-proof existence or other-person disclosure;
atomic restriction prevents publication/dedupe/optional messages/metric use;
no submitter-wins default. Every in-scope destination has completion or explicit
pending/held/outside-control result; backup tombstones apply before availability.
Original trigger never resets; documented binding duties remain protected.
**Negative/edge:** no-fresh-decision, wrong-scope/expired counsel disposition,
false claim, failed destination, public copy and restore resurrection must
block completion/activation, not trigger automatic purge or indefinite retention.
**Failure evidence:** proof-level result, restriction denials, scoped
propagation receipts/remaining counts, original-deadline comparison and safe
appeal outcome, not claimant details or advice.
**Owner:** Deanna Troi with Sarek/qualified counsel; Data reviews, Jett propagation.
**Runbooks:** [R08](../runbooks/feedback-operations.md#r08-access-correction-deletion-objection-withdrawal-and-holds),
[R05](../runbooks/feedback-operations.md#r05-github-preview-consent-failure-withdrawal-and-removal).
**Status:** NOT RUN.

## S05 Abuse, queue failure, accessibility and cost bounds

**Framework requirement:**

> Abuse, burst, replay, enumeration, oversized-payload, injection, unsafe
> attachment, queue-failure, and accessible-alternative scenarios fail safely
> within approved cost and availability bounds.

**Fixture/setup:** approved thresholds and resource/message/cost reservation
oracle; synthetic at/below/above-limit inputs, disabled attachment fixtures,
queue outage/ack-loss/replay hooks and keyboard/screen-reader operator.
**Action:** exercise every named attack/fault, sustained burst/backpressure,
challenge and accessible alternative, then queue recovery and independent status.
**Observable pass assertions:** hard bounds enforced before work/cost, no
unsafe echo or silent accepted-record loss; uniform pre-proof response classes;
idempotent replay/recovery; accessible alternative actually usable without
profiling or public disclosure. Incident material never becomes ordinary spam.
**Negative/edge:** simultaneous work at cap, cost-meter failure, missing accessible
alternative, attacker-induced victim lockout and expired authorization all deny
unsafe operation; no quota increase or unlimited retry.
**Failure evidence:** thresholds/units, reservations versus observed counts,
response-class/timing tolerance and accessibility failure observations.
**Owner:** Tuvok/Jett/Quark; Data evidence, Guinan appeal workflow.
**Runbooks:** [R09](../runbooks/feedback-operations.md#r09-abuse-appeal-cost-and-accessible-recovery),
[R01](../runbooks/feedback-operations.md#r01-intake-health-backpressure-and-recovery),
[R06](../runbooks/feedback-operations.md#r06-opaque-tracking-custody-recovery-rotation-and-suspected-enumeration).
**Status:** NOT RUN.

## S06 Independent duplicate loops

**Framework requirement:**

> Two sanitized reports dedupe before GitHub; each tracking reference retains
> independent status, follow-up, reopen, and verification.

**Fixture/setup:** two sanitized similar synthetic reports with independent
receipts/consents/contact choices; third report with distinct outcome.
**Action:** screen, compare sanitized key, confirm canonical match before
publication; update canonical work, verify one reporter, reopen the other;
split/unlink an intentionally incorrect merge and change canonical mapping.
**Observable pass assertions:** no duplicate repository work is created;
independent entitlement, status, follow-up, verification and reopen survive;
one verification/withdrawal/closure never acts for another reporter.
**Negative/edge:** automatic similarity-only merge, contact/tenant/IP-based key,
cross-reporter lookup and stale canonical update deny or require authorized
correction without lost history.
**Failure evidence:** link/transition counts, pre-publish ordering and
independent-status authorization results.
**Owner:** Guinan; Data reviews.
**Runbook:** [R04](../runbooks/feedback-operations.md#r04-dedupe-split-and-incorrect-merge-recovery).
**Status:** NOT RUN.

## S07 Exact publication consent and unsafe-publication removal

**Framework requirement:**

> GitHub publication shows the exact final preview, requires dedicated
> consent, invalidates consent after a material edit, and publishes no private
> identifiers, diagnostics, contacts, or tracking secrets. An accidental or
> unauthorized publication exercise suspends the publisher and affected route;
> removes or redacts controlled issue, comment, and attachment content; creates
> no public incident or DSR evidence; preserves only minimized private evidence;
> routes notification and legal assessment; seeks available cache/index
> removal; and records copies or forks that cannot be retracted.

**Fixture/setup:** locally simulated repository/publisher, exact sanitized draft/
receipt digest, canonical and approved override target configuration, stale/
withdrawn consent, grant failure/lost acknowledgment, controlled unsafe issue/
comment/attachment fixtures and simulated copy/index references.
**Action:** preview/confirm/revalidate/publish once; edit each material field or
target and retry; withdraw before/after action. Inject unsafe publication,
disable grant/route/retries, remove controlled objects, perform private legal/
notification handoff and available cache/index-removal request.
**Observable pass assertions:** exact title/body/labels/evidence/source/target/
warning binding and separate affirmative consent; no prohibited published
fields; no retry duplicates; no post-withdraw publication. Removal covers
issues/comments/attachments; no public incident/DSR evidence; restricted
minimized evidence and explicit unretractable-copy limits.
**Negative/edge:** diagnostic consent alone, changed warning, failed grant,
uncertain creation result or failed removal cannot yield success/retry blindly.
**Failure evidence:** consent/draft digests, operation marker, object action
receipts, suspended-route observation and residual-copy category only.
**Owner:** Guinan; Deanna Troi/Tuvok with Sarek legal handoff, Data evidence.
**Runbook:** [R05](../runbooks/feedback-operations.md#r05-github-preview-consent-failure-withdrawal-and-removal).
**Status:** NOT RUN.

## S08 Private incident routing

**Framework requirement:**

> A vulnerability and a suspected personal-data incident use the private
> route, expose no public issue, and produce only a safe ordinary-workflow
> acknowledgment.

**Fixture/setup:** generated vulnerability and data-incident fixtures in
authorized isolated restricted stores; verified direct recipients/coverage,
ordinary triage/public publication denial roles; synthetic conflict-class case.
**Action:** hand each class directly to the appropriate restricted recipient;
confirm acceptance without contents in ordinary workflow; test conflict routing
without Cyrus-mediated access and check verified `security.txt` route metadata.
**Observable pass assertions:** zero public issue or ordinary content disclosure;
only safe generic acknowledgment/opaque restricted reference. Conflict case
excludes Cyrus and uses appointed direct independent recipient.
**Negative/edge:** missing recipient/coverage/contact evidence, ordinary spam
classification or conflict routed to Cyrus blocks that capability; no invented
email or public fallback.
**Failure evidence:** restricted handoff receipt, ordinary/public access-denial
observations and contact/expiry validation outcome, not investigation details.
**Owner:** Tuvok/Deanna Troi or appointed independent recipient; Data evidence.
**Runbook:** [R03](../runbooks/feedback-operations.md#r03-private-vulnerability-and-personal-data-incident-handoff).
**Status:** NOT RUN.

## S09 Account-free status, delivery, verification and reopen

**Framework requirement:**

> An external submitter follows status, receives help/release evidence, and
> verifies or reopens the outcome without repository access. A private GitHub
> link is never the only status.

**Fixture/setup:** external synthetic client without repository/account access,
valid receipt, delivered evidence and limitations, expired/revoked credentials,
approved recovery and absent-return-channel cases.
**Action:** follow safe status, public help/release link, verify and reopen;
rotate/recover/revoke, interrupt responses pre/post commit and try concurrent
candidate operations; replay verify/reopen and test hostile/missing origin/CSRF.
**Observable pass assertions:** no GitHub access dependency; only coarse safe
status and entitled actions; proof-bound idempotent mutations; pre-commit
validity unchanged, committed rotation exactly one valid candidate; old
generation/session invalid immediately; no secret capture or reconstruction.
**Negative/edge:** guessed/display-only reference, cross-record/tenant/public
credential, changed-candidate retry, expired marker and lost candidate all deny
unsafe access; no-return appeal grants no status/recovery.
**Failure evidence:** active-generation/count transitions, denial/response-class
results and help accessibility observation, no secrets or private links.
**Owner:** Guinan/Tuvok; Data reviews, Jett custody.
**Runbooks:** [R06](../runbooks/feedback-operations.md#r06-opaque-tracking-custody-recovery-rotation-and-suspected-enumeration),
[R10](../runbooks/feedback-operations.md#r10-aging-handoff-decline-delivery-verification-closure-and-reopen).
**Status:** NOT RUN.

## S10 Every lifecycle transition and failure path

**Framework requirement:**

> Every lifecycle transition, ownership transfer, decline, duplicate,
> delivered, verified, unverified close, and reopen path has authorization,
> audit, notification, and failure-path evidence.

**Fixture/setup:** complete implemented state/allowed-transition inventory
matched to framework states, assigned owner/backup, approved clocks/targets/
follow-up windows, permitted/unauthorized actors and notification faults.
**Action:** traverse every allowed transition, handoff, decline, duplicate,
delivery, explicit/objective verification, unverified close and reopen; test
each with wrong actor, stale version, lost response, missing owner and failed
notification. Evaluate due/aging/escalation clocks against supplied calendar.
**Observable pass assertions:** authorization before mutation; one audited
transition and bounded safe notification per operation; transfer accepted by
new owner; reason and evidence preserved; silence/email delivery never Verified;
unverified closure truthful; reopen does not reset age/retention.
**Negative/edge:** unsupported transition, concurrent update, failed handoff,
unachievable target and replay cannot silently succeed or lose accountability.
**Failure evidence:** transition coverage matrix, expected/observed event counts,
owner acceptance, clock deltas, failure/retry outcomes and safe reason codes.
**Owner:** Guinan/domain delivery owner; Data reviews.
**Runbooks:** [R10](../runbooks/feedback-operations.md#r10-aging-handoff-decline-delivery-verification-closure-and-reopen),
[R04](../runbooks/feedback-operations.md#r04-dedupe-split-and-incorrect-merge-recovery).
**Status:** NOT RUN.

## S11 Authorized minimal outbound email

**Framework requirement:**

> Minimal outbound email to the authorization's verifier-controlled recipient
> allowlist passes identity/alignment, accessibility, idempotency,
> bounce/complaint/suppression, abuse, privacy, retention, cost, and
> provider-failure tests without message or metadata content leakage.

**Fixture/setup:** specifically authorized non-production sender/DNS/provider
and verifier-controlled recipients, reviewed plain-text/HTML/locale templates,
boundary-specific callback credentials, send/cost limits and event-retention/
suppression oracle. Local fakes alone cannot pass controlled-email evidence.
**Action:** verify address, send approved safe update, induce timeout/ack-loss,
duplicate callback, bounce/complaint/block, revoke/change channel, inject
header/template/address-bombing attempts, reach caps and purge events/provider
data; test sender disablement at expiry/revocation.
**Observable pass assertions:** SPF/DKIM/DMARC/alignment; accessible equivalent
templates; exactly one authorized send per marker; no optional send after
suppression/revocation; authenticated replay-safe callbacks cannot cross
boundaries; no message/body/header/tag/metadata prohibited content or pixels;
hard caps, bounded retries, purge and non-email fallback all observed.
**Negative/edge:** unauthorized recipient/domain, expired grant, spoofed callback,
missing alignment or provider outage denies safely without sender substitution.
**Failure evidence:** verifier receipt references, alignment/template/accessibility
verdicts, send/event counts, cap denials, purge/cancellation receipts and safe
error class; no raw destinations/messages/provider response body.
**Owner:** Jett/Quark; Guinan/Deanna Troi/Data reviews.
**Runbook:** [R07](../runbooks/feedback-operations.md#r07-return-channel-verification-revocation-and-email-suppression).
**Status:** NOT RUN.

## S12 Restricted aggregate metrics

**Framework requirement:**

> Aggregate metrics reconcile to controlled test records, suppress
> disallowed small groups, contain no content or direct/pseudonymous
> identifiers, and cannot be joined through the supported interface to
> product or financial data.

**Fixture/setup:** complete approved metric catalog, independently known
controlled-event totals, below/at/above-threshold and bucket-edge groups, zero
denominator, deleted/restricted scope and unauthorized reader/export roles.
**Action:** generate/reconcile, inspect emitted schema, query alternate buckets,
attempt prohibited joins and ranking/targeting/pricing/moderation/personalization
use; delete source and exercise aggregate purge/recompute/restore.
**Observable pass assertions:** exact approved definitions/totals with documented
zero handling; suppressed cells not zero or precise counts; no text/diagnostics/
contact/direct or pseudonymous IDs/IP/secret/timeline; restricted access only.
Supported interfaces deny product/profile/task/prompt/connector/advertising/
telemetry/financial joins and all prohibited uses; deletion cannot re-identify
or break thresholds.
**Negative/edge:** small-group differencing, unauthorized export, missing catalog
field, stale source and restore of suppressed/deleted output block the metric.
**Failure evidence:** expected/observed totals, schema field names, suppression/
access/use denial results and purge receipts, not individual records.
**Owner:** Guinan/Data; Deanna Troi/Tuvok review.
**Runbook:** [R11](../runbooks/feedback-operations.md#r11-metric-generation-threshold-failure-access-purge-and-misuse).
**Status:** NOT RUN.

## S13 Complete accessible, current help and routes

**Framework requirement:**

> Canonical help, known-issue, status, private-reporting, tracking, DSR,
> publication-consent, and verify/reopen content passes accessibility, link,
> ownership, version, and stale-page checks.

**Fixture/setup:** [feedback help](../help/feedback.md), complete proposed help/
route inventory (including #93 integration), supported version/language/
capacity, named owners/reviewers, approved expiry and change triggers.
**Action:** without repository access traverse every named content/route class;
test keyboard/focus, screen reader, zoom/reflow, error/challenge/recovery and
plain-text equivalents; compare claims to operational evidence and review dates.
Inject broken link, stale version/expiry, removed owner and unavailable route.
**Observable pass assertions:** every required content class is findable and
WCAG 2.2 AA evidence recorded, links actually work for intended audience,
owners/versions/review dates match, stale pages are detected and unavailable
routes never represented as active. This draft's offline link checks alone do
not satisfy active help/private-route evidence.
**Negative/edge:** placeholder incident contact, inaccessible challenge,
email-only DSR/status or private GitHub-only link blocks release claims.
**Failure evidence:** page/route ID/version, link outcome, accessibility criterion/
observation, stale-page detector result and owner/expiry check; no report data.
**Owner:** Guinan/Jadzia Dax; Data reviews with privacy/security/claims reviewers.
**Runbooks:** [R03](../runbooks/feedback-operations.md#r03-private-vulnerability-and-personal-data-incident-handoff),
[R10](../runbooks/feedback-operations.md#r10-aging-handoff-decline-delivery-verification-closure-and-reopen).
**Status:** NOT RUN.

## S14 All runbooks reviewed and exercised

**Framework requirement:**

> The required runbooks are reviewed and exercised with content-free
> evidence, and each unresolved failure has a blocking issue and owner.

**Fixture/setup:** all eleven [runbooks](../runbooks/feedback-operations.md),
actual eligible role assignments/approvals, bound operations, numeric cadence
and next-due dates, manifest/caps and fault/teardown fixtures.
**Action:** exercise each trigger, ordered steps, negative/stop/escalation and
recovery path with least-privilege operator and approver observation. Include
authorization expiry/revocation/automatic denial/hard caps, complete teardown,
receipt/session/key revocation, DNS/sender removal, queue/store/backup purge,
provider cancellation and cost reconciliation.
**Observable pass assertions:** every runbook field and successful exercise
record is populated with actual observations, reviewed on the same artifact;
each failure maps to blocking #155/#156/#157/#158 or a genuinely new approved
blocking issue with owner. Blank templates are not exercises. All inventory
objects and charges reconcile; no residual is silently considered removed.
**Negative/edge:** absent owner, failed drill, cadence overdue, missing custody/
teardown receipt, unresolved risk, unauthorized extension/substitution/
recipient/artifact drift stops execution and final proceed recommendation.
**Failure evidence:** drill coverage/results, stop/recovery observations,
inventory-to-exit receipt matrix, cost delta and blocking issue/owner reference.
**Owner:** Guinan/Data with Jett; each runbook approver reviews.
**Runbooks:** R01–R11 in the [operator collection](../runbooks/feedback-operations.md).
**Status:** NOT RUN.
