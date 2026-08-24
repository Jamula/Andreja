# Phase 1A local identity privacy contract

Local identity processes the minimum account data needed for one self-hosted owner:
display name, internal IDs, public passkey credential data, recovery-code hashes,
cookie/security-stamp state, and content-free operation/outcome/time audit facts.
It does not require email, phone, password, biometric data, authenticator private
keys, cloud identity, or attestation-based profiling.

The browser and authenticator handle biometric/PIN verification; Andreja receives
only the WebAuthn response required for public-key verification. Recovery plaintext
is returned once to the owner and is never stored. Bootstrap and recovery secrets,
cookies, credential responses, Data Protection keys, and raw user identifiers are
forbidden in logs, telemetry, application exports, errors, GitHub, or support
artifacts.

The effective client IP is used only as an in-memory recovery-rate-limit partition
after trusted-proxy validation. It is not placed in the identity audit table,
telemetry, export, or application logs. An unavailable or untrusted forwarded
address falls back to the immediate connection address, which may reduce
availability by sharing a bucket but cannot create attacker-selected buckets.

Credential management uses a short-lived protected browser marker containing only
the credential-user ID, security stamp, and fixed audience. It is HttpOnly, Secure,
SameSite=Strict, omitted from logs/telemetry/exports, invalidated by security-stamp
rotation, and consumed when a passkey is added or removed.

Identity rows and Data Protection keys follow the operator's encrypted backup,
restore, retention, and deletion controls. Ordinary application export excludes
both. Deleting an account must delete its credential and recovery rows under the
reviewed identity deletion flow while retaining only the minimum separately
approved audit continuity. This slice adds no sharing, model, advertising,
sponsorship, or external-provider use.
