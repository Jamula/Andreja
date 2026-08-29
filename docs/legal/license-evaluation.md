# Andreja licensing, IP, trademark, and contribution evaluation

**Status:** Counsel-ready research packet; not approved policy  
**Prepared:** 2026-08-23  
**Last re-verified:** 2026-08-29 (see section 1.1.1)  
**Scope:** United States baseline, with jurisdiction-specific questions preserved  
**Tracking:** GitHub issue #6  

> **Not legal advice.** This document organizes repository evidence, legal
> hypotheses, strategic options, risks, questions for qualified counsel, and
> decisions reserved to Cyrus. It does not create attorney-client privilege,
> determine ownership, authorize a license change, approve a trademark, or
> authorize publication or external contributions. Sensitive employment,
> recipient, invention, and agreement evidence belongs in a counsel-approved
> confidential channel, not this repository or a GitHub issue.

## Executive recommendation

Use a **private, no-new-recipient, no-external-contribution posture** until
qualified counsel and Cyrus complete the ownership and prior-recipient audit.
Do not change or remove `LICENSE` as an attempted cure. Do not publish a site,
package, container, protocol, SDK, domain, or stable namespace under the
Andreja name. Avoid substantive distribution of new code and preserve the
current evidence.

The leading strategy for evaluation is a **controlled open-core boundary**, not
a present decision:

- Keep the open-federation commitment at the protocol level.
- Consider a royalty-free specification covenant or agreement for the protocol,
  a permissive patent-bearing license such as Apache-2.0 for SDKs and reference
  code, and CC BY 4.0 for prose documentation, with examples clearly covered by
  a software license.
- Keep the hosted control plane, marketplace operations, trust/review systems,
  first-party commercial services, and differentiating implementation private
  or under a counsel-drafted commercial/source-available model.
- Reserve Andreja and compatibility marks; make conformance claims conditional
  on a published test and trademark policy.
- Use an inbound CLA with the rights actually needed for the chosen outbound
  strategy if dual licensing or future relicensing is required. A DCO alone
  ordinarily documents provenance and authority to submit under the indicated
  license; it does not assign copyright or independently provide broad
  relicensing rights.

This direction supports an interoperable builder ecosystem while preserving
control of the company, brand, hosted service, release channel, and
differentiating implementation. It is contingent on ownership, patent,
employment, trademark, tax/entity, and recipient facts that are not established
in this repository.

## 1. Fact record and limits

### 1.1 Verified repository facts

The following are point-in-time technical observations, not legal conclusions:

| Fact as of 2026-08-23 | Evidence |
|---|---|
| GitHub reported the predecessor repository under Cyrus Jamula's personal account as private and created 2026-08-22; the canonical repository is now `Jamula/Andreja`. | GitHub repository API |
| GitHub identifies the repository license as Apache-2.0. | GitHub repository API and root `LICENSE` |
| The complete Apache License 2.0 text has been present since the initial commit. | Commit `3f576b3ac4418e9975d858e303094e24672aacda`; `LICENSE` SHA-256 `c71d239df91726fc519c6eb72d318ec65820627232b2f796219e87dcf35d0ab4` |
| The evaluated branch head was `a875f926095b3194a92e06b86b0d15dfaa0a2d7a`, with seven commits and one human Git author identity. | Local Git history at preparation time |
| The current GitHub API snapshot showed one collaborator account, no pending repository invitations, no forks, no releases, and no deploy keys. The collaborator count includes the owner. | GitHub repository APIs |
| No Git tags were present in the local repository. | Local Git history |
| ADR 0000 blocks external contributions and visibility changes pending this Phase 0 decision. | `docs/adr/0000-plan-ratification.md` |

### 1.1.1 Re-verification as of 2026-08-29

Section 16 requires re-review when repository facts change. The following
observations re-verify or supersede the 2026-08-23 snapshot; they remain
technical observations, not legal conclusions:

| Fact as of 2026-08-29 | Evidence |
|---|---|
| `LICENSE` is byte-identical to the initial commit; SHA-256 is still `c71d239df91726fc519c6eb72d318ec65820627232b2f796219e87dcf35d0ab4` and the file has not been modified since commit `3f576b3ac4418e9975d858e303094e24672aacda` (2026-08-22). | Local Git history and file hash |
| The `Jamula/Andreja` `main` head was `f4f8f1442b545dbc9138f711b2f95f870052b17b` with 64 commits, superseding the seven-commit snapshot. | Local Git history after `git fetch --unshallow` |
| One human Git author identity still appears across all 64 commits; every commit is recorded with the GitHub web-flow committer identity, which reflects platform-mediated merges rather than an additional author. | Local Git history |
| Commit messages in history carry `Co-authored-by` trailers for AI coding-agent identities (`Copilot`, `Copilot App`, and `copilot-swe-agent[bot]`). This is new evidence relative to the 2026-08-23 snapshot. | Local Git history commit trailers |
| The repository still reports a single collaborator account (the owner), and no releases or tags exist. | GitHub repository APIs and `git ls-remote --tags` |

