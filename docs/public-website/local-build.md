# Unpublished local field guide (issue #93)

This implementation is a private local evaluation slice, **not public-site
activation** or acceptance of Proposed ADR 0008. It creates ordinary HTML, CSS,
JavaScript, metadata and integrity records independently of AppHost, identity,
PostgreSQL and every product-data plane. No cloud resources or paid tools are
used. Issue #93 remains open for the broader acceptance gates.

## Build, verify and review

From the repository root with the pinned .NET 10 SDK:

```powershell
dotnet build src\Andreja.PublicSite\Andreja.PublicSite.csproj --configuration Release
dotnet run --project src\Andreja.PublicSite --configuration Release --no-build -- build src\Andreja.PublicSite\Content src\Andreja.PublicSite\Content\local.config.json artifacts\public-site 2026-10-15
dotnet run --project src\Andreja.PublicSite --configuration Release --no-build -- verify artifacts\public-site
node scripts\public-website\local-preview.mjs artifacts\public-site
```

Visit `http://127.0.0.1:4173/`; stop the preview with Ctrl+C. The optional final
build argument is the UTC review/build date (`yyyy-MM-dd`); omitting it uses
today's UTC date. Rebuilds with identical sources, config and date have identical
bytes. An historical build date is for reproducing local artifacts, not extending
evidence: `verify` and the preview always recheck expiry against today's UTC date.
Expired artifacts cannot be previewed without re-reviewing the content.

Use a **new or empty dedicated output directory** for each build. The generator
never deletes or overwrites existing output. Do not serve the repository root,
the whole `docs` tree or generated files on a non-loopback interface.

The supplied preview adapter validates the artifact before listening, binds only
`127.0.0.1`, requires its exact Host header, accepts GET only, exposes only
manifest-listed files, and applies `headers.json` to every response. It checks each
file's hash again before serving it. It neither proxies nor calls the app. The
adapter rejects malformed request targets with 400, missing files with 404 and
changed bytes with 409. Unexpected artifact-read failures return 500 and emit
only a fixed diagnostic, never filesystem paths, request data or exception text.
Rejected requests do not terminate the preview. The fixed local port fails if
occupied; change the reviewed config to another
unprivileged port and build a new artifact. Microsoft Edge is used only by the
optional browser validator, not by the generator or preview.

The generated artifact does not enforce host security by itself. A generic file
server may ignore `headers.json`; use the supplied adapter for header evidence.
The HTML meta CSP is defense in depth and cannot enforce `frame-ancestors`.
HSTS is deliberately absent on local HTTP. `noindex`, robots and no-store do
**not** provide authorization. This adapter is not an approved private network
preview, production server or publishing mechanism.

## Configuration and publication boundary

`local.config.json` accepts exactly:

- `mode`: `local-unpublished`
- `baseUrl`: canonical `http://127.0.0.1:<1024-65535>/`, with no user info,
  path prefix, query or fragment
- `headerProfile`: `local-strict-v1`

Unknown JSON fields, all public/production/private-preview modes, domains,
wildcard binds and permissive header profiles fail closed before output is
written. There is intentionally **no approval boolean or activation switch**.
Any future publication implementation needs a separate reviewed authority and
must validate the applicable launch records; changing config cannot grant it.

Analytics, remote search, feedback submission, sponsorship, account forms,
product-data access and app routes are absent. Links remain inside this guide.
Canonical app/runbook source references are displayed as repository paths, not
copied wholesale or exposed as authenticated links.

## Authoring and stale-page checks

Author `src\Andreja.PublicSite\Content\site.json` as plain text sections, never
raw HTML. The generator encodes text; the search renderer uses text nodes.
Each page needs:

- unique safe slug, title and named owner;
- `local-v<number>` doc version and `en` locale at site level;
- last-reviewed and expiry dates (future expiry, at most 90 days from review);
- resolvable repository evidence under `docs` or `src`;
- related chapter slugs that exist in the same version;
- **empty** `claims`: this implementation cannot approve public claims.

The whole site is visibly local planning/repository guidance, not a public claim
source. The existing [claims inventory](claims-inventory.md) remains controlling
for future exact wording, owner, version, evidence hashes, reviewers, human
approval, expiry and withdrawal. Merely adding a claim ID or editing a date is
not review or approval. Public claims are rejected even when an ID exists.
Review material source changes immediately, not only at the scheduled expiry.

HTML and search are generated from the same metadata. Verification recomputes
page/index content, checks versions/dates, headers, file inventory, hashes, links
and fragment targets. Asset checks reject common network, persistence,
dynamic-code and raw-HTML insertion APIs; these checks are guardrails, not a
complete JavaScript sandbox or substitute for source review.

`manifest.json` records SHA-256 hashes for content, configuration, evidence,
generator source, SDK/build inputs and output files. It declares the framework
dependency and absence of extra package/project dependencies. This is a minimal
dependency inventory and reproducibility record, **not** a signed attestation,
full distribution SBOM, release certification or tamper-proof trust root. A
trusted reviewer must retain the manifest/hash externally to detect coordinated
replacement. The manifest does not hash itself. Build output contains no
absolute workstation paths, build timestamps beyond the requested date, task
records or copied repository documents.

## Security contact, support and deferred gates

