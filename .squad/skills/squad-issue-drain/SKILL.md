# Squad Issue Drain

Use this skill when the user asks to work through the GitHub issue backlog,
activate an agent fleet, run Ralph across local/cloud sessions, or continuously
drive issues through draft PRs and Agent Merge.

Read `PROMPT.md` in this skill directory and follow it as the orchestration
contract.

Start with the MVP defaults:

- one active orchestrator per repository;
- four App child sessions unless the platform confirms more capacity;
- one issue, owner, session, branch/worktree, and PR;
- at least 30 seconds between spawn attempts;
- require the previous child ACK before admitting the next;
- prefer eligible cloud work and fall back locally only after definitive
  non-creation;
- use Squad as the child agent when supported;
- use Agent Merge to reach merge-ready, while the app performs the merge; and
- archive owned sessions only after GitHub confirms merge.

Always render the ready/active/blocked issue tree before starting work. Reconcile
existing sessions, branches, worktrees, and PRs before every new admission.