A `Co-authored-by` trailer is metadata a tool or human inserted into a commit
message. It is not an adjudication of authorship, copyright ownership,
human-authorship sufficiency, or any platform's terms. Counsel should treat it
as one input into the section 3.2 provenance analysis.

### 1.2 What these facts do not establish

- A current private setting does not establish that the repository was never
  public or that no file, archive, patch, screenshot, attachment, clone, mirror,
  package, build artifact, or generated output was previously delivered.
- Current collaborator, invitation, fork, release, and deploy-key counts do not
  establish historical access. Local clones and forwarded copies are not
  exhaustively visible through the repository APIs.
- A Git commit author name is not proof of copyright ownership, work-made-for-
  hire status, authorization to contribute, human authorship, or freedom from
  employer and third-party obligations.
- The presence of an Apache-2.0 file does not prove that every repository item
  was validly licensed by every relevant rights holder, nor does private
  hosting alone answer who became a licensee. Apache-2.0 defines the covered
  "Work" by reference to material made available under the license.
- No trademark, common-law, domain, company-name, app-store, package, container,
  social-handle, or protocol-namespace clearance was completed by this packet.
- No employment, consulting, incorporation, assignment, invention, patent, or
  confidential recipient records were reviewed.

## 2. Current Apache-2.0 exposure

### 2.1 License text

Apache-2.0 section 2 grants each Contributor to "You" a perpetual, worldwide,
non-exclusive, no-charge, royalty-free, irrevocable copyright license for the
covered Work. Section 3 supplies a similarly broad patent license, limited to
specified necessarily infringed claims and subject to patent-litigation
termination. Section 5 applies Apache-2.0 by default to intentionally submitted
Contributions unless the contributor says otherwise or a separate agreement
controls. Section 6 does not grant general trademark rights.

The license defines Source form to include source code, documentation source,
and configuration files. Counsel should determine the scope created by the root
license and repository presentation for each file and version.

### 2.2 Recipient-specific effect

The material risk is **not** that a private repository automatically gave the
entire public a copy. The material risk is that any rights holder who authorized
a version to be made available or distributed under Apache-2.0 may have granted
the license to each actual recipient of that version. Subject to the license
terms and facts, a later prospective license change ordinarily does not claw
back that recipient's perpetual/irrevocable rights to the version already
received. A recipient may also have the Apache right to redistribute that
version and derivatives.

Qualified counsel must answer, recipient by recipient:

1. Was a protectable Work owned or licensable by the purported Contributor?
2. What exact commit, file set, archive, build, or derivative did the person or
   service receive?
3. Was it made available under Apache-2.0 by an authorized rights holder?
4. Did a separate contract, employment duty, platform term, confidentiality
   term, or "Not a Contribution" designation apply?
5. Which copyright and patent claims were actually licensable by each
   Contributor?

Removing `LICENSE`, adding "all rights reserved" text, making the repository
private, or asking a recipient to delete a copy should not be represented as
revoking an existing Apache grant. Those actions may affect future versions or
evidence, but only after counsel confirms ownership, scope, notice, and
transition mechanics.

### 2.3 GitHub is a separate contract surface

GitHub's Terms of Service say users retain ownership of their content while
granting GitHub rights needed to provide the service; the Terms separately
address private repositories. That service license and authorized operational
access should not be casually characterized as the same thing as an
Apache-2.0 downstream distribution. Counsel should review the effective terms,
account type, AI-feature settings, and any enterprise/business terms that
applied on each relevant date.

## 3. Prior-recipient, distribution, access, and authorship audit

Preserve a dated export before changing access or history. Record counts and
references in a confidential privilege-appropriate matter file; do not put
names, employment contracts, prompts, tokens, private diagnostics, or recipient
declarations in GitHub.

### 3.1 Distribution and access work plan