No `/.well-known/security.txt` is emitted. There is no approved monitored
contact/domain to populate a valid file, and no RFC 9116 compliance is claimed.
`UNPUBLISHED.txt` and the security chapter record this disabled state rather than
inventing contact details or a response promise.

Existing decision coverage was checked across open **and closed** issues:

- [#93](https://github.com/Jamula/Andreja/issues/93): publication, hosting/budget,
  claims, accessibility, release, security contact and other site acceptance.
- [#3](https://github.com/Jamula/Andreja/issues/3) and
  [#6](https://github.com/Jamula/Andreja/issues/6): charter/public wording and
  licensing, name/domain/trademark authority.
- Closed [#94](https://github.com/Jamula/Andreja/issues/94): research/matrix
  evidence, not publication or ADR ratification.
- [#155](https://github.com/Jamula/Andreja/issues/155),
  [#156](https://github.com/Jamula/Andreja/issues/156),
  [#157](https://github.com/Jamula/Andreja/issues/157) and open
  [#158](https://github.com/Jamula/Andreja/issues/158): separately governed
  feedback packages/readiness. Closed status alone is not activation authority.

No duplicate decision issue is needed for this local slice. Default:
unpublished, no intake, no managed signup, no monitored-contact or conformance
claim. Public activation remains blocked until the accountable authorities
record the relevant decisions and exact-version evidence.

## Validation

```powershell
dotnet build tests\Andreja.PublicSiteTests\Andreja.PublicSiteTests.csproj --configuration Release
dotnet test --project tests\Andreja.PublicSiteTests\Andreja.PublicSiteTests.csproj --configuration Release --no-build
node --test scripts\public-website\local-preview.test.mjs
node scripts\public-website\validate-local-site.mjs artifacts\public-site
```

The isolated MSTest/MTP project references only the generator. Its mutations use
copies of the real content/config and generated artifacts under ignored
`artifacts`, never operating on secret files. Tests cover reproducibility,
content/search/version/link/header/inventory integrity, staleness, unsafe config,
public modes, source evidence, encoding, boundary violations and rehashed
artifact mutations. The browser validator uses the existing local Edge/CDP
approach with no npm packages: all chapters at 320/768/1280 px, keyboard skip,
search input, result navigation/Enter, Escape, live/no-result states, empty
cookies/storage, no cross-origin requests, and actual HTTP headers. It cleans
up its own browser/profile/server. These are local automated checks, **not**
manual screen-reader review, cross-browser coverage or WCAG 2.2 AA conformance.
The Node built-in adapter tests build their own real artifact under ignored
`artifacts` and clean it up. Raw HTTP requests cover malformed targets and server
liveness, wrong Host, POST, traversal/unlisted paths, hash mismatch, missing
files, a non-directory artifact root and an unexpected directory-read error.
The latter checks an empty 500 response and the exact safe diagnostic.
Run the adapter and browser validators sequentially: both use the reviewed port.

### Local execution evidence — 2026-10-15

Windows ARM64, pinned .NET SDK, local Edge only; no AppHost/database/cloud
service was started. Commands run from the repository root:

| Exact command | Result |
|---|---|
| `dotnet build tests\Andreja.PublicSiteTests\Andreja.PublicSiteTests.csproj --configuration Release --no-restore --nologo` | Passed; zero warnings/errors |
| `dotnet test --project tests\Andreja.PublicSiteTests\Andreja.PublicSiteTests.csproj --configuration Release --no-build` | Passed; 28/28, including real-source and emitted-artifact mutations |
| `dotnet run --project src\Andreja.PublicSite --configuration Release --no-build -- build src\Andreja.PublicSite\Content src\Andreja.PublicSite\Content\local.config.json artifacts\public-site-adapter-verified 2026-10-15` | Passed; 12 chapters emitted; integrity verification passed |
| `dotnet run --project src\Andreja.PublicSite --configuration Release --no-build -- verify artifacts\public-site-adapter-verified` | Passed; local-only boundary retained |
| `node --test scripts\public-website\local-preview.test.mjs` | Passed; 7 focused adapter cases (8/8 tests including parent), including malformed-target liveness and safe unexpected-read failure |
| `node scripts\public-website\validate-local-site.mjs artifacts\public-site-adapter-verified` | Passed; all 12 pages at 320/768/1280 px, keyboard skip/search/Tab/Enter/Escape, live/no-results, header and network/storage checks |
| `dotnet format src\Andreja.PublicSite\Andreja.PublicSite.csproj --verify-no-changes --no-restore` | Passed |
| `dotnet format tests\Andreja.PublicSiteTests\Andreja.PublicSiteTests.csproj --verify-no-changes --no-restore` | Passed |
| `python .github\scripts\check_docs_consistency.py` | Passed |
| `node --check scripts\public-website\local-preview.mjs` | Passed |
| `node --check scripts\public-website\local-preview.test.mjs` | Passed |
| `node --check scripts\public-website\validate-local-site.mjs` | Passed |
| `git diff --check` | Passed |

The first focused run caught an assertion mismatch in the invalid-slug mutation;
validation was made more specific to name the invalid page before the generic
missing-index check. The rerun passed. Two builds in the reproducibility test
are byte-identical across every emitted file. This is not external release
provenance, whole-product testing or launch approval. Manual assistive-technology
and WCAG 2.2 AA review, non-Edge coverage, approved deployment/security-contact
evidence and the broader #93 gates were not performed.
