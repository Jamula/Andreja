# Public website design, hosting, and review matrix

- **Status:** Phase 0 recommendation; not an approval to implement or publish
- **Issue:** [#94](https://github.com/Jamula/Andreja/issues/94)
- **Decision owner:** Cyrus
- **Review owners:** Jadzia Dax (experience), Jett Reno (operations), Spock
  (architecture), Quark (cost), Tuvok (security), Deanna Troi (privacy), Data
  (quality/accessibility), Guinan (help/support), Sarek (legal), and Neelix
  (public wording after clearance)
- **Evidence date:** 2026-08-24
- **Revalidate:** before implementation, on a material requirement/vendor/cost
  change, and at least every 90 days while this recommendation remains open

## Decision frame

### Facts

1. The ratified plan requires a separately deployed, product-data-free,
   auth-independent public/help site.
2. Phase 0 permits local and paper research only and has a $0
   cloud-infrastructure cap.
3. The examined `Jamula-www-Website` repository has no reusable site toolchain,
   content model, search, accessibility evidence, security headers, deployment
   automation, or infrastructure. Its DNS/hosting signals are not an
   architecture precedent.
4. The Andreja repository has no approved public brand assets, production
   availability statement, pricing, customer evidence, public support endpoint,
   analytics purpose, sponsorship activation, or cleared domain/trademark.
5. Search, versioned documentation, WCAG 2.2 AA, `security.txt`, evidence-backed
   claims, preview controls, reproducible artifacts, rollback, and named
   ownership are requirements.

### Inference

All currently identified site and help journeys can be served from content
known at build time. Search can be generated at build time and evaluated in the
visitor's browser. No accepted requirement currently needs request-time
rendering, a session, personalization, product data, or server-side mutation.

### Recommendation

Use a pre-generated static artifact as the default architecture. Build content
and a search index together, record their hashes and provenance, and deploy the
same immutable artifact to an independently operated static origin/CDN. Keep the
generator, search implementation, and host replaceable. Do not add SSR unless a
named owner documents a requirement that static delivery cannot satisfy and a
time-boxed proof measures the security, privacy, cost, accessibility, latency,
and operability impact.

### Decisions not made here

- Generator/framework and package ecosystem
- Hosting/CDN, deployment identity, region, or domain
- Source-repository move away from this repository
- Public brand, trademark, license, legal terms, or contact addresses
- Feedback/support intake implementation
- Analytics, sponsorship, pricing, commerce, or managed-product availability
- A public availability SLO or support response promise

## Information architecture

| Route family | Visitor question | Minimum content | Owner | Publication gate |
|---|---|---|---|---|
| Home | What is this and what is its status? | Plain-language purpose, evidence status, routes by visitor need, no conversion claim | Neelix with Picard | Name/trademark and every displayed claim cleared |
| Product | What is intended, current, and later? | Capability boundaries, availability labels, user-agency and data-ownership principles | Product-domain owner | Release evidence and claims inventory current |
| Get started | What can I safely do now? | Separate self-hosted and managed paths; prerequisites; version applicability | Jadzia with Jett Reno | At least one supported release and tested journey |
| Docs | How does a supported version work? | Versioned user, operator, API, skill, channel, backup/export, and troubleshooting docs | Owning engineering lead | Versioned scenario evidence and links pass |
| Help / FAQ | How do I solve or understand a problem? | Search, known issues, safe diagnostics, escalation boundaries | Guinan | Support routes and content ownership approved |
| Release notes | What changed and what should I do? | Version, date, migrations, security relevance, compatibility, rollback guidance | Release owner | Release artifact/evidence exists |
| Security & privacy | What data and trust boundaries apply? | Data flows, reporting route, privacy notices, retention, subprocessors where applicable | Tuvok and Deanna Troi | Reviewed controls and approved legal wording |
| Accessibility | How was inclusion tested and how can barriers be reported? | Target/conformance distinction, test scope, known limitations, reporting route | Data and Jadzia | Current manual and automated evidence |
| Support | Where do questions, incidents, and feedback go? | Safe channels, prohibited content, tracking expectations, no unsupported response promise | Guinan | Tenant-less intake ADR/service or approved alternative |
| Legal / brand | Who owns this and what terms apply? | Entity/owner, copyright, license, marks, terms, privacy notice | Sarek | Qualified review and explicit human approval |

### Cross-cutting content rules

- Every page records owner, applicable product/doc version, last-reviewed date,
  expiry or revalidation trigger, and claim references.
- Product states use explicit labels: `available`, `limited`, `planned`,
  `research`, or `unavailable`. The local prototype uses only `planning`.
- The canonical URL for a version is immutable. A convenience `latest` route may
  redirect or alias only after compatibility and indexing behavior are tested.
- Initial content may be English, but routes, templates, metadata, navigation,
  dates, text expansion, bidirectional text, and search-index partitioning must
  be locale-ready. No launch claim of language support exists until reviewed
  localized content and assistive-technology evidence exist.
- Security reporting is distinct from general support. Vulnerabilities and
  suspected personal-data incidents are never directed to a public issue.
- Feedback, analytics, sponsorship, ads, and product sign-in do not appear until
  their separate gates are approved.

## Architecture and delivery matrix

Scores are relative for this requirement set: 5 is strongest or lowest burden.
They are an architecture aid, not measured production evidence.

| Option | Build-time content | Browser-only search | Data/auth separation | Portability | Security/patch burden | Operability | Modeled recurring cost | Result |
|---|---:|---:|---:|---:|---:|---:|---:|---|
| Pre-generated static artifact on static origin/CDN | 5 | 5 | 5 | 5 | 5 | 4 | 5 | **Recommend** |
| Pre-generated static artifact served from minimal OCI web server | 5 | 5 | 5 | 4 | 3 | 3 | 3 | Portable fallback where static hosting is unavailable; avoid as default because it adds runtime patching |
| Server-rendered Blazor SSR | 3 | 3 | 3 | 4 | 2 | 2 | 2 | Reject absent a proven request-time requirement |
| Hosted documentation SaaS with provider search | 4 | 2 | 4 | 2 | 4 | 4 | 2 | Do not select without export, privacy, indexing, accessibility, cost, and exit evidence |
| Authenticated app-hosted pages | 2 | 3 | 1 | 2 | 2 | 2 | 3 | Reject; violates the independent trust and failure boundary |

### Static versus SSR requirement test

SSR is allowed into a proof only when all of these are recorded:

1. A user outcome cannot be met by generated pages, browser behavior, or a
   separately governed service.
2. The outcome is not authentication, product-data access, feedback intake,
   analytics, or personalization that would improperly expand this boundary.
3. The owner supplies measurable acceptance criteria and an expiry for the
   experiment.
4. The proof compares performance, availability, accessibility, security
   patching, privacy/logging, deployment/rollback, and monthly cost against the
   static baseline.
5. Spock, Tuvok, Deanna Troi, Jett Reno, Quark, and Data record challenges, and
   Cyrus explicitly approves the changed boundary.

Without all five, the static decision stands.

## Generator and documentation candidates

No package is selected or installed in Phase 0. Qualification uses a small
representative corpus and the same output contract.

| Candidate class | Strengths to prove | Risks to prove | Qualification evidence |
|---|---|---|---|
| Minimal static generator (for example Eleventy) | Small runtime/tool surface, transparent HTML, flexible content pipeline | Plugin quality, versioning conventions, author ergonomics | Reproducible offline build, semantic HTML, stable URLs, dependency/license inventory |
| Content-focused static framework (for example Astro) | Component/content composition and static output | Client-JS leakage, ecosystem churn, unnecessary hydration | Zero-JS baseline pages, island budget, deterministic artifact |
| Documentation framework (for example Docusaurus or MkDocs) | Navigation, versions, localization, docs conventions | Framework-shaped IA, theme accessibility, larger dependency/plugin surface | WCAG evaluation, exportable static output, version/localization spike |
| Custom .NET static generator | Language/toolchain alignment and full output control | Bespoke maintenance, weaker author ecosystem, accidental coupling to app assemblies | Build isolation, no app/project reference, maintenance estimate |
| Blazor SSR/static rendering | Familiar .NET components | Compute/runtime coupling, interactive payload, server logs, patch burden | Admit only through the SSR requirement test |

The generator must emit ordinary HTML/CSS/JS and assets that any static server
can host. Host-specific route files are adapters generated after the artifact,
not the canonical site.

## Search and help matrix

| Option | Query leaves browser? | Build coupling | Accessibility/control | Scale behavior | Decision |
|---|---|---|---|---|---|
| Pre-built compact JSON index plus small dependency-free search | No | Low | Full semantic control | Good for the initial bounded corpus; measure payload and query latency | Prototype and initial baseline |
| Pagefind-style generated index | No | Medium | Must test rendered results and language behavior | Better partitioning for a growing corpus | Preferred production candidate when corpus measurements justify it |
| Lunr/FlexSearch-style bundled library and index | No | Medium | Full integration control | Payload/memory can grow with corpus | Candidate only with size/license/accessibility evidence |
| Hosted search service or SaaS crawler | Usually yes | High/provider | Vendor UI/privacy constraints | Strong large-corpus features | Reject initially; revisit only after purpose, data-flow, retention, cost, accessibility, and exit review |
| Server query endpoint | Yes | High/runtime | Full control but new service | Scalable with operations | Reject absent a proven requirement; never place it in the app data plane |

Search requirements:

- Index only published public content and non-sensitive metadata.
- Do not send, log, persist, fingerprint, or analyze queries.
- Provide a labeled search control, keyboard operation, result count/status via
  a polite live region, no-results guidance, and non-search navigation.
- Partition by locale and product/docs version. Display version and content type
  in results.
- Treat indexed content as untrusted build input: escape output, prohibit raw
  HTML injection by the result renderer, and validate generated links.

## Hosting, preview, and portability matrix

| Pattern | Immutable artifact | Provider-neutral | Preview isolation | Runtime patching | Recommendation |
|---|---:|---:|---:|---:|---|
| Build once, upload to static object/origin, optional CDN | Yes | High when headers/routes are adapters | Separate hostname plus access control; `noindex` defense in depth | None for site code | Default |
| Provider Pages builds source directly | Not necessarily | Medium | Convenient but provider-specific | Provider-managed | Permit only if the exact built artifact is downloadable, hashed, promoted unchanged, and export/rollback is proven |
| Static OCI image | Yes | High | Separate deployment | Web-server/base-image patches | Fallback |
| SSR service | Yes for code, no for response state | Medium | Separate deployment | Framework/runtime/OS | Do not select |

Preview requirements:

- Use a separate deployment identity, hostname, storage namespace, logs, and
  invalidation scope from production.
- Require access control for non-public or claim-uncleared content. `noindex`,
  `X-Robots-Tag: noindex, nofollow, noarchive`, and `robots.txt` are indexing
  defenses, not authorization.
- Prevent production canonical URLs and sitemap submission.
- Run a crawler test that fails if a preview response, canonical tag, sitemap,
  or search-engine directive can expose preview content.
- Destroy or expire previews and their logs/artifacts on a defined clock.

Portability requirements:

- Artifact contains only relative or configured-origin links and open web
  formats.
- Build emits file hashes, content version, generator/tool versions, dependency
  lock hash, source revision, and build timestamp policy.
- A clean environment reproduces byte-identical output or documents the only
  normalized nondeterminism.
- A second static server serves the artifact with equivalent routes, MIME
  types, caching, redirects, error pages, compression, and security headers.
- DNS, certificate, CDN, host, and source repository are replaceable adapters.

## Security and threat review

The static model reduces server-side attack surface; it does not make the site
trusted by default.

| Threat / failure | Exposure | Required control and evidence | Owner |
|---|---|---|---|
| Product data or credentials enter content/build | Severe boundary violation | Content classification; secret/PII/product-data scan; deny app/project references; reviewed artifact manifest | Tuvok and Deanna Troi |
| Dependency or build compromise | Malicious public artifact | Locked dependencies, license/SBOM review, least-privilege isolated build, provenance/attestation policy, artifact diff and hash | Tuvok and release owner |
| Script/content XSS | Visitor compromise | Escape generated content/search results, sanitize allowed author HTML, strict hash-based CSP, no third-party scripts, DOM-XSS tests | Tuvok |
| Stale or unsupported claim | Trust/legal harm | Claims inventory, evidence links, named owner, expiry, build-time stale failure, takedown path | Content owner and Sarek |
| Preview becomes indexable/public | Premature disclosure | Access control, separate identity/hostname, noindex headers/meta/robots, crawler evidence, expiry | Jett Reno |
| Domain/DNS/CDN takeover | Impersonation or redirect | Registrar/DNS ownership, MFA/passkeys, least privilege, renewal owner, DNS inventory, dangling-record scan | Jett Reno |
| Host/CDN logs collect excess data | Privacy loss | Provider field/retention/residency review, minimization, access control, deletion and subprocessor inventory | Deanna Troi |
| Search leaks queries | Sensitive intent disclosure | In-browser index only; no query network requests/storage/logging; network and storage test | Deanna Troi |
| Link or download substitution | Malware/phishing | Link checking, allowlisted release origins, integrity/signature guidance, visible destination, periodic external-link review | Release owner |
| Security contact is absent/stale | Reports lost | Approved `/.well-known/security.txt`, tested private contact, expiry before file expiry, backup route and incident drill | Tuvok |
| Cache/rollback serves mixed versions | Misleading or broken guidance | Versioned asset names, atomic promotion, controlled invalidation, previous artifact retained, rollback drill | Jett Reno |
| Clickjacking/MIME/referrer leakage | UI or navigation abuse | `frame-ancestors 'none'`, `X-Content-Type-Options: nosniff`, strict referrer policy, minimal permissions policy | Tuvok |

Required production header baseline, adjusted only by reviewed evidence:

```text
Content-Security-Policy: default-src 'none'; base-uri 'none'; object-src 'none'; frame-ancestors 'none'; form-action 'none'; img-src 'self' data:; style-src 'self'; script-src 'self'; font-src 'self'; connect-src 'none'; manifest-src 'self'
Referrer-Policy: no-referrer
X-Content-Type-Options: nosniff
Permissions-Policy: camera=(), microphone=(), geolocation=(), payment=(), usb=()
Strict-Transport-Security: max-age=<approved-after-domain-readiness>; includeSubDomains
```

The local single-file prototype is not production header evidence. A production
build should externalize executable assets or generate reviewed CSP hashes.

## Privacy review

### Data classification and flow

| Data | Phase 0 prototype | Recommended public artifact | Notes |
|---|---|---|---|
| Public content and metadata | Local files | Static files at origin/CDN | Claim-reviewed only |
| Search index | Embedded local data | Generated static files | Public content only |
| Search query | Browser memory | Browser memory | No network, logs, or persistent storage |
| IP/user-agent/request metadata | None when opened as a file | Host/CDN may process | Provider purpose, fields, retention, access, residency, subprocessors, and deletion require review |
| Cookies/local/session storage | None | None by default | A later purpose requires a separate review |
| Analytics/telemetry | Off | Off by default | Not authorized by this packet |
| Feedback/contact data | Absent | Absent until separate tenant-less intake is approved | Never post to app or directly to GitHub |
| Auth/product data | No path | No path | App cookies/tokens and tenant/task/prompt/connector data prohibited |

Privacy acceptance evidence:

1. Browser network trace shows only same-origin static resources and no request
   when a search is typed.
2. Cookies, local storage, session storage, IndexedDB, cache storage, and service
   workers remain empty unless a separately reviewed offline feature is added.
3. Artifact and source scans find no personal data, secrets, product records,
   prompts, tokens, connector payloads, or app endpoints.
4. Hosting review records request-log fields, purpose, retention, residency,
   access roles, subprocessors, deletion, incident authority, and opt-out/legal
   posture before deployment.
5. A future form cannot be added as static-site scope creep; it must call only
   the separately governed tenant-less intake boundary after its ADR and privacy
   notice are approved.

## Accessibility and inclusive design review

WCAG 2.2 AA is a target until a version-scoped conformance review is complete.
Do not publish “WCAG compliant” or equivalent wording based on automation alone.

Automated gates:

- Valid semantic HTML, one page-level heading, named landmarks, unique IDs,
  language metadata, labeled controls, accessible names, and valid ARIA
- Axe or equivalent WCAG rule automation at representative routes, themes, and
  320/768/1280 CSS-pixel widths
- Contrast, reflow/no horizontal page scroll at 320 CSS pixels, text resize to
  200%, target size, focus visibility, reduced motion, and forced-colors checks
- Keyboard-operable navigation/search and announced dynamic result counts

Manual gates:

- Full keyboard path, logical focus order, skip link, no focus loss or trap
- Screen-reader review with at least one Windows/browser combination, including
  navigation, search status, headings, links, tables, and no-results state
- 400% zoom/reflow and responsive orientation checks
- High contrast/forced colors, dark/light preference, reduced motion, and
  cognitive review for plain language, status labels, error recovery, and
  consistent navigation
- Locale expansion, bidirectional-content sample, dates/version pronunciation,
  and reading-order review before localization is claimed

Every blocker stops publication. Non-blocking limitations are documented with
owner, workaround, remediation date, and revalidation trigger.

## Cost and sustainability review

### Approved envelope

Phase 0 cloud infrastructure remains **$0 USD**. This packet creates no account,
subscription, free tier, trial, domain purchase, paid package, or remote build.

### Proposed Phase 1B ceiling

For decision planning only, propose a **$10 USD/month recurring ceiling** for
the minimal public artifact's hosting, CDN, build execution, storage, request
processing, and operational add-ons. Domain registration/renewal, taxes, and
approved professional services remain separately tracked. This ceiling is not a
budget authorization and is not inherited by the feedback service, analytics,
status service, or authenticated product.

Before provisioning, Quark records:

- Provider/SKU, currency/tax, fixed and variable rates, included quotas, overage
  behavior, data transfer, build minutes, storage, log retention, support, and
  cancellation/export fees
- Traffic, artifact size, deploy frequency, preview count/TTL, invalidation, and
  abuse assumptions at low/expected/stress cases
- Hard quota or automatic stop where supported; alerts do not count as a cap
- Owner and approval for any amount above $0, plus monthly actual-versus-budget
  reconciliation

Stop at 70% of the approved monthly ceiling for forecast review; stop new
deployments before an overage is possible unless Cyrus approves a revised
bounded budget. Revalidate quarterly, on pricing/currency/tax changes, when
traffic or artifact size doubles, or when preview/build usage changes materially.

Static delivery is preferred because it avoids always-on application compute,
runtime patching, database/queue costs, and request-time rendering. Minify and
compress without sacrificing readable source maps retained privately for
operations; set performance and transfer budgets from the measured prototype
corpus rather than marketing targets.

## Operability review

### Release contract

1. Build once in an isolated clean environment.
2. Validate content ownership/expiry, claims, links, HTML, accessibility,
   secret/PII/product-data absence, search, headers/routes, preview indexing,
   licenses/SBOM, and artifact determinism.
3. Emit an immutable artifact plus manifest: source revision, content version,
   tool/lock versions, file hashes, CSP hashes if used, and validation results.
4. Promote the exact validated artifact; never rebuild for production.
5. Run smoke checks from multiple viewport/assistive paths without analytics.
6. Retain the previous known-good artifact and record reversible cache behavior.

### Failure and recovery

| Event | Detection | Response | Evidence before launch |
|---|---|---|---|
| Bad content/claim | Link/claim gate or report | Withdraw affected artifact/page, correct through review, republish new immutable version | Takedown exercise |
| Broken deploy | Smoke/canary checks | Stop promotion and restore previous artifact | Rollback drill with hashes |
| CDN/origin outage | External availability probe after approval | Fail over only to a pre-qualified static origin or publish incident through separately approved status route | Provider-neutral restore drill |
| Security compromise | Integrity/provenance alert or report | Freeze deploy identity, revoke/rotate, restore reviewed artifact, preserve evidence, notify per incident process | Tabletop and contact test |
| Cost anomaly/abuse | Quota/meter review | Cap traffic/builds/previews, preserve essential security contact, escalate to Quark/Cyrus | Modeled abuse and quota test |
| Stale docs | Build-time expiry and scheduled review | Block release or visibly withdraw claim/page | Seeded-expiry test |

No public RTO, RPO, uptime, support response, or incident-notification promise is
made until measured drills and named operational capacity support it.

## Content ownership and expiry

| Content class | Accountable owner | Required reviewers | Review cadence / expiry |
|---|---|---|---|
| Purpose, product, roadmap boundary | Picard / product-domain owner | Neelix, Fact Checker, Sarek when legal/brand relevant | 90 days or roadmap/release change |
| User/operator documentation | Feature/release owner | Data, Guinan, Jadzia | Each release; expire when version support ends |
| Security posture/reporting | Tuvok | Sarek, Jett Reno, Data | 90 days and every security/control/contact change |
| Privacy/data flows | Deanna Troi | Sarek, Tuvok | 90 days and every field/provider/purpose/retention change |
| Accessibility | Data and Jadzia | Guinan | Every release and every component/theme/assistive finding |
| Availability/release/status | Release owner / Jett Reno | Data, Guinan | Each deploy/incident; no claim without current evidence |
| Support/help/known issues | Guinan | Feature owner, Data | Monthly and each release/incident |
| Legal/license/trademark/terms | Sarek and Cyrus | Qualified counsel where required | On legal/entity/license/mark/domain change; otherwise counsel-set |
| Cost/pricing/sponsorship | Quark | Sarek, Neelix, Deanna Troi, Tuvok as applicable | Monthly when live and on every price/term/provider change |

The build must fail closed on an expired claim or page. Emergency withdrawal is
allowed; emergency creation of unsupported replacement wording is not.

## Phase 1B evidence gates

Implementation may begin only after the open generator/host/repository decisions
have owners and bounded approval. Public promotion requires all gates below:

- [ ] Name, trademark, domain, license, ownership, and legal wording cleared
- [ ] Approved information architecture and version/content ownership model
- [ ] Reproducible artifact and clean-environment build with manifest/hashes
- [ ] Product-data, personal-data, secret, endpoint, and app-reference scans pass
- [ ] Link, HTML, search, locale, and version-routing tests pass
- [ ] WCAG 2.2 AA automation plus recorded manual review passes
- [ ] Threat/privacy review and production security headers pass
- [ ] Approved `security.txt` contact works and expiry is monitored
- [ ] Preview access/noindex/expiry and production indexing tests pass
- [ ] Provider-neutral serve, promotion, cache, rollback, and restore drills pass
- [ ] Phase 1B budget and monthly ceiling explicitly approved
- [ ] Every public claim is approved, evidenced, owned, and unexpired
- [ ] Analytics, feedback, sponsorship, ads, and commerce remain absent unless
      separately approved

## Stop conditions

Stop the build, preview, or release on:

- unapproved brand, domain, license, legal, or trademark wording;
- an unsupported, ownerless, or expired public claim;
- any requirement for app data, authentication, cookies/tokens, secrets,
  connector content, or a privileged app route;
- accessibility blockers or missing manual evidence;
- search that requires tracking, a query service, or persistent query storage;
- non-reproducible output, provider-only source, or inability to export/rollback;
- preview content that is not access-controlled or can be indexed;
- absent/stale private security reporting route;
- third-party scripts, analytics, feedback, sponsorship, ads, or commerce without
  their separate approvals;
- forecast or actual cost above the approved ceiling; or
- any remote provisioning during Phase 0.
