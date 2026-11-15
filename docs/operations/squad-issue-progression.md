# Squad issue progression

The existing workflows are an event-driven routing aid, not a fully unattended
factory. The checked-in `.squad/team.md` currently has an empty Members table
and `.squad/routing.md` has no routes. The approved roster is a target in
`squad-rebuild-prompt.md`, not an implemented team; complete that separately
under its recorded decisions. Both triage entry points now fail visibly when
there is no Lead or Architect member, rather than reporting an empty board.
They do not create or populate a roster.

1. **Intake and routing:** Once the roster and routing are configured, an
   issue labeled `squad` triggers deterministic keyword triage. The heartbeat
   also scans open `squad` issues on its configured events or manual dispatch;
   it is not scheduled. Label sync maintains the roster labels. Check Actions
   failures and the issue's routing comment; a `squad:*` label is not a GitHub
   assignee, an active session, or proof of ownership.
2. **Work handoff:** For a named member, start an authorized Copilot session
   and give it the issue. For Copilot, the triage workflows dispatch
   `squad-issue-assign.yml` only when `<!-- copilot-auto-assign: true -->`
   is explicitly configured. Otherwise an authorized operator can dispatch
   Squad Issue Assign with the issue number. The workflow requires
   `COPILOT_ASSIGN_TOKEN`; a successful assignment response is not proof that
   a session started. Verify a linked session or draft PR and investigate
   failures. Manually applied `squad:copilot` labels also trigger assignment.
   Do not enable automatic assignment, increase spending, or assume a
   configured token without the separate required authorization.
3. **Review and completion:** Track the PR and validation, obtain human review
   and applicable independent reviews and human decisions, and use only the
   authorized merge path. Routing does not bypass security, privacy, severity, admission, cost,
   or required checks. A comment, assignment, or PR alone is not evidence of
   completion. These workflows do not close issues or merge PRs.

The rebuild brief's cost, duplicate-work, issue-drain, and retrospective
requirements still apply. The workflows do not themselves enforce all of
those requirements; do not infer an unattended session, review, or approval
from their presence.