| Audit step | Evidence to collect | Why it matters |
|---|---|---|
| Build a version ledger | Initial commit, every license-affecting commit, branch/tag/release/package SHAs, artifact hashes, and dates | Identifies the Work each putative grant could cover |
| Export GitHub access history | Current and removed collaborators, invitations, permission changes, personal/organization security or audit-log events, support events, and visibility changes | Current API counts cannot prove historical access |
| Inventory repository copies | Forks, templates, mirrors, bundles, archives, backups, worktrees, codespaces, CI checkouts/caches/artifacts, and local clones | A recipient may hold a copy outside GitHub |
| Inventory delivery channels | Email/chat/file shares, issue/PR attachments, snippets/gists, support uploads, demos, screen shares with file transfer, and contractor handoffs | Distribution can occur outside a release |
| Inventory registries and sites | NuGet, npm, OCI/container registries, GitHub Releases/Packages/Pages, static sites, SDK/doc portals, and package caches | Public or private publication may create additional recipients |
| Inventory tools and processors | IDE assistants, hosted build/test/scanning services, AI features, code review tools, backup providers, and integrations | Terms and data use may create separate licenses or confidentiality issues |
| Obtain recipient declarations | Person/entity, authority, dates, versions, purpose, copies, onward transfers, deletion status, and separate terms | Establishes facts without implying that deletion revokes a license |
| Check secrets and personal data | Complete history, logs, artifacts, issues, comments, and release metadata | A future visibility change exposes more than the current tree |

For every identified recipient, counsel should decide whether to request a
factual acknowledgment, assignment, replacement agreement, waiver, or no
action. Do not demand a waiver or represent rights as extinguished without
counsel.

### 3.2 Authorship and chain-of-title work plan

1. Map each material file and design artifact to its human creator, creation
   date, source material, and contractual capacity.
2. Separate Git author/committer metadata from actual authorship and ownership.
3. Identify copied, adapted, generated, commissioned, employer-owned, open-
   source, standards-derived, and third-party material.
4. Preserve AI-assistance provenance privately: human selection, arrangement,
   revision, testing, and other creative control; model/tool and applicable
   terms; and any known third-party source concern. Do not commit prompts or
   private connector content. Because Git history records AI coding-agent
   `Co-authored-by` trailers (section 1.1.1), determine for each affected commit
   what a human actually authored, reviewed, and adopted, and whether the
   trailer misstates the record.
5. Run dependency, snippet, notice, and license scans before any publication,
   then have a human resolve provenance rather than treating scanner output as
   title evidence.
6. Confirm written assignments from every person or entity whose rights must
   be centralized. Confirm execution formalities and effective dates with
   counsel.
7. If a company will own Andreja, document the transfer from the individual
   owner to that entity for copyright, patent rights, trademarks, domains,
   repositories, packages, data, and contracts. Formation alone does not
   transfer pre-formation IP.

Copyright generally protects original expression rather than an idea, method,
system, or concept. Human-authored code, prose, artwork, selection, and
arrangement may be protectable; patent, trademark, contract, and trade-secret
tools protect different interests. The U.S. Copyright Office's AI materials
also require a human-authorship analysis; tool output should not be assumed to
carry the same copyright scope as human-authored expression.

## 4. Employment, consulting, and invention obligations

These facts are unknown and must not be inferred. Ask Cyrus and every material
contributor, through counsel where appropriate:

1. In which state/country did the person reside and perform the work on each
   creation date, and where was each employer/client organized?
2. Who employed or retained the person, in what role, during that period?
3. What invention-assignment, work-made-for-hire, confidentiality, moonlighting,
   conflict, open-source, publication, and outside-business terms applied?
4. Was Andreja related to an employer's actual or anticipated business,
   research, products, customers, or assigned duties?
5. Was any work performed on paid time or using employer/client equipment,
   accounts, facilities, repositories, confidential information, data,
   licenses, or personnel?
6. Was Andreja listed as a prior invention or excluded project? Is there a
   signed schedule, consent, waiver, release, or conflict approval?
7. Did any collective-bargaining agreement, university policy, grant, customer
   contract, government funding term, or fiduciary duty apply?
8. Did the person have authority to submit each contribution and grant patent
   rights? Was a corporate approval also required?
9. Are there post-employment confidentiality, invention, nonsolicitation,
   competition, or notice obligations that counsel must evaluate?
10. Which state's employee-invention statute and choice-of-law rules apply?

Provide the actual agreements and factual timeline to qualified employment/IP
counsel. Do not summarize sensitive terms in an issue or ask an employer for
consent before counsel approves the communication strategy.

## 5. Outbound strategy options

No option should be implemented until ownership and prior grants are mapped.
"Source available" must not be marketed as "open source" unless its terms
satisfy the Open Source Definition.

