# Feedback and support framework

## Status and scope

This framework defines how Andreja receives, protects, triages, routes, tracks,
publishes, resolves, verifies, and learns from feedback. It implements the
feedback direction ratified in `docs/plan.md`; it does not amend the plan or
authorize deployment.

Guinan owns the feedback lifecycle and the user-visible loop. Feature and
domain owners remain accountable for investigation and remediation. Security
vulnerabilities and suspected personal-data incidents leave the ordinary
feedback workflow immediately and use a private response path.

Phase 0 is local and paper research only. This document creates no cloud
account, subscription, free tier, trial, support commitment, legal basis, or
provider selection.

### Goals

- Give every submitter a safe route and a non-enumerable tracking reference.
- Minimize collection and keep personal context out of GitHub, logs, metrics,
  notifications, and support tooling.
- Let a submitter understand status without requiring repository access.
- Prevent duplicate GitHub work while preserving every submitter's follow-up
  path.
- Require explicit preview and consent before sanitized content is published
  to GitHub.
- Close the loop with delivery evidence, user verification, and a reliable
  reopen path.

### Non-goals

- General customer-service case management, live chat, or inbound email.
- Marketing email, engagement analytics, lead scoring, or unrestricted
  business analytics.
- Public disclosure or investigation of vulnerabilities or data incidents.
- Numeric response-time, update-time, or resolution commitments before
  capacity and escalation coverage are approved.
- A permanent queue, email, anti-abuse, hosting, or analytics provider choice.

## Operating principles

1. **Private by default.** Intake is private until privacy screening,
   sanitization, a publication preview, and explicit publication consent are
   complete.
2. **Collect the minimum.** Task, file, prompt, connector, identity, family,
   health, finance, location, credential, token, and raw-log content is
   excluded by default.
3. **Consent is granular.** Optional diagnostics are independently selected,
   explained, and previewed. A single "send diagnostics" checkbox is not
   sufficient.
4. **Status is not a repository link.** An external submitter receives an
   opaque tracking experience and safe return-channel updates even while the
   repository is private.
5. **Priority is evidence-based.** Impact, affected users, severity,
   security/privacy risk, strategic fit, workaround, evidence, and cost matter;
   loudness and social reach do not.
6. **Delivery is not closure.** A delivered change is verified with the
   submitter or objective acceptance evidence. Lack of reply never becomes a
   false `Verified` state.
7. **Metrics remain support controls.** Aggregate feedback measures may improve
   the intake and support process; they may not silently become user profiling,
   advertising, or broad product/business analytics.

## Channels and boundaries

All ordinary channels normalize into the same private triage workflow. They do
not have the same authentication or data boundary.

| Channel | Entry and audience | Boundary and required behavior |
| --- | --- | --- |
| Public-site feedback | Product, website/docs, accessibility, sponsorship/sales, and general feedback from people without a GitHub or Andreja account | Posts only to the separately deployed tenant-less intake boundary. Shows its privacy notice and prohibited-content warning before collection. Never writes to an Andreja tenant or directly to GitHub. |
| Authenticated app feedback | Product feedback and support from a signed-in user | Uses the current tenant only to authorize the action and create a tenant-scoped pseudonymous reference. Shows the exact envelope and each selected diagnostic before submission. It does not include tenant content by default. |
| Repository contributor | Maintainers and eligible contributors with GitHub access | Uses repository issue forms only for sanitized, contribution-safe content. The repository is not a private support route. Personal content and private diagnostics are prohibited. |
| Help and support | Searchable help, known issues, status, guided diagnostics, and escalation | Resolves common questions without collecting a case. Escalation creates a normal private intake record; help search terms do not become feedback or analytics unless separately approved and minimized. |
| Private security/privacy | Vulnerabilities, suspected unauthorized access/disclosure, or personal-data incidents | Clearly separated from ordinary intake and never requires a public issue. Route metadata is minimal; investigation content remains in the approved restricted incident system. |

### Repository forms: current state and required evolution

The repository currently provides sanitized `bug`, `feedback`, `feature`, and
`decision` forms. Each warns against personal or connector content, and
`.github/ISSUE_TEMPLATE/config.yml` points vulnerability and data-incident
reports to the private security policy.

