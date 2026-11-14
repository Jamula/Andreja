# Licensing, IP, trademark, and contribution decision packet

**Status:** Draft for Cyrus and qualified counsel review; not approved policy

**Prepared:** 2026-09-07

**Later reconciliation:** 2026-11-08; historical observations remain dated below.

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

The 2026-11-08 reconciliation also reviewed all comments on issue #6, the
public technical sources identified in the
[repository evidence inventory](repository-evidence-inventory.md), and the
primary platform terms cited in §6. It did not inspect the privately verified
reference, its outcome, counsel communications, or any underlying
confidential material.

## 2. Historical vs current as-of

- **Historical artifact — 2026-08-23:** [`docs/legal/license-evaluation.md`](license-evaluation.md)
  was prepared 2026-08-23 and merged 2026-08-24 via PR [#18](https://github.com/Jamula/Andreja/pull/18).
  Its factual premises (including its executive recommendation and §16
  interim posture premised on *"keep the repository private"*) reflect
  the reviewed record as of that date. The source-table access dates
  recorded in §15 are as-of 2026-08-23; treat them as historical.
- **Historical reviewed state — 2026-09-07 at `2697352`:** the bounded
  observations in §3 below. Later artifacts postdating PR #18 include
  [`docs/legal/regulatory-applicability.md`](regulatory-applicability.md)
  (issue #8 closed as completed; artifact remains draft), ADR [0006](../adr/0006-charter-ratification.md)
  and [`docs/charter.md`](../charter.md) (Proposed), and ADR
  [0008](../adr/0008-public-website-artifact-boundary.md) with
  [`docs/public-website/`](../public-website/) (relevant to Gate C).
- **Private-reference verification — 2026-10-11, by Cyrus's attestation:**
  Cyrus confirmed that qualified counsel privately verified the available
  reference and provided an outcome identifying its scope, document version,
  and disposition. The
  [recorded attestation](https://github.com/Jamula/Andreja/issues/6#issuecomment-6115340637)
  completes that **procedural** verification; it is not independent agent
  inspection. The reference, actual scope, document version, disposition,
  and any conditions remain private. Do not repeat the completed
  availability/verification request or request disclosure of those details.
  An earlier "yes-counsel" interpretation was described as a
  misunderstanding in the [issue discussion](https://github.com/Jamula/Andreja/issues/6#issuecomment-6112217998).
- **What the attestation does not establish publicly:** It does not show
  whether any particular H-Inbound, H-Outbound, H-IP, H-TM, or H-Gov
  category, or separate Gate A, B, or C, was approved, pending, or outside
  the private outcome's scope. It is not evidence that every category or
  gate was approved, nor evidence that a private decision does not exist.
  The authorized humans apply the private outcome only within its actual
  scope and conditions. Issue #6's stated completion criterion remains
  Cyrus's and qualified counsel's approval of the specified path; the gates
  are separately scoped.
- **Later technical evidence — 2026-11-08:** GitHub reports the repository
  as public with Apache-2.0 license metadata. The
  [inventory](repository-evidence-inventory.md) also links a separately
  documented 2026-08-25 GitHub Pages exposure. Neither today's metadata nor
  the private-reference attestation establishes the repository's visibility
  history, particular recipients or versions, rights-holder authority, or
  substantive approval.
- **Issue #8 reconciliation — checked 2026-09-26:** GitHub reports
  [#8](https://github.com/Jamula/Andreja/issues/8) closed at
  `2026-08-30T17:10:45Z`. In the reviewed rebased tree (`bf1e27a`),
  [`docs/plan.md`](../plan.md)'s Phase 0 artifact table still says
  "Issue #8 remains open"; that lifecycle snapshot is stale.
  [`docs/operating-model.md`](../operating-model.md)'s legal/regulatory
  source row says "pending ratification, issue #8", which describes
  artifact authority, not the issue's open/closed state. Closure does not
  establish Cyrus or qualified-counsel approval, and the artifact remains
  draft. The later issue #6 visibility-status corrections do not change the
  separate issue #8 lifecycle row or the operating-model source row;
  correcting those snapshots requires a separately scoped source/hash
  update, not an inferred ratification.
- **Provenance observation, bounded:** PR #18's merge commit `87ce4e6` is
  not an ancestor of current `HEAD`. In current HEAD ancestry,
  `license-evaluation.md` was added by `ab15b72` (#12, 2026-08-23) and
  modified by `e9995d1` (#34, 2026-08-23), titled "Migrate repository
  ownership to Jamula." PR #18 targeted a feature branch,
  not `main`. Preserve both provenance references; the ancestry difference
  alone does not establish a rewrite or its cause. No conclusion about
  validity, intent, or legal effect is drawn.

## 3. Bounded current-state observations (as of 2026-09-07 at `2697352`)

Here "current-state" means the state observed on 2026-09-07, not a claim
about later dates. Observations only. No ownership, validity, clearance, or
effect conclusion is drawn. The historical sequencing recommendations below
do not establish whether subsequent private work has already occurred.

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

The statuses below describe the historical reviewed scope; see §§2 and 7–8
for the later attestation and partial inventory. A missing public record is
not proof that private work or a decision is absent.

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

## 5. Issue #6 completion: evidence boundary

Issue #6's "Done when" clause requires that **"Cyrus and qualified counsel
approve the inbound/outbound license, IP, trademark, and collaborator governance
path."** In the record reviewed on 2026-09-07:

- **No engagement reference was found** for qualified counsel review of
  the evaluation or the §14 questions in that historical scope. The later
  user-attested private verification is recorded in §2; its outcome was not
  inspected and remains private.
- **No record of Cyrus approval** was found for any of the outbound
  license, inbound mechanism, patent strategy, trademark ownership, or
  collaborator-governance path. The merged PR is an analysis artifact;
  **this documentation merge does not satisfy issue #6 either.**
- **No reference was found** to production of the confidential factual
  predicate (recipient/version ledger, employment/consulting/invention
  agreements, invention disclosures, third-party inventory, entity/tax
  facts) into any counsel-controlled matter file. This is not evidence that
  such a file does not exist outside the historical reviewed scope.

Separately, **no record of downstream approval** (outbound license, inbound
mechanism, patent, trademark, marketplace, Gate A/B/C) **was found in that
historical scope.** The later public record does not establish any particular
category's disposition; the privately verified outcome is not available for
agent inspection. This is an evidence limitation, not a finding that private
decisions are absent. Issue #6 was still OPEN when checked on 2026-11-08.

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
rights or removes copies. Where a question remains unresolved under the
privately verified outcome's actual scope and any conditions, route only that
outstanding question through the approved confidential channel. Do not repeat
the completed verification or request its private contents.

### Platform-terms supplement — checked 2026-11-08

The current [GitHub Terms of Service](https://docs.github.com/en/site-policy/github-terms/github-terms-of-service)
identify an effective date of 2026-04-27. Separately from Apache-2.0:

- [§D.5](https://docs.github.com/en/site-policy/github-terms/github-terms-of-service#5-license-grant-to-other-users)
  addresses public-repository viewing and forking and a nonexclusive,
  service-based license to other users. It also distinguishes additional
  rights that may be granted by a repository license.
- [§D.6](https://docs.github.com/en/site-policy/github-terms/github-terms-of-service#6-contributions-under-repository-license)
  addresses content added to a repository with a license notice under those
  terms, subject to a separate agreement with different terms.
- [§J.3](https://docs.github.com/en/site-policy/github-terms/github-terms-of-service#3-development-and-improvement-using-your-input-and-output)
  describes an AI input/output opt-out separately from the licenses in
  §§D.4–D.8, and says that opt-out does not apply to content that is not
  input or output; it mentions public-repository content as an example.
  [§J.1](https://docs.github.com/en/site-policy/github-terms/github-terms-of-service#1-applicability)
  limits §J.3 to individual licenses and says a governing customer or volume
  agreement controls AI data use instead.

Counsel must assess the applicable account, content, dates, prior terms, and
any separate agreement. This summary does not establish actual recipients,
ownership, Apache grants, account settings, data use, or agreement
applicability; it authorizes no new contributions, settings, or visibility
change. Do not disclose private facts or the verified outcome to resolve it
in this public packet.

**Cyrus decisions — [`§14 Cyrus as human decision-maker`](license-evaluation.md#cyrus-as-human-decision-maker).**
References use the prefix `C` and match the source list positions 1–8: **C1** ownership vehicle & transfer timing; **C2** exclusive assets;
**C3** eventual open conversion acceptability; **C4** commercial dual licensing importance; **C5** ecosystem outcome ranking;
**C6** trademark ownership/enforcement/fork policy; **C7** professional-services budget & timeline; **C8** residual-risk tolerance before first collaborator/preview/reservation.

## 7. Human sign-off register

A **record**, not a grant. No approval is recorded here. The `Sign-off`
column is intentionally empty. The statuses mean category-specific approval
is not evidenced in the reviewed public record; they do not establish that
a decision is pending or absent privately. Authorities can establish and
apply decisions through an appropriate confidential process without disclosing
the verified reference or its contents here. If they elect to make a public
record, include only details explicitly approved for disclosure. Do not
repeat the completed private-reference verification. Keep underlying advice
and agreements outside GitHub. A specialist-agent review or merged PR is not
qualified-counsel sign-off.

### 7.1 Approval categories

| # | Category | Decider | Depends on | Status | Sign-off |
|---|---|---|---|---|---|
| H-Inbound | Inbound-contribution mechanism (DCO / individual CLA / corporate CLA / assignment) selected and drafted | Cyrus + IP counsel | G-Q7, C4, outbound selection | Not evidenced publicly |  |
| H-Outbound | **Outbound artifact matrix** — per-artifact license/boundary across specification, SDK, examples, prose docs, reference implementation, hosted control plane, marketplace | Cyrus + IP counsel | G-Q4, G-Q5, C2, C3 | Not evidenced publicly |  |
| H-IP | IP / patent posture (ownership vehicle, assignments, FTO direction, filing vs. defensive publication vs. covenant) | Cyrus + IP/patent counsel | C1, G-Q1–3, G-Q6, G-Q8, G-Q9, E1–3, E5–6, E8 | Not evidenced publicly |  |
| H-TM | Trademark strategy for **Andreja** (clearance, ownership vehicle, enforcement stance, compatibility-mark policy, fork policy, usage policy) | Cyrus + trademark counsel | G-Q10, C6, E7 | Not evidenced publicly |  |
| H-Gov | **Collaborator / release governance** — maintainer roles, admission/removal, release authority (two-person rule or documented sole-owner interim), rulesets, key/credential control | Cyrus + counsel | C7, outbound + inbound selected | Not evidenced publicly |  |

### 7.2 Gate authorizations (separately scoped — [§12](license-evaluation.md#12-external-contribution-and-visibility-gates))

| Gate | Authorization | Decider | Depends on | Status | Sign-off |
|---|---|---|---|---|---|
| A | Any new non-owner repository access, per named recipient and scope | Cyrus + qualified counsel | Every [Gate A](license-evaluation.md#gate-a-any-new-non-owner-repository-access) item satisfied | Not evidenced publicly |  |
| B | Any external contribution intake | Cyrus + qualified counsel | Every [Gate B](license-evaluation.md#gate-b-any-external-contribution) item satisfied; H-Inbound; C4 | Not evidenced publicly |  |
| C | Any public visibility, package, protocol, SDK, docs site, marketplace preview, domain, or stable namespace | Cyrus + qualified counsel | Every [Gate C](license-evaluation.md#gate-c-any-public-visibility-package-protocol-sdk-docs-or-site) item satisfied; C1, C6, H-TM | Not evidenced publicly |  |

Notes re Gates A and C: observation O1 records that the current visibility
state diverges from the historical evaluation's private-state premise. Gate A
does not imply that public access is blocked or that earlier access did not
occur. Future collaborator or permission changes, visibility changes, and
publication decisions remain separately scoped. Neither checklist erases the
already observed public state or determines its authorization; this packet
does not characterize legal effect or direct a settings change.

## 8. Evidence gap register

Each gap distinguishes the **sanitized in-repo** portion (producible in
`docs/legal/`) from the **counsel-held** portion (sensitive or privileged
material — identified recipients, employment/consulting agreements,
invention disclosures, entity/tax records, counsel communications —
which does **not** belong in this repository, a GitHub issue, a PR body
or comment, or ephemeral chat).

| # | Missing evidence | Owner | Source | Sanitized in-repo portion | Counsel-held portion | Exit evidence (closes the gap) | Blocks |
|---|---|---|---|---|---|---|---|
| E1 | Recipient / version ledger for every version, archive, patch, screenshot, attachment, clone, mirror, package, build artifact, or generated output | Cyrus, with counsel direction | GitHub access history, distribution records | Partial [surface inventory](repository-evidence-inventory.md), including documented historical Pages exposure and current GitHub observations; not a recipient or complete registry ledger | Identified recipient names, dates, materials, onward transfers | Ledger delivered to counsel matter file; sanitized surface inventory committed | G-Q3, Gate A |
| E2 | Employment, consulting, contractor, and internship agreements + factual timeline for Cyrus and each material contributor | Cyrus + each material contributor | Personal / employer records | None | Full agreements + timeline | Employment/IP counsel confirms receipt & analysis | [§4](license-evaluation.md#4-employment-consulting-and-invention-obligations), C1, Gate A |
| E3 | Invention-disclosure list for potentially patentable platform, federation, authorization, semantic/provenance, skill-isolation, marketplace mechanisms | Cyrus + each inventor | Confidential invention-disclosure process under patent counsel | Sanitized categorical topic list (no reduction to practice detail) may be committed under counsel direction | Full disclosures | Patent counsel confirms disclosure intake | [§8](license-evaluation.md#8-patent-posture), H-IP |
| E4 | Third-party material inventory (open-source, standards-derived, commissioned, adapted, generated) with license/source information | Cyrus, using dependency, snippet, notice, license scans | `Directory.Packages.props`, `Dockerfile`, `compose.yaml`, `supply-chain-policy.json`, `.github/dependabot.yml`, source tree | Partial [dependency baseline](repository-evidence-inventory.md): declarations, public metadata, graph limits, and CodeCoverage license-file/build-role evidence; not a complete third-party or distribution inventory | Any commercial-terms or vendor-confidential notes | Inventory committed in-repo; counsel review recorded | [§§5–6](license-evaluation.md#5-outbound-strategy-options), Gate C |
| E5 | Authorship & AI-assistance provenance record (human selection, arrangement, revision, testing; model/tool terms; third-party source concerns) | Cyrus, preserved privately | Repo history + private records | [Source-path and identity-free revision-history index](repository-evidence-inventory.md) at a bounded snapshot; AI involvement and actual authorship remain unknown | Model/tool terms, prompts, contributor attestations and sensitive source details | Source list reviewed; full provenance held with counsel; Git identity alone is not ownership evidence | [§3.2](license-evaluation.md#32-authorship-and-chain-of-title-work-plan), Gate C |
| E6 | Entity & tax facts: formation jurisdiction, ownership, transfer intent, cross-border implications | Cyrus + entity/tax counsel | Personal / entity records | None | Full records | Entity/tax counsel confirms scope | C1, C6, H-IP |
| E7 | Trademark clearance record for **Andreja** and any compatibility mark (federal, state, common-law, company-name, domain, app-store, package, container, social, relevant international) | Trademark counsel | Search vendors, USPTO/foreign registries | None | Clearance opinion | Written clearance opinion delivered | [§9.1](license-evaluation.md#91-clearance-sequence), C6, Gate C |
| E8 | Patent freedom-to-operate direction sufficient to sequence disclosure | Patent counsel | Search vendors, patent registries | None | FTO direction | Written FTO direction delivered | [§8](license-evaluation.md#8-patent-posture), Gate C |
| E9 | **Public-exposure and contribution-surface inventory** — public artifacts, Git history, Actions logs/artifacts, issues/PRs/comments, funding surfaces, sites, registries, and contribution controls given O1/O6/O7 | Cyrus | Repo tree, GitHub settings/history, service-owner records | Partial [surface inventory](repository-evidence-inventory.md) extending O1–O8; no sensitive contents | Recipient identities, access/visibility-change records, invitations, removed access, confidential settings or contact routing | Inventory and available history reconciled with E1; unavailable history explicitly recorded; counsel review referenced | O1, O6, O7, Gate A, Gate C |
| E10 | Recipient declarations (identity, authority, dates, versions, purpose, copies, onward transfers, deletion status, separate terms) | Each recipient, requested only through counsel-approved communication | Counsel-directed outreach | None | Declarations | Counsel confirms declarations on file | E1, Gate A |

## 9. Cyrus's next actions

| # | Action | Why it does not require missing facts |
|---|---|---|
| R0 | Where not already resolved within the private outcome's actual scope and conditions, reconcile visibility history and outstanding substantive decisions with counsel through the existing confidential process; preserve evidence | Does not repeat the completed reference verification or assume why the repository is public |
| R1 | Decide any additional counsel budget & timeline needed (C7) | Business decision within Cyrus's authority; existing private decisions are not presumed absent |
| R2 | Where additional specialists or intake are needed, privately determine appropriate IP/transactional, employment/invention, patent, trademark, and entity/tax coverage and confirm conflicts and intake | Does not repeat the completed reference verification or predetermine ownership or license choices |
| R3 | Where still needed, decide ecosystem-outcome ranking (C5) and residual-risk tolerance (C8) through the appropriate process | Strategic choices without assuming previous private decisions are absent |
| R4 | Continue reviewing the partial repository-derived E1/E4/E5/E9 [inventory](repository-evidence-inventory.md); handle remaining facts privately | Repository observations do not establish ownership, AI-authorship, historical completeness, or distribution |

No step in this packet changes `LICENSE`, visibility, access, contribution
settings, namespaces, or release authority. The evaluation's controlled
open-core direction remains a candidate for counsel/Cyrus evaluation, not an
adopted license strategy. Confirm any superseding human decisions rather than
treating the historical private recommendation as today's repository state.

## 10. Cross-references

- [`docs/legal/license-evaluation.md`](license-evaluation.md) — merged historical research artifact (2026-08-23; PR [#18](https://github.com/Jamula/Andreja/pull/18)).
- [`docs/legal/repository-evidence-inventory.md`](repository-evidence-inventory.md) — dated, partial source, dependency, and exposure observations; not provenance or approval.
- [`docs/legal/regulatory-applicability.md`](regulatory-applicability.md) — separate regulatory horizon scan; not a substitute for licensing/IP analysis.
- ADR [0000 plan-ratification](../adr/0000-plan-ratification.md); ADR [0006 charter-ratification](../adr/0006-charter-ratification.md) (Proposed); ADR [0008 public-website-artifact-boundary](../adr/0008-public-website-artifact-boundary.md).
- GitHub issue [#6](https://github.com/Jamula/Andreja/issues/6) — investigate licensing, IP, trademark, and contribution strategy; issue [#8](https://github.com/Jamula/Andreja/issues/8) — regulatory applicability.
