# Open Loops tasks

Open Loops turns an assistant request into a task only after you approve the exact
change.

## Create a task

1. Sign in to the local Andreja instance.
2. In **Ask the assistant**, enter one clear task and choose **Prepare proposal**.
3. Review the operation, source, expiry, before/after JSON, purpose, capability,
   and confirmation policy.
4. Choose **Confirm and save task** to persist it, or **Not now** to dismiss the
   proposal without saving anything.

The page shows the assistant provider and model selected by the self-host operator,
whether it is ready, and its content-exposure disclosure. The deterministic provider
is local and ready by default. Selecting `openai-compatible` is an explicit BYOK
configuration surface; it fails safely until a credential-backed transport is
configured, and credentials never enter task requests or exports.

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

Retries use idempotency receipts, so the same confirmed action does not create a
duplicate effect. A conflict means the item changed; refresh and review the latest
state.

## Errors and privacy

Provider and server details are never displayed. A cancellation changes nothing.
If a provider is temporarily unavailable, retry later. Authentication,
tenant/principal authorization, validation, proposal policy, and antiforgery checks
are enforced by the server even when the browser has already validated the form.

Task text and proposal payloads are excluded from operational telemetry. Source
references and audit events identify the operation without copying task content.
