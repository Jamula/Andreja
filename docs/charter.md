# Andreja company charter

- **Status:** Proposed for explicit ratification; not yet authoritative
- **Final human authority:** Cyrus Jamula
- **Tracking:** [GitHub issue #3](https://github.com/Jamula/Andreja/issues/3)
- **Governing context:** [Andreja platform plan](plan.md) and
  [ADR 0000](adr/0000-plan-ratification.md)

This charter governs Andreja's company, platform, products, brand, marketplace,
Customer Zero operations, staff, contributors, agents, sponsors, and partners.
It becomes effective and is the authoritative charter only when Cyrus explicitly
ratifies it. Until then, the `docs/plan.md` charter section is the seed summary;
after ratification, that summary will be reconciled with this authoritative file
in the next plan amendment.

## Mission

Empower people to understand and improve their lives through a trustworthy,
user-owned assistant and capability ecosystem that turns context into
meaningful, consented action.

## Commitments

1. **Human agency.** People set their own goals, control their data, understand
   material recommendations, approve consequential actions, correct mistakes,
   and can leave with their information.
2. **Respect and inclusion.** We design for varied abilities, cultures,
   families, identities, resources, languages, and technical comfort. We treat
   every person affected by Andreja with dignity, including people who are not
   direct users.
3. **Integrity and accountability.** We distinguish fact, inference,
   recommendation, and decision. We communicate capabilities, uncertainty,
   evidence, cost, sponsorship, incidents, and limits truthfully; own outcomes;
   and correct harm.
4. **Growth mindset.** We expect people and teams to learn. We reward curiosity,
   evidence-seeking, mentorship, careful experimentation, useful feedback, and
   learning from mistakes rather than hiding them.
5. **Responsible human-AI collaboration.** AI supports human judgment; it does
   not replace human authority or accountability. AI work must address
   transparency, fairness, privacy, security, safety, accessibility, and effects
   on people and society.
6. **Ethical optimization.** We optimize for user-defined wellbeing and
   meaningful outcomes, never addiction, manipulation, covert profiling,
   discriminatory exclusion, or engagement at any cost.
7. **Data dignity.** We collect the minimum necessary data, use it only for
   stated purposes, protect sensitive context, avoid shadow profiles, preserve
   provenance, and make access, correction, export, deletion, and revocation
   effective.
8. **Sustainable stewardship.** We pursue financially durable operations,
   efficient compute and model use, maintainable systems, responsible vendor
   choices, supportable commitments, and reduced environmental waste.
9. **Broad responsibility.** We consider users, affected non-users,
   collaborators and families, staff and contributors, publishers, providers,
   communities, and future society as Andreja scales.
10. **Protected dissent.** Anyone may challenge a decision with evidence, raise
    a safety or ethics concern, or stop work that appears to cross a gate.
    Approved work proceeds cohesively only after dissent and residual risk are
    recorded rather than concealed.

## Operating culture

Andreja turns these commitments into daily behavior:

- Start with the person and the meaningful outcome, not a feature or growth
  target.
- Take end-to-end ownership of effects, including failure, support, cost,
  accessibility, and exit.
- Prefer the smallest complete and responsible solution. Speed is useful only
  inside the trust boundary.
- Use durable evidence, seek disconfirming facts, and revise decisions when the
  evidence changes.
- Maintain high standards without blaming people for surfacing defects,
  uncertainty, or harm.
- Build for inclusion from the beginning rather than treating it as a later
  accommodation.
- Invest in learning and long-term trust even when a short-term metric would
  reward a weaker choice.
- Challenge respectfully, record unresolved risk, and never manufacture
  consensus.

### Source and attribution posture

This culture adapts broad ideas associated with
[Amazon's Leadership
Principles](https://www.amazon.jobs/content/en/our-workplace/leadership-principles),
such as customer focus, ownership, learning, high standards, and long-term
responsibility, and with
[Microsoft's mission](https://www.microsoft.com/en-us/about),
[culture](https://careers.microsoft.com/v2/global/en/culture), and
[AI principles and
approach](https://www.microsoft.com/en-us/ai/principles-and-approach), such as
growth mindset, empowerment, inclusion, transparency, and accountability.

Andreja expresses those influences in original language and adds its own
requirements for user ownership, data dignity, consent, portability, ethical
optimization, sustainability, and protected dissent. Amazon and Microsoft have
not sponsored, approved, or endorsed Andreja. This attribution is private-facing
until Sarek reviews whether the wording, source links, and trademark posture are
appropriate for any public culture page.

## Human and agent authority

Cyrus is Andreja's final human decision-maker and remains accountable for
mission, policy, risk acceptance, capital, launches, partnerships, and public
claims. Specialist review and required evidence gates cannot be replaced by an
agent recommendation.

Agents may research, draft, challenge, test, and execute explicitly authorized,
reversible repository work. Agent names and roles are operating aids, not
officers or legal persons. Agents cannot:

- spend or commit funds;
- sign contracts, accept binding terms, or make legal representations;
- publish public statements, product claims, or customer communications;
- provision cloud resources or approve deployment;
- approve their own security, privacy, responsible-AI, evidence, or launch
  gates; or
- make irreversible or consequential decisions for Cyrus or any user.

When authority is unclear, work pauses for human decision.

## Customer Zero

Cyrus and Andreja company operations are Customer Zero. Dogfooding must reveal
outcomes, pain, privacy and security risk, support burden, cost, and developer
ergonomics; it does not by itself prove broad demand.

Customer Zero data and privileges stay separate from reusable product
contracts. Internal access, workarounds, and bypasses cannot become hidden
advantages unavailable through supported user and marketplace contracts. Pain
points become evidence, workarounds become debt, and generalization requires
review beyond Customer Zero.

## Decision and launch enforcement

Every major ADR, launch gate, sponsorship or partner decision, and public claim
must include the impact assessment below. Missing evidence blocks approval.
Tuvok, Deanna Troi, Quark, Sarek, Data, and Rai may challenge work in their
domains; Cyrus makes the final human decision after required challenges and
residual risks are recorded.

### Ethics and sustainability impact assessment

1. **People and agency:** Who benefits, who may be affected, and what meaningful
   outcome, control, review, correction, and exit exist?
2. **Data and consent:** What data, inference, purpose, retention, sharing,
   model exposure, and deletion or revocation path are involved?
3. **Equity and accessibility:** Which abilities, identities, cultures,
   languages, resources, or technical-comfort levels may be excluded or harmed?
4. **AI and safety:** Where is AI used, how are uncertainty and provenance
   shown, what failure or misuse is plausible, and where is human confirmation
   required?
5. **Sustainability:** What compute, model, cloud, vendor, maintenance, support,
   financial, and environmental costs arise across the lifecycle?
6. **Stakeholders and incentives:** Do growth, sponsor, partner, or marketplace
   incentives conflict with users or affected communities?
7. **Evidence and alternatives:** What evidence supports the choice, what could
   disprove it, and what lower-risk or lower-resource alternative was assessed?
8. **Owner and stop conditions:** Who owns the outcome, which indicators will be
   reviewed, what residual risk remains, and what triggers pause, rollback,
   remediation, or exit?

## Measurable indicators

Before general availability, every indicator area below must have a named human
owner, definition, baseline, target, cadence, evidence source, and stop or
remediation threshold. Targets must not be presented as achieved until measured.

| Area | Minimum evidence | Review gate |
| --- | --- | --- |
| Human agency and user control | Consequential-action confirmation coverage; explanation, correction, undo, export, deletion, and revocation success and failure rates | Each release and launch |
| Accessibility and inclusion | Automated and human accessibility results, supported-path conformance, exception owners and due dates, and exclusion findings from representative users | Each release and before GA |
| Privacy and data dignity | Data inventory coverage; minimization exceptions; access, retention, export, deletion, and revocation tests; incident count and remediation time | Each release and incident review |
| Security and safety | Threat-control coverage, isolation and abuse-path tests, vulnerability age, incident severity, containment time, and verified corrective action | Each release and incident review |
| Product truth and quality | Claim-to-evidence coverage, uncertainty labels, correction/undo/reopen rates, escaped defects, and validated outcome measures | Each claim and release |
| Support and accountability | Acknowledgement and resolution time, recurrence, reporter follow-through, remediation verification, and aggregate non-retaliation review without exposing reporters | Monthly once operating |
| AI and compute efficiency | Model and compute use per successful outcome, failed or discarded work, latency, cost, fallback use, and quality-adjusted efficiency | Each model/provider change |
| Financial and operational sustainability | Burn, runway, unit cost, support load, maintenance burden, recovery evidence, and commitments due | Monthly once operating |
| Vendor and sponsor independence | Provider concentration, portability and exit-test results, sponsor conflicts, unresolved dependencies, and time to exercise an exit | Quarterly and each material decision |
| Environmental stewardship | Workload and storage volume, avoidable recomputation, lifecycle resource proxies, and documented reduction actions | Quarterly once operating |

The measurement system must minimize personal data. Aggregate evidence cannot be
used to conceal severe individual harm, and a favorable average cannot waive a
failed safety, privacy, accessibility, or consent gate.

## Reporting, non-retaliation, and remediation

Andreja will provide a confidential reporting path for staff, contributors,
users, publishers, partners, and other affected people. Access is limited to
Cyrus and an independent reviewer when needed; reporters are told the practical
limits of confidentiality. Conflicts of interest are rerouted. Anonymous or
pseudonymous reporting will be supported when a safe, sustainable mechanism is
available.

Retaliation for a good-faith report, refusal to cross a gate, participation in
an investigation, or evidence-based dissent is prohibited. Performance,
access, contribution, support, commercial, or community decisions must not be
used to punish protected reporting.

Reported concerns follow a documented lifecycle:

1. acknowledge and preserve relevant evidence without unnecessary disclosure;
2. assess urgency, affected people, conflicts, and the need to pause or contain;
3. investigate with appropriate independent expertise;
4. remedy harm, correct records or claims, and support affected people;
5. verify the remedy and monitor recurrence;
6. disclose material facts to affected parties or authorities when required and
   authorized; and
7. record aggregate lessons, ownership, and prevention work without exposing
   reporters or private content.

Responses may include blocking a launch, limiting or removing access, reverting
a change, correcting a public claim, repairing or deleting data, changing a
process or vendor, ending a sponsor or partner relationship, and escalating to
qualified counsel or other human experts.

## Precedence, ratification, and amendment

This charter outranks growth, launch dates, sponsor or partner requests, cost
savings, convenience, and agent recommendations. No revenue, schedule, or
competitive argument waives a safety, privacy, consent, accessibility, evidence,
or human-authority gate.

Cyrus alone ratifies this charter and material amendments. Changes to mission,
human authority, stakeholder rights, reporting or non-retaliation, enforcement,
precedence, or public attribution require a tracked issue and reviewed
documentation pull request. Editorial clarifications may use the same process
without re-ratification when they do not change meaning.

Public publication remains blocked until Sarek reviews attribution, trademark,
reporting, and legal posture. That review informs publication language; it does
not weaken the commitments in this private-facing charter.

Any repository visibility change requires renewed Sarek review of official-source
attribution, trademark posture, internal Star Trek codenames, and public-culture
wording before the visibility change or publication proceeds.
