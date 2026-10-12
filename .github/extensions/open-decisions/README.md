# Open decisions canvas

Shared project extension for `Jamula/Andreja`. Reload extensions, then open canvas
`open-decisions` (provider `project:open-decisions`). Agent action: `refresh`.
Requires Node and the existing authenticated `gh` CLI. The host resolves the SDK;
no packages, tokens or external content service are configured.

The priority issue order is #2, #73, #3, #6, #93, #155, #67. Other open
`[Decision]` issues or issues labeled `decision` are discovered too. Closed issues
are excluded. The GitHub issue and paginated dialogue are read live on open or
refresh. Any failed fetch keeps the last complete snapshot explicitly **stale**;
no demo data or partial success replaces it. The UI polls the local snapshot so
agent refreshes appear in open panels.
Every open, including reopening an existing panel, refreshes GitHub. Concurrent
opens and refresh actions coalesce into one in-flight fetch. The agent action
returns only counts, timestamps and freshness/error metadata; the iframe retains
the complete source snapshot.

Question cards quote source question headings, alternatives, recommendations and
context. Multiple question headings in one issue remain separate. A later comment
with the same normalized heading replaces that heading's earlier text; repeated
headings match by extracted-question occurrence within each source (first to first,
second to second). A single-heading follow-up updates only the first occurrence.
Use distinct headings when repeated questions need independent updates or reordering;
occurrence matching cannot infer which question an isolated generic heading refers to.
Other questions are not silently treated as resolved. Context headings (options/tradeoffs, evidence,
gates and boundaries) stay
with their question until the next distinct question. Explicit option sections
are quoted as alternatives; inline alternatives omit the question prompt rather
than repeating it. Latest comments and the full source link remain
visible because an open issue does not establish a question's decision status.
Unknown status is intentional: the canvas never infers Cyrus's approval from
comments, merged code or specialist recommendations. Phase 0 grouping reflects
the assessed entry issues; later feedback/publication and enforcement inventory
remain separate. The October 16 target waives no prerequisite.

Drafts require an explicit **Save local draft** action. Copy does not submit them.
Unsaved changes have an undo affordance and navigation warning. Drafts live in
`$COPILOT_HOME/extensions/open-decisions/artifacts/` (default
`~/.copilot/extensions/open-decisions/artifacts/`), keyed by repository, issue and
normalized question heading and occurrence—not panel ID. First occurrences retain
their original identity; subsequent occurrences have distinct draft keys. They
survive new panels/provider reloads and are
not committed, shared, governed decisions or GitHub comments. Changing a question
heading creates a distinct identity; old draft files remain inspectable. Concurrent
editors of the same private draft use last explicitly saved text. Avoid confidential
counsel details in drafts or the public GitHub discussion.

Each panel has a loopback-only server closed on panel close. Mutation routes
require exact same-origin JSON plus an unpredictable CSRF header; bodies and CLI
requests are bounded. Source text is rendered with `textContent`, and only HTTPS
links on `github.com` are clickable. No GitHub write, approval, risk acceptance,
prompt injection, hooks or session messages are implemented.

Validation (from repository root):

Requires Node.js 22 or newer. The dedicated `Open decisions canvas tests` workflow
runs all four suites and module syntax checks for pull requests targeting `main`
and pushes to `main` that change this extension or its test workflow, and for merge
groups. Extension-only changes are covered without a selective-CI sample label.

```powershell
node --test .github\extensions\open-decisions\tests.mjs .github\extensions\open-decisions\model-review.test.mjs .github\extensions\open-decisions\client-review.test.mjs .github\extensions\open-decisions\server-review.test.mjs
Get-ChildItem .github\extensions\open-decisions\*.mjs | ForEach-Object { node --check $_.FullName }
```

SDK canvas APIs are experimental. Verify discovery with extension list/inspect
after reload. Browser rendering, host-theme mirroring and screen-reader interaction
need a human visual/accessibility smoke test; automated tests are not a claim of
release accessibility conformance.
