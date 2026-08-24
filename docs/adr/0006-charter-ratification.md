# ADR 0006: Ratify the Andreja company charter

- **Status:** Proposed
- **Date proposed:** 2026-08-23
- **Approver:** Cyrus Jamula
- **Decision owner:** Cyrus Jamula
- **Tracking issue:** [#3](https://github.com/Jamula/Andreja/issues/3)
- **Charter:** [`docs/charter.md`](../charter.md)
- **Charter SHA-256:** `e00242f91ff9d4568cb29113b44f3d45bf76edb276d83ce480c3e416f7bcf81b`

## Context

`docs/charter.md` is a proposed company charter. It does not become authoritative
merely because this ADR or its implementation pull request is merged. Cyrus
retains the ratification decision.

The accepted platform plan governs architecture and roadmap. If this charter is
ratified, it governs mission, ethics, stakeholder rights, and human authority.
Conflicts become tracked `type:decision` issues and are reconciled by amendment
to the governing artifact.

## Proposed decision

Ratify the exact content-addressed version of `docs/charter.md` recorded here,
subject to Cyrus's explicit approval.

Acceptance is a reviewed pull request in which Cyrus's decision is recorded and
this ADR says `Status: Accepted` with the SHA-256 of the exact charter content in
that same pull request. A merge while this ADR remains Proposed is not
ratification.

## Acceptance checklist

The acceptance pull request must make these changes atomically:

- [ ] Record Cyrus's explicit approval, decision date, and the accepting pull
  request.
- [ ] Change this ADR's status to `Accepted` and replace the candidate hash with
  the current SHA-256 of the exact `docs/charter.md` content.
- [ ] Change `docs/charter.md` from proposed to ratified and change the README
  description from proposed/pending to ratified.
- [ ] Make charter-impact fields explicitly required in
  `.github/ISSUE_TEMPLATE/decision.yml` and
  `.github/pull_request_template.md`, including issue validation and the pull
  request gate.
- [ ] Open or link a `type:decision` issue for any charter/plan conflict and
  reconcile the plan's charter seed through a reviewed plan amendment.
- [ ] Run documentation consistency, YAML, Markdown/link, and diff checks and
  record their exact results.
- [ ] Do not represent agent research as legal advice, trademark clearance, or
  counsel approval.

## Consequences while Proposed

- `docs/plan.md` and ADR 0000 remain authoritative for architecture, roadmap, and
  the charter seed.
- The proposed charter informs review but does not supersede the plan or claim
  ratification.
- Documentation consistency checks do not enforce the candidate charter hash
  until this ADR becomes Accepted.

## Amendment policy after acceptance

Every material charter amendment records its issue, pull request, approver,
decision date, new charter SHA-256, and whether Cyrus approved an amendment or
re-ratification. Documentation CI must fail if an Accepted ADR hash no longer
matches `docs/charter.md`.
