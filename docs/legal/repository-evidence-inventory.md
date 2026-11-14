# Repository evidence inventory for issue #6

**Status:** Bounded factual inventory; not legal advice, provenance, clearance,
or authorization.

**Observed:** 2026-11-08; historical events are separately dated below.

**Source snapshot:** commit
[`7923b4d67f17f402c3e65d2c81dd1a981fa456e2`](https://github.com/Jamula/Andreja/tree/7923b4d67f17f402c3e65d2c81dd1a981fa456e2),
Git tree `a71ed745a9b8f7fc0b552afeb120ef7b8aa4d92b`. The worktree
documents added or changed after that commit are not part of this snapshot.

This is a partial, sanitized input to E1, E4, E5, and E9 of the
[decision packet](licensing-decision-packet.md#8-evidence-gap-register).
Declarations and present-day metadata alone do not establish actual
distribution or identify recipients. The separately cited Pages record is
positive evidence of the documented test responses, not a complete exposure
or recipient history. These observations do not establish authorship,
ownership, employer obligations, license compatibility, patent or trademark
clearance, or approval. No private agreements, prompts, recipient identities,
or counsel communications were inspected.

## 1. Source-path index (E5)

The immutable Git tree is the existing per-file path list; it is referenced
rather than duplicated as a second, easily stale list. The `git ls-tree`
commands below return tracked paths in each artifact class **at the source
snapshot**; the final loop counts non-merge history commits by class. Neither
establishes per-file creation or ownership.

| Class | Tree path | Tracked files | Non-merge path-history commits |
|---|---|---:|---:|
| Application and library source, contracts, and site inputs | [`src`](https://github.com/Jamula/Andreja/tree/7923b4d67f17f402c3e65d2c81dd1a981fa456e2/src) | 129 | 16 |
| Tests | [`tests`](https://github.com/Jamula/Andreja/tree/7923b4d67f17f402c3e65d2c81dd1a981fa456e2/tests) | 40 | 21 |
| Scripts and local evidence, including test-platform spikes | [`scripts`](https://github.com/Jamula/Andreja/tree/7923b4d67f17f402c3e65d2c81dd1a981fa456e2/scripts) | 37 | 19 |
| Documentation and architecture assets | [`docs`](https://github.com/Jamula/Andreja/tree/7923b4d67f17f402c3e65d2c81dd1a981fa456e2/docs) | 80 | 96 |
| Deployment configuration | [`deploy`](https://github.com/Jamula/Andreja/tree/7923b4d67f17f402c3e65d2c81dd1a981fa456e2/deploy) | 8 | 3 |
| GitHub automation, templates, and configuration | [`.github`](https://github.com/Jamula/Andreja/tree/7923b4d67f17f402c3e65d2c81dd1a981fa456e2/.github) | 63 | 77 |

```powershell
$revision = '7923b4d67f17f402c3e65d2c81dd1a981fa456e2'
git ls-tree -r --name-only $revision -- src
git ls-tree -r --name-only $revision -- tests
git ls-tree -r --name-only $revision -- scripts
git ls-tree -r --name-only $revision -- docs
git ls-tree -r --name-only $revision -- deploy
git ls-tree -r --name-only $revision -- .github
foreach ($path in @('src', 'tests', 'scripts', 'docs', 'deploy', '.github')) {
    $commits = @(git log --full-history --no-merges --format='%H' $revision -- $path)
    "${path}: $($commits.Count)"
}
```

The history counts exclude merges, can overlap between classes, and do not
count contributors. Directory walks do not follow renamed paths.
The root tree also contains `LICENSE`, `README.md`, `Directory.Build.props`,
`Directory.Packages.props`, `global.json`, `Dockerfile`, `compose.yaml`,
`supply-chain-policy.json`, and the solution file. A path or Git identity does
not establish who created, supplied, owns, or may license the material.
Generated, adapted, commissioned, vendor, and AI-assisted content cannot be
identified reliably from this list; AI involvement is **unknown** unless
separately established. The private provenance and chain-of-title work
remain open.

### Identity-free reachable-history anchors

The local checkout was not shallow and `git rev-list --count` reported 161
commits reachable from the snapshot. That does not establish that all
historical refs or external contributions are available. The following
path-limited results omit author and recipient identities:

| Example path | Earliest addition detected by the path walk | Latest detected change |
|---|---|---|
| `scripts/evidence/test-platform-spike/MSTestMtpSpike/MSTestMtpSpike.csproj` | `4aef94840c11719a607addebb56397ea2c390e7b` | `ea5bfabac36baf98bfa75032584a5c7a09f40e48` |
| `tests/Andreja.UnitTests/Andreja.UnitTests.csproj` | `509af4c989b3dfc1b8d4f1d1a1c524c064a43245` | `c1c366fd57ef704dcc5dee3eb323417592485beb` |

For either path, the following commands reproduce the identity-free index
at the snapshot; the oldest listed addition is the first table entry:

```powershell
$revision = '7923b4d67f17f402c3e65d2c81dd1a981fa456e2'
$path = 'scripts\evidence\test-platform-spike\MSTestMtpSpike\MSTestMtpSpike.csproj'
git log --follow --diff-filter=A --format='%H' $revision -- $path
git log -1 --format='%H' $revision -- $path
```

`--follow` uses rename heuristics, and a path-limited walk may miss other
refs, earlier names, copied material, or off-repository work. These are
navigation anchors, not proof of original creation, attribution, ownership,
authority to license, or receipt. A human/counsel-directed provenance and
chain-of-title assessment remains necessary.

## 2. Declared dependencies and public license metadata (E4)

The table records direct package declarations, not a resolved build or
distribution. Versions come from the snapshot's
[`Directory.Packages.props`](https://github.com/Jamula/Andreja/blob/7923b4d67f17f402c3e65d2c81dd1a981fa456e2/Directory.Packages.props),
[spike-specific `Directory.Packages.props`](https://github.com/Jamula/Andreja/blob/7923b4d67f17f402c3e65d2c81dd1a981fa456e2/scripts/evidence/test-platform-spike/Directory.Packages.props),
and project files. License values are declarations in the corresponding
public NuGet package manifests, retrieved on 2026-11-08; they are not counsel
opinions or a review of bundled material. The GitHub graph described below
reported the same expressions for the listed `PackageReference` packages.

| Package | Version | Declared project scope | NuGet-declared license expression |
|---|---|---|---|
| [Microsoft.AspNetCore.Identity.EntityFrameworkCore](https://api.nuget.org/v3-flatcontainer/microsoft.aspnetcore.identity.entityframeworkcore/10.0.14/microsoft.aspnetcore.identity.entityframeworkcore.nuspec) | 10.0.14 | Adapters | MIT |
| [Microsoft.EntityFrameworkCore.Design](https://api.nuget.org/v3-flatcontainer/microsoft.entityframeworkcore.design/10.0.14/microsoft.entityframeworkcore.design.nuspec) | 10.0.14 | Adapters | MIT |
| [Npgsql.EntityFrameworkCore.PostgreSQL](https://api.nuget.org/v3-flatcontainer/npgsql.entityframeworkcore.postgresql/10.0.3/npgsql.entityframeworkcore.postgresql.nuspec) | 10.0.3 | Adapters | PostgreSQL |
| [OpenTelemetry.Exporter.OpenTelemetryProtocol](https://api.nuget.org/v3-flatcontainer/opentelemetry.exporter.opentelemetryprotocol/1.20.1/opentelemetry.exporter.opentelemetryprotocol.nuspec) | 1.20.1 | Adapters | Apache-2.0 |
| [OpenTelemetry.Extensions.Hosting](https://api.nuget.org/v3-flatcontainer/opentelemetry.extensions.hosting/1.20.1/opentelemetry.extensions.hosting.nuspec) | 1.20.1 | Adapters | Apache-2.0 |
| [OpenTelemetry.Instrumentation.AspNetCore](https://api.nuget.org/v3-flatcontainer/opentelemetry.instrumentation.aspnetcore/1.20.2/opentelemetry.instrumentation.aspnetcore.nuspec) | 1.20.2 | Adapters | Apache-2.0 |
| [Microsoft.AspNetCore.Mvc.Testing](https://api.nuget.org/v3-flatcontainer/microsoft.aspnetcore.mvc.testing/10.0.14/microsoft.aspnetcore.mvc.testing.nuspec) | 10.0.14 | Unit tests | MIT |
| [Microsoft.Testing.Extensions.TrxReport](https://api.nuget.org/v3-flatcontainer/microsoft.testing.extensions.trxreport/2.5.2/microsoft.testing.extensions.trxreport.nuspec) | 2.5.2 | Test projects | MIT |
| [Microsoft.AspNetCore.Mvc.Testing](https://api.nuget.org/v3-flatcontainer/microsoft.aspnetcore.mvc.testing/10.0.11/microsoft.aspnetcore.mvc.testing.nuspec) | 10.0.11 | Test-platform spikes | MIT |
| [xunit.v3](https://api.nuget.org/v3-flatcontainer/xunit.v3/4.0.0/xunit.v3.nuspec) | 4.0.0 | Test-platform spike | Apache-2.0 |
| [MSTest.Sdk](https://api.nuget.org/v3-flatcontainer/mstest.sdk/4.5.1/mstest.sdk.nuspec) | 4.5.1 | Test project SDK | MIT |
| [MSTest.Sdk](https://api.nuget.org/v3-flatcontainer/mstest.sdk/4.3.3/mstest.sdk.nuspec) | 4.3.3 | Test-platform spike SDK | MIT |

The snapshot's [`global.json`](https://github.com/Jamula/Andreja/blob/7923b4d67f17f402c3e65d2c81dd1a981fa456e2/global.json)
pins .NET SDK `10.0.402`; SDK and framework components and their terms are not
completely inventoried by the table. The two MSTest SDK declarations above
were not found as name-and-version package nodes in the graph export; that
absence does not prove that a component is unused or undistributed.

### Existing dependency graph and unresolved material

The read-only
[GitHub dependency-graph SPDX export](https://api.github.com/repos/Jamula/Andreja/dependency-graph/sbom)
was available through `gh api repos/Jamula/Andreja/dependency-graph/sbom`.
One export created `2026-11-08T22:52:36Z` identified itself as SPDX 2.3 and
contained 208 package **nodes**: 197 NuGet, 10 GitHub Actions, and one
repository node. Nodes can repeat the same package/version, so this is not
a count of distinct dependencies. Its `licenseConcluded` fields reported
179 `MIT`, 15 `Apache-2.0`, two `PostgreSQL`, one
`LicenseRef-scancode-ms-net-library-2018-11`, and 11 absent values. These
are generator-reported metadata, not legal conclusions.

The 11 absent values were the 10 action nodes and the repository node, not
evidence that those components have no license. The separate pinned-action
metadata below supplies a starting point for the actions. The `LicenseRef`
was reported for `Microsoft.Testing.Extensions.CodeCoverage` `18.10.0`;
its [NuGet manifest](https://api.nuget.org/v3-flatcontainer/microsoft.testing.extensions.codecoverage/18.10.0/microsoft.testing.extensions.codecoverage.nuspec)
declares a license **file**, `License.txt`, rather than an SPDX expression.
The file in the corresponding
[package archive](https://api.nuget.org/v3-flatcontainer/microsoft.testing.extensions.codecoverage/18.10.0/microsoft.testing.extensions.codecoverage.18.10.0.nupkg)
was inspected in memory on 2026-11-08: it identifies Microsoft .NET Library
terms and includes provisions concerning installation/use, distributable
code, and third-party programs. Its 12,215 bytes hash as SHA-256
`b2a6b8b349a2b87d5b05ed687c4618f46052b90090352354b11bacb24f9bcb72`.
The provisions' applicability, compatibility, and any notice obligations
remain for human and qualified-counsel review; no legal conclusion is inferred.

The full graph response was not copied into this repository: it also carries
additional attribution/contact fields. The endpoint generates a new live
export, so a later result may differ; the counts above are a dated excerpt,
not a retained complete export. The graph can include automatically submitted
dependencies and does not establish that it describes the exact source
snapshot, a particular artifact, historical exposure, or every third-party
item. The [local website build guide](../public-website/local-build.md)
already describes its own narrower generated dependency manifest; it is not
a full distribution SBOM.

### CodeCoverage build-role evidence

- The snapshot's [MSTest spike project](https://github.com/Jamula/Andreja/blob/7923b4d67f17f402c3e65d2c81dd1a981fa456e2/scripts/evidence/test-platform-spike/MSTestMtpSpike/MSTestMtpSpike.csproj)
  sets `TestingExtensionsProfile=Default` and the CodeCoverage version
  property to `18.10.0`. Path-history inspection identifies commit
  `cf4960589bbf870aa732f9f628259985c56afc81` (commit timestamp 2026-08-29)
  as adding that property.
- The public [MSTest.Sdk 4.3.3 archive](https://api.nuget.org/v3-flatcontainer/mstest.sdk/4.3.3/mstest.sdk.4.3.3.nupkg)
  contains `Sdk/Runner/Common.targets`, which enables the extension for
  `Default` unless explicitly disabled, and `Sdk/Runner/ClassicEngine.targets`,
  which conditionally adds the package reference and version. The runner is
  selected when `UseVSTest=false`; the classic target is selected when
  `PublishAot` is unset or false. Other property values or imports can change
  the evaluated result.
- The four [test projects](https://github.com/Jamula/Andreja/tree/7923b4d67f17f402c3e65d2c81dd1a981fa456e2/tests)
  use `MSTest.Sdk/4.5.1` and set `TestingExtensionsProfile=None`. In that
  SDK's [public archive](https://api.nuget.org/v3-flatcontainer/mstest.sdk/4.5.1/mstest.sdk.4.5.1.nupkg),
  `None` does not itself auto-enable this extension; property overrides and
  other build inputs still require evaluation.
- The [2026-08-28 spike report](../research/test-platform-evidence-108.md)
  says the then-current `Default` profile resolved **18.9.0** while the
  spike was restored, built, and run. It is not evidence that the later
  `18.10.0` declaration was restored, loaded, or executed. A separate
  dependency-graph export at `2026-11-08T23:05:01Z` included an edge from the
  repository node to `18.10.0`, but did not identify its source project.
- The snapshot's [Dockerfile](https://github.com/Jamula/Andreja/blob/7923b4d67f17f402c3e65d2c81dd1a981fa456e2/Dockerfile)
  targets the AppHost project for publish. Its explicit `ProjectReference`
  path walk reaches five projects, none under `tests/` or the spike; this
  static observation does not evaluate dynamic imports or transitive packages.

The spike and four test projects had no `obj/project.assets.json` files in
this worktree when checked on 2026-11-08; that does not rule out builds
elsewhere. No current restore, evaluated assets, build output, package
execution, image contents, or distribution of this component in a particular
artifact was verified in this continuation. An SBOM node or edge is not such
proof.

### GitHub Actions and container inputs

The snapshot's workflow files pin the following action revisions. Public
GitHub license metadata queried at each pinned revision on 2026-11-08 reports
the indicated **root-repository** license. It does not inspect every bundled
or downloaded component or establish downstream notice obligations.

| Pinned action | GitHub-reported root license |
|---|---|
| [`actions/checkout@34e114876b0b11c390a56381ad16ebd13914f8d5`](https://github.com/actions/checkout/blob/34e114876b0b11c390a56381ad16ebd13914f8d5/LICENSE) | MIT |
| [`actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1`](https://github.com/actions/checkout/blob/3d3c42e5aac5ba805825da76410c181273ba90b1/LICENSE) | MIT |
| [`actions/download-artifact@9000827ccba6bdab643e8b6fd33ac0654aef8333`](https://github.com/actions/download-artifact/blob/9000827ccba6bdab643e8b6fd33ac0654aef8333/LICENSE) | MIT |
| [`actions/github-script@3a2844b7e9c422d3c10d287c895573f7108da1b3`](https://github.com/actions/github-script/blob/3a2844b7e9c422d3c10d287c895573f7108da1b3/LICENSE.md) | MIT |
| [`actions/setup-dotnet@0f4a354dd5547a28c70b7853dbd95d5b67886e73`](https://github.com/actions/setup-dotnet/blob/0f4a354dd5547a28c70b7853dbd95d5b67886e73/LICENSE) | MIT |
| [`actions/setup-node@949feb2413d6458794dcd2491c4babbbce0c15c1`](https://github.com/actions/setup-node/blob/949feb2413d6458794dcd2491c4babbbce0c15c1/LICENSE) | MIT |
| [`actions/setup-python@6d4fd22b20465ccc0f7354476588e8df7c9ca0e0`](https://github.com/actions/setup-python/blob/6d4fd22b20465ccc0f7354476588e8df7c9ca0e0/LICENSE) | MIT |
| [`actions/upload-artifact@cf430e030ddbb5b0abf93d22962f4752f3646cd9`](https://github.com/actions/upload-artifact/blob/cf430e030ddbb5b0abf93d22962f4752f3646cd9/LICENSE) | MIT |
| [`docker/setup-buildx-action@f87e5991a6d7451dcb8d9637bfbc97413f497069`](https://github.com/docker/setup-buildx-action/blob/f87e5991a6d7451dcb8d9637bfbc97413f497069/LICENSE) | Apache-2.0 |
| [`microsoft/psscriptanalyzer-action@6b2948b1944407914a58661c49941824d149734f`](https://github.com/microsoft/psscriptanalyzer-action/blob/6b2948b1944407914a58661c49941824d149734f/LICENSE) | MIT |

Other declared third-party surfaces include the .NET SDK and ASP.NET runtime
images in the snapshot's [`Dockerfile`](https://github.com/Jamula/Andreja/blob/7923b4d67f17f402c3e65d2c81dd1a981fa456e2/Dockerfile);
PostgreSQL, OpenTelemetry Collector, and Prometheus defaults in
[`compose.yaml`](https://github.com/Jamula/Andreja/blob/7923b4d67f17f402c3e65d2c81dd1a981fa456e2/compose.yaml);
a Caddy image in [`deploy/compose.evidence.yaml`](https://github.com/Jamula/Andreja/blob/7923b4d67f17f402c3e65d2c81dd1a981fa456e2/deploy/compose.evidence.yaml);
and Syft, Grype, Trivy, and Cosign images in
[`supply-chain-policy.json`](https://github.com/Jamula/Andreja/blob/7923b4d67f17f402c3e65d2c81dd1a981fa456e2/supply-chain-policy.json).
The files record pins and, where applicable, override inputs; they do not prove
that an image was built, run, published, or distributed. Image contents,
licenses, notices, and transitive dependencies have not been independently
inventoried here.

E4 remains open: the graph and direct declarations do not cover all
third-party, copied, adapted, generated, standard-derived, or commissioned
material. A human must reconcile exact artifacts, transitive licenses,
notices, the license-file review, provenance, and counsel review.

## 3. Public surfaces and historical evidence (E1/E9)

| Observation | Evidence and limit |
|---|---|
| Repository currently public; GitHub reports Apache-2.0 metadata. | Read-only GitHub repository API, 2026-11-08. This is a point-in-time technical observation, not proof of when visibility changed, a rights-holder grant, or an approval. |
| Current GitHub API reported `has_pages: false`, `forks_count: 0`, and `homepage: null`; release and tag lists visible to the caller were empty. | Read-only GitHub API, 2026-11-08. These values do not rule out prior Pages, deleted releases, earlier forks, other registries, clones, caches, or transfers. |
| A historical GitHub Pages exposure was already documented. | [Issue #114](https://github.com/Jamula/Andreja/issues/114) reports unauthenticated HTTP 200 responses for `/plan`, `/public-website/prototype/`, `/phase-1a/evidence-44`, and `/legal/regulatory-applicability` on 2026-08-25, reconfirmed at commit `1e43a50203784b4941079fa022e50d936a7e7a5a`; Pages was configured from `main:/docs`. The [plan's containment record at the source snapshot](https://github.com/Jamula/Andreja/blob/7923b4d67f17f402c3e65d2c81dd1a981fa456e2/docs/plan.md#phase-0-website-containment) reports the Pages deletion at `2026-08-25T15:55:36Z` and stable 404 responses for those four routes at `16:06:55Z`. This is a cited historical record, not a reconstruction from today's metadata. |
| Issues and pull-request templates, workflow definitions, and funding configuration exist in the source snapshot. GitHub reported issues enabled. | The [`.github` tree](https://github.com/Jamula/Andreja/tree/7923b4d67f17f402c3e65d2c81dd1a981fa456e2/.github) and 2026-11-08 repository API. Definitions do not establish actual submissions, workflow runs, payments, consent, or governance approval. |
| No tracked `CONTRIBUTING`, `CODEOWNERS`, `CODE_OF_CONDUCT`, or `NOTICE` file was found by filename in the snapshot. | A tree-path check only; it does not establish that off-repository agreements, notices within other files or packages, or platform enforcement do not exist. |

The Pages record does not establish particular recipients, other reachable
routes or versions, the history of repository visibility, or the existence or
scope of an Apache grant. Its containment record explicitly does not recall
previously downloaded bytes, caches, or provider logs. GitHub access history,
historical releases and packages, other registry activity, external copies,
and private approvals were not examined. Reconciliation of those facts and
their effect remains with Cyrus and qualified counsel, within the actual scope
and conditions of the privately verified outcome. Do not repeat the completed
private-reference verification or ask for its confidential details.