| Option | Ecosystem effect | Control and revenue | Principal risks |
|---|---|---|---|
| Proprietary, closed source | Selected collaborators and customers can receive negotiated rights; weakest public code ecosystem | Strongest control over copying, hosted service, and commercial licensing | Slower adoption; more contracting; no retroactive restriction of Apache-licensed versions already received |
| Source-available commercial license | Review/self-host rights can be offered with production, competitive-use, or service restrictions | Can protect hosted economics while showing code | Not OSI open source; custom interpretation and compliance burden; contributor/relicense rights must be centralized; community trust risk |
| Time-delayed source license (for example, BSL/FSL pattern) | Source is visible and converts to an open license after a defined period | Temporary commercial exclusivity with eventual openness | Version/change-date administration; not open source before conversion; fit and enforceability require counsel; prior Apache versions remain separate |
| Dual license: copyleft/open plus commercial | Community can use the open license; commercial customers can buy alternative rights | Commercial path can fund development and permit proprietary integration | Requires ownership or broad relicensing authority for all included contributions; license compatibility and fork competition |
| Open core | Open protocol/SDK/reference core encourages integration; proprietary services/features remain controlled | Separates ecosystem adoption from hosted/control-plane economics | Boundary disputes, code leakage, "crippleware" perception, dependency contamination, duplicated maintenance |
| Fully permissive open source (Apache-2.0/MIT family) | Lowest adoption friction and broad commercial reuse | Brand, hosted service, support, data stewardship, and execution become the moat | Competitors can fork and sell; limited copyleft leverage; trademark policy becomes critical |
| Copyleft/network copyleft core | Requires specified downstream source availability; network copyleft may reach modified hosted versions | Can encourage contributions or commercial exceptions | Enterprise adoption and compatibility concerns; scope disputes; still does not protect brand or hosted operations by itself |

### 5.1 Preliminary fit

The current plan's open-federation and external-builder commitments do not
require a single license for all artifacts. A controlled open-core or dual-
boundary design appears more coherent than publishing the entire implementation
under a permissive license or attempting to protect interoperability through
secrecy. That is a strategy hypothesis for business and counsel review, not a
license selection.

## 6. Protocol, SDK, examples, documentation, and implementation separation

Maintain an artifact license matrix. Every distributable archive and generated
site should carry machine-readable and human-readable license metadata.

| Artifact | Candidate posture | Conditions to resolve |
|---|---|---|
| Federation/protocol specification | Public specification under CC BY 4.0 plus a counsel-approved royalty-free patent covenant/agreement, or a standards-oriented agreement such as OWFa 1.0 | Essential-claims scope, defensive termination, contribution process, version governance, reference rights, and no implied Andreja endorsement |
| Schema/IDL files | Permissive software license or explicit spec license | Ensure generated-code rights and database rights are clear |
| SDKs and conformance tools | Apache-2.0 is a leading candidate because it includes an express patent grant | Confirm ownership, NOTICE practice, dependency compatibility, and whether commercial dual licensing is required |
| Code examples/templates | Same permissive software license as the relevant SDK, stated in each distribution | Do not rely on a prose-document license for executable examples |
| Prose documentation | CC BY 4.0 is a leading candidate | Attribution method, embedded code/assets, translations, screenshots, privacy, and trademark reservations |
| Reference server/core | Proprietary, source-available, dual, or selected open-core modules | Define the economic and security boundary before publication |
| Hosted control plane/marketplace operations | Proprietary service implementation | Contract, privacy, marketplace, payments, security, and portability duties |
| Skill/channel packages | Publisher-selected compatible licenses plus Andreja marketplace terms | Required permissions, support, takedown, data handling, and conformance rules |
| Names, logos, badges, compatibility claims | Reserved trademark rights under a separate usage policy | Clearance, ownership, quality control, nominative use, and enforcement |

Do not use a single root license to blur these boundaries. Use per-directory
notices, SPDX identifiers, `LICENSES/`, package metadata, generated-artifact
checks, and a release manifest after counsel approves the matrix.

## 7. Inbound contributions: CLA, DCO, and assignment

### 7.1 What each mechanism does and does not do

| Mechanism | Usually does | Does not inherently do |
|---|---|---|
| Developer Certificate of Origin 1.1 | Records the contributor's certification that the contribution was created or received through a permitted chain and may be submitted under the indicated open-source license; commonly evidenced by `Signed-off-by` | Does not assign copyright; does not by itself grant Andreja arbitrary relicensing rights; does not independently add a patent license beyond the outbound license; does not prove identity, employer approval, or factual accuracy |
| Contributor License Agreement | Creates a direct contract containing the selected copyright/patent license, representations, authority, notices, and administration terms | Is not necessarily an assignment; scope depends entirely on text; does not cure rights the signer does not own; does not replace required employer/corporate authorization |
| Copyright/IP assignment | Transfers defined ownership if validly executed, subject to scope, jurisdiction, and prior grants | Does not erase earlier valid licenses; does not automatically transfer patents, trademarks, domains, data, or contracts unless covered; does not cure third-party material |

