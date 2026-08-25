# Review-gate external GitHub App worker

Issue [#104](https://github.com/Jamula/Andreja/issues/104) owns this
design package. The repository contains a worker contract, policy library,
stateful tests, and read-only ruleset planning. It does **not** contain or
operate the publisher.

## Operational status: BLOCKED

Do not add `Andreja review policy` to the ruleset. Keep affected PRs draft or
under a documented manual merge hold, keep auto-merge disabled, and do not
represent this package as an operational gate.

At this revision:

- active ruleset `21199927` is unchanged;
- no bypass actor or approval-count reduction is authorized;
- required conversation resolution remains enabled;
- no independently hosted review-gate worker or dedicated checks-writer App
  installation has been provisioned;
- no least-privilege installation attestation, durable worker store, startup
  evidence, dropped-delivery evidence, or real merge-group canary exists; and
- Phase 0 authorizes no remote account, subscription, free-tier, trial, or
  worker provisioning.

The exact next human prerequisite is explicit authorization and provisioning
of a dedicated GitHub App plus an independently protected webhook worker or
credential broker outside repository GitHub Actions. That future deployment
must return the identity, permissions, revision, provenance, durable-state,
negative-canary, and rollback evidence listed below before a separate reviewed
change may enable rollout.

## Repository Actions boundary

There is no repository workflow that stores, mints, receives, or uses the
publisher App private key or installation token. The previous publisher and
admin workflows were deleted.

`.github/workflows/review-gate-app-tests.yml` executes only deterministic
contract tests with read-only repository permission. It cannot publish a check,
write policy metadata, administer the App, or mutate protection.

`pull_request_review_thread` is a GitHub App webhook event, but it is not a
supported GitHub Actions trigger. No Actions workflow relies on it. Native
required-conversation-resolution remains the immediate thread-reopening block;
late resolution recovery belongs to the external webhook worker, trusted
manual dispatch, and periodic reconciliation.

## Required independently hosted boundary

The future publisher must be a dedicated GitHub App installed only on
`Jamula/Andreja`. Its credential must be unavailable to all repository Actions,
including revisions selected from a pull request or merge group.

Required repository permissions:

| Permission | Access | Purpose |
| --- | --- | --- |
| Checks | Read/write | Publish pending generations and the exact required check |
| Issues | Read/write | Read policy labels and append authenticated ledger events |
| Pull requests | Read | Read exact PR, review, and base identity |
| Administration | Read | Revalidate reviewer and policy-admin authorization |
| Metadata | Read | Bind repository and installation identity |
| Contents | Read | Identify the reviewed contract revision only |

Do not grant contents write, actions write, workflows write, secrets access, or
broad organization/repository installation. The service must authenticate
webhook signatures before producing the normalized event envelope consumed by
`.github/scripts/review-gate-app.js`.

The envelope rejects GitHub Actions hosting and binds:

- exact repository ID/name;
- GitHub delivery ID and independently assigned worker run ID;
- event path and authenticated ingress source;
- immutable worker revision and worker instance;
- PR or merge-group association;
- head SHA and base repository/ref/SHA; and
- monotonically reserved durable generation.

The check `external_id` is the versioned digest of that complete provenance.
Matching a check name or prefix is insufficient.

## Durable mappings and pending-first processing

The worker contract requires durable mappings for:

- base repository/ref to every open PR and current head;
- bound issue to PR;
- reviewer identity to PRs whose evidence depends on that reviewer;
- merge group to exact constituent PRs; and
- repository/head to the newest monotonic generation.

For every event that can invalidate a success, the worker identifies targets
from durable state and publishes a new `in_progress` check on **all** affected
heads before reading mutable GitHub policy metadata. A terminal writer may
update only while both the durable generation and exact-App check-run are still
newest. API failures, 403/429 responses, mapping disagreement, stale writers,
or a changed second snapshot fail closed with sanitized reasons.

The durable store claims each authenticated delivery ID before dispatch.
Redelivery returns the existing claim and cannot start a second writer. A
failed or interrupted claim remains fail closed and is recovered through full
reconciliation rather than by replaying an ambiguous partial writer.

This contract does not prove the unavoidable deployment interval between
delivery and successful check creation. Activation remains blocked until the
independent deployment demonstrates that startup, credential, queue, restart,
and dropped-delivery behavior cannot expose an older success.

## Explicit event handlers

| Event path | Required behavior |
| --- | --- |
| `pull_request` | Update durable PR/base/head mapping, publish pending, observe policy, and evaluate every open PR sharing the head |
| `pull_request_review` | Publish pending, authenticate current Copilot/native review, append exact-diff attestation, and reevaluate |
| `pull_request_review_comment` | Publish pending and reevaluate current conversation/review state |
| `pull_request_review_thread` | External App webhook only; publish pending and reevaluate, with native thread resolution still required |
| `issue_comment` | Publish pending before restoring any deleted exact-App policy record and reevaluating |
| `issues` | Resolve affected PRs only from durable issue mappings, publish every head pending, then reevaluate |
| `push` | Resolve every affected open PR from durable base mappings, publish each head pending, then reevaluate against the new base |
| `member`, `membership`, `organization` | Resolve dependent reviewer mappings (or conservatively all open PRs), publish pending, and recheck live permission |
| `reconciliation` | Periodically publish all known heads pending, discover missed open PRs, and perform a full permission/policy/thread reconciliation |
| `merge_group` | Publish the merge-group head pending and revalidate every exact constituent PR against the current base |
| `specialist_attestation` | Publish pending before downloading and validating an allowlisted exact-run evidence artifact |
| `trusted_dispatch` | Require an authenticated human with current maintain/admin permission; never run through repository Actions |

### Base advances

A default/base push is not merely a successful default-branch check. The
worker must first enumerate every open PR mapped to the changed base, publish a
new pending generation on each distinct PR head, and then reevaluate current
base identity and evidence. Only afterward may it publish a separately bound
default/base `not_applicable` result. That result can never count as
merge-group evidence.

### Merge groups

A merge-group check is never unconditional. The worker must:

1. resolve the exact group and constituent PR numbers from durable state;
2. publish pending on the actual merge-group head;
3. resolve the constituents again from live GitHub state and require equality;
4. verify the event base SHA is still the live base-ref tip;
5. reevaluate every constituent's draft, policy, Copilot, independent evidence,
   reviewer permission, and full thread state against that current base; and
6. require a second identical full snapshot before success.

An empty group, changed base, mapping disagreement, incomplete constituent, or
API failure is rejected.

## Authenticated monotonic policy

Author-controlled PR bodies, closing references, labels, and label removal
never reduce requirements.

Each authenticated `bind-issue` or `require-domain` observation has a unique
epoch containing its exact PR/head/base identity, event path, delivery ID, and
worker revision. The event ID covers the entire event rather than a
deterministic source key.

For each source, the newest observation supersedes its historical observation
without deleting history. An independently authorized reduction must contain:

- current exact PR/head/base identity;
- the historical policy digest immediately before reduction;
- exact current observation event ID and epoch ID pairs;
- current maintain/admin human authorization;
- a bounded reason; and
- a durable repository-local audit URL.

The fold validates the historical digest and epoch before applying the
reduction. A push, retarget, base advance, or later authenticated observation
creates a new epoch that no earlier reduction can suppress. Removing a label or
closing reference alone has no effect.

## Review evidence

Every ready PR requires:

1. the newest native review from exact Copilot reviewer user ID `175728472`,
   login `copilot-pull-request-reviewer[bot]`, type `Bot`, on the current head;
2. an exact-App attestation binding that review ID to exact PR/head/base;
3. zero unresolved review threads after full GraphQL pagination; and
4. independent current-policy evidence for every persisted architecture,
   security, privacy, or quality requirement.

For human evidence, only the newest candidate for a domain counts. It must be a
current `write`, `maintain`, or `admin` human distinct from the PR author, bind
the exact current diff and policy digest, and approve. A copied, stale,
dismissed, or rejected newest candidate blocks; the evaluator never falls back.

For specialist automation, the external broker must authenticate an exact
allowlisted App, run ID/attempt, immutable workflow revision, repository, head,
and successful run. It then downloads a bounded evidence manifest, verifies
its SHA-256 and exact PR/head/base/policy content, and only then appends an
App-authenticated event. The artifact body is not copied into check output or
the policy ledger. No specialist installation is currently attested.

Reviewer authorization is live state. Membership/permission events trigger
immediate invalidation where available, and periodic full reconciliation
catches unavailable or dropped events. A previously successful head becomes
pending and then rejected when the reviewer no longer has the required
permission.

## Break-glass

Break-glass is an exact-App, exact-diff, current-policy event created only after
current maintain/admin human authorization at the external trusted admin
ingress. It records reason and repository-local audit URL.

It is not a bypass actor, approval-count reduction, generic status, workflow
artifact, or permission to ignore drafts and unresolved threads. Head, base, or
policy change invalidates it.

Never include prompts, connector content, tokens, personal data, artifact
contents, or private diagnostics in policy/check/audit metadata.

## Mandatory provisioning and canary evidence

A future activation proposal must provide all of the following:

1. exact dedicated non-Actions App installation ID and independently protected
   worker identity;
2. least-privilege permission attestation;
3. immutable worker revision plus delivery/run provenance;
4. durable base/PR/issue/reviewer/merge-group/generation state evidence;
5. proof that every invalidation publishes the newest pending generation on
   every affected head before mutable reads;
6. startup, missing credential, queue failure, worker restart, stale writer,
   wrong-App, 403/429, rate-limit, dropped/redelivered webhook, and durable-store
   failure canaries;
7. reviewer permission-revocation and periodic full-reconciliation canaries;
8. push, retarget, base advance, issue-policy increase/reduction/re-observation,
   Copilot delay, newest rejection, thread reopening/resolution, and
   break-glass transitions with no merge-ready interval;
9. distinct provenance for PR and default/base push paths; and
10. a **real merge-queue `merge_group`** with exact constituent/current-base
    evidence. A default push, synthetic SHA, or copied external ID does not
    count.

Record delivery IDs, worker runs/revisions, generation/check-run IDs, App IDs,
external IDs, PR/group/base identities, expected and actual mergeability,
timestamps, latency/cost, negative results, and rollback evidence on #104.
Any merge-ready interval or ambiguous identity fails the canary.

## Read-only rollout planning

The script is intentionally incapable of apply. It performs only read-only
planning and emits the exact blocker plus a complete rollback snapshot.

```powershell
node .github\scripts\review-gate-ruleset.js plan-rollout `
  --repo Jamula/Andreja `
  --ruleset-id 21199927 `
  --app-id <future-exact-numeric-app-id>
```

If an exact App check is ever present, read-only rollback planning is:

```powershell
node .github\scripts\review-gate-ruleset.js plan-rollback `
  --repo Jamula/Andreja `
  --ruleset-id 21199927 `
  --app-id <exact-numeric-app-id>
```

Any `apply*` operation exits nonzero with
`EXTERNAL_REVIEW_GATE_WORKER_NOT_INDEPENDENTLY_PROVISIONED`. The script
contains no ruleset mutation client, HTTP write, confirmation phrase, or hidden
override. A future activation must be a separate reviewed implementation after
all prerequisites exist; this revision cannot enable it.

## Latency, cost, and selective CI

The review delay that motivated #104 was 237 seconds on #100, 489 seconds on
#101, and 267 seconds on #105 (median 267, maximum 489). The worker contract
adds no model request. Future canaries must measure webhook-to-pending latency,
reconciliation delay, API calls/rate limits, durable-store cost, and total
review latency.

Issue [#102](https://github.com/Jamula/Andreja/issues/102) may reduce unrelated
C# work for docs-only PRs. It must not path-filter, skip, synthesize, or publish
this gate. This package coordinates that boundary only and does not implement
#102.