Before accepting broader contributor traffic, add or approve the plan-required
forms for documentation, security/privacy review requests that are safe to
discuss, connectors/skills, cost review, and retrospective actions. A
security/privacy *review request* may be public only when it contains no
vulnerability, incident, personal data, secrets, private diagnostics, or
exploitable detail. The private reporting route remains visually distinct on
every relevant form and help page.

## `FeedbackEnvelope`

`FeedbackEnvelope` is a versioned contract. Optional means "omit when not
needed," not "collect speculatively." Raw contact destinations and tracking
secrets are stored outside the envelope behind restricted references.

| Field | Required | Purpose and constraints |
| --- | --- | --- |
| `schemaVersion` | Yes | Version of the envelope contract. |
| `feedbackId` | Yes | Internal random identifier. Never shown as the public tracking credential. |
| `sourceChannel` | Yes | `PublicSite`, `AuthenticatedApp`, `Repository`, `HelpEscalation`, or `PrivateIncidentRoute`. |
| `feedbackType` | Yes | Controlled category such as bug, product, docs/help, accessibility, feature, support, sponsorship/sales, or general. |
| `submittedAt` | Yes | Server-recorded receipt time. Do not infer location or time zone. |
| `contactPreference` | Yes | `NoFollowUp` or an approved safe return channel. Does not contain the address or account identifier. |
| `returnChannelRef` | Conditional | Restricted reference to separately encrypted contact data. Required only when follow-up is requested. |
| `subjectRef` | No | Source-scoped pseudonymous user reference. It must not be reusable across unrelated systems or exported to GitHub. |
| `tenantRef` | Authenticated only | Tenant-scoped pseudonymous reference used for authorization and routing. Public tenant-less intake must not create or accept this field. |
| `affectedSurface` | Yes | Controlled product, website, help, skill, connector, or contributor surface. |
| `affectedVersion` | No | User-visible release, build, or commit; no device or account fingerprint. |
| `summary` | Yes | Short user-authored description after prohibited-content warning. |
| `expectedOutcome` | No | Expected user-visible outcome. |
| `actualOutcome` | No | Actual user-visible outcome. |
| `reproduction` | No | Minimal sanitized steps; never task, prompt, file, message, or connector content. |
| `reportedImpact` | Yes | Submitter-described effect and whether a workaround exists. It does not set final severity. |
| `severity` | Yes after triage | Guinan-assigned severity using the rubric below. |
| `diagnostics` | No | Array of independently consented diagnostic fields. Empty by default. |
| `privacyClassification` | Yes after screen | Screening result and handling restrictions, not a copy of detected sensitive content. |
| `attachments` | No | Empty by default. Enabled only after the attachment controls and retention decision are approved. |
| `dedupeKey` | Yes after screen | Fingerprint derived only from normalized sanitized fields. Never uses contact, tenant, IP address, or raw content. |
| `relatedFeedbackIds` | No | Internal links for duplicates or split records. |
| `repositoryIssueRef` | No | Restricted mapping to a sanitized issue. External status does not depend on access to this reference. |
| `trackingRef` | Yes | Random, non-sequential display reference that reveals no tenant, user, category, severity, or repository number. |
| `correlationId` | Yes | Operational correlation identifier safe for content-free logs. It is not a tracking secret. |
| `status` | Yes | Lifecycle state defined below. |
| `statusReasonCode` | No | Controlled reason such as duplicate, declined, waiting for information, delivered, or unverified close. |
| `ownerRef` | Yes after triage | Team/role reference, not an individual's contact details. |
| `consentReceipts` | Conditional | Versioned receipts for diagnostics and GitHub publication, each with scope, notice version, time, and withdrawal state. |
| `retentionClass` | Yes | Approved schedule identifier. No schedule is selected until Phase 0 privacy/legal approval. |

### Field-level diagnostic consent

Each optional diagnostic is represented separately:

| Property | Requirement |
| --- | --- |
| `fieldName` | Stable allow-listed diagnostic name. Free-form diagnostic keys are rejected. |
| `purpose` | Plain-language reason the field helps investigate this feedback. |
| `valuePreview` | Exact value or a faithful redacted preview shown before consent. |
| `classification` | Sensitivity and handling label that enforces storage, access, publication, and retention behavior. |
| `selected` | `false` by default; the user must select the field independently. |
| `consentedAt` | Recorded only after the final preview is confirmed. |
| `noticeVersion` | Version of the explanation presented to the user. |

