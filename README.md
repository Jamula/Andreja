# Andreja

Andreja is a user-owned personal assistant and skill platform. The ratified
architecture, product roadmap, company charter, and delivery phases are in
[`docs/plan.md`](docs/plan.md).

## Development

- .NET 10 is the application platform.
- Blazor is the first web experience.
- PostgreSQL is the Phase 1A self-hosted reference.
- Squad coordinates the project through GitHub Issues and isolated worktrees.

Install Squad CLI 0.11.0 through an approved package source, then validate it:

```powershell
squad --version
squad doctor
```

The repository MCP bridge invokes the installed `squad` executable directly.

Phase 0 provisions no cloud accounts, subscriptions, free tiers, or trials.
Issue work targets `main` through reviewed pull requests; dependent slices may
use GitHub native stacked PRs when available.