The Apache Software Foundation's ICLA is an example of a CLA under which
contributors retain ownership while granting broad copyright and patent rights.
ASF separately uses a corporate CLA where employer-owned contributions are
involved. "CLA" must therefore never be used as shorthand for "assignment."

### 7.2 Preliminary inbound recommendation

- **Before the outbound model is chosen:** accept no external code, docs,
  schemas, designs, or patent-bearing technical proposals. Configure intake so
  code is not silently accepted through issues, chat, or email.
- **If the project adopts one stable permissive/copy-left outbound license and
  does not need relicensing:** counsel may find a DCO plus contribution terms
  proportionate, with corporate authorization where needed.
- **If the project needs proprietary alternatives, dual licensing, or future
  relicensing:** use a counsel-drafted individual/corporate CLA or assignment
  that expressly supplies the necessary copyright and patent rights. A DCO
  alone is not the appropriate centralization mechanism.
- Preserve contributor ownership where commercially workable. If an assignment
  is required, explain why, limit it to the contribution, include a license-back
  where appropriate, and account for local formalities.
- Use a contribution bot/check only after counsel approves the exact text,
  signatory records, privacy notice, retention, versioning, and re-consent
  process.

## 8. Patent posture

1. Establish a confidential invention-disclosure process before publishing
   implementation or protocol details. Public disclosure can affect patent
   rights and may destroy foreign rights; obtain patent counsel advice before
   relying on any U.S. grace period.
2. Inventory potentially patentable platform, federation, authorization,
   semantic/provenance, skill-isolation, and marketplace mechanisms without
   putting enabling confidential details in this file.
3. Decide whether the strategy is patent filing, defensive publication,
   defensive aggregation, covenant/non-assert, standards essential-claims
   commitment, or no patents. Record the decision and jurisdictions.
4. Model Apache-2.0 section 3's necessarily-infringed-claims grant and defensive
   termination for any existing licensed version.
5. Ensure every CLA, commercial license, protocol agreement, acquisition, and
   publisher term treats patent ownership, essential claims, litigation
   termination, and downstream sublicensing intentionally.
6. Run a counsel-directed freedom-to-operate search before launch. A patent
   search by the project team is not a legal opinion.

## 9. Trademark, domain, and namespace posture

### 9.1 Clearance sequence

1. Identify the intended owner and goods/services: software, hosted assistant,
   developer tools/SDKs, marketplace, support, and any future regulated
   offerings.
2. Have trademark counsel run federal, state, common-law, company-name, domain,
   app-store, package, container, social, and relevant international searches
   for **Andreja**, logos, and confusingly similar marks.
3. Decide whether the word mark is registrable and commercially acceptable.
   Do not infer clearance from an available domain, GitHub repository, package
   ID, or an empty exact-match USPTO search.
4. Only after clearance and budget approval, reserve domains, GitHub
   organization/repository names, NuGet/npm/OCI package IDs, app-store names,
   social handles, signing identities, and stable URI/URN namespaces in the
   approved owner's name.
5. File and use marks on a counsel-approved schedule. Preserve specimens and
   first-use evidence without making unsupported registration claims.

NuGet's dispute process favors direct resolution and does not substitute for
clearance or guarantee recovery of a package ID. ICANN's UDRP likewise requires
specific trademark, legitimate-interest, and bad-faith findings; it is not a
reservation system.

### 9.2 Usage and ecosystem policy

Adopt a separate trademark policy covering nominative use, forks, modified
distributions, domain/package names, logos, screenshots, events, merchandise,
publisher listings, compatibility badges, and prohibited endorsement claims.
Permit truthful statements such as "compatible with Andreja" only under clear
format and conformance rules. Require forks to use a distinct product identity
when needed to avoid confusion. The Apache-2.0 code license itself does not
grant general trademark rights.

## 10. Maintainer, repository, and release governance

Before adding a maintainer or release principal:

- Identify the IP owner/licensor and the person authorized to bind it.
- Execute confidentiality, invention/contribution, acceptable-use, conflict,
  security, and account-recovery terms appropriate to the role.
- Use least privilege, phishing-resistant MFA, no shared accounts, and periodic
  access review/removal.
- Define maintainer admission, recusal, inactivity, suspension, removal,
  appeal, and emergency procedures.
