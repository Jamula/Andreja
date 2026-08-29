# Phase 1A decision and exit checklist

- **Status:** Provisionally accepted as the governing work target under #62; not
  fully accepted; Phase 1A exit is not claimed
- **Date:** 2026-08-28
- **Issues:** Original packet [#9](https://github.com/Jamula/Andreja/issues/9);
  provisional amendment [#62](https://github.com/Jamula/Andreja/issues/62)

Checked items below are supported by evidence already merged at the verified
main base. A checked implementation row is local evidence only, not release
authorization, full packet acceptance, or Phase 1A exit. Partially evidenced rows
remain unchecked and name what is still missing.

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
- [ ] A separately trusted operator reruns OCI evidence with an approved external
      signing key and trust anchor. The merged reproducible audit bundle is unsigned
      and does not authorize release, update, or startup.
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
      model-spend envelope/hard stop, specialist reviews, and Cyrus's final
      residual-risk acceptance remain open.

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
