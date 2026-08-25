# Public website Phase 0 packet

- **Status:** Recommendation for human decision; no publication or deployment
- **Issue:** [#94](https://github.com/Jamula/Andreja/issues/94)
- **Phase:** 0 (local and paper research only)
- **Decision owner:** Cyrus
- **Last reviewed:** 2026-08-24

This packet defines an information architecture and recommends a delivery
boundary for a future Andreja public/help site. It does not authorize a domain,
hosting account, public endpoint, product claim, feedback intake, analytics,
sponsorship, or any connection to the authenticated application.

## Artifacts

- [Design, hosting, and review matrix](design-hosting-matrix.md)
- [Claims inventory](claims-inventory.md)
- [Proposed ADR 0006](../adr/0006-public-website-artifact-boundary.md)
- [Local prototype](prototype/README.md)

## Recommendation in one sentence

Build a reproducible, vendor-neutral, pre-generated static artifact; deploy it
independently from Andreja application and user-data systems; search a
pre-generated index entirely in the browser; and introduce SSR only after a
recorded requirement and measured proof show that static delivery is
insufficient.

The production source-repository location remains governed by
[`docs/plan.md`](../plan.md): the current plan calls for a separate project in
this repository. A future separate-repository proposal must resolve source,
license, content, and release ownership through the plan's amendment policy.
The artifact and deployment boundary in this packet remains portable either way.

## Phase boundary

The only executable artifact is a local prototype. It has no remote
dependencies, form submission, authentication, cookies, storage, telemetry, or
product-data path. Open it locally as described in the prototype README. Do not
publish it: its wording is deliberately labeled as planning content and its
routes, contacts, legal text, and claims are incomplete.