- Record that `CODEOWNERS`, Git authorship, review, or commit access does not
  transfer ownership or grant corporate authority.

Before each release:

- Require protected-branch/ruleset review with a documented plan-aware fallback.
- Limit tag, release, package, container, signing, marketplace, and domain
  publication rights to named roles. Prefer short-lived trusted-publishing
  credentials over personal long-lived tokens.
- Produce a signed/attested release manifest: source SHA, artifact hashes,
  licenses, notices, SBOM/provenance, publisher identity, test/security/privacy
  evidence, and supported versions.
- Separate technical approval, legal/license approval, and business launch
  approval. Cyrus retains the final human decision until a board/entity
  delegation says otherwise.
- Require explicit approval for license, trademark, namespace, contributor-
  agreement, and public-visibility changes. Prevent a README or package metadata
  edit from silently changing policy.
- Define key compromise, unauthorized release, yanking, revocation, customer
  notice, evidence preservation, and replacement procedures.

If a two-person release rule is not yet practicable, document a sole-owner
interim procedure with hardware-backed authentication, protected credentials,
immutable evidence, and a logged break-glass path rather than pretending
segregation of duties exists.

## 11. Marketplace and publisher terms

Andreja's marketplace agreement is separate from the license to the platform,
SDK, and each publisher package. The plan's policy that publishers own their IP
and customer relationship should be implemented expressly, subject to the
limited licenses needed to host, scan, display, distribute, test, and support a
listing.

Counsel and product owners should cover:

- Publisher identity/entity verification, authority, sanctions/export and tax
  onboarding, and truthful listing information.
- Ownership/non-infringement representations; open-source and third-party
  notices; patent and trademark permissions; a limited listing/brand license.
- Publisher-to-user EULA and privacy notice; data roles, purpose limits,
  consent, subprocessors, retention/deletion/export, incident notice, and
  connector/provider terms.
- Capability declarations, security review, signing/provenance/SBOM,
  vulnerability handling, update compatibility, support, accessibility, and
  regulated-feature restrictions.
- Review criteria, rankings, sponsorship/paid placement disclosures, equal
  treatment of first-party products, policy-change notice, reasons, appeal, and
  non-retaliation.
- Listing suspension, kill switch, takedown, emergency action, reinstatement,
  wind-down, installed-user rights, settings/data export, and continuity.
- Price display, licensing metrics, trials, billing, tax, payout, reserves,
  refunds, chargebacks, fraud, revenue share, invoices, and abandoned funds.
- Warranties, disclaimers, indemnities, insurance thresholds, liability limits,
  governing law, disputes, audit, record retention, assignment, and termination.
- A separate builder privacy/data-processing schedule and transparent
  marketplace analytics; no unrelated user data or cross-skill profiles.

Any use of GitHub Marketplace, app stores, cloud marketplaces, package
registries, payment processors, or model/connector platforms requires a fresh
terms review. For example, GitHub's Marketplace Developer Agreement (effective
2025-05-27) requires a separate developer EULA, places product/support/IP/privacy
responsibility on the developer, grants GitHub listing and brand-display rights,
and preserves rights of prior installers when a listing is removed. Those are
contractual requirements for that channel, not defaults for Andreja's own
marketplace.

## 12. External contribution and visibility gates

### Gate A: any new non-owner repository access

- [ ] Recipient/version audit completed to counsel's satisfaction.
- [ ] Ownership, employer, AI-authorship, third-party, and patent issues triaged.
- [ ] Approved outbound license applies to the exact materials exposed.
- [ ] NDA/confidentiality terms cover only material that may lawfully be
      restricted; they do not purport to claw back Apache rights.
- [ ] Individual/corporate contribution agreement is signed if work will be
      accepted.
- [ ] Least-privilege access, logging, offboarding, and no-forwarding rules are
      configured and tested.
- [ ] Cyrus explicitly approves the named recipient and scope.

### Gate B: any external contribution

- [ ] Inbound mechanism (DCO/CLA/assignment) and privacy/retention approved.
- [ ] Outbound and inbound licenses are compatible with the business model.
- [ ] Contributor authority and employer/corporate approval are documented.
- [ ] Patent grant and third-party provenance are adequate.
- [ ] Review, security, privacy, accessibility, test, support, and release
      governance are operational.
- [ ] Issue/feedback channels distinguish ideas and reports from code intended
      as a Contribution, including a reliable "Not a Contribution" path.
- [ ] Cyrus and qualified counsel approve opening intake.

### Gate C: any public visibility, package, protocol, SDK, docs, or site

