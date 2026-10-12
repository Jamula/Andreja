# Local identity recovery runbook

This runbook is for the account owner. It does not define an operator bypass.

## Normal recovery

1. Confirm the browser is at the configured HTTPS origin. Stop if the certificate,
   host, or origin is unexpected.
2. Open `/Account/Recovery` directly. Do not send a recovery code through email,
   chat, a command argument, or a support tool.
3. Enter one offline recovery code and approve creation of a new passkey.
4. Replace the offline recovery set with the newly displayed codes and confirm
   storage. All old codes and passkeys are revoked.
5. Return to sign-in, authenticate with the new passkey, and verify expected data.
6. Verify another previously signed-in browser is rejected before considering the
   session-invalidation check complete.

Repeated or automated attempts receive the same generic response and are
rate-limited. Do not weaken the limiter or inspect credential/recovery values to
troubleshoot. Content-free audit rows may establish operation, outcome, and time.

## Restore recovery

Stop the application, restore the reviewed PostgreSQL backup and the matching full
Data Protection key history, apply only explicitly approved migrations, and start
the normal app. Verify readiness, prior cookie handling, passkey sign-in, one
controlled recovery rotation, session invalidation, tenant isolation, and audit
continuity. Never place keys or recovery codes in the restore manifest.

If every passkey and recovery code is lost, stop. Phase 1A has no password, email,
header, development, or operator break-glass bypass. Restore a verified recovery set
or wait for a separately approved custody design; do not edit credential tables.

## Proposed content-key recovery amendment (not ratified)

[ADR 0012](../adr/0012-owner-device-content-encryption.md) proposes a separate
owner-device content-key lifecycle. This paragraph is not an operating
instruction and does not change the current identity recovery procedure.
Identity passkeys, recovery codes, and Data Protection history would not recover
content keys. Only owner-controlled device keys or the owner-held recovery
package plus its separate high-entropy secret could restore content access;
operators and support would have no plaintext recovery path. Loss of all
usable owner keys and recovery materials would be permanent data loss.

Before any ratified implementation, synthetic-only drills must prove owner
recovery after ciphertext restore, denial of operator/identity-based content
recovery, device enrollment/revocation limits, failed recovery on incomplete
materials, and clear permanent-loss behavior. These tests are not currently
claimed. Existing encrypted database/Data Protection recovery and restored
passkey sign-in remain separate requirements.
