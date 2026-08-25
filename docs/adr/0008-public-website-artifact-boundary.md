# ADR 0008: Public website static artifact and deployment boundary

- **Status:** Proposed
- **Date:** 2026-08-24
- **Amended:** 2026-08-25
- **Issue:** [#94](https://github.com/Jamula/Andreja/issues/94)
- **Governing:** [Platform plan](../plan.md#public-website-help-and-support),
  [company charter](../charter.md#commitments), and
  [ADR 0000](0000-plan-ratification.md)
- **Evidence:** [Phase 0 website packet](../public-website/README.md)
- **Proposed by:** Web, Public Site and User Experience workstream
- **Decision owner:** Cyrus

## Context

The public/help site must explain Andreja and serve versioned documentation
without inheriting the authenticated application's data, identity, availability,
or attack surface. Current requirements are content known at build time. Search
can be generated with the content and executed in the browser. No accepted
requirement needs request-time rendering.

Phase 0 permits only local/paper research and has a $0 cloud-infrastructure cap.
Brand/domain, source/content license, hosting vendor, public claims, feedback,
analytics, sponsorship, and production support remain separately gated.

### Historical nonconformance and current containment

Phase 0 website artifacts are limited to loopback use or a private,
access-controlled review boundary. GitHub Pages, public previews, CDN delivery,
and DNS publication must remain disabled. Only a separately approved private
preview may use authorization and expiry; `noindex` is never authorization.

Earlier on 2026-08-25, GitHub Pages publicly served
`https://jamula.github.io/Andreja/` from `main:/docs`. Unauthenticated requests
returned `200` for `/plan`, `/public-website/prototype/`,
`/phase-1a/evidence-44`, and `/legal/regulatory-applicability`. The whole
`docs/` tree was in the Pages source with no `_config.yml` or `.nojekyll`
exclusion. That was an unapproved nonconformance, not evidence that this
Proposed ADR was accepted.

Closed issue [#114](https://github.com/Jamula/Andreja/issues/114) records
containment. The Pages `DELETE` returned `204` at
`2026-08-25T15:55:36Z`; the Pages API then returned `404`. The four routes
reached stable `404` at `2026-08-25T16:06:55Z` after bounded CDN expiry and
were independently reconfirmed `404`. Issue #114 required no repository
change. The Pages-specific merge hold on PR
[#113](https://github.com/Jamula/Andreja/pull/113) is lifted; normal draft,
review, and merge gates still apply.

Third-party/client caches, search-engine copies, previously downloaded bytes,
and provider-retained request logs may persist outside repository control.
Containment of the active Pages origin does not claim their deletion.

## Decision

If accepted, the public/help site will:

1. Build as a vendor-neutral, pre-generated static artifact containing ordinary
   HTML, CSS, JavaScript, public assets, and a public-content-only search index.
2. Search locally in the browser. Queries are not sent, logged, persisted, or
   analyzed.
3. Deploy independently from the authenticated Andreja application and every
   user data plane, with separate deployment identity, origin/hostname, logs,
   cache/invalidation, preview boundary, budget, incident authority, and
   rollback.
4. Have no app/project runtime reference, app cookies, user tokens, sign-in,
   tenant/task/prompt/connector data, or privileged route to the application.
5. Build one immutable artifact once, validate and hash it, then promote that
   exact artifact. Hosting/CDN route and header configuration are replaceable
   adapters.
6. Author canonical, versioned content in reviewed source with named owners,
   expiry/revalidation, link/search/accessibility gates, and the claims
   inventory.
7. Start with no third-party scripts, analytics, feedback form, sponsorship,
   ads, commerce, service worker, or remote font/media dependency.
8. Use access-controlled previews with separate identities and expiry.
   `noindex` headers/meta/robots are required defense in depth but never replace
   authorization.
9. Target WCAG 2.2 AA and block public launch on accessibility failures. A
   conformance claim requires version-scoped automated and manual evidence.
10. Publish an approved, monitored `/.well-known/security.txt` and production
    security headers before launch.

`docs/plan.md` currently places a separate public-site project in this
repository. This ADR does not move source or change that plan. The artifact
contract permits a later repository move only after source/content ownership,
license, release, and plan-amendment decisions are approved.

## SSR exception

Static delivery remains the default. SSR requires:

- a documented user requirement that static content, browser behavior, or a
  separately governed service cannot satisfy;
- measurable acceptance criteria and a time-boxed proof;
- comparison with the static baseline for accessibility, performance, privacy,
  security, operations, portability, and cost; and
- recorded architecture/security/privacy/operations/cost/quality challenge plus
  Cyrus's explicit approval.

Authentication, product-data access, analytics, feedback collection, or
personalization do not justify moving those concerns into this site.

## Cost boundary

Phase 0 remains $0 and provisions nothing. The packet proposes, but does not
authorize, a $10 USD/month Phase 1B recurring ceiling for minimal site hosting,
CDN, builds, storage, requests, and operational add-ons. Domain/tax and approved
professional services are separate. Any nonzero amount requires explicit budget
approval and metering before provisioning.

## Consequences

### Positive

- Product-data and authentication separation is structural and testable.
- Static files minimize runtime patching, compute, data processing, and vendor
  coupling.
- The same artifact can be served by multiple providers or a minimal static
  container.
- Local search supports help discovery without disclosing queries.
- Immutable promotion, ownership, expiry, and claims gates improve rollback and
  content integrity.

### Costs and residual risks

- The generator/search toolchain and content model still require maintenance and
  dependency review.
- Hosting/CDN request logs may process personal data such as IP addresses; a
  provider privacy review remains mandatory.
- Static delivery does not prevent supply-chain compromise, XSS in generated
  content, DNS/CDN takeover, stale claims, preview disclosure, or inaccessible
  design.
- Some future requirement may justify a separate service, but it must not
  silently widen this artifact's trust boundary.
- A source-repository split may improve ownership isolation but would contradict
  the current plan until governed as an amendment.

## Alternatives considered

- **Blazor SSR:** rejected as the default because it introduces request-time
  compute, runtime/security patching, server logs, failure modes, and cost
  without an accepted dynamic requirement.
- **Hosted documentation/search SaaS:** rejected initially because provider
  search can disclose queries and couple content, indexing, privacy, cost, and
  exit to a vendor before evidence exists.
- **Static OCI image as the only delivery:** retained as a portability fallback,
  not the default, because operating a web-server image adds patching and compute
  unnecessary for a static origin.
- **Serve pages from `Andreja.AppHost`:** rejected because it violates the
  product-data/auth-independent trust and failure boundary.
- **Separate source repository now:** not selected by this ADR. Research favors
  stronger source ownership separation, but the ratified plan currently requires
  a project in this repository and license/brand/release ownership is unresolved.

## Ethics and sustainability impact assessment

1. **People and agency:** Prospective users, operators, developers, and support
   seekers benefit from versioned, searchable explanations that distinguish
   plans from evidence. They retain ordinary browser control and can leave
   without an account or data trail controlled by this artifact. Misleading or
   inaccessible content can still harm decisions, so owned expiry, correction,
   withdrawal, and non-search navigation are required.
2. **Data and consent:** The artifact processes only reviewed public content.
   Search queries remain in browser memory; no inference, model, retention,
   sharing, feedback, account, or deletion workflow exists. A host/CDN may
   process request metadata, which blocks deployment until purpose, fields,
   retention, residency, subprocessors, access, and deletion are reviewed.
3. **Equity and accessibility:** Device, bandwidth, ability, language, culture,
   technical comfort, and self-host/managed access can exclude people. WCAG 2.2
   AA evidence, low-script/static delivery, plain status language, responsive
   reflow, locale readiness, and multiple navigation paths are launch gates;
   untranslated content remains a limitation, not an implied supported locale.
4. **AI and safety:** The site and search use no AI. Future AI-authored drafts
   remain untrusted content requiring provenance, factual review, claim approval,
   and human publication. Automation cannot approve a claim or accessibility
   conformance.
5. **Sustainability:** Static generation avoids always-on app compute, database,
   queue, and SSR patching. Costs remain build, storage, requests/CDN, logs,
   domains, dependencies, maintenance, accessibility, content, and support. The
   Phase 0 cap is $0; any Phase 1B ceiling needs explicit approval and measured
   lifecycle review.
6. **Stakeholders and incentives:** Growth, sponsor, host, search, and analytics
   vendors may favor tracking, conversion language, lock-in, or premature
   claims. No sponsor, ads, analytics, or engagement optimization is included;
   the charter, claims inventory, user trust, and public-interest correction
   path outrank promotion.
7. **Evidence and alternatives:** Ratified boundaries and repository research
   support static delivery; the local prototype demonstrates the information
   architecture and in-artifact search. SSR, hosted documentation/search, static
   OCI, app-hosted pages, and a source-repository split were assessed. A
   documented request-time requirement or failure to meet measured static
   accessibility/performance/operability needs could disprove the choice.
8. **Owner and stop conditions:** Cyrus owns the decision; named workstream
   owners govern experience, architecture, security, privacy, operations, cost,
   quality, support, legal, and public wording. Indicators include expired
   claims/pages, accessibility blockers, non-static requests/storage, artifact
   reproducibility, preview exposure, rollback, request-log fields, and spend.
   The stop conditions below require pause, withdrawal, rollback, remediation,
   or exit; residual risks include supply-chain compromise, hosting metadata,
   stale content, accessibility gaps, and provider/domain control.

## Stop conditions

Do not build, preview, or publish when any of these is true:

- a claim, brand/domain/license/legal statement lacks approval, owner, current
  evidence, or expiry;
- any app data, authentication, secret, cookie/token, connector content, or
  privileged app route is required;
- accessibility has a blocker or lacks required manual evidence;
- search depends on tracking, a remote query service, or query persistence;
- the artifact is non-reproducible, cannot be exported, or cannot roll back;
- preview content lacks access control or can be indexed;
- required security reporting, headers, privacy review, or cost approval is
  absent;
- third-party scripts, analytics, feedback, sponsorship, ads, or commerce are
  introduced without their own approval; or
- Phase 0 would provision or spend on remote infrastructure.

## Human decision

Cyrus must accept, reject, or amend this proposal. Acceptance does not select a
vendor, authorize spend/provisioning/publication, clear public wording, or
activate any deferred integration.