Initial allow-listed candidates are product version, client surface, local time
of occurrence, request correlation ID, and a bounded error code. Account
identifiers, IP addresses, device fingerprints, tenant names, URLs containing
identifiers, stack traces, request/response bodies, model inputs/outputs, and
raw logs are not initial diagnostics.

Changing the allow list, preview transformation, classification, purpose,
storage, or retention requires a privacy artifact and tests.

### Default exclusions

Client and server validation warns, blocks, or quarantines likely:

- Credentials, secrets, tokens, cookies, authorization headers, and recovery
  material.
- Names, direct identifiers, account addresses, precise locations, and tenant
  names not required for the return channel.
- Task, file, prompt, assistant, email, message, calendar, connector, and skill
  payload content.
- Family, relationship, health, finance, employment, benefits, insurance, and
  other sensitive personal context.
- Raw logs, traces, screenshots, archives, database exports, and arbitrary
  attachments.

Detection is a backstop, not permission to collect. A detected high-risk
submission is restricted and routed for private review; it is never copied into
an error log or a GitHub issue.

## Tenant-less public intake

Public intake is a separately deployed service and queue with no trust path to
an Andreja user data plane.

### Data flow

1. The independently deployed public/help site serves the form without Andreja
   app cookies, user tokens, or access to tenant data.
2. Before collection, the form presents the current privacy notice, prohibited
   content, optional return-channel purpose, publication behavior, retention
   statement, and access/delete route.
3. Client validation minimizes the payload and leaves diagnostics and
   attachments empty.
4. The tenant-less endpoint enforces size, schema, origin, rate, and abuse
   controls; performs secret and high-risk-content screening; and returns the
   same non-revealing response for accepted or quarantined submissions.
5. The service stores the envelope, contact record, consent receipts, and
   tracking credential as separately protected records. It enqueues only an
   internal `feedbackId` and content-free correlation metadata.
6. Guinan accesses the private triage view through least-privilege
   authorization. No direct database or queue browsing is the normal support
   workflow.
7. Any GitHub issue is produced only from a separately saved sanitized draft
   after dedupe, preview, and publication consent.

### Privacy and security controls

- No tenant identifier is accepted, generated, inferred, or used as a
  partition key.
- Encryption in transit and at rest, key ownership/rotation, data residency,
  subprocessors, and administrative access require reviewed Phase 0 decisions.
- Contact destinations are separately encrypted and access-logged. Triage
  views display only the chosen channel and a masked destination until a
  message must be sent.
- Least-privilege roles separate triage, restricted privacy/security review,
  system administration, and aggregate metric access.
- Content is absent from application logs, traces, metrics, alert payloads,
  queue metadata, and email-provider metadata.
- Backups, replicas, exports, quarantines, and dead-letter handling inherit the
  record's classification and deletion requirements.
- Failure messages do not confirm whether a person, address, tracking
  reference, duplicate, incident, or internal issue exists.
- Accessibility is not traded for anti-bot friction; an accessible alternative
  and recovery path are required.

### Notice, retention, and data-subject requests

The tenant-less notice must name the controller/owner, purposes, fields,
optional fields, recipients, publication possibility, approved legal
basis/consent result, retention schedule, security contact, and access,
correction, deletion, objection, and consent-withdrawal routes. Sarek and
Deanna Troi must review the legal-basis/consent and notice decisions; this
framework does not select them.

Every storage class has an approved retention trigger and purge behavior for
active records, closed records, contact data, consent receipts, quarantines,
backups, email delivery events, abuse evidence, and aggregate metrics. No
indefinite default is allowed. The schedule and any legal/security hold
exceptions are Phase 0 decisions.

A public submitter can request access, correction, or deletion using the
tracking receipt plus an approved proof mechanism, or the verified return
channel when one was supplied. The mechanism must not require an Andreja or
GitHub account and must not disclose a record to someone who merely knows the
display reference. The runbook records identity/proof decisions, request
status, approved exception, propagation to backups/derived systems, completion
evidence, and a safe response. Never place the request or its evidence in a
GitHub issue.