- [ ] Counsel signs off on ownership, prior grants, license matrix, notices,
      patents, and publication terms.
- [ ] Trademark/common-law/domain/namespace clearance and ownership are decided.
- [ ] Full Git history, Actions logs/artifacts, issues/PRs/comments, metadata,
      secrets, personal data, confidential information, and third-party content
      are reviewed. GitHub warns that making a repository public exposes code,
      activity, and Actions history/logs.
- [ ] A deliberate clean-history versus existing-history decision is approved;
      no destructive rewrite is performed casually.
- [ ] Public security, privacy, contribution, governance, support, takedown,
      code-of-conduct, and trademark policies are live.
- [ ] Package/domain/signing accounts and recovery are controlled by the
      approved owner, not an untransferable personal dependency.
- [ ] Publication is staged with exact artifact hashes and rollback/incident
      procedures.
- [ ] Cyrus records the explicit human decision; a merged README or license
      file alone is not approval.

## 13. Decision sequence

1. **Freeze and preserve:** maintain private/no-external-contribution status;
   preserve current license/history/access evidence.
2. **Prepare confidential fact packet:** recipient/version ledger, agreements,
   employment timeline, AI/provenance inventory, third-party materials, and
   invention list.
3. **Confirm ownership vehicle:** decide individual versus company ownership and
   execute the required assignments for each IP class.
4. **Obtain counsel analysis of existing Apache grants:** identify actual
   recipients and versions; do not seek retroactive restrictions before this.
5. **Choose business/open boundary:** decide proprietary, source-available,
   dual, open-core, or open-source posture for each artifact.
6. **Choose patent and specification policy:** filing/publication timing,
   essential claims, covenant/agreement, and defensive termination.
7. **Clear and own the brand:** mark, domains, package/container/app IDs, and
   protocol namespaces.
8. **Approve inbound terms:** DCO, CLA, corporate CLA, or assignment and their
   operational records.
9. **Implement governance:** maintainer roles, release authority, rulesets,
   trusted publishing, artifact license checks, and incident procedures.
10. **Run a limited collaborator pilot:** only with identified materials,
    signed terms, least privilege, and explicit approval.
11. **Re-evaluate publication:** satisfy Gate C separately for each public
    artifact; do not treat one approval as approval for the whole repository.

## 14. Questions reserved for counsel and Cyrus

### Qualified counsel

1. Which jurisdictions and employment/invention statutes govern each creator?
2. Who owned each material file and patent right when each version was made
   available, and who had authority to license it?
3. Who actually received which Apache-marked versions, and what rights or
   separate obligations apply to each recipient?
4. Does the root Apache file cover every repository artifact as presented, and
   are any notices or grants defective or incomplete?
5. What prospective relicensing/segmentation is possible without infringing
   prior grants or third-party rights?
6. What assignments are needed to place pre-formation and contributor IP,
   marks, domains, and contracts into the selected entity?
7. Which CLA/assignment and patent language is appropriate for the selected
   dual/open-core model and contributor jurisdictions?
8. What patent filing or defensive-publication steps must precede disclosure?
9. Is the proposed protocol patent covenant/agreement sufficient for
   interoperable independent implementations?
10. Is **Andreja** clear and protectable for the intended goods, services, and
    launch geographies, and what filing/domain strategy is appropriate?
11. What publisher, EULA, privacy, payment, tax, consumer, competition, and
    dispute terms are required before a marketplace pilot?
12. Which facts and communications should be handled under privilege or a
    litigation/investigation hold?
13. What is the copyright and ownership status of material produced with the AI
    coding agents recorded as commit co-authors, what do the applicable platform
    and model terms provide, and should the trailer practice be changed,
    corrected, or supplemented with a private provenance record?

### Cyrus as human decision-maker

1. Should the IP owner be Cyrus individually or a company, and when should any
   transfer occur?
2. Which assets must remain exclusive: hosted service, control plane,
   marketplace, first-party skills, core engine, or all implementation code?
3. Is eventual open conversion acceptable for source-available versions?
4. Is commercial dual licensing important enough to justify CLA/assignment
   friction?
5. Which ecosystem outcome matters most: independent implementations, SDK
   adoption, self-hosting, outside core contributions, or marketplace supply?
6. What trademark ownership, enforcement, compatibility-mark, and fork policy
   matches the desired community relationship?
7. What professional-services budget and timeline are approved for IP,
   employment, patent, trademark, entity, tax, and marketplace counsel?
8. What residual risk is acceptable before the first named collaborator,
   protocol preview, package reservation, or public artifact?

## 15. Authoritative sources

