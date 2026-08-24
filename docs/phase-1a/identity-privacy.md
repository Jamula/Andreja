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

Identity rows and Data Protection keys follow the operator's encrypted backup,
restore, retention, and deletion controls. Ordinary application export excludes
both. Deleting an account must delete its credential and recovery rows under the
reviewed identity deletion flow while retaining only the minimum separately
approved audit continuity. This slice adds no sharing, model, advertising,
sponsorship, or external-provider use.
