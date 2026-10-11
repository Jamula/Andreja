# Feedback security #156: decision and verification supplement

## Status, evidence, and authority

- **Status:** Proposed; not approved, implemented, or independently reviewed.
- **Issue:** [#156](https://github.com/Jamula/Andreja/issues/156).
- **Candidate version:** `feedback-security-v1.1`, comprising this supplement
  and the unchanged [v1 package](feedback-security-abuse-incident-routing.md).
- **Owner:** Tuvok; operational co-lead Jett Reno.
- **Required review:** Jett Reno, Deanna Troi, Guinan, Data, and Quark.
- **Decision authority:** Cyrus Jamula, including residual-risk disposition.
- **Evidence checked:** 2026-10-10; the v1 package arrived through merged
  [#177](https://github.com/Jamula/Andreja/pull/177), merge commit
  `2aa256345615b4cbe1d5b87d7e64ae5e44dbe9b6`.

This is a proposed supplement, not a replacement threat model or a second
approved policy. The [feedback framework](feedback-support.md),
[canonical threat model](../threat-model.md), and
[privacy baseline](../privacy.md) retain their authority and status. V1 already
maps public/authenticated forms, endpoints, queues, stores, triage, tracking,
publishers, return channels, backups, and administrators and gives intake-specific
STRIDE/LINDDUN threats and a ten-row abuse register. Those analyses are reused,
not recreated or reported as reviewed.

Live #156 and [#155](https://github.com/Jamula/Andreja/issues/155) are open.
#155 was reopened during this preparation; its earlier closed state and last
decision-request comment did not record approval of `feedback-privacy-v1`.
The [privacy package](../legal/feedback-privacy-retention-dsr.md) remains proposed.
Its historical specialist recommendations do not approve this supplement,
its subsequent amendments, or activation. Deanna is rechecking transfer and
hold gaps; no retention duration, proof ladder, exception, or hold is inherited
as approved.

Deanna's unmerged [#193](https://github.com/Jamula/Andreja/pull/193) proposes
`feedback-privacy-v1.1` / `feedback-notice-v1.1` at commit
`49f07d8be9400fb544b073de8311781bf3460811`; reported LF-normalized privacy
artifact SHA-256:
`25b21c71590df22c48a3351a3a5af163c4130d63a93bb3b7dc08181ffe673649`.
That is a proposed dependency revision for transfer evidence and distinct scoped
hold-record inventory, not an approved output or locally merged source.
Cyrus's direction to Deanna to **AMEND and proceed** authorizes that amendment,
not acceptance, a new proof policy, or activation. Recheck the final privacy
revision before recording #156 reviews; do not bind them to obsolete v1 text.

There is no feedback implementation or exact-release evidence in this
package. All runtime results below are **Blocked / unavailable**. No
collection, publication, provider selection, procurement, spend, deployment,
external email, DNS change, support-time commitment, or launch is authorized.
Attachments remain disabled.

## Necessary delta register against merged #177

These gaps come from v1's own open-decision register and #156's binary
criteria, not from approval being absent. No data-flow deployment, trust
boundary, lifecycle, or privacy policy is redesigned.

| Delta | Existing gap and decision consequence | Narrow addition here |
| --- | --- | --- |
| D1: acknowledgment | V1 open decision 5 leaves receipt/stub handling unresolved; #156 requires all six outcomes non-enumerating without silent loss | Explicit processing-versus-acceptance proposal, storage-failure behavior and #155 stub-policy dependency |
| D2: credential custody | V1 open decision 4 leaves verifier algorithm/storage, expiry and throttling details for review; #156 requires verifier-only custody and record/boundary denial evidence | Proposed namespace encoding and generation/session binding, browser/proxy capture controls; expiry/proof still privacy-gated |
| D3: transaction proof | V1 requires atomic swap but does not enumerate nonce/proof-grant consumption, stale-session invalidation and scoped retry-marker transaction assertions | Linearizable transaction/rollback oracle and authenticated lost-response retry; preserves client-held candidate protocol |
| D4: recoverable custody | V1 open decision 3 lacks key inventory and backup/restore evidence; #156 includes encryption/key custody/rotation and revoked-credential isolation | Required custody fields, key-retirement and restore reconciliation criteria, without provider selection |
| D5: measurable abuse | V1 open decision 2 lacks numerical limits/load evidence; #156 requires measurable rules, cost bounds and degraded behavior for every control | Parameter/evidence shape and cap-boundary oracle for existing controls; Jett/Quark still own numeric proposal |
| D6: acceptance proof | V1 tests are blocked and open decision 7 lacks exact-version reviews; #156 cannot be decided on inferred passing behavior or inherited verdicts | Test identifiers, observable transaction/boundary assertions, criterion crosswalk and version-bound pending ledger |

## Proposed decisions that close specification gaps

These are reviewable proposals, not instructions to activate a service.
Final consideration by Cyrus must wait for a valid #155 decision and actual
required reviews on the same version set.

### Non-enumerating processing versus durable acceptance

Use a common pre-proof processing response for accepted, quarantined, rejected,
duplicate, missing, and incident cases. The proposed message is:
**"Request processed. This does not confirm that a case exists or was accepted.
Use the private status or appeal route with your receipt."**
It contains no user-supplied text, destination hints, classification, duplicate
target, tenant, repository mapping, or incident details.

For structurally valid requests that pass boundary/security and resource checks,
propose a minimal durable receipt object even when no ordinary case is accepted.
It contains only random reference, verifier, boundary, generation, policy
version, expiry, and the minimum authorized outcome mapping. Rejected bodies
are not stored; duplicates never share credentials. A receipt is not an
acceptance promise. After possession proof, expose only the safe status allowed
for that receipt; incident evidence still requires separate incident authority.

**Unresolved #155 dependency:** receipt/stub purpose, recipient/access class,
retention trigger/duration, purge and hold exception must be explicitly covered
and approved through a separate inventory/lifecycle amendment and human
decision. Deanna confirmed that #193 is transfer/hold-only and does not include
or authorize this new receipt/stub class. **Keep the proposed class disabled.**
Existing private-case or proof-token retention is not permission to create
stubs. Do not expand #193 or infer that its eventual approval clears this gap.

In the proposed design, no stub or receipt is issued before origin/CSRF/schema
checks, or when durable storage fails. A common failure response contains no
receipt or acceptance claim. State-independent malformed/unauthorized/over-capacity
failures may use a distinct failure class; they must not depend on lookup
existence, content classification, or incident routing. Apply any abuse refusal
uniformly before record lookup. Unproved lookup outcomes share status, body
schema/length bucket, headers, rate accounting, and notification behavior, with
a measured timing tolerance fixed in the reviewed release artifact. Data must
test the distinction between processing and acceptance; a friendly response is
not durability proof.

### Receipt custody and authorization namespace

Initial secrets are 32 CSPRNG bytes (256 bits), separate from a CSPRNG-generated,
non-semantic display reference. Never reduce entropy by truncation, predictable
seeding, UUID formatting, or encoding; reject malformed credential lengths before
cryptographic work. Enforce reference uniqueness within the source boundary;
collision retries never overwrite or alias an existing receipt.

Propose a domain-separated SHA-256 one-way verifier over an unambiguous,
length-prefixed encoding of purpose/version, deployment boundary, server-derived
tenant scope where applicable, internal record ID, generation, and secret.
Use constant-time digest comparison. High entropy, not an expensive password
hash, resists offline guessing. The exact encoding and cryptographic review are
required implementation evidence. The public namespace never accepts a tenant
field, including a null/empty field. A verified secret authorizes only its own
record and approved receipt operations, not incident-store access, a contact
vault read, another record, a rights disclosure, or publication.

Only the verifier and necessary approved metadata persist. No raw or encrypted
recoverable secret enters storage, queues, backups, exports, browser persistent
storage, URLs (path/query/fragment), history, referrers, logs, traces, metrics,
alerts, support screenshots, or provider metadata. Accept proof/candidates only
in protected POST bodies; disable request/response capture at proxy, application,
error handler, and provider boundaries. Return issuance once over HTTPS in a
non-cacheable response; use `Cache-Control: no-store`, a no-referrer policy,
no third-party scripts, and a restrictive CSP. Keep the secret in browser memory
only; user-directed offline custody is separate from service persistence.
Do not resend it through email or recreate it after a refresh.

Receipt-session authority is bound to boundary, record, generation, purpose,
and expiry. Rotation, revocation, verified deletion, and expiry invalidate that
authority on every read and mutation, not merely at login. A tenant session is
not blanket case ownership; authorize principal and operation as well as tenant.
The issuer and verifier cannot use a public receipt against the authenticated
plane even if the same reference or digest appears in both stores.

### Linearizable rotation, recovery, replay, and idempotency

The client-held candidate protocol in v1 is retained. An approved browser client
generates 32 bytes with its cryptographic RNG and holds the candidate in memory
until definitive completion; no non-cryptographic fallback is allowed. The
server checks the candidate format, but cannot infer RNG entropy from length.
Client source and RNG-failure tests are evidence requirements. The server never
persists or reconstructs the candidate and need not echo an already client-held
candidate in the completion response.

| Step | Required transaction or authorization rule |
| --- | --- |
| Authorize | Validate boundary, record ownership, current generation and permitted operation. Recovery instead requires an unexpired, purpose/record/boundary-bound proof grant produced by the approved #155 mechanism or verified return channel. A reference, address, ordinary session, or operator assertion alone is not recovery proof. Before proof, reveal no existence. |
| Bind request | Bind nonce and idempotency key to actor/proof grant, record, boundary, operation, expected generation, and candidate verifier. Browser mutations require antiforgery and exact allowed `Origin`; missing or hostile origin fails closed. A public form also needs a source-origin-bound antiforgery ceremony; an authenticated cookie is never borrowed for public intake. |
| Commit once | One transaction/CAS locks or compares the current generation, consumes the nonce and recovery grant if applicable, installs the candidate verifier at the next generation, invalidates old receipt sessions, and writes a content-free completion marker and transition event. All succeed or none do. Enforce at most one active verifier per record. |
| Pre-commit failure | Roll back nonce/grant consumption and state together. Exactly the previously valid credential remains valid; no success or newly active candidate is returned. Recovery never resurrects an already expired/revoked credential. If none was valid initially, failure leaves none; success installs exactly one new credential after proof. |
| Post-commit acknowledgment loss | Exactly the committed candidate is valid. Same-operation retry authenticates with that candidate and checks the scoped completion marker; it acknowledges without another transition. Old proof is not reused to authorize a fresh swap. An idempotency key alone never authorizes a retry. |
| Concurrent or changed retry | One generation-CAS winner. A different candidate, actor, boundary, record, or operation with the same key denies without mutation. A losing request cannot rotate the winner or revive the old verifier. |
| Client state loss / marker expiry | Do not restore the old verifier or reconstruct a candidate. Use fresh approved recovery proof. If the marker has expired, valid candidate possession may obtain safe status but cannot repeat the rotation without a new authorized operation/nonce/key. Retry-window and marker-retention policy remain unapproved. |

Apply the same authorization, origin, CSRF, nonce consumption, expected-version,
and transactional idempotency binding to verify, reopen, withdrawal, revoke,
contact changes, publication consent, and every other mutation. Status retrieval
does not mutate the case or consume a recovery grant. Rate-limit status/proof
attempts before expensive comparison without attacker-controlled permanent
lockout of a victim reference. A read-only degraded mode exists only if current
authorization, revocation, generation, and expiry can still be checked.

### Encryption, roles, restore, and incident containment

Jett must supply a boundary-specific custody inventory: TLS termination,
case/tracking/quarantine/incident/backup encryption, distinct contact-vault keys,
key ID/version, authorized runtime/admin roles, custodian and recovery custodian,
rotation trigger/runbook, compromise revocation, and decrypt/restore evidence.
Propose authenticated encryption with record/boundary/version as authenticated
context; keys remain outside the stores they protect. No algorithm or provider
choice here authorizes provisioning. Encryption is not isolation from a
compromised process, root operator, or key custodian; Cyrus must disposition
that residual risk explicitly.

Triage may see only its authorized sanitized case projection; the sender obtains
only a purpose-bound contact lookup for an approved message; the publisher sees
only the exact consented sanitized draft. None gains incident, raw verifier,
backup, key-management, or cross-boundary authority. Incident responders receive
separately authorized source-boundary access. A source's opaque incident alert
is not a capability to the other source. Break-glass is independently authorized,
time-limited, content-minimally audited, and reviewed; it cannot grant publication
or bypass rights/hold policy.

Key rotation must keep approved retained ciphertext readable without extending
retention; old decrypt keys retire only after permitted re-encryption/backup
expiry and recovery evidence. Compromise pauses affected access and sends the
case to restricted Tuvok/Deanna/Jett handling, with Sarek for legal/notification
questions. Do not rotate all receipt secrets merely to change an encryption key.
Restore in isolation and reconcile authoritative revocation generations,
deletion tombstones, consent withdrawal, expiry, and scoped holds before serving
traffic. If their freshness cannot be proved, tracking and publication stay off;
a stale backup may not revive an old credential or destination.

Suspected secrets, vulnerabilities, personal-data incidents, threats, or uncertain
high-risk classification stop ordinary triage/dedupe/publication. Route security
cases to Tuvok and privacy cases jointly to Deanna/Tuvok; Jett contains the source
boundary, Sarek assesses counsel/notification questions, and Guinan receives
only authorized safe workflow status. Neither spam rejection nor human appeal
releases an incident to ordinary handling. False-positive release needs a
restricted reviewer, minimized reason, fresh privacy screen and publication
revalidation; pending conflict/hold authority cannot be bypassed.

Before any public intake activation, evidence must show an accurate
`/.well-known/security.txt` with owned private route, canonical location and
expiry, plus synthetic vulnerability and personal-data-incident handoff,
accessible alternatives, receiver acknowledgment, unavailable-route containment,
and credential/publication kill-switch behavior. Do not publish placeholders
or test by transmitting real reports. No reporting file is published here.
If the private route fails, stop affected intake and publication; only a
previously approved private alternative may be displayed.

## Measurable abuse and cost evidence contract

The v1 abuse register remains the control inventory, including each owner,
evidence-retention rule, private appeal/accessible alternative, cost bound, and
degraded mode. Every row remains subject to #155 retention/hold approval;
no numeric period is silently adopted. Jett and Quark's
[#157](https://github.com/Jamula/Andreja/issues/157) package owns measured capacity
and cost proposals, not weaker security behavior. Before review, attach this
complete parameter record, not provider defaults:

| Control group from v1 | Required measurable decision and pass/fail oracle |
| --- | --- |
| Parser/body/time/concurrency | Numeric maximum wire and decoded bytes, field count/length, parsing depth, request/screening time and concurrent work. At the cap behavior is documented; above it no expensive allocation, screening, or durable body storage occurs. |
| Rate/burst/status/recovery | Per-route and approved transient source window/burst limits, global ceiling, proof-attempt ceiling, throttle-state size/expiry and response timing tolerance. Exact cap, cap-plus-one, distributed flood and attacker-targeted-reference tests enforce refusal without existence signals or permanent victim lockout. |
| Screening and challenge | Versioned deterministic detector rules, synthetic secret/incident positive and benign/accessibility negative corpus, expected outcomes and review rule. All incident canaries restrict; uncertainty never auto-releases. Challenges stay off without measured necessity and a tested non-challenge alternative. |
| Dedupe/idempotency | Maximum authorized candidate set/comparisons, bounded index work and retry window. No out-of-scope record reaches similarity evaluation; zero automatic merges; one committed transition for duplicate request keys. |
| Queue/quarantine/dead letter | Byte/count ceilings, retry/backoff/age limits and high-water stop point with durable reserve for already accepted work. Saturation produces no false acceptance, silent eviction, unbounded retry, or incident fallback. |
| Cost/degraded operation | Per-request worst-case CPU/memory/store/crypto work, bounded retained bytes and total approved resource/cost ceiling. Test hard stop before the limit is exceeded; no paid control or provider call before separate authorization. |
| Incident/publication/attachments | Any suspected incident restricts regardless of spam score; no incident publication; unavailable private route stops affected intake. Zero accepted attachment types and zero durable upload bytes, including multipart and encoded-inline upload bypasses. |

Each measurement records configuration/revision, units, workload, hardware,
expected boundary behavior, observed peak, cost assumption, owner and safe
failure result. Data checks cap-minus-one/cap/cap-plus-one, cancellation, partial
failure and saturation without real data. Accessible/private appeal consumes a
separately bounded path, never an unlimited bypass; a challenged submitter need
not obtain an account, email, GitHub access, or visual challenge capability.
Missing parameters, unapproved retention, absent accessible alternative, or an
unknown cost bound blocks activation rather than becoming an implementation
default. This document claims no measured numeric threshold.

## Boundary-negative and failure test plan

Data owns future exact-release execution; Tuvok reviews adversarial expectations.
Retain test source, configuration, exact command/result, build commit and
content-free observations. Test failures/unknowns are blockers, not skipped
passes. Use synthetic canaries only; never keep raw receipt values in reports.

| Test ID | Fixture / adversarial operation | Required observable assertion |
| --- | --- | --- |
| FS156-01 | Public input with tenant/principal fields, including null/empty; tenant A session plus B record; record A receipt plus record B | Denied before unauthorized lookup/mutation; spy/store evidence shows zero out-of-boundary calls, zero changed rows and no returned metadata. Apply to every read/mutation and worker. |
| FS156-02 | Force identical references in public/A/B namespaces and insertion collisions | No alias/overwrite; verifier namespace binds record/boundary/generation; same display reference never grants cross-scope access. |
| FS156-03 | Existing/missing/duplicate/quarantine/rejected/incident pre-proof cases, including guessed and stale credentials | Equal outward contract and measured timing tolerance; no classification/contact/duplicate hint, reflected canary or notification existence signal. Storage failure emits no receipt/acceptance claim. |
| FS156-04 | Issuance and candidate RNG failure, malformed lengths, proxy/error capture, browser refresh and export/restore | At least 128 bits required; proposed generator uses 32 CSPRNG bytes with no fallback. Store only verifier; canary scan covers response capture, URLs/history/referrers, persistent browser storage, logs/traces/metrics/alerts/queues/provider metadata and backups. |
| FS156-05 | Crash/fault at every rotation/recovery transaction boundary; lost response; simultaneous swaps/recovery vs revoke/delete | Exactly one previously valid credential on rollback, exactly one committed candidate after successful swap, none after committed deletion/revocation. Nonce/grant/event/marker commit or roll back together; stale sessions deny. Retry cannot create a second transition. |
| FS156-06 | Replay nonce, consumed proof grant, idempotency key alone; changed actor/key candidate/scope; missing/hostile origin or CSRF | Zero unauthorized transitions for verify/reopen/recovery/rotate/revoke/contact/consent and every mutation; legitimate lost-response retry authenticates with committed candidate, not spent proof. |
| FS156-07 | False proof, lost receipt and no return channel; non-user/conflicting claim and scoped hold | No existence disclosure before approved proof; no operator shortcut or submitter preference; restrict disputed processing, preserve permitted partial result and accessible private appeal. Block execution until #155 proof/hold policy is valid. |
| FS156-08 | Cap boundary/flood, slow input, challenge/screen/index/queue outages | Enforce every measured register cap, bounded cost/retry/storage, durable acceptance and protected incident route; accessible alternate never bypasses authorization or admits unbounded work. |
| FS156-09 | Ordinary triage/sender/publisher/admin tries incident/contact/key/backup access; consent withdrawal during publish | Deny unauthorized reads/writes; no secret/incident GitHub publication; consent digest/version and restriction rechecked at publication commit. No split-check race. |
| FS156-10 | Multipart, undeclared fields, encoded file uploads, remote URLs and malicious markup in text | Attachments rejected without durable bytes; no URL fetching/scanner/provider action or markup execution. Text remains untrusted; secret/high-risk screen runs before dedupe/triage. |
| FS156-11 | Restore stale verifier/contact/consent data; decrypt after key rotation; expired/deleted/held records | No serving before reconciliation; no credential/destination resurrection, retention extension or lost approved ciphertext. Missing keys/freshness pause access. Hold policy is neither created nor released by restore. |
| FS156-12 | Synthetic vulnerability/data-incident handoff, private route unavailable, accidental publication, false positive and appeal | Incident never enters ordinary triage/spam/public issue. Kill switch stops publication; controlled disclosure removed without reproduction under #155 runbook. Evidence remains restricted and minimized; private accessible appeal and keyboard/screen-reader operation evidenced. |

## Acceptance trace and review handoff

| #156 binary criterion | Proposed artifact coverage | Remaining evidence / decision |
| --- | --- | --- |
| Reviewed intake data flow and STRIDE/LINDDUN | V1 flow, boundary invariants and threat table; this supplement narrows transitions/custody | Required reviewers on exact version set; no reviewed claim |
| Every abuse control measurable and owned with retention/appeal/accessibility/cost/degraded rules | V1 ten-row register and parameter/oracle record above | Measured #157 inputs, #155 retention/hold policy and reviewer acceptance |
| Non-enumerating accepted/quarantine/rejected/duplicate/missing/incident behavior | Processing/acceptance proposal and FS156-03 | #155 minimal receipt/stub policy and timing/notification tests |
| Entropy, one-way verifier, no raw-secret leakage | Receipt namespace/custody and FS156-02/04 | Cryptographic/RNG and full capture/egress evidence |
| Recovery and atomic rotation | Transaction table and FS156-05/07 | Approved proof and concurrent/crash/response-loss execution |
| CSRF/replay/idempotency on every mutation | Operation binding and FS156-06 | Endpoint inventory and all-path negative results |
| Record/tenant/tenant-less isolation | Unchanged separate v1 deployments; FS156-01/02/09 | Application, database, worker and service-identity/network denials |
| Private incident routing and no ordinary/public fallback | Incident containment and FS156-09/12 | Owned private routes, reviewer handoff and failure exercise |
| `security.txt` and tested private reporting before activation | Pre-activation route specification | Accurate deployed file and synthetic route evidence after separate authorization |
| Attachments disabled | Zero accepted types/bytes and FS156-10 | Parser, transient-upload and persistent-store negative tests |
| Owner/reviewers/Cyrus verdict and residual risk | Version-bound ledger below | All entries pending; issue remains open |

For each actual review retain reviewer, role, approve/amend/reject, timestamp,
exact artifact paths and Git-blob SHA-256 hashes, release/evidence revision,
conditions, unresolved risks and resolution pointer. A review of v1 or #155 is
not a review of v1.1. Any material change invalidates affected reviews; explicitly
reconfirm unaffected reviews rather than silently carrying them forward.

| Role | Current record | Blocking input |
| --- | --- | --- |
| Tuvok | Prepared proposed v1.1; owner verdict pending | Valid privacy outputs and required challenges; authorship is not independent review |
| Jett Reno | Pending | Custody/restore and measured failure/capacity/cost record |
| Deanna Troi | Pending | Valid #155 decision, transfer/hold/proof/stub mapping; no privacy-review substitution |
| Guinan | Pending | Honest acknowledgment, accountless workflow and private accessible appeal |
| Data | Pending | All acceptance oracles and future exact-release evidence plan/results |
| Quark | Pending | Bounded work/storage/challenge/incident cost and hard-stop evidence |
| Cyrus Jamula | Not yet solicited; pending | Consolidated exact-version reviews, valid #155 dependency and explicit residual-risk disposition |

Do not request final approval while these inputs remain unresolved. Once ready,
ask Cyrus one scoped question on the hashed version/evidence set: approve, amend,
or reject the consolidated #156 policy and its enumerated residual risks.
Production implementation/activation evidence stays a separate gate; a policy
decision cannot be represented as passing runtime tests or launch approval.

The existing v1 status-artifact hash and shared plan/ADR/catalog records are
unchanged. This supplement is not yet in that inventory. The coordinator must
arrange serialized inventory/hash reconciliation and bind both blobs into the
review/decision record before considering v1.1 consolidated; do not overwrite
concurrent privacy/platform packages or accepted/pending plan hashes.
