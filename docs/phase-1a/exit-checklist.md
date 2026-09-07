# Phase 1A decision and exit checklist

- **Status:** Accepted as the bounded Phase 1A architecture baseline under #66;
  Phase 1A exit is not claimed
- **Date accepted:** 2026-09-07
- **Approver:** Cyrus Jamula
- **Issues:** Original packet [#9](https://github.com/Jamula/Andreja/issues/9);
  provisional amendment [#62](https://github.com/Jamula/Andreja/issues/62);
  final architecture decision [#66](https://github.com/Jamula/Andreja/issues/66)

Checked items below are supported by evidence already merged at the verified
main base. A checked implementation row is local evidence only, not release
authorization or Phase 1A exit. Partially evidenced rows
remain unchecked and name what is still missing.

## Packet approval

The [packet decision record](packet-decision-66.md) reconciles the rows below
and records each specialist's findings and re-review triggers.

- [x] Cyrus accepts the amended ADRs 0001–0005 as the bounded Phase 1A
      architecture baseline.
- [x] Spock challenges architecture boundaries, portability, alternatives, and
      federation coherence.
- [x] Tuvok challenges threat and identity controls.
- [x] Deanna Troi challenges privacy, retention, model exposure, export, and delete.
- [x] Data challenges test topology, production-impossible auth, SLO queries, and
      evidence reproducibility.
- [x] Quark challenges cost, accounting, operator labor, and sustainability.
- [ ] Quark supplies and Cyrus approves numeric Phase 1A model-spend limits.
- [x] Jett Reno challenges OCI/runtime, channel seam, offline-start, telemetry,
      backup/update/recovery, and operational support proof.
- [x] Seven of Nine challenges assistant/skill contracts and the
      grant/consent/disclosure/share-audit/peer-envelope conformance boundary.
- [x] Rai challenges AI safety, human confirmation, harmful misuse, fairness, and
      the packet-level impact assessment.
- [x] Sarek challenges legal/regulatory, consent/audit, external-provider terms,
      licensing/IP, and claims; qualified counsel remains authoritative where needed.
- [x] Residual risks, owners, expiry/re-review triggers, and deferred exit
      decisions are recorded in [`packet-decision-66.md`](packet-decision-66.md).
- [x] No cloud runtime, managed DB, CIAM, graph DB, account, subscription, free
      tier, trial, provisioned resource, or paid live call was selected or created.

## Implementation evidence required for Phase 1A exit

Every item records a reproducible proof link in the canonical artifact indexed by
[the evidence gates](evidence-gates.md), including build/config/schema versions,
owner, command/test ID, result, and known exclusions.

- [x] After image/source acquisition, a clean host with networking disabled starts
      and restarts the pinned Compose bundle from a preloaded image, a locally built
      image, or an operator-controlled local registry. No startup/runtime call reaches
      GitHub or Andreja cloud; acquisition provenance and digest are recorded.
- [ ] A reviewed hosted version-tag run produces accepted ADR 0010's keyless
      Sigstore bundle and retained root copy, exact-matches issuer, repository,
      workflow identity/revision, trigger, and tag ref, then verifies with networking
      blocked against an independently authenticated root outside the evidence.
      The implementation and historical unsigned bundle do not themselves
      authorize release, update, or startup; local operator-key evidence cannot
      substitute for the hosted run.
- [x] Passkey first-admin bootstrap works once; collision, replay, origin, rate-limit,
      recovery-code, and session-invalidation cases pass. Second-passkey enrollment
      is tested only when configured; break-glass is tested only if Cyrus approves
      and the optional mechanism is implemented.
- [x] Two-tenant tests prove read/write/inference/enumeration isolation and database
      composite constraints reject cross-tenant references.
- [x] The responsive typed API/Blazor slice completes
      `assistant -> task proposal -> confirm -> persist -> list -> complete ->
      export -> delete`.
- [x] The fake provider gates CI; local OpenAI-compatible BYOK conformance covers
      timeout, cancellation, malformed tool call, provider error, revocation, and
      budget stop without an external provider call or spend.
- [x] Skill/channel hosts reject undeclared capabilities, tampered manifests,
      wrong-tenant context, expired proposals, and ambient secret/data access.
- [x] In-memory conformance vectors prove `Grant`, bilateral `ConsentRecord`,
      append-only/content-minimized `ShareAuditEntry`, ordered disclosure reduction,
      and `IPeerChannel` signed envelopes. Tamper, unknown version/key/algorithm,
      wrong audience/grant/purpose, expiry/future time, replay, and conflicting
      idempotency fail closed; valid retry has no duplicate effect. The proof also
      shows no inactive persistence migration and no live federation traffic.
- [x] Empty/prior-schema migrations pass; startup cannot migrate implicitly.
- [ ] Encrypted PostgreSQL logical restore plus identity/key recovery succeeds in a
      clean instance and restored passkey sign-in completes. Database-only restore
      and restart persistence passed, but combined encrypted recovery custody and
      restored-key sign-in remain unproven.
- [x] Portable Andreja export/import succeeds separately against disposable
      PostgreSQL and proves all declared exclusions and reauthorization
      requirements; see the supplemental #87 section in `evidence-44.md`.
- [ ] Restart and approved update/rollback paths preserve data, identity,
      configuration, audit, and idempotency. Restart passed; a genuine second,
      separately approved and signed revision has not completed update and rollback.
- [x] Local OTel evidence queries run with nonzero sample counts and canary tests
      prove no task, prompt, response, token, recovery material, or raw user ID leaks.
- [x] Production publish/image contains no fake-auth code or activation path.
- [ ] Phone, tablet, and desktop viewport, keyboard/accessibility, reconnect, and
      provider-failure experiences pass. Real-browser 320/768/1280, reconnect, and
      keyboard basics passed; human assistive-technology review and a complete
      provider-failure experience remain outside the merged proof.
- [ ] Threat/privacy/cost evidence is current and internal SLO/RPO/RTO targets are
      approved; no high/critical residual risk lacks explicit acceptance. Numeric
      SLO and RPO/RTO limits, numeric retention limits, the numeric Phase 1A
      model-spend envelope/hard stop and Cyrus's final exit residual-risk
      acceptance remain open.

## Stop, de-scope, and exit rules

Stop and open a decision issue when an approved spike cycle cannot prove an exit
condition, a trust-boundary assumption changes, spend is unknown/outside its
envelope, or isolation/recovery/content-suppression fails.

Issue #62 remains provisional and this packet does not choose among restoring
local tooling, using an isolated equivalent evidence host, or stopping. Cyrus
retains that technical evidence-host decision.

De-scope in this order:

1. any optional second external provider beyond required BYOK; real GitHub
   Copilot is excluded from Phase 1A under
   [Proposed ADR 0009](../adr/0009-copilot-provider-phase-scope.md), which remains
   pending Cyrus acceptance, and cannot be treated as optional Phase 1A exit
   evidence;
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
