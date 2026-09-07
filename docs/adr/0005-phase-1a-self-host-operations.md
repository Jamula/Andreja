# ADR 0005: Phase 1A independent self-host operations

- **Status:** Accepted
- **Date proposed:** 2026-08-23
- **Date accepted:** 2026-09-07
- **Issues:** [#9](https://github.com/Jamula/Andreja/issues/9),
  [#66](https://github.com/Jamula/Andreja/issues/66)
- **Approver:** Cyrus Jamula
- **Governing:** [Platform plan](../plan.md#phase-1a---self-hosted-assistant-walking-skeleton)
  and [ADR 0000](0000-plan-ratification.md)
- **Non-authoritative input:** Proposed [company charter](../charter.md#commitments)
- **Proposed by:** Jett Reno
- **Decision owner:** Cyrus
- **Decision record:** [Phase 1A packet decision record](../phase-1a/packet-decision-66.md)
  under [#66](https://github.com/Jamula/Andreja/issues/66)

## Context

Self-hosting is a product boundary, not merely a development topology. Phase 1A must
start, observe, update, stop, back up, and recover without Andreja cloud.

## Decision

Publish one non-root OCI application image and a Compose contract containing:

- the authenticated application;
- PostgreSQL with a durable host-managed volume;
- a local OpenTelemetry Collector;
- an opt-in local evidence profile with a queryable Prometheus-compatible metrics
  backend.

Published Compose references immutable image digests, never `latest`. It declares
health/readiness checks, dependency order, resource guidance, named durable paths,
network boundaries, and validated configuration. The supported Docker-, Podman-, or
other Compose implementation remains a measured host-matrix decision; OCI/Compose
does not select a cloud runtime or orchestrator.

The accepted Phase 1A evidence host is Linux containers through Docker Compose v5
on the tested ARM64 environment. Podman, x64, native Windows containers, and other
Compose implementations remain unclaimed until separately exercised. This is an
evidence-host boundary, not production support or a cloud-runtime selection.

The offline-start proof begins **after image acquisition**. It uses either an image
already preloaded into the host content store, an image built locally from the
checked-out source, or a digest-pinned image served by an operator-controlled local
registry. The proof then removes internet access and starts/restarts the bundle.
“Without GitHub” means startup, operation, restart, backup, and restore do not call
GitHub after the source/image/release metadata has been acquired; it does not claim
first acquisition can occur without a distribution source.

```mermaid
flowchart LR
  U[User browser] -->|HTTPS| A[Andreja OCI app]
  A --> P[(PostgreSQL)]
  A -->|OTLP, content suppressed| O[Local OTel Collector]
  O --> M[Optional local evidence backend]
  A -. disabled by default .-> C[Optional future Andreja control plane]
```

### Keys, secrets, and configuration

- Strongly typed configuration is validated before readiness.
- TLS, Data Protection, envelope-encryption, and operator-recovery key material live
  outside the image and database in least-privilege host mounts.
- Historical keys required for cookies, identity, or encrypted data are retained
  according to a documented rotation inventory.
- BYOK/provider credentials are encrypted at rest and excluded from application
  exports and telemetry.
- Local development certificates are never accepted as a production default.

### Backup, update, and recovery

The operator runbook performs a consistent database logical dump, snapshots the
required key/config inventory, records image/config/schema versions and checksums,
encrypts the recovery set, and verifies it by restoring into a clean instance.
Portable application export is exercised separately.

Updates are pull-by-digest, inspect release/migration notes, back up, run the explicit
migration artifact, start the new revision, verify readiness and sign-in, then
retire the old revision. Rollback uses the prior image only when schema-compatible;
otherwise it restores the pre-update recovery set.

### Independent and content-safe operation

Normal identity, assistant BYOK, skills, tasks, audit, export, backup, and restore
have no Andreja-cloud dependency. Application configuration restricts assistant
traffic to the user-configured endpoint, but the Compose network does not establish
network-level default-deny egress. The offline fake-provider smoke test proves the
reviewed path makes no hidden calls; it is not a firewall claim. OTel uses an
allowlist of low-cardinality operational attributes and rejects task text, prompts,
responses, tokens, raw user identifiers, and connector content.

## Local/paper Phase 0 evidence

This ADR is documentation only. Phase 0 may inspect OCI/Compose/PostgreSQL/OTel
documentation and existing local capabilities, but creates no cloud account,
subscription, free tier, trial, or provisioned resource. No runtime, tool, provider,
or package is installed by this decision.

## Alternatives considered

- **Build or pull from GitHub on every start:** rejected because distribution
  availability would become a runtime dependency and make offline evidence invalid.
- **Use mutable tags:** rejected because update, rollback, and evidence could not bind
  to one artifact.
- **Require Kubernetes or a managed control plane:** rejected because one-user Phase
  1A has no measured need and must remain independently operable.

## Accepted operating boundary

The exact HTTPS origin/RP domain and trusted proxy are installation inputs that
must pass startup validation. Keys and encrypted backups remain in
operator-controlled, access-restricted destinations. Accepted ADR 0010 governs
hosted signing; the local Prometheus-compatible backend is evidence-only. Numeric
RPO/RTO, a production host matrix, combined encrypted recovery, and a separately
approved signed update/rollback pair remain exit gates.

## Cost delta

The self-host bundle requires at least 2 CPU cores, 3 GiB free memory, durable
storage, backup capacity, scanner/build time, and operator labor for TLS, keys,
updates, and recovery. One OCI app, PostgreSQL, and optional local evidence backend
avoid managed-cloud commitments. No cloud runtime, subscription, paid observability,
registry, backup, or support service is selected or authorized.
