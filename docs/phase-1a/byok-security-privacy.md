# Phase 1A BYOK security and privacy contract

The OpenAI-compatible adapter is optional. The deterministic provider remains the
offline/default CI path. Enabling BYOK is an operator decision to disclose the
current Open Loops request and typed tool schema to exactly one selected provider and
model; Andreja is not a funded proxy and provides no Copilot entitlement or cloud
account.

## Trust and data flow

- Configuration contains only endpoint, model, a non-secret `credential://` handle,
  an absolute secret-file path, human-readable provider/retention disclosures, and
  numeric limits. The credential value is never accepted from configuration or an
  environment value.
- The approved file store opens only the exact path mapped to the handle, requires
  owner-read-only mode (`0400`) on Unix or the read-only attribute on Windows, caps
  the value at 4096 UTF-8 bytes, rereads it for rotation, and treats missing/empty as
  revoked.
- The authorization value is attached only as the outbound Bearer header. It is not
  copied into the JSON request, prompt, tool schema, skill invocation, response,
  error, metric, trace, audit, export, or support evidence.
- The provider receives the user-entered request and only tools already intersected
  with the session allowlist. Provider output is untrusted. Unknown names, duplicate
  or extra fields, wrong JSON types, omitted required fields, and malformed argument
  JSON fail before `ISkillHost`.
- A valid tool call reaches `open-loops.propose-task`, which returns a proposal. The
  provider cannot confirm the proposal or mutate a task.

## Network and availability controls

The base URI must be on the exact operator allowlist and contain no user information,
query, or fragment. Plain HTTP is accepted only for a URI the platform identifies as
loopback. HTTPS uses the platform's normal trust store, certificate chain, expiry, and
hostname validation; no custom certificate acceptance callback is
installed. The transport appends the fixed `/chat/completions` path, disables
automatic redirects and decompression, and rejects every 3xx response.

One linked cancellation scope covers caller cancellation, session kill, and the
configured overall timeout. Only 408, 429, 5xx, network, and response-read failures
are retryable, with exponential delay, a maximum of five configured retries, and a
five-second delay ceiling. Responses are streamed into a bounded buffer (1 KiB to
16 MiB configuration range). Error bodies are not parsed, returned, or logged.

## Privacy, retention, metrics, and cost

The authenticated provider surface shows the selected provider/model plus the
operator-authored disclosure of who receives content and the reviewed retention
terms. Andreja cannot verify or override a third-party provider's retention; the
operator must keep those statements accurate and disable the profile if terms
change.

Metrics use fixed instrument names and bounded result classes. They contain duration,
retry events, and provider-reported input/output units only; the typed usage response
also contains retry and tool counts. Prompt, response, task title/details, tool
arguments, credential/handle/path, authorization header, provider error body, and raw
tenant/user/principal identifiers are forbidden.

Non-loopback calls are stopped before credential resolution or network I/O while the
approved total-unit envelope is zero. A positive envelope must correspond to a
separately recorded numeric human approval. The current counter is process-local and
reserves configured maximum input plus output units per attempt; it is a conservative
stop, not durable billing or invoice reconciliation. Live paid evidence therefore
remains blocked.

## Rotation, revocation, and incident response

Rotate by atomically replacing the exact credential file with a newly issued value
and the same restrictive permissions, then revoke the old value at the provider.
The next attempt reads the new file. Revoke Andreja access immediately by deleting or
emptying the file; the next request fails before network I/O. If exposure is
suspected, pause the provider, revoke it at the provider, preserve only content-free
timestamps/result classes for investigation, rotate the file, and verify the old
value no longer authenticates. Never paste either value or provider content into an
issue, PR, log, or test artifact.
