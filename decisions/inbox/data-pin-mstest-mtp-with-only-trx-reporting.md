### 2026-10-04T19-44-48: Pin MSTest MTP with only TRX reporting
**By:** Data
**What:** Pin MSTest MTP with only TRX reporting
**References:** Jamula/Andreja#112, Jamula/Andreja#108
**Why:** ### 2026-10-04: Pin the MSTest/MTP runner and minimize enabled extensions
**By:** Data (Quality Engineering Lead)
**What:** For issue #112, use the latest stable packages confirmed from the NuGet version indexes on 2026-10-04: `MSTest.Sdk` 4.4.1 and Microsoft.Testing.Platform 2.4.1. Select `TestingExtensionsProfile=None` and explicitly include only `Microsoft.Testing.Extensions.TrxReport` 2.4.1, with the .NET 10 `global.json` runner set to `Microsoft.Testing.Platform`.
**Why:** These versions support the pinned .NET SDK 10.0.301. The SDK, MTP, and TRX extension declare MIT. The `Default` profile includes additional code coverage and CI report extensions not used by Andreja's validation; selecting only TRX preserves retained test evidence while limiting dependencies and avoiding the code-coverage extension's distinct licensing terms noted in #108. Verify the resolved MTP version and package signatures/advisories after restore. This is scoped to #112, not broader tooling.