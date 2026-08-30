# ADR 0010: Keyless Sigstore signing with GitHub Actions OIDC

- **Status:** Accepted
- **Date:** 2026-08-29
- **Issue:** [#127](https://github.com/Jamula/Andreja/issues/127)
- **Approver:** Cyrus Jamula
- **Governing:** [Platform plan](../plan.md),
  [ADR 0000](0000-plan-ratification.md), and
  [Phase 1A evidence packet](../phase-1a/README.md)

## Context

Phase 1A supply-chain evidence supports a locally controlled, operator-held
Cosign key. That path is useful for private self-host evidence but cannot prove
that a hosted release came from one protected repository workflow without
distributing a long-lived hosted signing key.

GitHub Actions can supply a short-lived workload identity to Fulcio. Sigstore
then places the signing certificate and artifact signature in public,
append-only certificate-transparency and Rekor records. This avoids a hosted
private key, but permanently discloses repository/workflow metadata, the
workflow revision and tag ref, and the digest-bound artifact identity.

## Decision

Hosted release evidence uses the GitHub-hosted Actions workload identity with
Cosign `3.1.3`, pinned by immutable image digest. The signing job:

1. runs only for a version tag whose commit is an ancestor of protected `main`;
2. has `contents: read` and `id-token: write`, with `id-token: write` present in
   no other job;
3. requires the exact GitHub repository, workflow path, workflow name,
   triggering workflow revision, `push` trigger, and version-tag ref;
4. signs provenance whose sole subject is the immutable OCI manifest digest;
5. uploads to Fulcio, certificate transparency, and Rekor with no
   transparency-log or SCT bypass;
6. authenticates and pre-positions the Sigstore TUF trusted root outside the
   evidence directory before signing, then retains a matching copy with the
   standardized bundle; and
7. immediately verifies the bundle in a network-disabled container using only
   the independently pre-positioned root as Cosign's trust anchor.

Actor identity is neither an authorization input nor a substitute for workload
identity. Verification exact-matches:

- OIDC issuer `https://token.actions.githubusercontent.com`;
- repository `Jamula/Andreja`;
- workflow identity
  `https://github.com/Jamula/Andreja/.github/workflows/oci-supply-chain.yml@<exact-tag-ref>`;
- workflow name `OCI Supply Chain`;
- workflow revision equal to the signed source commit;
- trigger `push`; and
- the exact permitted version-tag ref.

Offline verification requires the retained bundle, a separately acquired and
authenticated trusted root outside the evidence directory, the preloaded pinned
Cosign image, and a separately acquired trusted copy of
`supply-chain-policy.json`. The independent root must byte-match the retained
copy; only its external path is passed to Cosign. Network failure cannot select
an online fallback.
Missing or altered inclusion material, an untrusted chain, claim mismatch,
bundle tampering, or any insecure transparency option fails closed.

## Coexistence and downgrade prevention

The operator-held-key mode remains available only for local operator evidence.
It cannot satisfy the hosted release policy. Hosted pull-request and ordinary
`main` builds remain explicitly unsigned validation evidence; only the
tag-gated signing job can convert that evidence to `keyless-sigstore`.

Evidence has exactly one signing mode. Operator signature fields, keyless
bundle/root fields, and hosted-unsigned deferral fields are mutually exclusive.
A mixed mode, an unsigned hosted release, a branch ref, or a keyless signature
without both transparency proofs is rejected.

Evidence schema 1.1 adds keyless metadata without replacing retained v1
operator evidence. Policy 1.0 remains accepted only for retained local operator
verification; keyless evidence requires policy 1.1. Operator mode is rejected
for refs matching the hosted release-tag policy.

## Disclosure and privacy boundary

The accepted public disclosure is limited to repository identity, workflow
identity and revision, tag ref, artifact digest, and Sigstore verification
material. Do not publish personal email identity, prompts, connector content,
user data, credentials, private diagnostics, or private OCI layers.

The public Fulcio, certificate-transparency, and Rekor records are permanent
and cannot be recalled by deleting a GitHub artifact or release. Stop signing
if the public record exceeds this metadata boundary.

## Operational and cost boundary

This decision uses existing GitHub-hosted Actions and public Sigstore services.
It authorizes no new account, cloud infrastructure, subscription, free tier,
trial, paid service, production resource, or external spend. It does not
authorize release publication, production deployment, Phase 1A exit, or merge.

Local development never requests a GitHub OIDC token or creates a real
transparency record. Local negative tests use synthetic bundles, roots, and
claims. The first real hosted signature remains separate post-review evidence.

## Consequences

- Hosted signing has no long-lived private key to distribute or rotate.
- Trust moves to GitHub repository/workflow protections, the exact workload
  claims, Fulcio, certificate transparency, Rekor, TUF root distribution, and
  the retained offline evidence set.
- A compromised protected workflow revision or tag-control path can produce a
  valid public signature; branch protection, tag governance, review, and
  transparency monitoring remain required.
- Service outage blocks new signatures but does not block verification of a
  retained bundle with an independently held trusted root.
- Operator-held local evidence remains useful but is not silently promoted to
  hosted release evidence.

## Alternatives considered

- **Store a hosted private key in GitHub secrets:** rejected because it creates
  a reusable signing credential and rotation/recovery burden.
- **Trust actor identity or any workflow in the repository:** rejected because
  a user or unrelated workflow is not the approved workload.
- **Disable transparency or verify online only:** rejected because it weakens
  public auditability and makes verification depend on live network access.
- **Remove operator-held signing:** rejected because private self-host evidence
  still needs an operator-controlled path independent of GitHub.
