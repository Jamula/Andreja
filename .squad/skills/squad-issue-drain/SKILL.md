# Squad Issue Drain

Use this skill when the user asks to work through the GitHub issue backlog,
activate an agent fleet, run Ralph across local/cloud sessions, or continuously
drive issues through draft PRs and Agent Merge.

Read `PROMPT.md` in this skill directory and follow it as the orchestration
contract.

Start with the fail-closed defaults:

- writer operation only with verified existing repository-scoped atomic
  ownership; otherwise read-only;
- waves of up to five App child issue sessions, capped by lower verified
  capacity;
- one issue, owner, session, branch/worktree, and PR;
- reserve each selected issue before creation and wait exactly 10 seconds
  between all spawn attempts without sleeping inside a turn;
- continue same-wave launches without waiting for individual ACKs, then require
  a correlated valid ACK from every successfully created child before release
  or another wave;
- stop a partial wave on failed/ambiguous creation, changed eligibility, lost
  capacity, or a closed safety gate, without replacing uncertain children;
- prefer eligible cloud work and fall back locally only after definitive
  non-creation;
- use Squad as the child agent when supported;
- report `READY_FOR_AGENT_MERGE` without activating it; the app owns landing; and
- archive owned sessions only after GitHub confirms merge.

Queue admission is fail-closed behind the built-in weekly retrospective protocol
in `PROMPT.md`. It uses runtime state tools and remains active when the optional
`retro-enforcement` component is missing.

Always render the ready/active/blocked issue tree before starting work. Reconcile
existing sessions, branches, worktrees, and PRs before every new admission.
