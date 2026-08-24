# Repository organization migration

- **Tracking:** [GitHub issue #30](https://github.com/Jamula/Andreja/issues/30)
- **Canonical repository:** [`Jamula/Andreja`](https://github.com/Jamula/Andreja)
- **Organization Project:** [Andreja Roadmap](https://github.com/orgs/Jamula/projects/2)
- **Verified base:** `90970550414279d341f4362f572faec8564b5743`
- **Review date:** 2026-08-23

`Jamula/Andreja` is the source of truth. Supported workflows must not depend on
the former personal-account repository or its redirect.

## Repository and planning state

- The local `origin` fetch and push URL resolves directly to
  `https://github.com/Jamula/Andreja.git`; the default branch is `main`.
- The organization-owned Andreja Roadmap Project contains all 14 repository
  issues. Its built-in Status, Milestone, Labels, Assignees, Repository, linked
  pull request, parent issue, and date fields avoid duplicating issue metadata.
- All 15 phase milestones remain repository-owned and appear through each
  Project item's Milestone field. GitHub Projects do not add milestones as
  standalone items.
- `cyrusjamula` in `.github/FUNDING.yml` is intentionally the GitHub Sponsors
  profile, not a repository owner or route.

## Migration impact review

| Area | Verified state and required follow-up |
|---|---|
| Actions | Actions are enabled, all actions are allowed, and SHA pinning is required. Workflows use repository context and contain no former-owner route. Keep reusable workflow references and permissions organization-safe. |
| Branch rules and stacks | Two active repository rulesets protect policy and branch behavior. The repository allows squash merge only and deletes merged branches. Stacked work continues to target `main`; branch and PR links must use the canonical repository. |
| Security | Code security, Dependabot security updates, secret scanning, validity checks, and push protection are enabled. The issue chooser now routes private reports to the canonical security policy. No secret value was read or changed. |
| Billing | No billing setting, paid feature, visibility, or production resource was changed. An organization owner must review billing before enabling any paid feature; this migration does not authorize spend. |
| OAuth and GitHub Apps | The organization reports the Azure Pipelines, Managed DevOps Pools, and Configure Azure Settings GitHub Apps as installed for all repositories. Their owners must verify callback URLs, webhook targets, and organization authorization in each provider console; no credential or installation was changed. |
| Packages and containers | No committed package or container namespace names the former owner. Package inventory was not readable with the current token; an authorized owner can run `gh auth refresh -h github.com -s read:packages` and then `gh api "orgs/Jamula/packages?package_type=container"` to verify any existing `ghcr.io/jamula/*` namespace. |
| Feedback | The documented publisher target defaults to `Jamula/Andreja` but remains deployment-configurable. Overrides require equivalent privacy and security controls. |
| Federation and semantic namespaces | Repository links use the canonical owner. Durable protocol, identity, and semantic identifiers must remain product-owned and must not derive identity from a GitHub owner or redirect. |

## Compatibility

Historical references may describe the repository before transfer, but they are
not supported configuration values. Do not add redirect-dependent examples,
clone URLs, API endpoints, badges, callbacks, package coordinates, or issue
targets.
