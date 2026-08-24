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

## Current production sign-in limitation

Production passkey bootstrap, sign-in, and recovery remain
[P0 issue #55](https://github.com/Jamula/Andreja/issues/55).
The production login route exists so browser challenges have a safe destination, but
it intentionally offers no bypass and states that production sign-in is unavailable.
Do not treat the self-host task flow as production-usable until that blocker's
passkey, recovery, PostgreSQL, restart, and browser evidence is complete.
