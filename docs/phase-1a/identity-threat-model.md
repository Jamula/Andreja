# Phase 1A local identity threat model

## Assets and boundaries

Assets are the bootstrap token, passkey private/public material, recovery codes,
Identity cookie, security stamp, PostgreSQL identity rows, and Data Protection key
history. Trust boundaries are browser/authenticator, HTTPS termination, ASP.NET Core
Identity, the application process, PostgreSQL, and host-mounted secret/key storage.

The application stores only public passkey data, salted recovery verification
hashes plus high-entropy lookup hashes, content-free audit facts, and a singleton
bootstrap-consumed row. Secrets and credential payloads are excluded from
application logging, telemetry, exports, errors, and support evidence.

## Abuse cases and controls

| Abuse case | Control and negative evidence |
| --- | --- |
| HTTP, hostile host, RP, origin, or cross-origin ceremony | Exact HTTPS request/origin allowlist, configured RP domain, built-in WebAuthn challenge/origin verification, strict host comparison |
| Stolen/replayed bootstrap token | Read-only 256-bit token file, verification before ceremony and completion, PostgreSQL serializable transaction plus advisory lock and singleton row |
| Concurrent bootstrap | Transaction lock, empty tenant/owner checks inside the transaction, unique state, rollback on any Identity/credential collision |
| Attestation-state substitution/replay | .NET `SignInManager` Data Protection cookie, user-entity binding, single-use temporary state |
| Credential collision or database exhaustion | Global credential lookup, unique passkey storage, bounded passkeys and names |
| Open redirect or CSRF | Local-only ReturnUrl, SameSite cookies, antiforgery header/form token on every mutation |
| Recovery guessing or replay | High-entropy single-use codes, PBKDF2 verification, lookup hash, expiry, fixed-window request limiter, generic responses, audit |
| Stolen session after recovery | Security-stamp rotation, zero validation interval, old passkey removal |
| Last authentication path removal | Server-side count of passkeys and unexpired unused recovery codes before revoke |
| Release-only bypass | Development helper is inside `#if DEBUG` and requires Development; no header/password/test scheme exists |

## Residual risk and stop conditions

The built-in passkey implementation does not claim authenticator hardware
attestation or MFA. A compromised browser/host, lost recovery set, untrusted TLS
proxy, missing Data Protection history, or unavailable PostgreSQL recovery set
remains a stop condition. Real browser ceremony and disposable PostgreSQL runtime
evidence are required before Phase 1A exit; compiled/WAF evidence cannot replace
them.