Deletion removes or irreversibly disassociates contact and source records and
propagates to permitted derived stores. A previously consented public GitHub
issue is a separate public artifact; the publication preview must explain the
limits of later retraction, copies, notifications, and forks. Approved
redaction/removal procedures apply when publication later proves unsafe.

## Abuse and unsafe-content handling

Controls are layered and purpose-limited:

- Per-endpoint and coarse network rate limits, bounded payloads, timeouts, and
  concurrency limits.
- Accessible bot challenge or equivalent risk control only when evidence
  justifies it; no advertising tracker or cross-site behavioral profile.
- Schema allow lists, text/attachment type allow lists, archive rejection,
  malware scanning where attachments are later enabled, and secret/high-risk
  content detection.
- Duplicate and burst detection using sanitized fields and short-lived,
  protected abuse signals rather than durable user fingerprinting.
- Queue backpressure, quarantine, dead-letter limits, cost caps, and a
  documented degraded mode that does not silently lose accepted records.
- Restricted review for threats, harassment, illegal content, vulnerability
  details, and suspected personal-data incidents.
- Block, appeal, evidence retention, and law-enforcement/counsel escalation
  rules approved before launch.

Abuse controls never downgrade a suspected vulnerability or data incident into
ordinary spam, and rejected submissions never echo sensitive input.

## Privacy screening and deduplication

### Privacy screen

Guinan or an authorized restricted reviewer:

1. Checks the category and private-route triggers.
2. Removes or restricts prohibited content without copying it elsewhere.
3. Confirms every diagnostic has a valid field-level consent receipt.
4. Produces a minimal sanitized working summary.
5. Assigns the enforced privacy classification and handling route.
6. Records only controlled reason codes for redaction or restriction.

Security/privacy incident content is not investigated in this workflow.
Guinan sends the safe acknowledgment and hands the restricted reference to the
approved incident owner.

### Dedupe before GitHub

Deduplication occurs after privacy screening and before issue drafting:

1. Compute a candidate key from controlled category, affected surface,
   compatible version range, normalized sanitized outcome, and bounded error
   code where consented.
2. Search active and recently resolved private feedback plus sanitized
   repository issues. Do not compare contact data, tenant identifiers, IP
   addresses, or excluded content.
3. Have Guinan confirm the match; similarity alone does not merge records.
4. Link the new `feedbackId` to the canonical work item while preserving its
   own tracking reference, contact preference, consent receipts, and
   verification opportunity.
5. Explain the duplicate decision through the safe return channel. Never send
   an inaccessible private GitHub link as the status experience.

If one report contains independently actionable outcomes, split private child
records before publication. If records were incorrectly merged, unlink and
retriage them without losing status history.

## Severity and escalation

Severity describes user impact and routing urgency, not a promised response or
resolution time.

| Severity | Definition | Route |
| --- | --- | --- |
| `S0 Restricted` | Suspected vulnerability, unauthorized cross-tenant access, secret exposure, or personal-data incident | Stop ordinary triage; acknowledge without detail and hand to the approved private security/privacy incident route. |
| `S1 Critical` | Widespread unavailability, unrecoverable data loss/corruption, critical accessibility barrier, or severe safety/user-agency failure without a viable workaround | Escalate to Guinan, the accountable domain owner, operations, Data, and the applicable security/privacy lead. Cyrus decides extraordinary prioritization. |
| `S2 High` | Major supported outcome blocked for one or more users, repeated data-integrity risk, or serious accessibility failure; workaround absent or unsafe | Route promptly to the domain owner with acceptance and failure-path evidence. |
| `S3 Normal` | Material defect, documentation/support gap, or degraded outcome with a reasonable workaround | Normal triage and prioritization. |
| `S4 Improvement` | Feature request, usability improvement, question, or low-impact documentation suggestion | Route to the appropriate portfolio or help backlog after dedupe. |

Guinan may raise or lower severity when evidence changes and communicates the
reason. Security/privacy classification overrides ordinary severity handling.
Numeric escalation and aging thresholds are Phase 0 capacity decisions.

## Lifecycle and communication

### State model

The canonical lifecycle is:

`Received` -> `PrivacyScreened` -> `Acknowledged` ->
`NeedsInformation` or `Triaged` -> `Planned` / `InProgress` / `Declined` /
`Duplicate` -> `Delivered` -> `Verified` -> `Closed`

Rules:

- `Received` means durable acceptance into the correct private boundary, not
  merely an HTTP success.
- `PrivacyScreened` is required before ordinary staff access, dedupe, or
  publication drafting.
- `Acknowledged` records the opaque tracking reference and safe return route.
- `NeedsInformation` asks only for the minimum missing evidence and repeats
  prohibited-content guidance.
- `Triaged` records severity, route, owner, dedupe result, and next-decision
  reason.
- `Planned` and `InProgress` communicate intent, not a delivery date.
- `Declined` provides a reason and conditions that could justify
  reconsideration.
- `Duplicate` retains an independent status subscription and verification path.
- `Delivered` includes release, help, or remediation evidence and known
  limitations.
- `Verified` requires explicit submitter confirmation or documented objective
  acceptance evidence. Silence is never verification.
- `Closed` records a reason such as verified, unverified after an approved
  follow-up window, declined, duplicate-following-canonical, withdrawn,
  deleted, or unable-to-contact.

`NeedsInformation`, `Declined`, `Duplicate`, `Delivered`, and `Closed` can
return to `Triaged`. A submitter can request reopen through the tracking
experience or safe return channel. Guinan records the new evidence, confirms or
rejects reopen with a reason, and never requires a new public disclosure.

### Opaque tracking

- `trackingRef` is random, non-sequential, non-semantic, and contains no
  repository, tenant, user, severity, or category information.
- A status lookup requires a separate high-entropy receipt secret or verified
  return channel. The display reference alone is not an authenticator.
- The external view exposes only safe coarse status, last safe update,
  information requested, public release/help links, and reopen/verify actions.
- Internal owners, private issue numbers, security classifications, other
  reporters, dedupe counts, and restricted evidence are not exposed.
- Rotation, recovery, failed-attempt throttling, expiry, and retention are
  Phase 0 decisions and must have test evidence.

### Safe return channels

The user chooses no follow-up or an approved channel. Messages:

- Include the tracking reference, current safe status, required action, and
  public help/release/status links.
- Never include the original submission, diagnostics, private GitHub URL,
  tenant name, sensitive classification, or another submitter's information.
- Use content-free delivery logs keyed by correlation ID.
- Respect channel revocation, bounce/complaint suppression, and approved
  essential-notification rules.
- Provide a non-email way to view status and request access/delete.

## GitHub publication

Publication is optional and never required to receive support.

The configurable publication target defaults to `Jamula/Andreja`. Implementations
must accept the repository owner and name through deployment configuration rather
than compile a personal account into the publisher. An override must identify an
approved repository with equivalent privacy, security, and retention controls;
tests must cover both the canonical default and an override.

1. Complete privacy screening, private incident routing, and dedupe.
2. Create a sanitized draft containing only the proposed title, body, labels,
   acceptance evidence, and safe source reference.
3. Show the submitter the exact GitHub preview plus a plain-language warning
   that the content may become visible to repository collaborators now and to
   the public if repository visibility changes. Explain the limits of later
   withdrawal.
4. Obtain a dedicated, versioned publication consent receipt. Diagnostic
   consent, contact consent, terms acceptance, or a general privacy checkbox
   cannot substitute for publication consent.
5. Revalidate the unchanged draft immediately before creation. Any material
   edit requires a new preview and confirmation.
6. Create or update the GitHub issue through an app-owned, least-privilege
   grant. Never expose a user credential or let an intake caller invoke GitHub
   directly.
7. Store the restricted mapping privately. The GitHub issue contains no
   `feedbackId`, tracking secret, contact reference, tenant reference, raw
   diagnostic, or personal content.
8. Return status through the opaque experience. A GitHub link is supplementary
   only when the submitter can safely access it.

Consent may be withdrawn before publication. Post-publication handling follows
the approved correction/redaction/removal procedure and cannot promise removal
from notifications, caches, clones, or forks.

## Routing and ownership

### Routing

