# Andreja

Andreja is a user-owned personal assistant and skill platform. The ratified
architecture, product roadmap, and delivery phases are in
[`docs/plan.md`](docs/plan.md). The proposed company mission, commitments, and
operating culture are in [`docs/charter.md`](docs/charter.md); the charter remains
pending explicit ratification and takes effect only if Cyrus approves it.

## Development

- .NET 10 is the application platform.
- Blazor is the first web experience.
- PostgreSQL is the Phase 1A self-hosted reference.
- Squad coordinates the project through GitHub Issues and isolated worktrees.

See [`docs/development.md`](docs/development.md) for the pinned SDK, solution
layout, local validation commands, and foundation scope.

Install Squad CLI 0.11.0 through an approved package source, then validate it:

```powershell
squad --version
squad doctor
```

The repository MCP bridge invokes the installed `squad` executable directly.

Phase 0 provisions no cloud accounts, subscriptions, free tiers, or trials.
Issue work targets `main` through reviewed pull requests; dependent slices may
use GitHub native stacked PRs when available.