All web sources were accessed 2026-08-23. Dates below distinguish license
versions, effective dates, publication/update dates, and access dates.

| Source | Date/status | Relevance |
|---|---|---|
| [Apache License 2.0](https://www.apache.org/licenses/LICENSE-2.0) | Version dated 2004-01 | Copyright/patent grants, contribution default, redistribution, trademarks |
| [ASF Contributor Agreements](https://www.apache.org/licenses/contributor-agreements.html) and [ASF ICLA](https://www.apache.org/licenses/icla.pdf) | Current pages accessed 2026-08-23 | CLA example; contributor retains rights while granting project rights; corporate authorization |
| [Developer Certificate of Origin 1.1](https://developercertificate.org/) | Copyright 2004, 2006 | Certification text and public-record notice |
| [Open Source Definition 1.9](https://opensource.org/osd) | Last modified 2007-03-22 | Distinguishes open source from merely visible source |
| [Business Source License 1.1](https://mariadb.com/bsl11/) | License text copyright 2024; accessed 2026-08-23 | Example delayed-open source-available structure; expressly not open source before change |
| [Functional Source License](https://fsl.software/) | Current page accessed 2026-08-23 | Example two-year delayed conversion pattern |
| [Creative Commons Attribution 4.0 legal code](https://creativecommons.org/licenses/by/4.0/legalcode.en) | Version 4.0 | Candidate prose-document license; excludes patent/trademark grants |
| [Open Web Foundation Agreement 1.0](https://www.openwebfoundation.org/the-agreements/the-owf-1-0-agreements-granted-claims/owfa-1-0) | Version 1.0 | Candidate framework to evaluate for specification copyright/patent commitments |
| [U.S. Copyright Office Circular 1, Copyright Basics](https://www.copyright.gov/circs/circ01.pdf) | Current circular accessed 2026-08-23 | Copyright protects expression, not ideas/methods/systems |
| [U.S. Copyright Office Circular 30, Works Made for Hire](https://www.copyright.gov/circs/circ30.pdf) | Current circular accessed 2026-08-23 | Work-made-for-hire framework and written-agreement issues |
| [Copyright and Artificial Intelligence, Part 2: Copyrightability](https://www.copyright.gov/ai/Copyright-and-Artificial-Intelligence-Part-2-Copyrightability-Report.pdf) | Published 2025-01 | Human authorship and AI-assisted works |
| [USPTO Trademark Basics](https://www.uspto.gov/trademarks/basics) | Updated 2026-08-04 | Federal trademark baseline |
| [USPTO Trademark Search](https://www.uspto.gov/trademarks/search) | Updated 2026-04-08 | Federal search starting point, not complete clearance |
| [USPTO Patent Basics](https://www.uspto.gov/patents/basics) | Updated 2026-04-17 | Patent process baseline |
| [ICANN Uniform Domain Name Dispute Resolution Policy](https://www.icann.org/resources/pages/policy-2024-02-21-en) | Updated policy published 2024-02-21 | Domain disputes; confusing similarity, legitimate interest, and bad faith |
| [GitHub Terms of Service](https://docs.github.com/en/site-policy/github-terms/github-terms-of-service) | Effective 2026-04-27 | Content ownership/service grants, private repositories, AI features |
| [GitHub repository visibility documentation](https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/managing-repository-settings/setting-repository-visibility) | Current page accessed 2026-08-23 | Consequences of making a private repository public |
| [GitHub Marketplace Developer Agreement](https://docs.github.com/en/site-policy/github-terms/github-marketplace-developer-agreement) | Effective 2025-05-27 | Marketplace EULA, IP, privacy, support, brand, removal, and installed-user duties |
| [NuGet package-name dispute resolution](https://learn.microsoft.com/en-us/nuget/nuget-org/policies/dispute-resolution) | Updated 2025-10-31 | Package namespace disputes and limits of package-name possession |

## 16. Interim posture pending decision

Until Cyrus and qualified counsel approve a replacement:

- Keep the repository private.
- Do not invite new collaborators or accept external code, docs, schemas,
  designs, or patent-bearing technical proposals.
- Do not publish releases, packages, containers, protocols, SDKs, docs sites,
  marketplace previews, domains, or stable namespaces.
- Do not alter `LICENSE` or claim that prior Apache rights were revoked.
- Do not sign assignments, waivers, NDAs, CLAs, publisher agreements, or patent
  commitments without counsel.
- Preserve evidence and route sensitive facts through a confidential,
  counsel-approved channel.
- Re-review this packet whenever ownership, employment, recipient, visibility,
  contribution, release, entity, patent, trademark, or marketplace facts
  change.