| Feedback class | Accountable destination |
| --- | --- |
| Product/domain behavior | Relevant feature or domain owner |
| Public site, help, accessibility | Jadzia Dax, with Data for accessibility evidence |
| Platform/channel operations | Jett Reno |
| Assistant, skills, semantic graph, federation | Seven of Nine or the named capability owner |
| Security vulnerability or technical incident | Tuvok through the private incident process |
| Privacy/consent or suspected data incident | Deanna Troi, with Tuvok and Sarek as required, through the private process |
| Legal, IP, regulatory, or privileged inquiry | Sarek through the confidential counsel route |
| Cost, sponsorship, or sustainability | Quark; Neelix joins for approved public/community communication |
| Quality, regression, or release evidence | Data |
| Portfolio priority or cross-owner conflict | Picard; Cyrus retains final human accountability |

### RACI

| Activity | Responsible | Accountable | Consulted | Informed |
| --- | --- | --- | --- | --- |
| Intake operation, acknowledgment, privacy screen, dedupe, tracking, and closure | Guinan | Guinan | Deanna Troi, Tuvok, Jadzia Dax, Jett Reno as applicable | Submitter |
| Feature investigation and remediation | Assigned delivery team | Feature/domain owner | Guinan, Data, applicable specialists | Submitter |
| Security vulnerability/incident handling | Approved incident responders | Tuvok | Deanna Troi, Sarek, Jett Reno, Data | Guinan and Cyrus on a need-to-know basis |
| Privacy/consent/data-incident handling | Approved privacy/incident responders | Deanna Troi | Tuvok, Sarek, Jett Reno, Data | Guinan and Cyrus on a need-to-know basis |
| Acceptance and regression evidence | Delivery team and Data | Data | Domain owner, Guinan, submitter where safe | Cyrus |
| Help, known-issue, and release-content update | Domain owner and Jadzia Dax | Domain owner | Guinan, Data, Neelix when public claims apply | Submitters following the item |
| Feedback metric definitions and quality | Guinan and Data | Guinan | Deanna Troi, Tuvok, Quark, Picard | Cyrus |
| Numeric targets, retention, legal basis, provider, and launch approval | Named artifact owners | Cyrus | Guinan, Deanna Troi, Tuvok, Sarek, Data, Jett Reno, Quark | Relevant delivery owners |

RACI does not grant broad access to feedback content. Each participant receives
only the minimum sanitized information needed for the assigned action.

## Phase 1B minimal transactional email

Phase 1B email is outbound only for acknowledgments, safe status updates,
information requests, delivery evidence, and verify/reopen prompts. General
inbound support email, reminders, marketing, and product messaging remain
outside this scope.

Minimum requirements:

- An approved sender identity and domain with SPF, DKIM, DMARC, and alignment
  evidence before external sending.
- Plain-text and accessible HTML forms with equivalent meaning, honest sender
  identity, stable subject conventions, locale handling, and no tracking
  pixels.
- No feedback body, diagnostic, tenant name, private issue link, or sensitive
  classification in subject, body, headers, templates, provider tags, or
  delivery metadata.
- Idempotent sends, bounded retries, provider timeout/failure handling, and a
  content-free message correlation ID.
- Bounce, block, complaint, and suppression processing; approved handling for a
  previously chosen channel that becomes unsafe or unavailable.
- Address verification and change/revocation controls that do not reveal
  whether a feedback record exists.
- Delivery-event minimization, access controls, retention/purge, export/delete
  propagation, audit, cost caps, and reconciliation evidence.
- Abuse controls for address bombing, repeated notifications, template
  injection, header injection, tracking-reference guessing, and queue replay.
- A visible non-email tracking and DSR path.

Phase 0 must decide the provider, sender/domain, legal and consent posture,
essential-notification policy, data residency/subprocessors, retention, volume
caps, cost envelope, support ownership, and bounce/complaint escalation. No
provider is implied by this framework.

## Help and runbook requirements

### Canonical help content

Before Phase 1B acceptance, publish reviewed, versioned, searchable, accessible
content for:

- What each feedback route is for and what must never be submitted.
- How public tenant-less and authenticated intake differ.
- What diagnostics are available, why each is useful, and how preview/consent
  works.
- How opaque tracking, safe status, verify, reopen, access, correction,
  deletion, and consent withdrawal work.
