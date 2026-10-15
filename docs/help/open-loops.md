# Open Loops tasks

Open Loops turns an assistant request into a task only after you approve the exact
change.

## Create a task

1. Sign in to the local Andreja instance. In Development, use the clearly marked
   **Sign in to the development workspace** action. It is a fixed in-memory identity,
   exists only in Debug builds running in Development, is unavailable in Production,
   and is not passkey or launch evidence.
2. In **Ask the assistant**, enter one clear task and choose **Prepare proposal**.
3. Review the operation, source, expiry, before/after JSON, purpose, capability,
   and confirmation policy.
4. Choose **Confirm and save task** to persist it, or **Not now** to dismiss the
   proposal without saving anything.

The page shows the assistant provider and model selected by the self-host operator,
whether it is ready, who receives submitted content, and the operator-recorded
retention disclosure. The deterministic provider is local, offline, and ready by
default. A configured `openai-compatible` profile sends only the current request and
the exact typed tool schema to its one allowlisted endpoint. It does not send the
credential in the JSON body or expose it to the skill.

The assistant is allowed to call only the versioned
`open-loops.propose-task` tool. Preparing or dismissing a proposal does not create
a task. An expired proposal must be prepared again.

## Manage and own your data

- **Mark complete** changes an open task to completed.
- **Export JSON** prepares a local `andreja.open-loops.tasks.v1` export containing
  your visible tasks and source state. Credentials, passkeys, recovery secrets,
  provider tokens, Data Protection keys, and caches are excluded.
- **Delete** first shows a second, explicit **Yes, delete** control. **Cancel**
  leaves the task unchanged.

Confirmation retries reuse one idempotency key for the same retained proposal, so
retrying that confirmation replays the committed result rather than creating a
duplicate task. A new proposal is a new intent with a new key. A conflict means
the item changed; refresh and review the latest state.

## Errors and privacy

Provider and server details are never displayed. **Not now** before confirmation
dismisses an unconfirmed proposal without saving it, and **Cancel** on the delete
prompt leaves the task unchanged. A cancelled or lost response after submitting a
mutation is different: the server may already have committed it. During
confirmation, the page retains the proposal and says the result is unknown;
retry that **same confirmation** safely. If you have left the page and lost the
proposal, inspect current tasks before preparing a new one. Completion and
deletion failures also require checking current state, not assuming rollback.
If a provider is temporarily unavailable, retry later. Authentication,
tenant/principal authorization, validation, proposal policy, and antiforgery checks
are enforced by the server even when the browser has already validated the form.

Provider output is untrusted. Andreja rejects unknown tools, missing or incorrectly
typed tool arguments, malformed JSON, redirects, oversized responses, and usage above
the configured limits. Timeout, cancellation, revocation, exhausted budget, and
provider rejection during proposal preparation do not confirm or save a task.
This is not a rollback guarantee for a confirmation already sent to the server.
Rotating the file-backed credential affects the next call. An empty secured file
revokes use; a missing, inaccessible,
writable, oversized, or invalid-UTF-8 file reports only `credential-unavailable`.
Neither condition places the value, path, or store exception in the response.

Task text and proposal payloads are excluded from operational telemetry. Source
references and audit events identify the operation without copying task content.
Assistant metrics contain only bounded result class, duration, retry events, and
provider-reported input/output units; the typed usage response also reports retry and
tool counts. Neither contains prompt, response, tool arguments, credential,
authorization header, or raw user identifier.

The canonical [privacy baseline](../privacy.md) and
[threat model](../threat-model.md) describe model exposure, task sensitivity,
confirmation abuse, retention/export limits, evidence gaps, and stop conditions.

## Local passkey implementation and readiness

Local passkey bootstrap, sign-in, passkey management and recovery are implemented.
Use **Sign in with a passkey** at the operator-configured exact HTTPS origin; see
[local identity](local-identity.md) for configuration and recovery instructions.
The earlier statement that the production login page intentionally offers no
sign-in is obsolete.

Implemented routes do not establish production or public availability. Do not
treat the self-host task flow as production-ready until version-scoped passkey,
recovery, PostgreSQL, restore/restart and browser evidence passes the applicable
Phase 1A gates. The Debug/Development workspace remains a separate test identity,
not a production bypass or evidence that those gates passed.
