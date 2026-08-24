# Application export and import contract

Andreja application portability is separate from PostgreSQL recovery. Version 1 is
defined by
[`application-export-v1.schema.json`](application-export-v1.schema.json) and the
framework-neutral types in
`src/Andreja.Platform.Contracts/Portability/ApplicationExportContract.cs`.

## Archive layout

An export is an operator-encrypted archive containing `manifest.json` plus only the
relative artifact paths declared by that manifest. Producers serialize the
manifest as UTF-8 JSON with camel-case property names and string enum values.
Artifact checksums are lowercase SHA-256 of the exact stored bytes. Paths use `/`,
are case-insensitively unique and relative, and may not contain `\`, `:`, `.`, or
`..` segments.

Every manifest identifies archive/schema/application versions, export ID, UTC
creation time, opaque tenant ownership, byte/record counts, checksums, exclusions,
and provider reauthorization steps. Even an empty area has a zero-record artifact,
so these six areas are explicit and inspectable:

- records;
- attachments and their metadata;
- grants (imported inactive);
- content-minimized audit history;
- non-secret settings; and
- proposal/source provenance.

The manifest must explicitly report exclusion of credentials, passkeys, recovery
secrets, provider access/refresh/BYOK tokens, Data Protection or other host keys,
and caches. Provider runtime handles, hidden metadata, leases, subscriptions,
transient streams, unsupported derived data, and already-deleted/non-retained
content are also excluded when applicable. Exclusion is not silent data loss.

## Export sequence

1. Authorize the export in the application and select the intended tenant/data.
2. Materialize a consistent snapshot without logging record content.
3. Write each versioned artifact, then its length/count and SHA-256 to the manifest.
4. Emit all exclusions and required channel/provider reauthorization actions.
5. Validate the JSON schema and reopen every artifact through
   `ApplicationExportVerifier`.
6. Encrypt the completed archive to a user-controlled destination and remove
   plaintext staging data.

The production exporter must not create inactive channel, grant, or federation
persistence merely to fill the contract. Approved conformance fixtures may use
empty artifacts.

## Dry-run and clean-instance import

Import is deny-by-default:

1. Decrypt into a least-privilege staging location.
2. Parse `manifest.json` with duplicate/unknown-property rejection and validate the
   v1 schema.
3. Run `ApplicationExportVerifier.ValidateAsync`. It rejects unsupported versions,
   missing areas/exclusions, path traversal/duplicates, invalid lengths/checksums,
   and any target for which `ICleanInstanceImportProbe` does not prove clean.
4. Produce a dry-run report with tenant/identity mapping, conflicts, exclusions,
   counts, inactive grants, and reauthorization actions. Perform no writes.
5. Obtain explicit operator approval of mapping/conflict policy, then commit once
   under the export/import ID. The future importer must make retry idempotent.
6. Restore identity and host keys only through their separate recovery procedure.
   Reauthorize every provider/channel; never recreate a credential from the
   archive.
7. Keep imported grants inactive until principals resolve and current bilateral
   consent is revalidated.
8. Compare counts, attachments, audit continuity, provenance, settings, exclusions,
   user-visible records, and delete behavior on the clean instance.

A valid archive does not authorize overwriting an existing instance. The clean
probe is an explicit composition seam so the persistence slice can implement the
authoritative check later without coupling the portability contract to PostgreSQL.
Import writing/domain behavior is intentionally not implemented by this operations
layer.

## Compatibility and security

Archive v1 acceptance is exact. A future reader may add an explicit compatibility
window only after human review; it must never guess how to reinterpret an unknown
schema. Verify release provenance before parsing, bound archive/file/count sizes,
avoid following links, and keep extraction beneath staging. Checksums detect
corruption but do not authenticate the exporter, so distribution signature policy
is a separate operator decision.

Application exports contain user data and audit history. Protect them as sensitive
data, never attach them to issues or telemetry, and test with synthetic fixtures
only.
