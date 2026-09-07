# Feedback privacy, retention, and data-subject rights decision

## Status and authority

- **Status:** Proposed for Cyrus approval; collection and publication remain blocked
- **Decision issue:** [#155](https://github.com/Jamula/Andreja/issues/155)
- **Parent framework:** [Feedback and support framework](../frameworks/feedback-support.md)
- **Accountable owner:** Deanna Troi
- **Legal lead:** Sarek
- **Required reviewers:** Tuvok, Guinan, and Data
- **Decision authority:** Cyrus Jamula
- **Decision version:** `feedback-privacy-v1`
- **Notice version:** `feedback-notice-v1`

This package turns the parent framework's privacy and legal gates into a
decision-ready policy. It is not legal advice, does not record privileged
communications, and does not claim that any law applies before launch
jurisdictions, operating entities, and scale are known. Qualified,
jurisdiction-appropriate counsel must confirm the marked legal hypotheses
before external collection starts.

Approval selects product policy and engineering requirements. It does not
select a provider, create a cloud service, authorize GitHub publication, approve
spend or support commitments, or authorize launch.

## Recommended decision

Approve `feedback-privacy-v1` subject to all of these gates:

1. The deploying operator and launch jurisdictions are named in a release
   artifact.
2. Counsel confirms or replaces each conditional lawful-basis hypothesis and
   any jurisdiction-specific response period, appeal, minor, employee, or
   sensitive-data requirement.
3. Every processor and independent recipient is named, contracted, and checked
   against this inventory before receiving data.
4. The notice is completed with operator contact details and is tested against
   the deployed fields, routes, recipients, and schedules.
5. Tuvok, Guinan, and Data accept the implementation evidence required below.
6. Cyrus separately approves activation after the provider, architecture,
   security, cost, support-capacity, and launch packages are complete.

If a gate is unresolved, the affected route or field stays disabled. An
implementation must not choose a legal basis, proof method, retention exception,
or disclosure outcome by default.

## Roles and disclosure boundaries

`Controller` below means the party that determines why and how the feedback is
processed. A self-hosted operator is not automatically acting for Jamula.

| Channel or action | Role decision | External role and boundary |
| --- | --- | --- |
| Official Jamula tenant-less public intake | The named Jamula operating entity is controller. It owns the notice, intake purposes, rights responses, and deletion decisions. | Hosting, queue, storage, email, anti-abuse, and support vendors are processors only after a data-processing agreement, subprocessor review, deletion terms, incident terms, and transfer/residency review. |
| Feedback inside a self-hosted Andreja instance | The deploying operator is controller. Andreja software is not a recipient merely because it supplied the code. | Local infrastructure vendors follow the operator's role determination. Jamula receives nothing unless the person separately chooses a sanitized Jamula publication or transfer route. |
| Official hosted Andreja app feedback | The named service operator is controller for account-linked intake. Authentication proves access only; it does not authorize collecting tenant content. | Identity, hosting, storage, and delivery vendors are processors under approved terms. |
| Direct repository issue by a contributor | The contributor intentionally gives content to GitHub and repository participants under GitHub's terms. The repository operator is a controller for moderation, response, and project records. | Treat GitHub as an independent controller for the public collaboration service and its own platform purposes. A different role may be claimed only after review of the exact account terms. The repository is not a processor-confidential support system. |
| Sanitized publication from private intake | The intake controller discloses the exact approved draft to GitHub only after dedicated consent. The repository operator becomes controller for the public project artifact. | Treat GitHub as an independent controller for the public collaboration service and its own platform purposes. Notifications, caches, clones, forks, and third-party indexes may be independent copies outside Andreja's control. |
| Private security, privacy, or legal route | The named route operator is controller for intake and incident/legal handling. Ordinary support staff receive only a restricted reference and safe status. | Incident, forensics, notification, or counsel recipients require purpose-specific authorization. Counsel is not treated as an ordinary support processor. |

No processor may reuse feedback for advertising, model training, product
profiling, data brokerage, or its own unrelated analytics. A vendor that
determines an additional purpose is not covered by the processor assumption and
requires a new role, notice, transfer, and legal review.

Every processor agreement must cover documented instructions, confidentiality,
security controls, approved subprocessors and change notice, transfer and
residency safeguards, incident notification and cooperation, rights-request
assistance, audit evidence, retention enforcement, and verified deletion or
return at termination. Contract expiry, role drift, an unapproved subprocessor,
or an incompatible term disables transfer to that provider.

## Purpose, field, and derived-record inventory

The legal-basis column is a recommendation for counsel confirmation, not a
claim of law. `LI` means a documented legitimate-interest hypothesis with a
necessity and balancing assessment where that basis exists. `Contract` applies
only when responding is objectively necessary to provide a requested service.
`Consent` is freely given, specific, informed, affirmative, recorded, and
withdrawable without losing the private support route.

| Fields or record | Purpose | Basis / consent result | Recipients and access | Retention and purge |
| --- | --- | --- | --- | --- |
| `schemaVersion`, `feedbackId`, `sourceChannel`, `feedbackType`, `submittedAt`, `affectedSurface`, optional `affectedVersion`, `correlationId`, `status`, optional `statusReasonCode`, `ownerRef`, `retentionClass` | Accept, route, secure, and explain a requested feedback case | LI; Contract only for a necessary support request. Counsel must confirm by jurisdiction | Restricted intake and assigned support roles; content-free correlation data may reach approved operational processors | Review after 90 days without activity; close after 180 days without activity unless a documented continuing need is communicated. No active record exceeds 365 days from submission without a hold or renewed documented need. After closure retain the minimized record for 180 days, then hard-delete |
| `summary`, optional `expectedOutcome`, `actualOutcome`, `reproduction`, `reportedImpact` | Understand and resolve the reported outcome | LI; Contract where necessary. Not consent-based merely because the person typed it | Privacy screen first, then assigned support/domain staff. Processors may handle it only to provide the contracted intake service | Same active ceiling; retain for 180 days after closure, then purge body and search/index copies together |
| `severity`, `privacyClassification`, `dedupeKey`, optional `relatedFeedbackIds` | Restrict unsafe content, prioritize impact, and prevent duplicate work | LI for service integrity and minimization | Authorized screeners and assigned owners; no public or email disclosure | Minimized classification and relationship metadata follow the private record: closure plus 180 days |
| Optional `subjectRef`, authenticated-only `tenantRef` | Authorize an app submission and keep its status within the source boundary | Contract or LI as counsel confirms; never infer identity for public intake | Authentication and restricted support systems only; never GitHub, metrics, email, or cross-source dedupe | Remove on account/tenant deletion or private-record purge, whichever occurs first; irreversibly disassociate sooner when no longer needed |
| `contactPreference` and restricted `returnChannelRef` plus the encrypted destination | Send requested case communications | LI or Contract for essential requested messages; Consent for optional follow-up | Masked to triage; unmasked only for an approved send. Approved delivery processor receives the minimum destination and template | Delete destination 30 days after closure, immediately on withdrawal when no essential message remains, or after final failed verification; destroy the reference with it |
| `trackingRef` and separate high-entropy tracking secret | Give accountless status and rights access without exposing a record | LI or Contract | Hashed/derived verifier in restricted tracking service; secret shown only to requester | Expire 180 days after closure or immediately after verified deletion; destroy verifier and invalidate all sessions |
| Each diagnostic's `fieldName`, `purpose`, `valuePreview`, `classification`, `selected`, `consentedAt`, and `noticeVersion`, plus the approved value | Investigate a specific case with an allow-listed field | Consent. Off by default; selection is per field. Withdrawal stops future use and removes the value unless a narrow hold applies | Screeners and assigned technical owner only. Never GitHub or ordinary email | Diagnostic value follows active record plus 30 days after closure, then deletes before the rest of the case. Receipt follows the consent-receipt schedule |
| `consentReceipts` for diagnostics or publication: receipt ID, subject/source reference, purpose, exact scope or draft digest, notice version, affirmative action, time, withdrawal state/time | Prove what was shown and authorized; enforce withdrawal | Legal accountability/LI hypothesis; the underlying optional action uses Consent | Privacy/compliance roles; publication service receives only a current authorization result | Three years after withdrawal, publication, or private-record purge, whichever is later; retain no diagnostic value or public body in the receipt |
| Optional `repositoryIssueRef` and sanitized draft digest/version | Prevent duplicate publication, bind consent to an exact draft, and operate public status | LI for mapping; Consent for disclosure | Restricted publisher and repository maintainers. Submitter sees only a safe public link where appropriate | Mapping and digest remain while the public artifact exists and for 180 days after its removal; no tracking secret or private ID in GitHub |
| Sanitized GitHub issue/comment created from intake | Maintain a public project work item after separate publication consent | Consent for the initial disclosure; LI for necessary project maintenance, subject to objection and balancing | Public or repository collaborators, GitHub as independent controller, and uncontrolled downstream readers/copies | Review at closure and 24 months later. Remove personal content immediately when discovered; after 24 months minimize stale content to what remains necessary for project history or remove it. Independent copies follow their controllers' rules |
| Optional `attachments` | No approved purpose in v1 | **Disabled.** A future purpose, type list, scanning, accessibility, consent/basis, and schedule require amendment | None | Reject without storage; transient upload bytes must not survive the failed request |
| Quarantined submission and controlled reason code | Prevent unsafe disclosure and route likely secrets, incidents, threats, or prohibited sensitive content | LI for security and rights protection; legal obligation only when counsel identifies one | Restricted privacy/security reviewer only | 30 days from quarantine; transfer a minimized subset to an incident record when required, then purge the quarantine copy |
| Short-lived network/rate signal, challenge result, and abuse counter | Resist enumeration, flooding, replay, and address bombing | LI for service security, with documented balancing | Anti-abuse processor and restricted operations; never support analytics or public artifacts | Raw/coarse signal for 24 hours; aggregated endpoint counter for 30 days. No cross-site or durable device profile |
| Escalated abuse evidence: event time, route, result code, salted source token, action, appeal, and minimal excerpts only when essential | Investigate repeated attacks, defend claims, and review blocks | LI; legal-claims basis as counsel confirms | Restricted security/privacy staff and counsel when necessary | 90 days after last event; up to one year only with a documented severe-abuse reason and monthly review |
| Email send intent, correlation ID, template/version, provider message ID, delivery class, bounce/complaint/suppression event | Deliver and reconcile safe transactional messages | LI or Contract for requested essential messages; Consent for optional messages | Restricted delivery service and approved processor; no case content in metadata | Send/delivery events 30 days; minimal suppression record for three years to prevent repeat contact, subject to counsel review and objection handling |
| DSR case: request ID, route, right, scope, timestamps, proof level/result, decision/reason, systems checked, propagation evidence, appeal and safe response | Receive, decide, prove, and audit a rights request | Legal obligation where applicable; otherwise LI to honor the published commitment and protect people | Privacy caseworkers, security, and counsel on need-to-know basis | Delete raw proof 30 days after verification or final appeal. Retain minimized decision/completion ledger for three years |
| Private incident evidence created after unauthorized publication | Contain, investigate, notify, prevent recurrence, and establish necessary facts | Legal obligation or LI/security and legal-claims hypotheses, as counsel confirms | Restricted incident responders, counsel, affected processors, and authorities/people only when approved | One year after closure by default; extend only under a documented legal/security hold with 90-day review |
| Encrypted backups containing the above records | Recover the service from loss or corruption | Same purpose and basis as source record; no independent use | Restricted backup operators and contracted infrastructure processors | Rolling maximum 35 days. Deleted records are not restored into active service; erase through expiry and replay deletion tombstones after restore |
| Aggregate support metrics made only from controlled categories and thresholded time buckets | Improve intake quality, accessibility, and support operations | LI; outside personal-data scope only after documented irreversible anonymization | Restricted support-quality roles; no advertising, ranking, pricing, targeting, or user-level joins | Source events follow their source schedule. Thresholded aggregate buckets retain 24 months, then delete or re-approve |

Access logs for restricted records are content-free security records. They retain
for 90 days, use the abuse/security basis, and are available only to security,
privacy, and audit roles. They are not a separate analytics source.

## Lawful-basis and consent posture by channel

| Processing | Decision |
| --- | --- |
| Receive and resolve voluntary private feedback | Use LI where available after documenting necessity, benefit, safeguards, reasonable expectations, and opt-out. Use Contract only for the part objectively necessary to answer a requested support service. If neither is confirmed, keep the route disabled |
| Account-linked submission | Authentication authorizes the action but is not blanket consent to inspect account or tenant content. Apply the private-feedback basis only to the displayed envelope |
| Optional return contact | Essential status for a requested case may use the core basis. Optional follow-up requires a separate affirmative choice and stops on withdrawal |
| Optional diagnostics | Require granular consent even if another basis might be available. Refusal must not block submission. Sensitive or prohibited diagnostics remain disabled rather than relying on consent |
| Sanitized GitHub publication | Require dedicated consent bound to the exact preview and repository visibility warning. Refusal or pre-publication withdrawal must not reduce private support. A material edit invalidates consent |
| Repository-native contribution | The contributor knowingly uses GitHub; repository notices prohibit personal/support content. Do not import private intake evidence into the issue |
| Non-user personal data | Do not solicit it. Restrict, redact, or delete it. Process the minimum temporarily under LI/security/rights-protection or an identified legal duty; do not claim the submitter can consent for the other person |
| Security/privacy incident and abuse evidence | Use the minimum under LI/security, legal obligation, or legal-claims grounds as counsel confirms. Do not reuse it for ordinary product analytics |
| Aggregate metrics | Use LI only for restricted support improvement. Apply minimum-group thresholds and prohibit individual timelines or cross-product joins |

Consent withdrawal is prospective. It immediately disables unused diagnostics,
future optional messages, and unpublished drafts. It also starts deletion of the
covered private value. A post-publication withdrawal is handled as a
removal/redaction request and cannot guarantee recall of independent copies.

## Versioned tenant-less notice

The deployed notice must render the following meaning before any field accepts
input. Bracketed values are release-blocking configuration, not optional text.

> **Feedback privacy notice - version feedback-notice-v1**
>
> **Who is responsible.** [Legal operator name and address] operates this
> feedback route and decides how the information is used. Contact
> [privacy contact] privately about this notice or your rights. If you run your
> own Andreja instance, its operator is responsible for that instance; your
> private submission is not sent to Jamula unless the form clearly shows and
> you choose a separate transfer or publication.
>
> **What to send.** Send only the short description, expected and actual
> outcome, minimal reproduction steps, impact, and optional contact method
> shown in the form. Do not send passwords, tokens, private files, raw logs,
> prompts, messages, health or financial details, or information about another
> person. Diagnostics are off by default. The form will show the purpose and
> exact value or faithful redacted preview for each diagnostic you choose.
> Attachments are not accepted.
>
> **Why we use it.** We use the minimum information to receive, protect,
> investigate, deduplicate, respond to, and improve the feedback process; keep
> the service secure; and honor privacy requests. We do not use feedback for
> advertising, data brokerage, model training, user ranking, pricing, or
> unrelated profiling. [Jurisdiction-specific lawful-basis text] applies.
>
> **Who receives it.** Authorized feedback, engineering, privacy, and security
> personnel receive only what they need. Approved hosting, storage, delivery,
> and anti-abuse providers process limited information under contract. The
> current provider and subprocessor list, locations, and transfer safeguards
> are at [provider notice link].
>
> **GitHub is optional and separate.** Private feedback is not automatically a
> GitHub issue. If a sanitized draft could help the project, we will show the
> exact draft and repository before asking for separate publication consent.
> Refusing does not affect private support. GitHub content may be visible to
> collaborators or the public and may be copied into notifications, caches,
> clones, forks, or indexes that Andreja cannot fully recall.
>
> **How long we keep it.** Private case content is deleted 180 days after
> closure; contact details after 30 days; selected diagnostic values after
> 30 days; quarantined content after 30 days; and rolling backups within
> 35 days. Some minimized consent, rights, suppression, abuse, or incident
> records have the longer periods listed in [retention schedule link]. A
> documented legal or security hold may delay a narrow deletion, and we will
> explain the permitted result when we can.
>
> **Your choices and rights.** You may submit without optional diagnostics,
> publication, or follow-up. Use your private receipt or contact
> [private rights route] to ask for access, correction, deletion, objection, or
> consent withdrawal. You do not need an Andreja or GitHub account. A person
> described in someone else's submission may use the same private route. We
> verify only what is proportionate, do not reveal another person or confirm a
> record to an unverified requester, and provide an appeal route at
> [private appeal route]. Use [private incident route] for a vulnerability or
> suspected personal-data incident; never post it publicly.

The form records the notice version and locale. The field/derived-record
inventory, recipient and processor register, retention schedule, proof matrix,
reason codes, notice, consent schema, and rights/incident runbooks are one
versioned release set. A change to any member requires compatibility review,
deployed-text and schema tests, and a migration decision for active records.

## Consent receipts

Diagnostic and publication receipts are separate immutable-in-meaning records:

| Element | Diagnostic receipt | Publication receipt |
| --- | --- | --- |
| Subject | Source-scoped requester reference | Source-scoped requester reference |
| Purpose | One named diagnostic's investigation purpose | Disclosure of one exact sanitized draft to one named repository |
| Scope | Field name, classification, and SHA-256 digest of the preview/value representation | Repository owner/name/visibility warning and SHA-256 digest of title/body/labels |
| Notice | Notice and consent-text version plus locale | Notice, publication-warning, and consent-text version plus locale |
| Action | Affirmative selection after preview; no preselection | Affirmative confirmation after exact final preview |
| Time | Server receipt time | Server receipt time and immediate pre-publish revalidation time |
| Withdrawal | State, time, route, affected value deletion | State, time, unpublished cancellation or post-publication remediation reference |

Receipts contain neither the diagnostic value nor public draft body. A changed
purpose, value preview, draft, repository, or visibility warning requires a new
receipt. Bundled, implied, coerced, or inactivity-based consent is invalid.

## Rights routes and proportionate proof

All rights requests use a private route. They never require an Andreja or GitHub
account and never enter a public issue, PR, email subject, log body, or metric.
The service returns the same acknowledgment whether a matching record exists.

### Proof ladder

Use the least intrusive sufficient level and record only the level and result:

1. **Possession proof:** valid high-entropy tracking secret plus a one-time
   challenge. A display reference alone is never enough.
2. **Return-channel proof:** a one-time challenge to the already verified
   contact destination. Do not expose a masked destination until proof succeeds.
3. **Context corroboration:** privately supplied approximate time, route, and
   non-sensitive content fragments that can locate a record without revealing
   whether it exists. This can support correction or restriction but does not
   by itself authorize disclosure.
4. **Non-user relationship proof:** the requester identifies what information
   they believe describes them and why. Request only the minimum corroboration
   needed to distinguish them from the submitter or another person. Redact
   unrelated people before review.
5. **Exceptional identity proof:** only when a high-risk disclosure cannot be
   resolved by lower levels and counsel has approved the method. Do not retain
   a government-ID image; use a specialized verifier or record a pass/fail
   result, then delete raw proof within 30 days.

Do not ask a non-user subject for the submitter's identity, tracking receipt, or
account. Do not notify the submitter of the claimant's identity unless necessary
and approved. Proof failure produces a safe denial and appeal route, not a
statement that no record exists.

### Proof and disclosure matrix

| Request and risk | Minimum sufficient proof | Maximum permitted result |
| --- | --- | --- |
| Coarse status, withdraw unpublished consent, or delete using a valid receipt | Possession proof with fresh one-time challenge | Action and safe completion status for that receipt only |
| Correct contact destination or receive a private export | Possession plus return-channel proof when available; otherwise reviewed context corroboration | Verified requester's fields only, with other people and security details removed |
| Non-user restriction, objection, correction, or deletion | Non-user relationship proof sufficient to identify the disputed fields; no submitter receipt required | Restrict first; correct/delete verified fields or provide a safe partial/denied result |
| Disclosure containing sensitive content, another person, or a contested identity | Two independent lower-level proofs or counsel-approved exceptional proof | Minimized private disclosure only after conflict review |
| Public-artifact removal or redaction | Proof sufficient to identify the claimant and affected content; urgency may justify restriction before proof completes | Restrict/remove controlled content; do not disclose the private source or submitter |
| Appeal | Original request reference plus fresh proof appropriate to the requested result | Independent review of reason, scope, hold, and propagation; no broader disclosure |

Proof tokens are single-purpose, expiring, replay-resistant, segregated from
ordinary intake, and access-logged without content. Shared-device and lost-
receipt flows must offer an accessible alternative. No proof route requires a
government-ID image by default.

### Request flow

1. Acknowledge privately with a random request reference and no existence
   confirmation.
2. Classify the requested right and possible safety/conflict conditions.
3. Apply the proof ladder, rate limits, and anti-enumeration controls.
4. Search only approved private systems using restricted staff. Public GitHub
   is searched only for controlled repository artifacts; never search broadly
   for the claimant as a substitute for proof.
5. Restrict disputed processing and pause new publication while deciding.
6. Return access in a private, portable, human-readable package that excludes
   another person's data, security details, privileged material, internal abuse
   signals, and information that would create an unsafe disclosure. Explain
   each withheld category.
7. Correct the private source and permitted derived records, or annotate a
   disputed factual claim when erasing it would misrepresent the case history.
8. Execute deletion/objection/withdrawal as described below. Record system-level
   completion evidence without copying content.
9. Send a safe outcome: completed, partially completed, denied, or delayed by a
   named category of hold; include the controlling reason and private appeal
   route. Use the legally required deadline where applicable. Until counsel
   confirms jurisdictions, the product makes no numeric response-time promise.

The restricted state is an atomic enforced status, not a staff note. Entering it
blocks publication, ordinary support disclosure, dedupe merging, optional
messages, metrics generation, and deletion of fields under an approved hold.
Only the named privacy/security role can release it after recording the
controlling reason, scope, evidence level, expiry/review date, and propagation
actions.

Safe outcome messages distinguish `deleted`, `irreversibly disassociated`,
`redacted`, `retained under scoped exception`, `pending backup expiry`, and
`outside Andreja control`. Every partial or denied outcome names a safe reason
category and private appeal route. A generic anti-enumeration response always
includes a safe next step without implying whether a record exists.

### Conflicting claims

No request defaults to the submitter. When a submitter and non-user subject, two
claimants, another person's rights, a safety concern, or a hold conflict:

1. Immediately restrict access, dedupe, model use, support reuse, and
   publication of the disputed fields.
2. Separate uncontested fields and satisfy them without waiting for the dispute.
3. Determine the purpose, source, each person's interests and rights, proof
   strength, disclosure risk, public status, and applicable legal duty.
4. Prefer deletion or de-identification of unnecessary third-party content.
   For a necessary factual record, restrict and annotate rather than expose it.
5. Escalate legal ambiguity, threats, minors, dependent adults, privilege,
   litigation, regulatory inquiry, or severe safety risk to the approved
   private owner and counsel.
6. Record the controlling reason, fields affected, duration/review date,
   partial result, propagation duties, and appeal route.
7. Respond to each person without identifying the other, quoting disputed
   content, or confirming records beyond their verified entitlement.

## Deletion, redaction, holds, and propagation

Verified deletion removes or irreversibly disassociates the covered private
content, contact destination, source/tenant references, diagnostics, search
documents, dedupe material, caches, queue/dead-letter copies, delivery payloads,
and non-required derived records. It invalidates tracking credentials. The
system records only the minimized DSR completion ledger and any separately
approved suppression or hold record.

Active replicas purge with the source transaction or retry from an idempotent,
durable, content-free deletion job. Each destination returns a scoped receipt.
Retries use bounded backoff; unresolved destinations enter a restricted
exception queue and block any completion claim. Scheduled reconciliation
compares source scope, processor receipts, controlled GitHub actions, index and
queue state, and backup-expiry deadlines. Backups age out within 35 days. A
restore must apply deletion tombstones before restored data becomes available.
Metrics are recomputed or deleted if a person remains identifiable or a
threshold is broken; demonstrably anonymous aggregates need not be changed.

A hold is not a blanket copy. It requires an authorized owner, legal/security
category, exact fields and systems, start, review date, end condition, access
list, and reason that can be disclosed safely. Held data is isolated from
ordinary support, publication, metrics, and model use. Review every 90 days and
delete promptly when the hold ends. Partial deletion proceeds around the hold.
Silence, timeout, case status, or submitter preference cannot release a conflict
or hold for the submitter.

Reopening a case does not reset age silently. It creates a new activity event
and documented continuing need. The active ceiling is recalculated from that
event but remains subject to the 365-day submission ceiling unless a renewed
need, new notice where required, or scoped hold is recorded.

For a controlled GitHub issue, remove or redact personal, diagnostic, or unsafe
content when the repository operator can do so, minimize surrounding quoted
context, and record the action privately. Do not promise removal from
notifications, caches, clones, forks, search indexes, archives, or third-party
copies. Seek available cache/index removal when risk justifies it and record
unretractable copies without reproducing them.

## Accidental or unauthorized GitHub publication runbook

1. **Stop:** disable the publisher credential and affected route; block retries,
   edits, notifications under Andreja's control, and new publications.
2. **Contain:** privately capture only the URL/object IDs, timestamps, actor,
   field categories, exposure window, and cryptographic digest needed to track
   the event. Do not copy the published personal content into an issue or PR.
3. **Remove:** delete the controlled issue/comment/attachment when possible, or
   redact the minimum unsafe text without quoting it in the edit explanation.
4. **Preserve narrowly:** store minimized incident evidence in the restricted
   incident system. Do not preserve the public artifact merely for convenience.
5. **Assess:** route to Deanna Troi and Tuvok; involve Sarek/counsel for
   notification, legal duty, platform request, or hold questions. Identify
   affected people and processors without using public discussion.
6. **Notify safely:** use approved private channels and jurisdiction-specific
   decisions. Never reveal one affected person to another.
7. **Pursue copies:** request available GitHub cache/search removal and record
   known notifications, caches, indexes, clones, or forks that cannot be
   retracted. Do not visit or reproduce more copies than necessary.
8. **Recover:** rotate/restrict credentials, correct the consent/draft binding
   or access control, add a regression test, and re-evidence the route before
   an explicit owner re-enables it.

## Proportionate privacy impact assessment

| Risk | Inherent | Required treatment | Residual / decision |
| --- | --- | --- | --- |
| Free text exposes sensitive information or another person | High | Warnings, minimum fields, no attachments, automated backstop, first-step privacy screen, restricted quarantine | Medium; activate only with tested restricted handling |
| Display reference or DSR route enables enumeration | High | Separate high-entropy secret, uniform responses, rate limits, proof ladder, no destination hints | Low/medium; Tuvok must accept negative evidence |
| Submitter asserts authority over a non-user | High | No proxy-consent assumption, private accountless route, restriction on conflict, independent proof and appeal | Medium; legal ambiguity escalates |
| Optional diagnostics become coerced or overbroad | High | Off by default, field allow list, exact preview, separate receipt, refusal parity, short value retention | Low/medium |
| Private content is published or remains public after withdrawal | Critical | Separate exact-draft consent, immediate revalidation, least-privilege publisher, removal runbook, copy-limit warning | Medium/high because independent copies cannot be guaranteed; explicit residual-risk acceptance required |
| Contact, tracking, and case data become linkable profiles | High | Separate stores/keys, source-scoped references, no user timelines or cross-product joins, short schedules | Low/medium |
| Abuse controls create inaccessible or discriminatory denial | Medium/high | Coarse purpose-limited signals, accessible alternative, human appeal, no cross-site profile, error monitoring | Medium; Guinan and Data review |
| Vendor terms conflict with deletion, role, transfer, or reuse limits | High | Provider inventory, contract and subprocessor gate, deletion/incident evidence, stop on drift | Unaccepted until provider package |
| Backups or derived stores resurrect deleted content | High | 35-day maximum, tombstone replay before restore availability, propagation ledger and drill | Medium; implementation evidence required |
| Holds become indefinite shadow retention | High | Exact scope, separation, owner, 90-day review, end condition, partial deletion | Low/medium with counsel oversight |

The assessment is proportionate to a voluntary, low-volume feedback route with
unstructured text and possible public disclosure. Reassess before attachments,
automated content models, new diagnostics, children-directed use, biometrics,
precise location, managed hosting, new jurisdictions, broader analytics, or a
material increase in scale or abuse.

## Required implementation and negative evidence

Activation requires tests and a controlled exercise proving:

1. The deployed schema rejects undeclared fields and attachments, every
   retained field maps to the inventory, and the notice matches the live route.
2. A display reference alone, guessed receipt, email address, GitHub identity,
   or copied request reference cannot reveal existence, status, content, contact
   hints, DSR state, or incident evidence.
3. Uniform accountless responses, throttling, and accessible recovery resist
   enumeration, false claims, replay, address bombing, and proof escalation.
   Existing and nonexistent lookups are equivalent in status class, body
   schema/length bucket, headers, identifier format, notification behavior, and
   rate-limit accounting. The release artifact must set and test a bounded
   timing tolerance under controlled load; no timing threshold is invented by
   implementation.
4. A non-user with no account, receipt, or return channel can submit a private
   request, receive proportionate proof options, appeal, and obtain a safe
   outcome without learning the submitter or unrelated people.
5. Conflicting requests immediately restrict disputed processing; partial,
   denied, and appealed outcomes expose neither claimant to the other and never
   default to the submitter.
6. Diagnostics are off by default, each exact preview has an independent
   receipt, refusal preserves private submission, and withdrawal deletes the
   value while preserving only the minimized receipt.
7. Publication requires an unchanged exact draft and dedicated receipt.
   Material edits, stale notice, changed repository/visibility, withdrawal,
   missing consent, and publisher over-privilege fail closed.
8. A DSR, proof document, conflict, hold, and incident remain absent from public
   issues, PRs, logs, traces, metrics, queues, alerts, and email metadata.
9. Deletion covers source, contact, diagnostics, indexes, dedupe, caches,
   delivery payloads, derived stores, and credentials; backup expiry and
   tombstone replay prevent resurrection.
10. The accidental-publication exercise disables the route, removes or redacts
    controlled content without public reproduction, preserves only minimized
    private evidence, triggers notification/legal assessment, seeks available
    cache removal, and records unretractable copies.
11. Retention jobs purge each class at its trigger, holds preserve only their
    exact scope, 90-day hold review is enforced, and metrics suppress a bucket
    that falls below the approved threshold after deletion.
12. Processor failure, deletion failure, provider-term drift, missing
    subprocessor information, or unknown residency blocks the affected route
    rather than silently retaining or disclosing data.

## Review and decision record

| Role | Required recorded result |
| --- | --- |
| Deanna Troi, Privacy and Consent Lead | **Approve recommendation.** The package minimizes collection, separates optional consent, protects non-user subjects, and makes conflicting claims restrictive rather than submitter-controlled. |
| Sarek, Legal Lead | **Approve with incorporated amendments.** Role allocations remain deployment-specific; processor terms, GitHub's then-current role/terms, applicable rights and deadlines, lawful bases, periods, transfers, notification, and holds require counsel confirmation. |
| Tuvok, Security Reviewer | **Approve with incorporated amendments.** Activation requires evidence for atomic restriction, replay-resistant proof, anti-enumeration, publisher authorization, least privilege, idempotent deletion reconciliation, and the publisher kill switch. |
| Guinan, Workflow Reviewer | **Approve with incorporated amendments.** Activation requires comprehensible accountless/non-user routes, accessible proof alternatives, consequence-free refusal, specific outcome language, and private appeal. |
| Data, Testability Reviewer | **Approve with incorporated amendments.** All twelve scenarios and the version-set consistency checks are release-blocking; unknown fields and evidence gaps fail closed. |
| Cyrus Jamula, Decision Authority | **Pending:** record `approve`, `amend`, or `reject` with date and any conditions in issue #155. |

Issue closure, label changes, PR merge, or specialist approval do not approve
this package. Only Cyrus's explicit recorded decision does. Until then, all
feedback collection and publication described here remain future/gated.
