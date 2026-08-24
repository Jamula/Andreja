# Personal semantic graph research and Phase 1A contract

- **Status:** Decision recommendation; pending Seven, Spock, T'Pol, Deanna Troi,
  Tuvok, Data, and Cyrus review
- **Prepared:** 2026-08-23
- **Tracking:** [GitHub issue #4](https://github.com/Jamula/Andreja/issues/4)
- **Governing sources:** [platform plan](plan.md),
  [company charter](charter.md), and
  [ADR 0000](adr/0000-plan-ratification.md)

This packet defines the smallest private, portable semantic model needed by the
Phase 1A walking skeleton. It does not amend the plan, select production
infrastructure, publish a vocabulary, authorize cloud resources, or authorize
new collection, inference, sharing, or federation.

## Decision summary

Adopt a **versioned JSON-LD export and projection contract over ordinary
relational domain records**. In Phase 1A:

- tasks, contacts, identities, and grants remain normal tenant-scoped domain
  records;
- a narrow `SemanticClaim` contract adds source-linked, reviewable meaning only
  where a user journey needs it;
- provenance is modeled with a small PROV-O-compatible subset;
- schema.org terms are reused when their meaning is a close match;
- custom terms use a versioned Andreja context and namespaced skill extensions;
- inferred claims are proposals and cannot silently replace asserted facts;
- embeddings and search indexes are disposable derivatives;
- export is JSON-LD 1.1, with deterministic N-Quads as an optional conformance
  fixture rather than the user-facing format; and
- graph databases, RDF stores, OWL reasoning, a universal ontology, automatic
  sensitive inference, live federation, and bitemporal architecture are
  deferred until measured journeys require them.

This is a reversible boundary: stable identifiers, explicit claim metadata, and
a standards-based export preserve future graph projections without imposing a
graph persistence model now.

## Representative journeys

### 1. Explain a task recommendation

Cyrus captures “renew vehicle registration” as a task and links the official
notice. Andreja may propose a due date from the notice. The review surface shows:

- the user-authored task assertion;
- the notice as the source;
- the extracted date as an observation;
- the proposed due date as an inference, with method and confidence;
- the purpose, “manage this task”; and
- accept, correct, reject, expire, and delete controls.

Acceptance creates a new user assertion that cites the proposal; it does not
rewrite the inference into an observation. Rejection prevents that exact
proposal from resurfacing unless new evidence or a changed method is disclosed.

### 2. Keep contact context private and bounded

Cyrus records that “Alex” is the contact for a home repair. The contact can be
related to the project without claiming Alex is an Andreja user, enriching the
record, merging it with public profiles, or inferring a friendship. The display
name and relationship are private, purpose-bound tenant data. Linking the contact
to an Andreja principal later requires verified identity and the sharing or
federation consent flow.

### 3. Correct a preference-derived suggestion

Cyrus asserts a preference for morning appointments. A scheduling skill proposes
a morning time and cites that preference. Cyrus can correct the preference,
reject the suggestion, or disable the scheduling-preference domain. The change
invalidates dependent suggestions and purges/rebuilds their search or embedding
entries.

### 4. Export and restore independently

Cyrus exports a versioned package containing domain records, semantic claims,
sources, consent/grant state, extension contexts, and a manifest with hashes.
Import into a clean self-hosted instance preserves stable internal identifiers,
unknown extension properties, timestamps, and provenance. It does not require
Andreja cloud, a graph database, or dereferencing a private vocabulary URL.

### 5. Prepare, but do not perform, consented sharing

A future travel skill asks for “calendar timing for trip planning,” not graph
access. The policy evaluator can identify the minimum eligible projection and
the required grant, purpose, disclosure level, and expiry. Phase 1A tests this
contract locally; it does not transmit data or create cross-tenant persistence.

## Smallest Phase 1A contract

Phase 1A needs one semantic **projection**, not a parallel source of truth.

### Required records

| Record | Minimum fields | Phase 1A rule |
| --- | --- | --- |
| `SemanticNode` | `id`, `tenantId`, `type`, `schemaVersion` | Stable GUIDv7 internal ID; a type is descriptive, never authorization. |
| `SemanticClaim` | `id`, `tenantId`, `subjectId`, `predicate`, typed `object`, `epistemicStatus`, `sourceIds`, provenance, time, sensitivity, purposes, review, sharing | Unit of review and deletion. Cross-tenant references are rejected. |
| `Source` | `id`, `tenantId`, `sourceType`, optional `artifactId`/`channelId`, `capturedAt`, integrity metadata | Store a reference and minimum evidence, not a duplicate payload. |
| `Grant` | resource/scope, grantee, purpose, disclosure, operations, expiry, revocation, consent reference | Contract/test fixture only where no Phase 1A sharing persistence is needed. |
| `ConsentRecord` | parties, notice/version, scope, purpose, status, offered/decided/expired/revoked times | Consent is evidence for a specific use, not a generic boolean. |
| `Derivation` | derived record/index ID, input claim IDs, method/version, created time | Makes invalidation and purge computable. |

`SemanticNode` may project an existing domain entity; it need not be a second
stored row. `SemanticClaim` is warranted only for source-linked or reviewable
statements. Ordinary task status, title, completion, and audit fields stay in the
task domain model.

### Claim value and metadata

A claim object is exactly one of:

- `nodeRef`: another stable node ID;
- `string`, with optional BCP 47 language;
- `boolean`;
- `integer` or `decimal`;
- RFC 3339 `dateTime`, date, or bounded interval; or
- a versioned structured value whose schema is declared by an approved
  extension.

Every claim carries:

- `epistemicStatus`: `observed`, `asserted`, or `inferred`;
- at least one `sourceId`, except a user assertion made directly in the current
  interaction, whose interaction receipt becomes its source;
- `recordedAt`, plus optional `observedAt`, `validFrom`, and `validTo`;
- `createdByPrincipalId` or a versioned producing skill/system agent;
- `sensitivity`: `ordinary`, `personal`, `sensitive`, or `highlyRestricted`;
- one or more controlled `purpose` values;
- `reviewState`: `unreviewed`, `accepted`, `corrected`, `rejected`, or `expired`;
- `sharingPolicy`: `private`, or a reference to explicit grants; and
- optional `confidence`, required for inferred claims.

Confidence is `{ value, method, methodVersion }`, where `value` is in `[0,1]`.
It describes a method's confidence, not objective truth. The UI must show a
plain-language explanation and the decisive sources; it must not imply that
scores from different methods are comparable unless calibration evidence says
so.

### Epistemic meaning

| Status | Meaning | Permitted transition |
| --- | --- | --- |
| `observed` | A source directly contained or emitted the value. It does not prove the value true. | Correction creates a new claim linked to the observation. |
| `asserted` | A named principal or authoritative domain workflow stated the value. | The asserter may correct, expire, or delete it. |
| `inferred` | A deterministic rule or model derived a hypothesis from cited inputs. | User acceptance creates a separate assertion; rejection never upgrades it. |

No inference engine may infer absence from missing data. RDF and OWL's open-world
semantics do not make application validation automatic, so input constraints
remain explicit application/schema rules.

### Illustrative export

The namespace below is deliberately non-resolving and provisional. A public,
stable namespace requires the licensing, trademark, governance, and publication
gates in the [license evaluation](legal/license-evaluation.md).

```json
{
  "@context": {
    "and": "https://andreja.invalid/ns/v1#",
    "prov": "http://www.w3.org/ns/prov#",
    "schema": "https://schema.org/",
    "id": "@id",
    "type": "@type"
  },
  "schemaVersion": "1.0-draft",
  "exportedAt": "2026-08-23T00:00:00Z",
  "@graph": [
    {
      "id": "urn:uuid:018f0000-0000-7000-8000-000000000001",
      "type": ["and:Task", "schema:Action"],
      "and:tenantId": "urn:uuid:018f0000-0000-7000-8000-000000000002"
    },
    {
      "id": "urn:uuid:018f0000-0000-7000-8000-000000000003",
      "type": "and:SemanticClaim",
      "and:subject": {
        "id": "urn:uuid:018f0000-0000-7000-8000-000000000001"
      },
      "and:predicate": "and:proposedDueAt",
      "and:value": {
        "@value": "2026-09-01T17:00:00-07:00",
        "@type": "http://www.w3.org/2001/XMLSchema#dateTime"
      },
      "and:epistemicStatus": "inferred",
      "and:confidence": {
        "and:value": 0.87,
        "and:method": "notice-date-extraction",
        "and:methodVersion": "1"
      },
      "and:purpose": ["task-management"],
      "and:sensitivity": "personal",
      "and:reviewState": "unreviewed",
      "and:sharingPolicy": "private",
      "prov:wasDerivedFrom": {
        "id": "urn:uuid:018f0000-0000-7000-8000-000000000004"
      },
      "prov:generatedAtTime": "2026-08-23T00:00:00Z"
    }
  ]
}
```

The production exporter must emit tenant-safe packages without a reusable
cross-tenant `tenantId`; import remaps ownership to the authenticated destination
tenant while preserving a package-local mapping. IDs are locators, not secrets
or capabilities.

## Minimal ontology

The ontology is deliberately small and compositional. It defines record meaning,
not every domain workflow.

### Core concepts

| Concept | Minimum meaning and boundary |
| --- | --- |
| `Identity` | A verified or claimed identifier for a principal; provider issuer/subject remains an external-identity mapping, never a domain foreign key. |
| `Contact` | A tenant-local representation of a person or organization; it does not imply an account, verified identity, relationship, or consent. |
| `Role` | A context-bounded capacity held by a principal/contact, such as owner or project contact; never grants access by label alone. |
| `Relationship` | A directed, time-bounded claim between nodes, with source and review state; labels such as family/friend are not authorization. |
| `Goal` | A user-defined desired outcome. |
| `Task` | An actionable unit in the task domain; semantic fields are projections or claims. |
| `Event` | Something occurring at a time or interval; ActivityStreams can inform future activity exchange but does not replace the domain event model. |
| `Artifact` | A referenced digital or physical work product; content remains in its owning domain/store. |
| `Preference` | A user assertion scoped to a domain, purpose, and optional validity interval; never ambient permission. |
| `Claim` | A reviewable subject-predicate-value statement with epistemic and governance metadata. |
| `Source` | The minimum reference/evidence from which a claim was observed, asserted, or inferred. |
| `Opportunity` | A reviewable proposal that a possible action may advance a user goal; never an eligibility, worth, or vulnerability score. |
| `Grant` | Revocable authorization over a resource/scope for a grantee, purpose, disclosure level, operations, and time. |
| `Consent` | A versioned record of an offered and expressed choice; it supports but does not by itself prove every legal or ethical requirement. |

`Principal`, `Organization`, `Project`, and `Place` may be used as supporting
node types. New concepts require a demonstrated journey; they are not added to
make the ontology appear complete.

### Initial predicates

The core vocabulary should start with:

- `hasIdentity`, `representsContact`, `holdsRoleIn`;
- `relatedTo`, with a required relationship kind rather than a new predicate for
  every social label;
- `supportsGoal`, `partOfProject`, `assignedTo`, `involvesContact`;
- `occursAt`, `startsAt`, `endsAt`, `referencesArtifact`;
- `hasPreference`, `hasSource`, `derivedFrom`;
- `proposesOpportunity`, `requiresGrant`, and `governedByConsent`.

Inverse predicates are computed in projections, not stored as independent facts.
No transitive social relationship, equivalence, identity merge, or sensitive
classification rule exists in the core.

## Standards matrix

| Standard or approach | Useful contribution | Limit or risk | Phase 1A disposition |
| --- | --- | --- | --- |
| [JSON-LD 1.1](https://www.w3.org/TR/json-ld11/) | JSON-compatible linked-data serialization, typed values, contexts, and stable IRIs | Context changes can alter meaning; remote context retrieval adds availability and substitution risk | **Adopt for export/projection.** Pin and embed the exact versioned context; never fetch a mutable context while importing. |
| [RDF 1.1 concepts](https://www.w3.org/TR/rdf11-concepts/) and [RDF Schema 1.1](https://www.w3.org/TR/rdf-schema/) | Portable graph model, named datasets, basic classes/properties | Open-world semantics, blank-node portability, and unconstrained triples do not enforce application rules | **Use as conceptual/export model.** Prefer stable IRIs and avoid blank nodes in portable records. |
| [OWL 2](https://www.w3.org/TR/owl2-overview/) | Formal ontology semantics and machine reasoning | Complexity and inferred sensitive facts can outgrow explainability; equality/transitivity rules are dangerous for identity and relationships | **Defer reasoning.** Reuse no OWL rule until a bounded journey and privacy test justify it. |
| [schema.org](https://schema.org/docs/schemas.html) | Broad shared vocabulary and downloadable JSON-LD/RDF forms | Publication-oriented breadth and changing pending terms do not define Andreja policy | **Selectively map** stable close matches such as Person, Organization, Event, Action, and CreativeWork; Andreja governance metadata stays local. |
| [PROV-O](https://www.w3.org/TR/prov-o/) | Entity/activity/agent and derivation/attribution vocabulary | Full qualified provenance is too large for the first slice and may retain deleted personal data | **Adopt a subset** for source, generation, attribution, and derivation; minimize provenance payloads. |
| [ActivityStreams 2.0](https://www.w3.org/TR/activitystreams-core/) | JSON/JSON-LD activity representation useful for future social/federated actions | An activity feed is not a personal knowledge model or authorization protocol | **Reference only** for future federation/event mappings. |
| [Solid Protocol](https://solidproject.org/TR/protocol) | User-controlled resources, HTTP access, and interoperable data-pod concepts | Pod/container topology and Solid authorization are not required for a local walking skeleton | **Preserve concepts**, especially user control and resource boundaries; defer protocol/server adoption. |
| [Data Privacy Vocabulary](https://www.w3.org/community/reports/dpvcg/CG-FINAL-dpv-20240801/) | Candidate vocabulary for purposes, processing, personal data, consent, and data subjects | Community Group report, not a W3C Standard; legal mappings require counsel and jurisdiction analysis | **Evaluate mappings**, do not make it a normative dependency. |
| Portable RDF formats: [N-Quads](https://www.w3.org/TR/n-quads/) and [TriG](https://www.w3.org/TR/trig/) | Standard dataset exchange; N-Quads is simple and diffable | Poor primary UX; RDF canonical equivalence is not byte equivalence | **Optional conformance fixtures.** JSON-LD remains the user export. Evaluate [RDF Dataset Canonicalization](https://www.w3.org/TR/rdf-canon/) only for signatures/deduplication. |
| Relational and JSON projections | Strong constraints/transactions plus flexible extension payloads; PostgreSQL supports [`jsonb`](https://www.postgresql.org/docs/current/datatype-json.html) and [recursive CTEs](https://www.postgresql.org/docs/current/queries-with.html) | Ad hoc JSON can evade constraints; deep traversal can become costly | **Adopt.** Normalize ownership, references, policy, and lifecycle; constrain extension JSON; measure representative queries. |
| Property/RDF graph databases | Native traversal or RDF/SPARQL can fit deep, irregular graph workloads | New operational store, dual-write/migration risk, authorization complexity, and premature lock-in | **Defer.** Reconsider only against recorded query/scale thresholds. |
| Embeddings/vector indexes | Semantic recall and similarity can help search or suggestions | Opaque similarity, inversion/membership risk, cross-domain leakage, model drift, and hard deletion | **Derived only and off by default in Phase 1A.** Keep model/version/input references, enforce domain/purpose partitions, and prove purge/rebuild. |

## Persistence and portability alternatives

### A. Relational source of truth plus semantic projections — recommended

Keep domain tables and tenant-aware constraints authoritative. Add narrow claim,
source, and derivation tables only when a journey needs semantic metadata. Build
JSON-LD at the API/export boundary. This best matches Phase 1A PostgreSQL,
transactions, deletion, isolation tests, and the task vertical slice.

### B. Generic node/edge/claim tables in PostgreSQL

This gives flexible traversal while retaining one store, but shifts domain
constraints into application code and encourages every field to become a triple.
Use only for claims that cannot remain ordinary domain fields, not as the whole
application model.

### C. JSON document source of truth

JSONB is useful for bounded extension payloads and round-tripping unknown
properties. It is not recommended for tenant ownership, grants, consent, task
state, or deletion dependencies that need enforceable relational constraints.

### D. RDF store or property graph database

These may become useful for standards-native SPARQL, highly variable schemas, or
deep/high-volume traversals. They add another authorization, backup, migration,
observability, and deletion boundary. Do not adopt one until a benchmark shows
that the relational projection fails an approved workload and that the benefit
outweighs these controls.

### E. Event-sourced or bitemporal semantic store

Full event sourcing or system-time plus valid-time history can reconstruct past
belief state, but increases erasure, correction, migration, and UI complexity.
Phase 1A records `recordedAt`, observation/validity bounds, revisions, and
derivation links. That is not a bitemporal architecture. Reconsider only when a
regulated audit, retroactive correction, or temporal query cannot be met by the
simple history model.

### Adoption evidence for a graph database

Open a new ADR only if all are true:

1. named, privacy-approved journeys require multi-hop or pattern queries that
   cannot be replaced by bounded projections;
2. a representative local data set and query suite shows the relational design
   misses an approved latency or maintainability threshold;
3. tenant/grant/purpose filtering is proven inside every traversal;
4. export/import, backup/restore, migration, deletion, and derived purge pass;
5. operating, support, licensing, and portability costs are measured; and
6. Spock, T'Pol, Deanna Troi, Tuvok, Data, and Cyrus accept the residual risk.

## Provenance, time, review, and sharing rules

1. Provenance is source-linked and minimal. It records who/what produced a claim,
   which inputs were used, when, and under which method/version. It must not copy
   an entire email, file, prompt, or model transcript.
2. Recorded time describes Andreja's record. Observed time describes the source
   event. Valid time describes when the claim applies. Unknown values stay
   unknown; the system does not manufacture precision.
3. Every inference has a dependency set. Changing, rejecting, expiring, or
   deleting an input invalidates dependent inferences and their derived indexes.
4. Sensitivity drives enforceable rules for model exposure, skill/channel access,
   logging, retention, export, and sharing. A label without policy tests is not a
   control.
5. Purposes are allow-listed, human-readable, and checked at use time. “Improve
   services,” “personalization,” and “AI” are too broad.
6. Private is the default. A relationship label, source access, skill capability,
   or successful query never implies permission to share.
7. A grant authorizes the intersection of tenant policy, grantee, resource/scope,
   purpose, disclosure level, operation, validity, and active consent.
8. Non-owner writes and machine inferences are proposals. Consequential actions
   always use the separate confirmation policy.

## User agency and lifecycle

The user can operate on one claim, a source and its dependants, a context domain,
or the whole account:

| Control | Required behavior |
| --- | --- |
| Inspect | Show value, status, source, producer/method, confidence, time, sensitivity, purpose, sharing, and material downstream uses in plain language. |
| Correct | Preserve the correction relationship, make the corrected assertion authoritative for supported uses, and invalidate dependants. Do not present the prior value as current. |
| Reject | Mark the inference unusable, record a bounded suppression key where necessary, explain downstream invalidation, and prevent silent re-creation from unchanged inputs/method. |
| Expire | Stop current use at `validTo` or immediately; retain only policy-approved history until its retention limit. |
| Export | Produce a documented, versioned, integrity-manifested JSON-LD package with sources, extensions, grants/consent, and deletion limitations. Export grants no new sharing rights. |
| Delete | Hard-delete or irreversibly disassociate the selected source/claim/domain, cascade or invalidate dependants, revoke related grants, and produce content-minimized completion evidence. |

### Derived-index purge contract

Every derived store registers `indexKind`, `indexVersion`, `inputClaimIds`,
`purpose`, `partition`, `createdAt`, and a purge adapter. Deletion, correction,
expiry, domain disablement, consent withdrawal, or grant revocation emits a
transactionally recorded invalidation job. Reads must exclude invalidated inputs
immediately even if physical purge is asynchronous. The job:

1. removes vector/search/cache/materialized-view entries;
2. rebuilds aggregates or embeddings only from still-authorized inputs;
3. covers replicas, queues, dead letters, test fixtures, and exports under their
   approved retention/backup rules;
4. records content-free completion/failure evidence; and
5. retries visibly and blocks a successful deletion claim until required stores
   report completion.

Model-provider retention is not an index purge. Sending semantic content to any
external model requires a separate provider policy and informed opt-in.

## Non-user subjects and shadow-profile controls

A `Contact` may describe a person who is not a user. That person does not become
a `Principal`, profile, prospect, or federation peer merely because their data is
present in another user's context.

- Collect the minimum label and relationship needed for the user's stated
  purpose; prefer aliases or local descriptions where a legal name is needless.
- Do not scrape, purchase, enrich, face-match, infer identity, merge contacts
  across tenants, or generate a cross-user identifier.
- Do not infer a non-user's health, finances, sexuality, religion, politics,
  immigration status, vulnerability, relationship quality, location pattern, or
  other sensitive trait.
- Do not expose one user's description of a person to that person or another
  tenant without a reviewed purpose, minimum disclosure, and consent/legal
  basis. A user-authored label is not an objective fact.
- Keep contact search/embeddings tenant-, domain-, sensitivity-, and
  purpose-partitioned. No global “people graph” or similarity service.
- Before Relationships/Communities or public onboarding, define non-user notice,
  access, correction, objection, erasure, conflict, safety, and identity-proof
  workflows with Deanna Troi, Sarek, Tuvok, and Cyrus.

Content-minimized suppression records may prevent re-ingestion after a justified
objection or deletion, but cannot become a hidden dossier. Their identifier,
purpose, derivation, access, retention, and appeal path require a privacy
decision.

## Sensitive inference controls

Phase 1A permits no automatic sensitive-trait inference. Self-declared sensitive
data remains sensitive and does not authorize secondary inference.

- The default inference denylist includes health, disability, biometrics,
  finances, exact location/routines, sexuality, relationship quality, religion,
  politics, ethnicity, immigration, addiction, distress, and vulnerability.
- Joining benign claims across domains is a new processing purpose, not a free
  query. Cross-domain inference requires an approved privacy artifact, user-
  initiated purpose, minimum inputs, an explanation, and a reversible review
  surface.
- Models and skills receive access-scoped projections, never raw graph traversal.
  Inputs from skills, channels, peers, and artifacts are untrusted data, not
  instructions.
- Opportunity matching cannot score personal worth, exploit vulnerability,
  determine protected-class eligibility, or optimize engagement. It proposes an
  option tied to a user-defined goal and discloses decisive evidence and gaps.
- Rejected inferences are excluded from downstream prompts, training/evaluation
  corpora, recommendations, and analytics.

## Skills, channels, and federation

### Skill extensions

Each extension declares in its signed/versioned manifest:

- a stable publisher and skill ID, semantic version, minimum platform version,
  and namespace;
- introduced types, predicates, value schemas, mappings, and migration rules;
- read/propose capabilities, purposes, sensitivities, retention, model/network
  use, and derived indexes;
- export/import behavior and a lossless unknown-extension round-trip fixture; and
- removal behavior, including orphaned data and purge.

Extensions cannot redefine core terms, infer permission from type, or request
“read graph.” A capability resembles `read:semantic:task-management` or
`propose:semantic:preference`, is intersected with current grants/purpose, and
returns a minimum access-scoped projection.

### Channel ingestion

A channel creates a `Source` and proposes typed observations/claims through an
application-owned contract. Connector-native IDs and payloads stay in the
adapter/source boundary. The channel cannot merge identities, promote an
observation to fact, widen purpose, or retain content beyond its declared policy.

### Federation mapping

The local schema reserves stable IDs, schema versions, provenance, proposals,
grants, consent, and extension namespaces so a later protocol can map them.
ActivityStreams and Solid are reference designs, not selected federation
protocols. Phase 1A exports locally and runs contract tests only. Exportability
does not imply discoverability, publication, remote dereferencing, or consent to
transmit.

## Threat and privacy questions

| Question | Required control or evidence before implementation |
| --- | --- |
| Can an ID, count, error, timing difference, or traversal reveal another tenant/person? | Tenant-aware database constraints, access-scoped projections, non-enumerable IDs, uniform denials, and two-tenant negative tests. |
| Can a skill/channel/peer inject instructions or escalate graph scope? | Treat payload as data; schema/size validation; capability-purpose intersection; no raw store, token, secret, or unrestricted query access. |
| Can provenance become a second copy of deleted source content? | Reference/minimize evidence, prohibit raw prompt/payload duplication, classify provenance, and test cascade/purge. |
| Can mutable contexts or extensions change imported meaning? | Embedded/pinned contexts, digest manifest, allow-listed versions, no import-time network dereference, and explicit migration. |
| Can identity equivalence merge different people? | No email-only linking, no automatic `sameAs`, verified link workflow, collision handling, and reversible proposals. |
| Can graph traversal reveal sensitive traits or social structure? | No unrestricted traversal; domain/sensitivity/purpose partitions; denylisted inferences; explanation and review tests. |
| Can embeddings leak deleted or unauthorized content? | Per-purpose partitions, input lineage, immediate logical exclusion, physical purge/rebuild, deletion probes, and no global vectors. |
| Can a stale grant, consent, cache, export, or backup preserve access? | Revocation-at-read, expiry, purge adapters, export warnings, backup retention/restore deletion replay, and exercised evidence. |
| Can an attacker infer that a non-user has a profile? | No cross-tenant/global contact lookup; uniform responses; no public resolution endpoint or cross-user identifier. |
| Can correction/rejection histories harm the user after deletion? | Content-minimized history, bounded retention, hard deletion, and no immutable personal-data audit claim. |
| Can low confidence look authoritative? | Status-first UI, method and source explanation, calibrated wording, no unsupported numeric comparison, and user acceptance as a separate assertion. |
| Can export/import widen access or execute content? | Data-only import, schema limits, no remote context fetching, destination ownership remap, grants inactive by default, and malicious package tests. |

## Validation and decision gates

### Phase 1A conformance evidence

1. A task assertion and one inferred proposal round-trip through JSON-LD without
   losing ID, type, source, status, time, confidence, sensitivity, purpose, or
   review state.
2. Unknown namespaced extension data round-trips unchanged but remains inert.
3. Import performs no network fetch and activates no grants.
4. Two-tenant tests reject cross-tenant node, claim, source, derivation, and
   grant references at both application and database boundaries.
5. Correct/reject/expire/delete invalidates downstream claims and immediately
   excludes them from all supported reads.
6. Delete and consent/grant revocation purge each registered derived store and
   survive backup/restore verification.
7. Sensitive inference, cross-domain access, unrestricted skill traversal, and
   non-user identity merging fail closed.
8. The deterministic fake inference provider produces reproducible lineage;
   external models are not required.
9. Export/import works between clean instances using only the portable package
   and documented schema version.
10. Accessibility tests cover provenance explanation and every review/lifecycle
    control.

### Open decisions

- Exact internal table boundaries and whether the narrow claim set warrants a
  generic claim table or typed domain-owned tables.
- Controlled purpose and sensitivity vocabularies, retention schedules, and
  legal/ethical basis per data class.
- Public vocabulary namespace, governance, compatibility, and licensing after
  publication gates.
- Canonicalization/signature needs for future federation.
- Numeric graph-query thresholds and representative scale corpus.
- Whether any future embedding use passes privacy, deletion, quality, local
  resource, and provider-retention evidence.

### Recommendation for approval

Approve the relational-source-of-truth plus JSON-LD-projection boundary for
Phase 1A, subject to the conformance evidence above. Record graph database and
bitemporal adoption as explicit deferrals, not architectural promises. Preserve
the following non-negotiable invariants:

1. tenant/user ownership and authorization are outside graph semantics;
2. observed, asserted, and inferred claims remain distinguishable;
3. every inference is source-linked, purpose-bound, reviewable, and disposable;
4. private is the default, and export never means publish;
5. non-users are not silently profiled;
6. sensitive inference is denied unless a later, explicit gate authorizes a
   bounded workflow;
7. users can inspect, correct, reject, expire, export, and delete; and
8. revocation/deletion reaches every derived index.

This choice should be rejected if Phase 1A cannot prove tenant-safe deletion and
lossless portable round-trip without a second database.

## Review responsibilities

| Reviewer | Approval question |
| --- | --- |
| Seven of Nine | Is the contract sufficient for the task skill, bounded extensions, and future federation without ambient graph access? |
| Spock | Are portability seams real and complexity deferrals explicit? |
| T'Pol | Can relational constraints enforce ownership, references, lifecycle, and migrations without contaminating domain models? |
| Deanna Troi | Are purpose, non-user subjects, sensitive inference, review, revocation, and deletion understandable and effective? |
| Tuvok | Do authorization, import, extension, inference, enumeration, and purge boundaries fail closed? |
| Data | Are round-trip, provenance, isolation, correction, purge, and adverse-input claims reproducible? |
| Cyrus | Do the user outcome, residual risk, scope, and deferrals justify entering Phase 1A? |

## Authoritative sources

Sources were accessed 2026-08-23.

- [W3C JSON-LD 1.1 Recommendation](https://www.w3.org/TR/json-ld11/)
- [W3C RDF 1.1 Concepts and Abstract Syntax](https://www.w3.org/TR/rdf11-concepts/)
- [W3C RDF Schema 1.1](https://www.w3.org/TR/rdf-schema/)
- [W3C OWL 2 Overview](https://www.w3.org/TR/owl2-overview/)
- [W3C PROV-O](https://www.w3.org/TR/prov-o/)
- [W3C ActivityStreams 2.0](https://www.w3.org/TR/activitystreams-core/)
- [Solid Protocol](https://solidproject.org/TR/protocol)
- [schema.org schemas and extension model](https://schema.org/docs/schemas.html)
- [W3C N-Quads](https://www.w3.org/TR/n-quads/)
- [W3C TriG](https://www.w3.org/TR/trig/)
- [W3C RDF Dataset Canonicalization](https://www.w3.org/TR/rdf-canon/)
- [W3C Community Group Data Privacy Vocabulary 2.0](https://www.w3.org/community/reports/dpvcg/CG-FINAL-dpv-20240801/)
- [RFC 3339: Date and Time on the Internet](https://www.rfc-editor.org/rfc/rfc3339.html)
- [PostgreSQL JSON types](https://www.postgresql.org/docs/current/datatype-json.html)
- [PostgreSQL recursive queries](https://www.postgresql.org/docs/current/queries-with.html)
- [NIST AI Risk Management Framework](https://www.nist.gov/itl/ai-risk-management-framework)
