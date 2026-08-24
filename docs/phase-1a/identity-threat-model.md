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
| HTTP, hostile host, RP, origin, or cross-origin ceremony | Forwarded headers accepted only from exact configured proxy IPs, symmetry and one-hop limit, forwarded-host allowlist, effective HTTPS request/origin allowlist, configured RP domain, built-in WebAuthn verification, exact host/port comparison |
| Forwarded client-IP spoofing or shared recovery bucket | Untrusted headers are ignored; trusted single-hop `X-Forwarded-For` restores the client address before the per-client limiter; a separate bounded global recovery limiter resists distributed abuse |
| Stolen/replayed bootstrap token | Read-only 256-bit token file, verification before ceremony and completion, PostgreSQL serializable transaction plus advisory lock and singleton row |
| Concurrent bootstrap | Transaction lock, empty tenant/owner checks inside the transaction, unique state, rollback on any Identity/credential collision |
| Attestation-state substitution/replay | .NET `SignInManager` Data Protection cookie, user-entity binding, single-use temporary state |
| Bootstrap user-handle mismatch or client substitution | The reserved credential-user GUID is placed in creation options and in a separate short-lived Data-Protection ticket that also binds verified token, tenant/display names, and challenge. Completion accepts no client user ID/token, requires exact ticket/user-entity/challenge matches, and creates that same Identity ID. .NET 10 assertion resolves `response.userHandle` through `UserManager.FindByIdAsync` |
| Credential collision or database exhaustion | Global credential lookup, unique passkey storage, bounded passkeys and names |
| Concurrent registration exceeds limit or concurrent revoke removes last path | Per-user PostgreSQL transaction advisory lock serializes the authoritative re-read, policy check, and mutation; concurrent integration tests preserve the configured maximum and one remaining path |
| Open redirect or CSRF | Local-only ReturnUrl, SameSite cookies, antiforgery header/form token on every mutation |
| Stolen long-lived application cookie changes credentials | Registration and revocation require a separate short-lived Data-Protection marker issued only by successful passkey assertion, bound to user/security stamp/audience and a PostgreSQL-backed one-time nonce hash atomically consumed on mutation |
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

## Pinned user-handle evidence

The fix is based on the shipped ASP.NET Core 10.0.9 source, not an assumption:

- [`PasskeyHandler` places `PasskeyUserEntity.Id` in the credential user
  handle](https://github.com/dotnet/aspnetcore/blob/v10.0.9/src/Identity/Core/src/PasskeyHandler.cs#L58).
- During discoverable assertion it
  [resolves `response.userHandle` with
  `UserManager.FindByIdAsync`](https://github.com/dotnet/aspnetcore/blob/v10.0.9/src/Identity/Core/src/PasskeyHandler.cs#L446-L466).
- `SignInManager` [stores and retrieves attestation state in the protected
  two-factor cookie and signs it out when
  read](https://github.com/dotnet/aspnetcore/blob/v10.0.9/src/Identity/Core/src/SignInManager.cs#L696-L728).

`PasskeyUserHandleTests` generates a valid ES256 assertion against the built-in
handler: the reserved/stored ID succeeds and a different user handle fails. The
PostgreSQL bootstrap test separately proves that the reserved ID is the credential
user ID committed by the transaction. `BootstrapCeremonyEndpointTests` drives the
actual bootstrap, sign-out, and discoverable sign-in endpoints and rejects
tampered, expired, replayed, user-mismatched, and challenge-mismatched protected
tickets before persistence.

All JSON account mutations use the single API contract header `X-CSRF-TOKEN`.
Production-DI `WebApplicationFactory` tests use tokens emitted by the real
antiforgery registration and cover accepted plus missing/wrong-token bootstrap,
sign-in, recovery, registration, and revoke requests. Recent-auth endpoint tests
reject absent, tampered, expired, wrong-user, wrong-audience, and stale-security-
stamp markers. The external identity script uses one idempotent delegated listener
set and reruns only page initialization after Blazor `enhancedload`.
