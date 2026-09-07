### 2026-09-07: Activate weekly dependency automation
**By:** Jett Reno
**What:** Replace the inert Dependabot placeholder with weekly update checks for root and spike NuGet manifests, the Squad template npm manifest, GitHub Actions, and the root Dockerfile.
**Why:** These are the dependency ecosystems present in the repository. Automated coverage prevents the test spike and supply-chain dependencies from drifting while preserving the repository's existing manifest boundaries.