- When content may become a GitHub issue and what public consent means.
- How to report a vulnerability or suspected personal-data incident privately,
  including `security.txt`.
- Known issues, release notes, availability status, guided troubleshooting, and
  escalation.
- Supported return channels and the limited role of transactional email.

Each page has an owner, product/version applicability, last review date,
revalidation trigger, privacy/security/public-claim reviewer where applicable,
and tested links. Product behavior is incomplete until matching help and
scenario evidence are updated.

### Required runbooks

- Intake health, queue backlog, backpressure, quarantine, dead-letter, and
  recovery without content exposure.
- Privacy screening, restricted-content handling, and safe redaction.
- Private vulnerability and personal-data-incident handoff.
- Dedupe, split, incorrect-merge recovery, and canonical issue changes.
- GitHub preview/consent, grant failure, publication correction, and unsafe
  publication removal.
- Opaque tracking credential rotation/recovery and suspected enumeration.
- Return-channel verification/revocation and transactional-email
  bounce/complaint/suppression.
- Access, correction, deletion, objection, consent withdrawal, hold exception,
  and backup/derived-data propagation.
- Abuse escalation, appeal, cost protection, and accessible challenge recovery.
- Status aging, ownership transfer, decline, delivery, verification, unverified
  closure, and reopen.
- Metric generation, privacy threshold failure, access review, purge, and
  unauthorized-use response.

Every runbook names an owner, approver, trigger, prerequisites, least-privilege
access, safe evidence, escalation path, rollback/stop condition, drill cadence,
and last successful exercise. Numeric cadence and response targets remain
Phase 0 decisions.

## Metrics and use restrictions

Allowed support-quality measures are:

- Counts by controlled source, type, safe surface, severity, lifecycle state,
  and aggregate theme.
- Time to privacy screen, acknowledgment, triage, owner assignment, delivery,
  verification, and closure.
- Aging, reopen rate, duplicate rate, incorrect-merge rate, information-request
  rate, delivery-to-verification rate, and unverified closure rate.
- Help success/deflection, known-issue usefulness, accessibility outcomes, and
  submitter-provided outcome quality or satisfaction.
- Privacy-screen restriction rate, diagnostic-selection rate by allow-listed
  field, publication-consent rate, DSR completion, abuse-control errors, and
  safe delivery/bounce/complaint rates.

Controls:

- Maintain a metric catalog with purpose, decision/use case, owner, definition,
  numerator/denominator, source fields, classification, aggregation threshold,
  freshness, quality checks, access, retention, and deletion behavior.
- Generate metrics from controlled classifications and events, not feedback
  text, attachments, contact data, tenant identifiers, tracking secrets, raw
  diagnostics, IP addresses, or user-level timelines.
- Apply approved minimum-group and time-bucket thresholds. Suppress small
  groups rather than exposing them as zero or a precise count.
- Prohibit joins to personal task/profile data, assistant prompts, connector
  data, advertising identifiers, company accounting, or unrestricted product
  telemetry.
- Keep support metrics separate from Quark's financial ledger and any future
  business/product analytics platform.
- Do not rank, target, price, moderate, or personalize users from feedback
  behavior.
- Start public-site analytics off. A future analytics purpose, field list,
  retention, consent/legal basis, and opt-out require separate approval.
- New uses and exports require purpose review; convenient availability is not
  authorization.

Dashboards beyond restricted operational support views remain deferred until
the plan's business/product analytics gates are met.

## Phase 1B acceptance evidence

Phase 1B may claim this framework is implemented only when automated tests,
reviewed artifacts, and a controlled live exercise demonstrate:

1. A public submitter without Andreja or GitHub credentials receives an opaque
   tracking receipt; the record exists only in the tenant-less boundary and no
   user data-plane record, cookie, token, or GitHub issue is created.
2. Authenticated intake defaults every diagnostic off, previews each selected
   field and purpose, records independent consent, and proves tenant isolation.
3. Default-exclusion tests block or restrict representative secrets, prompts,
   task/file/connector content, raw logs, and sensitive personal context
   without echoing them into logs, traces, metrics, alerts, or errors.
4. Public access, correction, deletion, and consent-withdrawal exercises work
   without an Andreja or GitHub account and propagate to approved derived
   stores, delivery records, and backup handling.
