# OCI image migration notes

This Phase 1A image contains the current EF Core migration set. Normal application
startup never applies migrations.

Before every image update:

1. verify the complete signed supply-chain evidence bundle;
2. review the migration files added between the installed and target commits;
3. create and verify a database and key/configuration recovery set;
4. run `scripts/operations/migrate-database.ps1` with the digest-pinned target image;
5. start the target digest, then verify readiness and sign-in;
6. retain the prior image and recovery set until the update is accepted.

Rollback to the prior image is permitted only when the reviewed migration delta is
backward compatible. Otherwise restore the pre-update recovery set. Absence of these
notes, an unknown migration delta, or a failed backup/restore check blocks the update.
