# Publishing Blazor.Ink

## Identity and prerequisites

- NuGet ID, assembly name, and public namespace: `Blazor.Ink`.
- Repository: [chandrakanth877/Blazor.Ink](https://github.com/chandrakanth877/Blazor.Ink).
- Git remote: `https://github.com/chandrakanth877/Blazor.Ink.git`.
- Current version: `1.0.0`, not a full Ink-parity release.
- First published version: `0.1.0-preview.1`.
- Dependencies: `Yoga.Net` 3.2.3 and `Wcwidth` 4.0.1, restored from their
  independently published packages and pinned in package lock files.
- Targets: .NET 8 and 10. .NET 10 is recommended; .NET 8 support ends
  November 10, 2026.

NuGet IDs are case-insensitive. `Ink.NET` is already published by an unrelated
project, so this library uses `Blazor.Ink`. Its absence from the public package
index is not proof that the name is unreserved or that an account can publish it.
Confirm the package name and account/organization rights on NuGet.org first.

The standalone export does not change or delete files in the original research
workspace. Historical design and execution ledgers are retained in this
repository's documentation; their old source-acquisition notes do not describe
the current package dependencies.

## One-time GitHub.com and NuGet.org setup

1. Use the existing `chandrakanth877/Blazor.Ink` repository; do not create a
   second repository. This checkout's `origin` is
   `https://github.com/chandrakanth877/Blazor.Ink.git`. Authenticate to GitHub.com
   as an authorized contributor, commit the export including package lock
   files, and push `codex/nuget-release`. This branch push runs validation only.
   Do not reuse corporate credentials without authorization.
   Establish `main` and merge the verified branch before tagging a release.
2. Protect `main`, requiring all three OS verification jobs and disallowing
   force pushes and deletion. The owner reviews workflow changes before
   approving publication. Do not allow untrusted contributors to publish releases.
3. Create the GitHub environment `nuget` with `chandrakanth877` as its required
   reviewer. Allow self-approval for this single-maintainer repository, but keep
   the manual approval gate. Restrict deployments to tags matching `v*`, not
   branches. These protections must be configured in GitHub; a workflow cannot
   configure them for you.
4. Add the environment secret `NUGET_USER` with the value `Chandrakanth`: the
   authorized NuGet **profile username**, not an email address or API key.
5. On NuGet.org, create a Trusted Publishing policy owned by the intended
   publishing account/organization: repository owner `chandrakanth877`, repository `Blazor.Ink`,
   workflow filename `build.yml`, environment `nuget`, package pattern
   `Blazor.Ink`. Permit new packages for the first publication and new versions
   afterwards.
6. Only after confirming ownership and environment protections, set the
   repository variable `NUGET_PUBLISH_ENABLED` to `true`. Without it, release
   verification runs but publishing is skipped.

No long-lived NuGet API key, author-signing certificate, or strong-name key is
required. Do not commit credentials or certificate private keys.

<!-- ponytail: single-owner approval is manual authorization, not independent
review; add a second maintainer and prevent self-approval when one is available. -->

## Validation pipeline and release plan

1. Push the local branch and inspect the
   [Actions run](https://github.com/chandrakanth877/Blazor.Ink/actions/workflows/build.yml).
   Alternatively, choose **Run workflow** once the workflow exists on the default
   branch. Leave `published_tag` empty for platform/build verification.
   `workflow_dispatch` is verification-only; it cannot publish.
2. Require `Verify (windows-latest)`, `Verify (ubuntu-latest)`, and
   `Verify (macos-latest)` to pass. Each runs .NET 8 and 10 checks, samples,
   package inspection, and a clean NuGet consumer. Windows adds ConPTY checks;
   Linux/macOS add POSIX ABI/PTY checks. Download `validation-<os>` artifacts
   for logs, including failed checks. Linux also retains the package and symbols.
3. Confirm NuGet package ownership, configure the protected `nuget` environment
   and Trusted Publishing policy above, and enable `NUGET_PUBLISH_ENABLED`.
4. Merge the validated code to `main`, publish GitHub release
   `v1.0.0` at that commit, wait for all three OS gates, then approve
   the environment deployment. The publisher consumes the exact Linux artifact,
   never rebuilds, and saves the NuGet.org-signed download and verification log.

Branch pushes, pull requests, and manual runs have no NuGet publishing
permissions. No NuGet push is needed to run platform validation.

## Build and release

Install the pinned .NET 10 SDK and the .NET 8 ASP.NET Core runtime/targeting
packs. Run:

```sh
bash scripts/verify.sh
bash scripts/check-native-input.sh  # macOS/Linux
```

On Windows, use Git Bash for verification and run the ConPTY checks:

```sh
PYTHON=python bash scripts/verify.sh
for framework in net8.0 net10.0; do
  dotnet run --project tests/Blazor.Ink.Checks -c Release -f "$framework" --no-build -- --conpty-input
done
```

The verification script restores in locked mode. Update dependency versions
deliberately, regenerate locks with `dotnet restore --force-evaluate`, review
the lock diff, and rerun the complete checks. Restore errors and auditing are
not suppressed. `NUGET_SOURCE` can override the upstream feed for an explicitly
configured local validation feed; CI uses NuGet.org. A folder feed supplies no
vulnerability service, so an offline run alone does not establish an audit.
The isolated consumer also checks that its cached package archive is byte-for-byte
the locally built artifact, rather than an already published copy of that version.

For subsequent releases, change the shared project version, commit it, and
publish a GitHub release using the exact tag `v<version>`. The current release
tag is `v1.0.0`. A mismatch fails before publication.

The workflow checks both frameworks and native input on all three operating
systems. After all jobs pass and an environment reviewer approves publication,
the publish job downloads the Linux-built package artifact, checks its version
and source commit, obtains an OIDC-based temporary key, and pushes those exact
bytes with their symbol package. It does not rebuild.

Package versions are immutable. A collision fails instead of silently skipping
an existing package. If push succeeded but indexing/signature verification
failed, **do not rerun the publishing job**: the version was already accepted.
NuGet validation and indexing are asynchronous; the download waits up to
15 minutes and retains failure logs. Choose **Run workflow** on `main` and set
`published_tag` to the existing release tag (for example `v1.0.0`).
This recovery checks out the unchanged tag, downloads the indexed package,
verifies its signature and release metadata on Windows, and preserves the
signed package and logs. It neither rebuilds nor requests credentials nor
pushes a package. If the download still returns 404 after the bounded wait,
check the package's validation status on NuGet.org; do not republish or move
the release tag.

## Signing and evidence

Build artifacts under `artifacts/packages` are unsigned. NuGet.org adds a
repository signature to accepted `.nupkg` files. The publication workflow
downloads the indexed package, runs `dotnet nuget verify --all`, and preserves
the verified download plus log as `repository-signed-nuget`. `.snupkg` files
are not signed, and the assembly is not strong-named.

This release uses free NuGet.org repository signing only. No domain, purchased
certificate, private key, or author-signing step is needed. Do not register an
author-signing certificate on the sole-owner NuGet profile unless CI is also
updated to author-sign future submissions: registration makes that mandatory.

Local verification does not certify Windows/Linux until their GitHub native
gates actually pass. Package signing verification may be unsupported on some
macOS/.NET combinations; the publish job verifies signatures on Windows.

References:
- [NuGet Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing)
- [NuGet package signing](https://learn.microsoft.com/en-us/nuget/create-packages/sign-a-package)
- [Signature verification](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-nuget-verify)

## If vendors need to remain

| Approach | Cost |
|---|---|
| Retain inactive source snapshots outside normal builds | Keep references for research/offline review; release still uses upstream packages. |
| Separate source-sharing release project | Maintain a vendored development build and NuGet-based release build; validate both. |
| Publish source-wrapper dependencies first | Maintain three packages, licenses/provenance, and dependency release ordering. |

This repository deliberately uses none of those alternatives: it publishes one
library package with two upstream dependencies and no vendored source.