5. Abuse, burst, replay, enumeration, oversized-payload, injection, unsafe
   attachment, queue-failure, and accessible-alternative scenarios fail safely
   within approved cost and availability bounds.
6. Two sanitized reports dedupe before GitHub; each tracking reference retains
   independent status, follow-up, reopen, and verification.
7. GitHub publication shows the exact final preview, requires dedicated
   consent, invalidates consent after a material edit, and publishes no private
   identifiers, diagnostics, contacts, or tracking secrets.
8. A vulnerability and a suspected personal-data incident use the private
   route, expose no public issue, and produce only a safe ordinary-workflow
   acknowledgment.
9. An external submitter follows status, receives help/release evidence, and
   verifies or reopens the outcome without repository access. A private GitHub
   link is never the only status.
10. Every lifecycle transition, ownership transfer, decline, duplicate,
    delivered, verified, unverified close, and reopen path has authorization,
    audit, notification, and failure-path evidence.
11. Minimal outbound email passes identity/alignment, accessibility,
    idempotency, bounce/complaint/suppression, abuse, privacy, retention, cost,
    and provider-failure tests without message or metadata content leakage.
12. Aggregate metrics reconcile to controlled test records, suppress
    disallowed small groups, contain no content or direct/pseudonymous
    identifiers, and cannot be joined through the supported interface to
    product or financial data.
13. Canonical help, known-issue, status, private-reporting, tracking, DSR,
    publication-consent, and verify/reopen content passes accessibility, link,
    ownership, version, and stale-page checks.
14. The required runbooks are reviewed and exercised with content-free
    evidence, and each unresolved failure has a blocking issue and owner.

Guinan, Deanna Troi, Tuvok, Data, and Cyrus review the evidence appropriate to
their roles. Cyrus approves the Phase 1B launch decision and residual risks.

## Phase 0 decisions and blocking artifacts

The following remain decisions, not assumptions:

| Decision | Required owners/review |
| --- | --- |
| Numeric acknowledgment, update, escalation, aging, verification, closure, and DSR targets based on support capacity | Guinan and Data propose; Jett Reno and Quark assess operations/cost; Sarek reviews commitments; Cyrus approves |
| Retention schedules, controller/processor roles, notice, legal basis/consent, DSR proof, hold exceptions, and public-artifact handling | Deanna Troi and Sarek lead; Tuvok reviews controls; Cyrus approves |
| Tenant-less queue/store, encryption/key, residency, backup, restricted access, and isolation topology | Spock and Jett Reno lead; Tuvok and Deanna Troi challenge; Quark validates cost; Cyrus approves |
| Transactional-email provider, sender identity/domain, delivery events, consent posture, subprocessors, cost cap, and support ownership | Jett Reno and Guinan lead; Tuvok, Deanna Troi, Sarek, Quark, and Data review; Cyrus approves |
| Anti-abuse service/control set, thresholds, evidence retention, appeal, and degraded mode | Tuvok and Jett Reno lead; Guinan, Deanna Troi, Data, and Quark review; Cyrus approves |
| Attachment support, types, size, scanning, quarantine, retention, and accessibility | Guinan and Tuvok lead; Deanna Troi, Data, Jett Reno, and Quark review; Cyrus approves |
| Opaque tracking secret lifecycle, recovery, coarse status fields, and notification policy | Guinan leads; Tuvok, Deanna Troi, Data, and Jett Reno review; Cyrus approves |
| Metric definitions, aggregation thresholds, retention, access, and prohibited-use enforcement | Guinan and Data lead; Deanna Troi, Tuvok, Picard, and Quark review; Cyrus approves |
| Support hours/languages, staffing, restricted incident coverage, and externally stated commitments | Guinan and Picard lead; Tuvok, Deanna Troi, Sarek, Jett Reno, Quark, and Data review; Cyrus approves |

Required blocking artifacts are the data-flow and privacy artifact, threat/abuse
model, provider-neutral architecture decision, retention/DSR decision,
transactional-email decision, cost model and caps, testing-matrix scenarios,
canonical help content, and exercised runbooks. Provider-specific work and any
managed deployment wait for the separate Phase 1B budget, legal, isolation,
SLO, and human approvals required by the ratified plan.
