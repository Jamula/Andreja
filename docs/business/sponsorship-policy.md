# Andreja sponsorship policy

- **Status:** Draft for ratification — **no sponsorship is accepted under this
  policy yet, and no payment provider is selected by this policy**
- **Owner:** Quark (financial terms) drafts with Sarek (terms/licensing), Deanna
  Troi (privacy/trust), Tuvok (integration security), and Neelix (public
  communication) reviewing; Cyrus approves
- **Issue:** [#11 - Establish burn, usage, and sponsorship controls](https://github.com/cyrusjamula/Andreja/issues/11)
- **Milestone:** [Phase 0 - Govern and decide](https://github.com/cyrusjamula/Andreja/milestone/1)
- **Depends on / referenced by:** [`docs/plan.md`](../plan.md) (`### Initial
  sustainability model`, `## Public website, help, and support`),
  [`docs/adr/0000-plan-ratification.md`](../adr/0000-plan-ratification.md),
  [`docs/cost-model.md`](../cost-model.md),
  [`.squad/agents/quark/charter.md`](../../.squad/agents/quark/charter.md),
  [`.squad/agents/sarek/charter.md`](../../.squad/agents/sarek/charter.md),
  [`.squad/agents/deanna-troi/charter.md`](../../.squad/agents/deanna-troi/charter.md),
  [`.squad/agents/tuvok/charter.md`](../../.squad/agents/tuvok/charter.md),
  [`.squad/agents/neelix/charter.md`](../../.squad/agents/neelix/charter.md)

## Scope and non-duplication

This document is the policy Andreja will follow **if and when** it accepts project
sponsorship. It defines eligibility, disclosure, conflicts, prohibited access, and
termination rules. It does **not**:

- Accept sponsorship, list a current sponsor, or open a sponsorship intake channel.
- Choose GitHub Sponsors, Open Collective, direct sponsorship, or any other payment
  processor. `docs/plan.md`'s initial sustainability model requires evaluating those
  options only **after** licensing, tax/accounting, payment-processing, and
  repository-ownership questions are reviewed by Sarek and Quark.
- Set the cost model, burn ledger, or unit-economics reporting (see
  [`docs/cost-model.md`](../cost-model.md)).
- Authorize public posting of a sponsorship page. Publication requires this policy's
  approval **and** separate product-name/trademark/domain clearance per
  `docs/plan.md`'s licensing/IP section.

## Principles: transparent donations and recognition

- Sponsorship is treated as **transparent project donation with optional public
  recognition**, not as paid influence, advertising, or a commercial partnership
  tier.
- Any sponsor recognition (name, logo, link) is published on the public site only,
  clearly labeled as sponsorship, and never mixed with editorial or product content
  in a way that implies endorsement of specific claims.
- Sponsorship income is treated as separate from user subscription revenue and does
  not, by itself, prove a durable SaaS business model. Quark reports sponsor income
  separately in the cost model's actual-versus-budget reporting.
- Accepting sponsorship records income in the company financial ledger but does not
  authorize or increase the development AI-credit, professional-services, or
  cloud-infrastructure envelope. Cyrus must approve any allocation separately, and a
  sponsor cannot direct an allocation in exchange for product or roadmap influence.

## Eligibility and prohibited sponsors

Sponsors must be eligible under criteria approved by Cyrus before any sponsorship is
accepted. At minimum, Andreja will not accept sponsorship from:

- Entities whose primary business is illegal in the jurisdictions Andreja operates
  in, or that are subject to sanctions/export-control restrictions.
- Entities seeking sponsorship in exchange for product placement, ranking influence,
  feature prioritization, or any editorial control over Andreja's roadmap, public
  claims, or documentation.
- Entities that would require Andreja to grant access to user data, telemetry, or
  private planning artifacts as a condition of sponsorship (see [Prohibited
  access](#prohibited-access-no-ads-no-personal-data-no-private-planning)).
- Entities whose association would create an unresolved conflict of interest with
  Cyrus, a Squad agent's owning organization, or an existing vendor/legal
  relationship, until that conflict is disclosed and resolved.
- Anonymous or unverifiable sponsors above a threshold Quark and Sarek set before
  intake opens.

The final eligibility list, verification process, and threshold are ratified as part
of accepting this policy, not assumed from this draft.

## Disclosure

- Every accepted sponsor is disclosed by name (or approved alias) on the public
  sponsorship page, alongside the recognition tier and the fact that the listing is
  sponsorship, not endorsement.
- Sponsorship terms (what a sponsor receives, and does not receive) are published in
  full; no side letter grants a sponsor rights beyond the published terms.
- Sponsor income is included in Quark's aggregate financial reporting; individual
  sponsorship amounts are disclosed only to the extent the sponsor and Cyrus agree is
  appropriate, but the existence and tier of every sponsorship relationship is never
  hidden.

## Conflicts of interest and independence

- A sponsorship must not create — or must fully disclose and mitigate — a conflict
  between the sponsor's interests and Andreja's product decisions, public claims, or
  user trust commitments.
- Sarek reviews sponsorship terms for legal/contractual conflicts; Deanna Troi
  reviews for privacy/trust conflicts; Tuvok reviews any proposed technical
  integration for security conflicts; Neelix reviews public communication for
  undisclosed influence.
- Product, architecture, and roadmap decisions remain governed by `docs/plan.md` and
  accepted ADRs. No sponsorship agreement can amend `docs/plan.md`, override a
  security/privacy/legal gate, or bypass Cyrus's final decision authority.
- Andreja's crew charters (Picard, Quark, Sarek, Deanna Troi, Tuvok, Neelix) continue
  to apply unchanged to any work touching a sponsor relationship; no charter is
  suspended or overridden by a sponsorship agreement.

## Prohibited access: no ads, no personal data, no private planning

Sponsors receive **none** of the following, without exception:

- **No targeted advertising.** Sponsorship never funds or enables ads targeted using
  Andreja user data, behavior, or inferred attributes.
- **No personal data.** Sponsors receive no user personal data, task/prompt content,
  connector payloads, or any data-subject information, aggregated or not.
- **No privileged telemetry.** Sponsors receive no access to product telemetry beyond
  what Andreja already publishes publicly (for example, aggregate uptime or release
  notes).
- **No hidden product influence.** Sponsors receive no non-public influence over
  roadmap, prioritization, feature scope, or public claims. Any sponsor feedback is
  routed through the same feedback channels as any other user (see `docs/plan.md`'s
  Feedback and Support Framework) and disclosed as sponsor-originated if it
  influences a decision.
- **No access to private planning.** Sponsors receive no access to `.squad/` runtime
  state, private roadmap discussions, unreleased ADRs, or any non-public planning
  artifact.
- **No security or privacy exceptions.** A sponsorship never grants a bypass of a
  security control, privacy commitment, tenant-isolation guarantee, or data
  classification rule. Tuvok and Deanna Troi's gates apply identically regardless of
  sponsorship status.

## Termination and refund

- Either party may terminate a sponsorship agreement on the notice period stated in
  the published terms; Andreja removes sponsor recognition promptly on termination or
  on discovery that a sponsor no longer meets eligibility criteria.
- Refund terms (if any, for prepaid or tiered sponsorships) are stated in the
  published terms before acceptance; this policy does not itself define a refund
  schedule until a payment provider and terms are chosen.
- Andreja may terminate immediately, without refund obligation beyond what law or the
  published terms require, if a sponsor violates eligibility, attempts to obtain
  prohibited access, or creates an undisclosed conflict of interest.
- Termination of a sponsorship does not retroactively alter past disclosed
  recognition history in project records; the public page is updated going forward.

## Tax, payment, accounting, and legal questions

The following are explicitly **open questions**, owned by Sarek and Quark, that must
be resolved with qualified counsel **before** this policy is activated and before any
payment provider is selected:

- Tax treatment of sponsorship/donation income for Andreja's legal entity (or lack of
  one) and jurisdiction.
- Payment-processing terms, fees, chargebacks, and compliance obligations for
  whichever provider (GitHub Sponsors, Open Collective, direct sponsorship, or
  another) is eventually evaluated and chosen.
- Accounting treatment of sponsorship income in Quark's company financial ledger,
  including recognition timing and reporting in actual-versus-budget/runway figures.
- Repository-ownership and licensing questions that affect who may legally receive
  and administer sponsorship funds on Andreja's behalf (see `docs/plan.md`'s
  Licensing, IP, and project governance section and, once available,
  `docs/legal/license-evaluation.md`).

This policy does not answer these questions; it records that they gate activation.

## Public posting gate

A public sponsorship page or any public solicitation of sponsorship is published
only after **all** of the following:

1. This policy is approved by Cyrus (with Sarek, Deanna Troi, Tuvok, and Neelix
   review recorded).
2. Product name, trademark, domain, and namespace clearance is complete, per
   `docs/plan.md`'s licensing/IP section — the public site otherwise stays on
   internal placeholders or a gated `noindex` page.
3. The tax/payment/accounting/legal questions above are resolved enough to select a
   payment provider and disclose accurate terms.
4. A concrete eligibility/verification process and disclosure format are ratified,
   not merely drafted.

Until all four conditions are met, Andreja does not solicit, accept, or publicly
reference active sponsorship beyond this policy's existence.
