# Public website claims inventory

- **Status:** Phase 0 working inventory; no row is approved for public use
- **Issue:** [#94](https://github.com/Jamula/Andreja/issues/94)
- **Inventory owner:** Neelix with Fact Checker
- **Approval owner:** Cyrus
- **Last reviewed:** 2026-08-24

This inventory distinguishes repository facts, design commitments, targets, and
future product claims. “Evidence candidate” means a source to review; it is not
approval. Every published claim needs exact wording, scope, evidence, an
accountable owner, required reviewers, approval date, and expiry. Missing or
expired evidence blocks the build.

## Allowed only inside the local prototype

The prototype may state:

- “Private planning prototype.”
- “No product availability is represented.”
- “Search stays in this file” when the local network/storage test passes.
- “No sign-in, product data, feedback, analytics, or sponsorship is included”
  when the artifact scan and browser test pass.

These statements describe the local artifact, not a public product or deployed
site.

## Candidate claims

| ID | Candidate wording / topic | Claim class | Evidence candidate | Accountable owner | Required reviewers | Status | Expiry / revalidation trigger |
|---|---|---|---|---|---|---|---|
| CLM-001 | “Andreja is a user-owned personal assistant and skill platform.” | Purpose/positioning | Ratified `docs/plan.md`; implemented ownership/export evidence for any capability implication | Picard | Neelix, Fact Checker, Sarek | **Hold:** working description only; public brand and capability scope not cleared | Earlier of 90 days after approval, material plan change, or product-boundary change |
| CLM-002 | “You control your data.” | Product/privacy | Version-scoped export, delete, grants, audit, recovery, tenant isolation, and support evidence | Product owner | Deanna Troi, Tuvok, Data, Sarek, Fact Checker | **Blocked:** too broad without scoped proof and limitations | Every release or any data-flow/control change |
| CLM-003 | “Self-hosted.” | Availability/deployment | Supported immutable artifact, operator guide, clean-instance start, backup/restore, update/rollback, offline evidence, support policy | Jett Reno | Data, Tuvok, Guinan, Fact Checker | **Blocked:** do not imply general availability from Phase 1A development evidence | Every release; immediately on critical known issue or support change |
| CLM-004 | “Managed.” / managed availability | Availability/commercial | Approved Phase 1B deployment, isolation, identity/recovery, SLO/cost/support, privacy/legal terms | Managed-product owner | Data, Tuvok, Deanna Troi, Quark, Sarek, Guinan | **Blocked:** future gated work | Each deploy/incident and at least monthly |
| CLM-005 | Feature or connector availability | Capability | Released version, scenario/conformance tests, provider terms/scopes, help and known limitations | Feature/channel owner | Data, Tuvok, Deanna Troi, Guinan, Fact Checker | **Blocked by default:** inventory each feature separately | Every release/provider/control change |
| CLM-006 | “Privacy-first,” “secure,” “safe,” or equivalent | Security/privacy | Exact scoped control statement, threat/privacy review, test evidence, limitations, incident history | Tuvok and Deanna Troi | Sarek, Data, Fact Checker | **Prohibited as unqualified wording** | Each release/control/incident; maximum 90 days |
| CLM-007 | “WCAG 2.2 AA conformant” or “accessible” | Accessibility/conformance | Version/route/scope-defined automated and manual audit, known limitations, evaluator/date | Data and Jadzia | Guinan, Sarek, Fact Checker | **Blocked:** target is not conformance | Every release/component/theme/content change; maximum 90 days |
| CLM-008 | Uptime, availability, reliability, latency, recovery, or support response | Performance/service | Defined SLI/SLO scope and window, measured query/evidence, incident exclusions, operational capacity | Jett Reno / Guinan | Data, Quark, Sarek, Fact Checker | **Blocked:** no public SLO or support promise | At least monthly and every material incident |
| CLM-009 | Price, “free,” “low cost,” savings, or value comparison | Commercial/cost | Approved offer/terms, tax/currency, cost basis, eligibility, expiry, comparison methodology | Quark | Sarek, Neelix, Fact Checker | **Blocked:** no offer or pricing approved | Displayed expiry; immediate on any rate/term/tax change |
| CLM-010 | Customer count, testimonials, outcomes, time saved, adoption, or market demand | Social proof/outcome | Consented, representative, reproducible methodology with privacy review and source record | Neelix | Deanna Troi, Sarek, Fact Checker, Data | **Blocked:** no evidence exists; never fabricate | Evidence-specific; maximum 90 days |
| CLM-011 | Open source, Apache-2.0, proprietary, source-available, compatibility, or certification | Legal/license | Approved license/trademark policy, exact artifact/content scope, provenance and counsel review | Sarek and Cyrus | Fact Checker | **Blocked:** counsel-ready packet is not approved policy | Immediate on license/ownership/mark change; counsel-set maximum |
| CLM-012 | Andreja name, logo, domain, affiliation, or endorsement | Brand/trademark | Name/mark/domain clearance, ownership, approved asset and usage policy | Cyrus / Sarek | Neelix, Fact Checker | **Blocked:** working name only | Immediate on clearance/ownership/domain change; annual at maximum |
| CLM-013 | Sponsorship, sponsor recognition, donation acceptance, or “supported by” | Sponsorship/commercial | Ratified sponsorship policy, legal/tax/payment approval, accepted agreement, disclosure and active relationship | Quark | Sarek, Neelix, Deanna Troi, Tuvok, Fact Checker | **Blocked:** policy draft does not activate sponsorship | Each relationship/term/payment-policy change; displayed end date |
| CLM-014 | “No tracking,” “no cookies,” “analytics off,” or search privacy | Site privacy | Production artifact scan, browser network/storage trace, host/CDN log and subprocessor review, approved notice | Deanna Troi | Tuvok, Data, Sarek, Fact Checker | **Hold:** may be scoped to a tested artifact; hosting logs prevent broad wording | Every deploy/provider/header/script change; maximum 30 days |
| CLM-015 | “Product-data-free” and auth-independent public site | Architecture/privacy | Dependency/reference scan, network trace, app cookie/token isolation test, deployment identity/data-flow review | Spock | Tuvok, Deanna Troi, Data, Fact Checker | **Hold:** architecture requirement, not yet deployed evidence | Every deploy/dependency/data-flow change |
| CLM-016 | Release/version support, compatibility, or migration claim | Product/support | Published support matrix, release artifact, migration/rollback evidence, known issues | Release owner | Data, Guinan, Fact Checker | **Blocked until a supported release exists** | Every release and support-window change |
| CLM-017 | Security reporting route and response expectation | Security/support | Tested private contact, monitored backup path, `security.txt` dates, incident handoff drill | Tuvok | Guinan, Sarek, Data | **Blocked until contact and process are approved** | Contact/process change; file expiry must be monitored before date |
| CLM-018 | Legal/regulatory compliance | Legal/compliance | Qualified jurisdiction-specific legal conclusion plus technical/process evidence and exact scope | Sarek and Cyrus | Qualified counsel, Fact Checker | **Prohibited without qualified approval** | Counsel-set; immediate on law/jurisdiction/product change |

## Build record required for an approved claim

```text
claimId:
exactWording:
routesAndLocales:
productAndDocVersions:
evidenceUrisAndHashes:
accountableOwner:
requiredReviewers:
approvedBy:
approvedAt:
expiresAt:
revalidationTriggers:
knownLimitations:
withdrawalOwnerAndProcedure:
```

The public build rejects a claim when any required field is missing, evidence
cannot be resolved, the approval is absent, or `expiresAt` has passed. Emergency
withdrawal does not extend the claim or substitute weaker unsupported wording.
