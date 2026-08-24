# Andreja cost model

- **Status:** Draft for ratification
- **Owner:** Quark (CFO, FinOps and Sustainability Lead)
- **Issue:** [#11 - Establish burn, usage, and sponsorship controls](https://github.com/Jamula/Andreja/issues/11)
- **Milestone:** [Phase 0 - Govern and decide](https://github.com/Jamula/Andreja/milestone/1)
- **Depends on / referenced by:** [`docs/plan.md`](plan.md) (`## Cost and FinOps`,
  `### Initial sustainability model`, `## Documentation structure`),
  [`docs/adr/0000-plan-ratification.md`](adr/0000-plan-ratification.md),
  [`.squad/directives.md`](../.squad/directives.md),
  [`.squad/agents/quark/charter.md`](../.squad/agents/quark/charter.md),
  [`docs/business/sponsorship-policy.md`](business/sponsorship-policy.md)

## Scope and non-duplication

This document is Quark's cost model: the three usage/spend ledgers, the Phase 0
no-provisioning rule, the separate AI-credit and professional-services envelopes, the
Phase 1A and Phase 1B spend approval gates, vendor commitment tracking, and the
actual-versus-budget/forecast/runway/unit-economics reporting Quark publishes. It does
not set sponsorship terms or eligibility (see
[`docs/business/sponsorship-policy.md`](business/sponsorship-policy.md)), does not
replace an architecture ADR's cost-delta section, and does not authorize spend by
itself — Cyrus decides every funding, stop, de-scope, or risk-acceptance choice from
Quark's recommendation.

## Three separate ledgers

Andreja tracks financial and usage evidence in three ledgers with different schemas,
storage, retention, access, and reconciliation. They are never combined, and raw
user/product telemetry never becomes company accounting input.

| Ledger | What it records | Owner | Storage / access | Retention | Reconciliation |
|---|---|---|---|---|---|
| **Development AI/session usage** | Squad/agent session and fleet-research AI-credit consumption, per-run cost, success/failure outcome, orchestration efficiency | Quark (records), Coordinator/Squad (source events) | Session/orchestration logs and Quark's FinOps issues; no personal task/prompt content | Duration of Phase 0/1 development; aggregate summaries only in GitHub issues | Quark reconciles credit counts against the AI-credit provider's usage statement; the statement's monetary charge is reconciled only against the AI-credit envelope, with variance explained in the recurring FinOps report |
| **Product tenant/provider metering** | Per-tenant and per-capability usage (compute, storage, model tokens, egress, queues/functions, notifications) once the product has tenants | Jett Reno (instrumentation), Quark (cost aggregation) | Product metering store, scoped to aggregate/tenant-billing fields only; excluded from company accounting systems | Per data-classification and billing-cycle retention set in `docs/privacy.md` once ratified | Quark reconciles metering aggregates against provider invoices to compute cost per active user/tenant/capability |
| **Company financial burn/income** | Cash burn, income (including sponsorship), invoices, vendor commitments, payroll/contractor cost, tax/accounting entries | Quark | Company accounting system/spreadsheet, access limited to Quark and Cyrus | Statutory/tax retention once a legal entity and accounting system are chosen (Sarek review) | Quark reconciles ledger entries against actual vendor/payment-processor invoices monthly |

Only the corresponding invoiced monetary charges from the product and development
ledgers flow into the company financial ledger (for example, "$X model spend this
month"), as separately classified entries; raw usage, AI-credit counts, and personal
data do not. Only Quark performs that roll-up.

## Phase 0: $0 and no-cloud-provisioning rule

Per [ADR 0000](adr/0000-plan-ratification.md) and `docs/plan.md`'s `## Cost and
FinOps` section:

- Phase 0 has a **$0 cloud-infrastructure cap**. No cloud accounts, subscriptions,
  free tiers, or trial subscriptions that can convert to billable resources.
- Phase 0 cost estimation uses local benchmarks, official provider documentation,
  public pricing calculators, and paper provider mappings only — never live
  provisioning.
- **AI-credit** and **professional-services** spend (domain registration, counsel,
  trademark fees) are governed by **separate envelopes** outside the $0
  cloud-infrastructure cap. Each requires its own explicit budget approval before
  spend; neither is silently absorbed into the cloud cap or into each other.
- Every architecture ADR and material feature proposal must include a cost-delta
  section; a missing or unestimated cost delta blocks approval.

### Actual initial factory research usage

Cyrus explicitly left the initial `fleet-research` run uncapped to maximize
foundational research quality; no automatic cap applies retroactively to this run.
Quark recorded the actual result to inform future limits:

| Item | AI credits |
|---|---|
| Two successful `fleet-research` runs | 820.25 |
| Failed/halted attempts (earlier capped runs that exhausted credits/subagent limits without an artifact) | 76.62 |
| **Total initial factory research burn** | **896.87** |

AI credits are not currency. Quark reconciles credit counts against the AI-credit
provider's billing statement separately, and uses the failed-run ratio (76.62 of
896.87, ~8.5%) as evidence to improve future orchestration and to propose caps or
checkpoints for subsequent uncapped-scope requests. This figure is preserved as
Quark's failure-cost baseline and is not amended retroactively.

## Cost categories and SKU tracking

`docs/cost-model.md` maintains the provider/SKU baseline Quark updates as
evidence changes. Phase 0 remains no-provisioning, so cloud SKUs stay at `$0.00`
until a separately approved Phase 1B spike budget exists:

| Provider | SKU/service baseline | Expected monthly fixed cost (USD) | Variable unit driver | Assumptions | Free-tier/trial limit | Scale threshold / trigger |
|---|---|---:|---|---|---|---|
| Current AI-credit provider | Development agent/session credit consumption | 0.00 | Credits consumed per run/session | Billing statement treated as the source of truth; initial observed burn is 896.87 credits | Not a cloud free tier; governed by explicit AI-credit envelope approvals | Phase 1A live model-token spend gate approval |
| GitHub (repository CI/docs hosting) | Repository hosting, Actions/docs workflow execution | 0.00 (Phase 0 baseline) | Build minutes, artifact/storage usage, docs/deploy runs | Stay within included quota; no paid upgrades in Phase 0 | Included quota only; no paid overage approval in Phase 0 | Quota-pressure or overage risk triggers FinOps review before any upgrade |
| Cloud provider (TBD by ADR) | Compute, SQL/Postgres, storage, backups, queues/functions, secrets, egress | 0.00 (Phase 0 cap) | vCPU-hours, GB-month, requests, egress GB, function invocations | Local/paper estimation only; no account/subscription/trial provisioning in Phase 0 | N/A in Phase 0 (free tiers/trials are prohibited) | Phase 1B spike budget + TTL/quota/teardown evidence required |
| Domains/CDN registrar/provider (TBD) | Domain registration/renewal and CDN add-ons | 0.00 baseline until purchased | Domain years purchased, add-on subscriptions | Professional-services envelope only; not cloud-infrastructure spend | No auto-renew purchase without explicit approval | Renewal date or proposed first purchase triggers approval workflow |
| Legal/compliance vendors (TBD) | Trademark search/counsel and regulatory/compliance assessments | 0.00 baseline until engaged | Fixed-fee engagement or hourly billing | Professional-services envelope only; Sarek-reviewed before commitment | N/A | New matter request triggers explicit spend approval |

Every paid dependency in any category has a named owner, a renewal or usage trigger,
a documented cancellation path, and a lower-cost alternative on file where one is
credible (see [Vendor commitments](#vendor-commitments-and-renewalcancel-paths)).

## Actual-versus-budget, forecast, and runway

Quark publishes, on a recurring cadence (at minimum every Phase 0/1 milestone review
and monthly once there is live spend):

- **Actual-versus-budget** for every tracked category and vendor commitment, with
  variance explanations.
- **Forecast** for the next period based on committed vendor terms and expected usage
  growth.
- **Runway**: months of operation remaining at current burn against available
  funding, recalculated whenever burn or funding changes materially.
- **Unit economics**: cost per active user, per tenant, and per capability, computed
  only from the product tenant/provider metering ledger's aggregate figures.
- **Sponsor income** and variance explanations, computed only from the company
  financial ledger and reported using aggregate financial data — never tied to an
  individual user's activity.

Reports are posted to the relevant FinOps GitHub issue (label `area:finops`) as
aggregate findings only; raw prompts, personal data, or tenant-identifying detail are
never included.

## Vendor commitments and renewal/cancel paths

Before any Phase 1 vendor commitment is approved, its entry here (or in a linked
FinOps issue) must include:

| Field | Requirement |
|---|---|
| Owner | Named accountable person/agent for the commitment |
| Renewal/usage trigger | Date, usage threshold, or event that triggers renewal review |
| Cancellation path | Documented steps and notice period to cancel or downgrade |
| Lower-cost alternative | A credible alternative on file, or an explicit note that none exists |
| Cost-delta ADR/issue link | The architecture ADR or FinOps issue where the spend was approved |

No vendor commitment is entered without all five fields populated.

## Separate AI/professional-service envelopes

- The **AI-credit envelope** funds development-time agent/session usage (fleet
  research, Squad orchestration) and is reconciled against the AI provider's billing
  statement, never against cloud infrastructure spend.
- The **professional-services envelope** funds domain registration, trademark
  search/counsel fees, and other one-time or recurring professional costs identified
  in `docs/plan.md`'s licensing/IP and legal sections. It requires its own explicit
  approval before purchase and is tracked independently of both the AI-credit
  envelope and the $0 cloud-infrastructure cap.
- The **cloud-infrastructure envelope** remains $0 in Phase 0. A separately approved
  Phase 1B spike budget may replace that cap only for the bounded spike described
  below; it never absorbs development AI or professional-services spend.
- The **company financial ledger** records the resulting currency transactions as
  separately classified entries. It does not replace or merge the development usage
  ledger, product metering ledger, professional-services envelope, or
  cloud-infrastructure envelope.
- Quark reports the envelopes' actual-versus-budget figures alongside one another,
  but never merges their budgets or reconciliation.

## Phase 1A: model-spend approval gate

Before Phase 1A (independent self-hosted assistant MVP) incurs live model-token
spend:

1. Quark estimates expected model-token cost per user session/task using the
   assistant provider's published pricing.
2. The estimate, assumptions, and a stop/de-scope threshold are recorded in a FinOps
   issue linked to the Phase 1A milestone.
3. Cyrus approves the model-spend budget before any BYOK/managed-provider call runs
   against real spend (test/mock providers do not require this gate).
4. Quark tracks actual model spend against the approved budget from the first live
   call and issues a no-go recommendation if spend is unmeasured or exceeds the
   approved envelope.

The Phase 1A OpenAI-compatible transport enforces this gate locally:
`ApprovedExternalTotalUnits` defaults to zero and stops a non-loopback request before
credential resolution or network I/O. A positive value is configuration evidence of
the separately recorded human approval, not approval by itself. Each attempt reserves
the profile's maximum input plus output units; completed calls reconcile the
provider-reported units into an in-process content-free counter. This conservative
process-local stop does not replace provider billing reconciliation or survive a
restart, so production spend remains blocked until a durable envelope design and
numeric approval are recorded. Loopback conformance traffic has no external spend and
does not consume the envelope.

## Phase 1B: cloud-spike approval gate

Phase 1B (managed reference deployment, invite cohort) is the first phase that may
provision real cloud infrastructure. Budgets and alerts alone do not cap cloud
spending, so before any Phase 1B provisioning:

1. **Separate spike budget approved** — a bounded, time-limited budget distinct from
   the Phase 0 $0 cap and from the AI-credit/professional-services envelopes.
2. **TTL** — every provisioned resource has an explicit time-to-live; nothing is
   provisioned to run indefinitely by default.
3. **Quota** — maximum-resource policies cap size/scale per resource and per
   environment.
4. **Automatic teardown** — spike/preview environments tear down automatically at
   TTL expiry, with deterministic teardown/purge evidence.
5. **Anomaly alerts** — budget alerts notify Quark and Cyrus on unexpected spend, but
   are a detective control, not a spending cap.
6. **Invoice reconciliation** — Quark reconciles the actual provider invoice against
   the approved spike budget and the metering ledger before the spike is closed out.

Only after the spike budget, TTL/quota/teardown evidence, and reconciliation are
complete does Quark define the steady-state personal-use/production budget and
propose it for its own separate approval. Any Phase 1B cloud spike requires a new,
explicitly approved budget decision — none is inherited from a prior spike or from
Phase 0.

## Invoice reconciliation

For every ledger and every approved envelope/spike, Quark reconciles the recorded
ledger figure against the actual invoice or provider statement:

- **Development AI/session usage** reconciles against the AI-credit provider's
  billing statement.
- **Product tenant/provider metering** reconciles against cloud/model-provider
  invoices to compute accurate unit economics.
- **Company financial burn/income** reconciles against vendor invoices, payment
  processor statements, and (once accepted) sponsor payment records.

Reconciliation variances beyond an agreed threshold trigger a documented no-go
recommendation or a revised forecast; Cyrus decides whether to stop, fund, de-scope,
or accept the recorded risk.

## No-go recommendations

Quark issues a documented no-go recommendation whenever cost is unestimated,
unmeasured, or outside the approved envelope for any ledger, vendor commitment, or
approval gate above. Quark cannot spend, sign, or accept sponsorship; Cyrus decides
whether to stop, fund, de-scope, or accept the recorded risk after reviewing Quark's
recommendation.

## Open items

- Steady-state Phase 1B production budget: pending spike-budget evidence above.
- Company legal entity, accounting system, and statutory retention schedule: pending
  Sarek's counsel-reviewed legal/regulatory work (`docs/legal/regulatory-applicability.md`,
  once ratified).
- Managed-host pricing, subscription tiers, and marketplace economics: deferred until
  usage and cost evidence exists, per `docs/plan.md`'s initial sustainability model.
