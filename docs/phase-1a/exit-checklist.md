# Phase 1A decision and exit checklist

- **Status:** Proposed
- **Date:** 2026-08-23
- **Issue:** [#9](https://github.com/Jamula/Andreja/issues/9)

## Packet approval

- [ ] Cyrus accepts or amends ADRs 0001–0005.
- [ ] Spock challenges architecture boundaries, portability, alternatives, and
      federation coherence.
- [ ] Tuvok challenges threat and identity controls.
- [ ] Deanna Troi challenges privacy, retention, model exposure, export, and delete.
- [ ] Data challenges test topology, production-impossible auth, SLO queries, and
      evidence reproducibility.
- [ ] Quark supplies and Cyrus approves numeric Phase 1A model-spend limits.
- [ ] Jett Reno challenges OCI/runtime, channel seam, offline-start, telemetry,
      backup/update/recovery, and operational support proof.
- [ ] Seven of Nine challenges assistant/skill contracts and the
      grant/consent/disclosure/share-audit/peer-envelope conformance boundary.
- [ ] Rai challenges AI safety, human confirmation, harmful misuse, fairness, and
      the packet-level impact assessment.
- [ ] Sarek challenges legal/regulatory, consent/audit, external-provider terms,
      licensing/IP, and claims; qualified counsel remains authoritative where needed.
- [ ] Residual risks, owners, expiry/re-review triggers, and every deferred human
      decision are recorded.
- [ ] No cloud runtime, managed DB, CIAM, graph DB, account, subscription, free
      tier, trial, provisioned resource, or paid live call was selected or created.

## Implementation evidence required for Phase 1A exit

Every item records a reproducible proof link in the canonical artifact indexed by
[the evidence gates](evidence-gates.md), including build/config/schema versions,
owner, command/test ID, result, and known exclusions.

- [ ] After image/source acquisition, a clean host with networking disabled starts
      and restarts the pinned Compose bundle from a preloaded image, a locally built
      image, or an operator-controlled local registry. No startup/runtime call reaches
      GitHub or Andreja cloud; acquisition provenance and digest are recorded.
- [ ] Passkey first-admin bootstrap works once; collision, replay, origin, rate-limit,
      recovery-code, and session-invalidation cases pass. Second-passkey enrollment
      is tested only when configured; break-glass is tested only if Cyrus approves
      and the optional mechanism is implemented.
- [ ] Two-tenant tests prove read/write/inference/enumeration isolation and database
      composite constraints reject cross-tenant references.
- [ ] The responsive typed API/Blazor slice completes
      `assistant -> task proposal -> confirm -> persist -> list -> complete ->
      export -> delete`.
- [ ] The fake provider gates CI; approved BYOK smoke covers timeout, cancellation,
      malformed tool call, provider error, revocation, and budget stop.
- [ ] Skill/channel hosts reject undeclared capabilities, tampered manifests,
      wrong-tenant context, expired proposals, and ambient secret/data access.
- [ ] In-memory conformance vectors prove `Grant`, bilateral `ConsentRecord`,
      append-only/content-minimized `ShareAuditEntry`, ordered disclosure reduction,
      and `IPeerChannel` signed envelopes. Tamper, unknown version/key/algorithm,
      wrong audience/grant/purpose, expiry/future time, replay, and conflicting
      idempotency fail closed; valid retry has no duplicate effect. The proof also
      shows no inactive persistence migration and no live federation traffic.
- [ ] Empty/prior-schema migrations pass; startup cannot migrate implicitly.
- [ ] Encrypted PostgreSQL logical restore plus identity/key recovery succeeds in a
      clean instance.
- [ ] Portable Andreja export/import succeeds separately and proves all declared
      exclusions and reauthorization requirements.
- [ ] Restart and approved update/rollback paths preserve data, identity,
      configuration, audit, and idempotency.
- [ ] Local OTel evidence queries run with nonzero sample counts and canary tests
      prove no task, prompt, response, token, recovery material, or raw user ID leaks.
- [ ] Production publish/image contains no fake-auth code or activation path.
- [ ] Phone, tablet, and desktop viewport, keyboard/accessibility, reconnect, and
      provider-failure experiences pass.
- [ ] Threat/privacy/cost evidence is current and internal SLO/RPO/RTO targets are
      approved; no high/critical residual risk lacks explicit acceptance.

## Stop, de-scope, and exit rules

Stop and open a decision issue when an approved spike cycle cannot prove an exit
condition, a trust-boundary assumption changes, spend is unknown/outside its
envelope, or isolation/recovery/content-suppression fails.

De-scope in this order:

1. optional GitHub Copilot or second provider;
2. inactive channel/federation contract persistence beyond conformance fixtures
   (the required contracts and in-memory signed-envelope tests remain);
3. noncritical UI polish;
4. optional local evidence-backend convenience.

Do **not** cut the independent self-host path, tenant/database isolation,
assistant-provider seam, typed API boundary, user-confirmed task slice,
production-impossible auth, telemetry redaction, key-aware backup/restore,
application export/import, or minimum end-to-end evidence.

Phase 1A may exit only when all implementation evidence is reproducible, every
blocking gap is closed, and Cyrus explicitly chooses proceed, extend learning,
de-scope, or stop. Phase 1B is not implied by Phase 1A completion.
