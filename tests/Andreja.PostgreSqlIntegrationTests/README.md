# Local PostgreSQL identity evidence

These tests are intentionally outside `Andreja.slnx`: the normal deterministic test
suite has no database dependency. Database evidence is **blocked**, rather than
reported as skipped or passed, unless a disposable local PostgreSQL database is
explicitly supplied.

Create an empty local database whose name starts with `andreja_test_`, then run:

```powershell
$env:ANDREJA_TEST_POSTGRES = 'Host=localhost;Database=andreja_test_identity;Username=postgres;Password=<local-only>'
dotnet test tests\Andreja.PostgreSqlIntegrationTests\Andreja.PostgreSqlIntegrationTests.csproj
```

The fixture refuses any other database name, drops it before and after the run,
applies the real migrations from empty, and proves tenant-filtered enumeration,
fail-closed writes, composite cross-tenant foreign keys, global issuer/subject
uniqueness, durable Open Loops tasks, idempotent task receipts, and cross-tenant
task denial. Missing configuration fails the run with a `BLOCKED` error; no test
is silently skipped. Do not use a shared or production database.
