# Licensing, IP, trademark, and contribution decision packet

**Status:** Draft for Cyrus and qualified counsel review; not approved policy

**Prepared:** 2026-09-07

**Scope:** Handoff wrapper around [`docs/legal/license-evaluation.md`](license-evaluation.md); United States baseline with jurisdiction-specific questions preserved

**Tracking:** GitHub issue [#6](https://github.com/Jamula/Andreja/issues/6); merged source PR [#18](https://github.com/Jamula/Andreja/pull/18)

> **Not legal advice; not counsel-drafted; does not create attorney-client privilege.**
> This packet organizes what has and has not been evidenced in the reviewed
> record. It does not retain counsel, determine ownership, clear a trademark,
> select a license, approve a recipient, or authorize any visibility or
> namespace change. Counsel must advise on whether privilege applies; it is
> not presumed. Marking material
> confidential, or storing it in a designated location, does not by itself
> confer privilege. Cyrus and qualified counsel remain authoritative.

## 1. Evidence boundary

This packet reviewed: the repository tree and available Git history at commit
`26973525b199e2aed370b4546e5ee2ee84fbd839` (`2697352`) on 2026-09-07,
GitHub issue [#6](https://github.com/Jamula/Andreja/issues/6) and its comments,
merged PR [#18](https://github.com/Jamula/Andreja/pull/18) and its comments/reviews,
and the read-only GitHub observations cited in §3. API observations describe
the live repository at retrieval, not historical settings at that commit.
This review did not access
private communications, engagement letters, agreements, invention
disclosures, matter files, entity/tax records, or any material outside
that scope. **Absence of evidence in the reviewed scope is not evidence
of absence.** Every statement below is limited to what was observed.

## 2. Historical vs current as-of

- **Historical artifact — 2026-08-23:** [`docs/legal/license-evaluation.md`](license-evaluation.md)
  was prepared 2026-08-23 and merged 2026-08-24 via PR [#18](https://github.com/Jamula/Andreja/pull/18).
  Its factual premises (including its executive recommendation and §16
  interim posture premised on *"keep the repository private"*) reflect
  the reviewed record as of that date. The source-table access dates
  recorded in §15 are as-of 2026-08-23; treat them as historical.
- **Current reviewed state — 2026-09-07 at `2697352`:** the bounded
  observations in §3 below. Later artifacts postdating PR #18 include
  [`docs/legal/regulatory-applicability.md`](regulatory-applicability.md)
  (issue #8 open; draft), ADR [0006](../adr/0006-charter-ratification.md)
  and [`docs/charter.md`](../charter.md) (Proposed), and ADR
  [0008](../adr/0008-public-website-artifact-boundary.md) with
  [`docs/public-website/`](../public-website/) (relevant to Gate C).
- **Provenance observation, bounded:** PR #18's merge commit `87ce4e6` is
  not an ancestor of current `HEAD`. In current HEAD ancestry,
  `license-evaluation.md` was added by `ab15b72` (#12, 2026-08-23) and
  modified by `e9995d1` (#34, 2026-08-23), titled "Migrate repository
  ownership to Jamula." PR #18 targeted `squad/7-sanitize-cast-squad`,
  not `main`. Preserve both provenance references; the ancestry difference
  alone does not establish a rewrite or its cause. No conclusion about
  validity, intent, or legal effect is drawn.

## 3. Bounded current-state observations (as of 2026-09-07 at `2697352`)

Observations only. No ownership, validity, clearance, or effect conclusion is drawn.

| # | Observation | As-of source |
|---|---|---|
| O1 | **Repository visibility is `public` / `private: false`** — via `gh api repos/Jamula/Andreja`. This differs from the historical private snapshot and the evaluation's §16 recommendation. The timing, scope, and approval basis of the change are not established by this snapshot. **Cyrus and counsel must reconcile this first.** No visibility change or legal effect is prescribed by this packet. | `gh api` 2026-09-07 |
| O2 | Owner `Jamula` is a GitHub **Organization** (node type `Organization`); repo `createdAt` 2026-08-22T14:48:22Z; default branch `main`. | `gh api` 2026-09-07 |
| O3 | GitHub license metadata reports Apache-2.0. In the available HEAD history, `git log --follow -- LICENSE` returns only `3f576b3` "Initial commit"; no later change to `LICENSE` was found. This does not establish rights-holder authority or artifact-specific license scope. | GitHub API / Git 2026-09-07 |
| O4 | Point-in-time counts (do **not** establish absence of distribution): `forks_count: 0`, `stargazers_count: 1`, `subscribers_count: 0`, `has_pages: false`, `homepage: null`. `gh release list` and `git tag` return empty. | `gh api` / Git 2026-09-07 |
| O5 | Commit `e9995d1` (#34) updates repository references from `cyrusjamula/Andreja` to `Jamula/Andreja`, including the evaluation's fact table. A GitHub account transition is not evidence of an IP assignment or formation of a legal entity. | Git 2026-09-07 |
| O6 | No dedicated `CONTRIBUTING.md`, `NOTICE`, `CODEOWNERS`, `CODE_OF_CONDUCT.md`, executed CLA, or DCO enforcement configuration was found in the reviewed tree. Public issue/PR templates exist. This does not establish the absence of off-repository agreements or GitHub App enforcement, nor displace the root Apache text or its contribution provisions. | Repo tree 2026-09-07 |
| O7 | `.github/FUNDING.yml` is active with `github: cyrusjamula` (all other platforms commented) — a public sponsorship surface naming the **individual**, while the repository is owned by the **Organization**. Related: [`docs/business/sponsorship-policy.md`](../business/sponsorship-policy.md). Bounded observation relevant to the C1 entity question; no conclusion. | Repo tree 2026-09-07 |
| O8 | Dependency surfaces present: `Directory.Packages.props`, `Directory.Build.props`, `global.json`, `Dockerfile`, `compose.yaml`, `supply-chain-policy.json`, `.github/dependabot.yml`. Third-party inbound licensing is in scope; a sanitized inventory is producible from these files. | Repo tree 2026-09-07 |

## 4. Acceptance mapping against issue #6

| Issue #6 item | Evaluation section | Status | What remains (evidence-limited) |
|---|---|---|---|
| Audit Apache-2.0 exposure | [§2](license-evaluation.md#2-current-apache-20-exposure) | Analysis complete; recipient-specific effect not audited in the reviewed record | Counsel-directed recipient audit; see G-Q3 |
| Repository access history | [§1.1](license-evaluation.md#11-verified-repository-facts), [§3.1](license-evaluation.md#31-distribution-and-access-work-plan) | Work plan present; no export or ledger found in the reviewed record | Access-history export + recipient ledger (E1) |
| Prior recipients | [§3.1](license-evaluation.md#31-distribution-and-access-work-plan) | Work plan present; no recipient identification found in the reviewed record | Recipient ledger + declarations (E1, E10) |
| Authorship | [§3.2](license-evaluation.md#32-authorship-and-chain-of-title-work-plan) | Work plan present; no per-file mapping found in the reviewed record | Authorship + chain-of-title map (E5, sanitized portion producible in-repo) |
| Employer invention obligations | [§4](license-evaluation.md#4-employment-consulting-and-invention-obligations) | Questions posed ([§4](license-evaluation.md#4-employment-consulting-and-invention-obligations) 1–10); no facts found in the reviewed record | Answers routed through employment/IP counsel (E2) |
| Compare outbound options | [§§5–6](license-evaluation.md#5-outbound-strategy-options) | Comparative analysis complete; selection not evidenced in the reviewed record | Cyrus + counsel per-artifact selection (H-Outbound, H-IP) |
| CLA vs DCO, assignment | [§7](license-evaluation.md#7-inbound-contributions-cla-dco-and-assignment) | Analysis complete; no inbound mechanism found adopted in the reviewed record | Counsel-drafted mechanism (H-Inbound) |
| Patent grants | [§8](license-evaluation.md#8-patent-posture) | Options analyzed; no strategy found selected in the reviewed record | FTO direction + filing/covenant decision (E8, H-IP) |
| Contributor governance | [§7.2](license-evaluation.md#72-preliminary-inbound-recommendation), [§10](license-evaluation.md#10-maintainer-repository-and-release-governance) | Framework present; issue #6 governance-path approval not evidenced | Reconcile existing operational controls with approved admission/removal and release authority (H-Gov) |
| Release authority | [§10](license-evaluation.md#10-maintainer-repository-and-release-governance) | Framework present | Two-person or documented sole-owner interim procedure (H-Gov) |
| Trademark/domain/namespace | [§9](license-evaluation.md#9-trademark-domain-and-namespace-posture) | Sequence + usage-policy framework present; no clearance found in the reviewed record | Trademark counsel clearance (E7, H-TM) |
| Visibility & external-contribution gates | [§12](license-evaluation.md#12-external-contribution-and-visibility-gates) | Gate A/B/C checklists defined; execution and approval not evidenced | Confirm each scoped authorization; reconcile O1; see §7 |

## 5. Why the issue #6 "Done when" clause is not satisfied

Issue #6's "Done when" clause requires that **"Cyrus and qualified counsel
approve the inbound/outbound license, IP, trademark, and collaborator
governance path."** In the reviewed record:

- **No engagement reference was found** for qualified counsel review of
  the evaluation or the §14 questions. Whether any exists outside the
  reviewed scope is for Cyrus to confirm.
- **No record of Cyrus approval** was found for any of the outbound
  license, inbound mechanism, patent strategy, trademark ownership, or
  collaborator-governance path. The merged PR is an analysis artifact;
  **this documentation merge does not satisfy issue #6 either.**
- **No reference was found** to production of the confidential factual
  predicate (recipient/version ledger, employment/consulting/invention
  agreements, invention disclosures, third-party inventory, entity/tax
  facts) into any counsel-controlled matter file. Whether any exists
  outside the reviewed scope is for Cyrus to confirm.

Because those three preconditions were not evidenced, **no record of
any downstream approval** (outbound license, inbound mechanism, patent,
trademark, marketplace, Gate A/B/C) **was found in the reviewed record.**
Issue #6 remains OPEN.

## 6. Indexed counsel questions

The linked source lists contain the exact questions. The labels below are
navigation summaries, not replacements for those questions.

**Qualified-counsel questions — [`§14 Qualified counsel`](license-evaluation.md#qualified-counsel).**
References use the prefix `G-Q` and match the source list positions 1–12: **G-Q1** jurisdictions/statutes; **G-Q2** ownership per version;
**G-Q3** actual recipients & prior grants; **G-Q4** root Apache scope & defective/incomplete notices or grants; **G-Q5** prospective relicensing/segmentation;
**G-Q6** assignments to consolidate rights; **G-Q7** CLA/assignment/patent language; **G-Q8** patent filing / defensive-publication timing;
**G-Q9** protocol patent covenant sufficiency; **G-Q10** trademark clearance for **Andreja**; **G-Q11** marketplace/EULA/privacy/payment/tax/consumer/competition/dispute terms; **G-Q12** privilege or litigation/investigation hold.

**Current-state supplement to G-Q3/G-Q4/G-Q12:** Given O1, which dates,
versions, GitHub surfaces, and recipients can be established; what public
availability or downstream rights must be accounted for; what records must
be preserved; and what prospective containment or remediation, if any, should
Cyrus authorize? Do not assume a visibility reversal or license edit revokes
rights or removes copies. Ask counsel to return a written disposition of each
question, unresolved facts, applicable jurisdictions, conditions, and scoped
go/no-go recommendations through the approved confidential channel.

**Cyrus decisions — [`§14 Cyrus as human decision-maker`](license-evaluation.md#cyrus-as-human-decision-maker).**
References use the prefix `C` and match the source list positions 1–8: **C1** ownership vehicle & transfer timing; **C2** exclusive assets;
**C3** eventual open conversion acceptability; **C4** commercial dual licensing importance; **C5** ecosystem outcome ranking;
**C6** trademark ownership/enforcement/fork policy; **C7** professional-services budget & timeline; **C8** residual-risk tolerance before first collaborator/preview/reservation.

## 7. Human sign-off register

A **record**, not a grant. No approval is recorded here. The `Sign-off`
column is intentionally empty. Confirm any existing decisions with Cyrus
before requesting new ones. Each completed row needs the authorized human
name/role, date, exact artifact/version and scope, conditions, and a
non-sensitive evidence reference approved for disclosure. Keep underlying
advice and agreements outside GitHub. A specialist-agent review or merged
PR is not qualified-counsel sign-off.

### 7.1 Approval categories

| # | Category | Decider | Depends on | Status | Sign-off |
|---|---|---|---|---|---|
| H-Inbound | Inbound-contribution mechanism (DCO / individual CLA / corporate CLA / assignment) selected and drafted | Cyrus + IP counsel | G-Q7, C4, outbound selection | Not evidenced; Cyrus to confirm |  |
| H-Outbound | **Outbound artifact matrix** — per-artifact license/boundary across specification, SDK, examples, prose docs, reference implementation, hosted control plane, marketplace | Cyrus + IP counsel | G-Q4, G-Q5, C2, C3 | Not evidenced; Cyrus to confirm |  |
| H-IP | IP / patent posture (ownership vehicle, assignments, FTO direction, filing vs. defensive publication vs. covenant) | Cyrus + IP/patent counsel | C1, G-Q1–3, G-Q6, G-Q8, G-Q9, E1–3, E5–6, E8 | Not evidenced; Cyrus to confirm |  |
| H-TM | Trademark strategy for **Andreja** (clearance, ownership vehicle, enforcement stance, compatibility-mark policy, fork policy, usage policy) | Cyrus + trademark counsel | G-Q10, C6, E7 | Not evidenced; Cyrus to confirm |  |
| H-Gov | **Collaborator / release governance** — maintainer roles, admission/removal, release authority (two-person rule or documented sole-owner interim), rulesets, key/credential control | Cyrus + counsel | C7, outbound + inbound selected | Not evidenced; Cyrus to confirm |  |

### 7.2 Gate authorizations (separately scoped — [§12](license-evaluation.md#12-external-contribution-and-visibility-gates))

| Gate | Authorization | Decider | Depends on | Status | Sign-off |
|---|---|---|---|---|---|
| A | Any new non-owner repository access, per named recipient and scope | Cyrus + qualified counsel | Every [Gate A](license-evaluation.md#gate-a-any-new-non-owner-repository-access) item satisfied | Not evidenced; Cyrus to confirm |  |
| B | Any external contribution intake | Cyrus + qualified counsel | Every [Gate B](license-evaluation.md#gate-b-any-external-contribution) item satisfied; H-Inbound; C4 | Not evidenced; Cyrus to confirm |  |
| C | Any public visibility, package, protocol, SDK, docs site, marketplace preview, domain, or stable namespace | Cyrus + qualified counsel | Every [Gate C](license-evaluation.md#gate-c-any-public-visibility-package-protocol-sdk-docs-or-site) item satisfied; C1, C6, H-TM | Not evidenced; Cyrus to confirm |  |

Note re Gate C: observation O1 records that the current visibility state
diverges from the merged evaluation's premise. This packet does not
characterize the legal effect and does not direct a change.

## 8. Evidence gap register

Each gap distinguishes the **sanitized in-repo** portion (producible in
`docs/legal/`) from the **counsel-held** portion (sensitive or privileged
material — identified recipients, employment/consulting agreements,
invention disclosures, entity/tax records, counsel communications —
which does **not** belong in this repository, a GitHub issue, a PR body
or comment, or ephemeral chat). Nothing here is directed into `.squad/`.

| # | Missing evidence | Owner | Source | Sanitized in-repo portion | Counsel-held portion | Exit evidence (closes the gap) | Blocks |
|---|---|---|---|---|---|---|---|
| E1 | Recipient / version ledger for every version, archive, patch, screenshot, attachment, clone, mirror, package, build artifact, or generated output | Cyrus, with counsel direction | GitHub access history, distribution records | Sanitized surface inventory (public artifacts, releases, tags, package registries touched) in `docs/legal/` | Identified recipient names, dates, materials, onward transfers | Ledger delivered to counsel matter file; sanitized surface inventory committed | G-Q3, Gate A |
| E2 | Employment, consulting, contractor, and internship agreements + factual timeline for Cyrus and each material contributor | Cyrus + each material contributor | Personal / employer records | None | Full agreements + timeline | Employment/IP counsel confirms receipt & analysis | [§4](license-evaluation.md#4-employment-consulting-and-invention-obligations), C1, Gate A |
| E3 | Invention-disclosure list for potentially patentable platform, federation, authorization, semantic/provenance, skill-isolation, marketplace mechanisms | Cyrus + each inventor | Confidential invention-disclosure process under patent counsel | Sanitized categorical topic list (no reduction to practice detail) may be committed under counsel direction | Full disclosures | Patent counsel confirms disclosure intake | [§8](license-evaluation.md#8-patent-posture), H-IP |
| E4 | Third-party material inventory (open-source, standards-derived, commissioned, adapted, generated) with license/source information | Cyrus, using dependency, snippet, notice, license scans | `Directory.Packages.props`, `Dockerfile`, `compose.yaml`, `supply-chain-policy.json`, `.github/dependabot.yml`, source tree | **Sanitized dependency & third-party inventory committed to `docs/legal/`** | Any commercial-terms or vendor-confidential notes | Inventory committed in-repo; counsel review recorded | [§§5–6](license-evaluation.md#5-outbound-strategy-options), Gate C |
| E5 | Authorship & AI-assistance provenance record (human selection, arrangement, revision, testing; model/tool terms; third-party source concerns) | Cyrus, preserved privately | Repo history + private records | Sanitized per-artifact source list (paths, artifact classes, commit references; AI involvement **unknown** unless supported by evidence) | Model/tool terms, prompts, contributor attestations and sensitive source details | Source list reviewed; full provenance held with counsel; Git identity alone is not ownership evidence | [§3.2](license-evaluation.md#32-authorship-and-chain-of-title-work-plan), Gate C |
| E6 | Entity & tax facts: formation jurisdiction, ownership, transfer intent, cross-border implications | Cyrus + entity/tax counsel | Personal / entity records | None | Full records | Entity/tax counsel confirms scope | C1, C6, H-IP |
| E7 | Trademark clearance record for **Andreja** and any compatibility mark (federal, state, common-law, company-name, domain, app-store, package, container, social, relevant international) | Trademark counsel | Search vendors, USPTO/foreign registries | None | Clearance opinion | Written clearance opinion delivered | [§9.1](license-evaluation.md#91-clearance-sequence), C6, Gate C |
| E8 | Patent freedom-to-operate direction sufficient to sequence disclosure | Patent counsel | Search vendors, patent registries | None | FTO direction | Written FTO direction delivered | [§8](license-evaluation.md#8-patent-posture), Gate C |
| E9 | **Public-exposure and contribution-surface inventory** — public artifacts, Git history, Actions logs/artifacts, issues/PRs/comments, funding surfaces, sites, registries, and contribution controls given O1/O6/O7 | Cyrus | Repo tree, GitHub settings/history, service-owner records | Sanitized surface inventory extending O1–O8; no sensitive contents | Recipient identities, access/visibility-change records, invitations, removed access, confidential settings or contact routing | Inventory and available history reconciled with E1; unavailable history explicitly recorded; counsel review referenced | O1, O6, O7, Gate A, Gate C |
| E10 | Recipient declarations (identity, authority, dates, versions, purpose, copies, onward transfers, deletion status, separate terms) | Each recipient, requested only through counsel-approved communication | Counsel-directed outreach | None | Declarations | Counsel confirms declarations on file | E1, Gate A |

## 9. Cyrus's next actions

| # | Action | Why it does not require missing facts |
|---|---|---|
| R0 | Confirm O1's visibility history and any existing approval/engagement references; preserve evidence and route the current-state supplement to counsel | Starts reconciliation without assuming why the repository is public or changing settings |
| R1 | Approve counsel budget & timeline (C7) | Business decision within Cyrus's authority |
| R2 | Identify qualified IP/transactional, employment/invention, patent, trademark, and entity/tax counsel; confirm conflicts, engagement scope, and confidential intake channel before sending sensitive facts | Counsel selection/intake does not predetermine ownership or license choices |
| R3 | Record ecosystem-outcome ranking (C5) and residual-risk tolerance (C8) | Strategic priors Cyrus can articulate today |
| R4 | Collect the repository-derived portions of E4, E5, E9; confirm gaps privately and disclose only sanitized references appropriate for this public repository | Repository observations can start now; ownership, AI-authorship, and historical completeness must not be inferred |

No step in this packet changes `LICENSE`, visibility, access, contribution
settings, namespaces, or release authority. The evaluation's controlled
open-core direction remains a candidate for counsel/Cyrus evaluation, not an
adopted license strategy. Confirm any superseding human decisions rather than
treating the historical private recommendation as today's repository state.

## 10. Cross-references

- [`docs/legal/license-evaluation.md`](license-evaluation.md) — merged historical research artifact (2026-08-23; PR [#18](https://github.com/Jamula/Andreja/pull/18)).
- [`docs/legal/regulatory-applicability.md`](regulatory-applicability.md) — separate regulatory horizon scan; not a substitute for licensing/IP analysis.
- ADR [0000 plan-ratification](../adr/0000-plan-ratification.md); ADR [0006 charter-ratification](../adr/0006-charter-ratification.md) (Proposed); ADR [0008 public-website-artifact-boundary](../adr/0008-public-website-artifact-boundary.md).
- GitHub issue [#6](https://github.com/Jamula/Andreja/issues/6) — investigate licensing, IP, trademark, and contribution strategy; issue [#8](https://github.com/Jamula/Andreja/issues/8) — regulatory applicability.
