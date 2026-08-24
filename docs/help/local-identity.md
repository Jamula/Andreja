# Local passkey identity

Andreja self-host uses local ASP.NET Core Identity passkeys. It does not require an
Andreja, GitHub, email, password, or other cloud account.

## First administrator

1. Apply the reviewed `ProductionPasskeyIdentity` migration with the explicit
   migration command in the [self-host runbook](../operations/self-hosting.md).
2. Configure one exact HTTPS allowed origin and its relying-party domain.
   When TLS terminates at the documented same-host reverse proxy, configure its
   exact Kestrel-observed IP and the required single-hop forwarded headers from the
   [self-host runbook](../operations/self-hosting.md#same-host-tls-reverse-proxy).
3. Generate a 32-byte random bootstrap token, store only its Base64 value in an
   account-readable, read-only host file, and mount it at the configured path.
4. Open `/Account/Bootstrap` at that exact HTTPS origin. Enter the token, workspace
   name, and display name, then approve the browser's passkey prompt.
5. Store every recovery code offline and confirm storage before continuing.

Bootstrap is unavailable after the first successful transaction. Reusing the token,
opening the page over HTTP, changing origin/host, using an initialized database, a
credential collision, and concurrent completion all fail closed. Delete the host
copy of the bootstrap token after successful setup; the database stores only the
single-use completion marker, never the token.

The bootstrap ceremony reserves the ASP.NET Core Identity user ID before asking the
authenticator to create a credential. That exact protected user handle is used when
the transaction creates the credential user. A different handle would make
discoverable passkey assertion fail and is rejected.

## Sign in, sign out, and passkeys

Use **Sign in with a passkey** at `/Account/Login`. A return URL is accepted only
when it is a local absolute-path reference. Use **Manage passkeys** at
`/Account/Passkeys` to add an independently stored passkey or remove one. The
configured device limit is enforced, and Andreja refuses to remove the last usable
passkey/recovery path.

Sign out through the account form. Cookies are Secure, HttpOnly, SameSite=Strict,
security-stamp validated, and protected by the separately persisted Data Protection
key ring. Back up and restore both PostgreSQL and that key ring; a database-only
restore is incomplete.

## Recovery

Open `/Account/Recovery`, enter one unused recovery code, and enroll a new passkey.
Successful recovery:

- consumes the presented code and revokes every old recovery code;
- removes existing passkeys and stores only the newly verified public credential;
- rotates to a new recovery set shown once;
- changes the security stamp so existing sessions are rejected; and
- writes a content-free security audit record.

Attempts are rate-limited. Andreja never reveals existing credentials or whether a
particular account/code exists. See the
[recovery runbook](../operations/identity-recovery.md).

Never paste a bootstrap token, recovery code, cookie, passkey response, Data
Protection key, or database secret into logs, telemetry, exports, support requests,
issues, or pull requests.
