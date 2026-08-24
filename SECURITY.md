# Security Policy

## Supported versions

Andreja is pre-release software. No public version currently carries a production
security-support commitment. Security fixes are applied to the private `main` branch
and reviewed release candidates; this statement must be replaced before any public
supported release.

## Reporting a vulnerability

Use the repository's private vulnerability-reporting surface when it is available.
Do not put a vulnerability, credential, prompt, task content, provider response,
personal data, or private diagnostic in a public issue or pull request. If private
reporting is unavailable, contact the repository owner through an already approved
private channel and provide only the minimum reproduction data.

## Assistant provider boundary

The deterministic assistant is the offline default. OpenAI-compatible BYOK is an
explicit operator choice with an exact endpoint allowlist, normal TLS validation,
loopback-only HTTP, no redirects, a read-only file-backed credential, timeout and
response limits, bounded retries, typed-tool validation, and a default-zero external
usage envelope. Credentials and provider bodies must not enter logs, telemetry,
exports, configuration values, skill inputs, or support artifacts. See the
[Phase 1A security/privacy contract](docs/phase-1a/byok-security-privacy.md).
