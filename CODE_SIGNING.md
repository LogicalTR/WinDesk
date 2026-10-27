# Code signing policy

Status: integration prepared; SignPath Foundation acceptance and production
configuration are pending. Existing unsigned releases remain unsigned. This
page does not claim that signing has already been provided.

Releases 0.7.13 and 0.7.14 are maintainer-approved unsigned exceptions while production
signing is pending. Their applications and installers have no Authenticode
publisher signature. This exception does not enable unsigned publication in
the standard release scripts or change the planned signing requirements below.

The planned service attribution, effective after acceptance, is:
Free code signing provided by [SignPath.io](https://about.signpath.io/), certificate by [SignPath Foundation](https://signpath.org/).

## Team

- Committer and reviewer: [LogicalTR](https://github.com/LogicalTR).
- Release signing approver: [LogicalTR](https://github.com/LogicalTR).

Contributors outside this role submit pull requests for review. People with
source write access and SignPath access must enable MFA on both services before
production signing. The maintainer must configure this in the accounts; a
repository file alone does not enforce MFA or create approval roles.

## Release process

The public source repository is [LogicalTR/WinDesk](https://github.com/LogicalTR/WinDesk).
Production builds must run from its reviewed `main` commit on GitHub-hosted
Windows runners using `.github/workflows/signpath-release.yml`. The source
must be present on that branch before this workflow can be used. The pinned
.NET SDK, runtime and Inno Setup version are recorded in the repository.

Two signing requests are required: the application and generated Inno Setup
uninstaller/runtime first, then the final installer containing those signed
files. Each request requires manual approval in SignPath. The service checks
the GitHub build origin and the `WinDesk` product name and release product
version. Local builds, arbitrary binary uploads and pull-request builds are
not a production signing path. Upstream runtime libraries are not re-signed.

Downloaded results must have valid embedded Authenticode signatures, a trusted
non-self-signed signing identity, timestamps, matching publisher certificates,
and unchanged content apart from PE signing fields and the added signature.
The workflow tests the installed uninstaller, builds checksums after signing,
and exposes a verified release bundle for maintainer review. Publishing a
release is a separate deliberate action; a successful build is not publication.

Protect `main` against force pushes, review external contributions, and review
workflow, build and packaging changes with special care. Restrict the GitHub
`signpath-production` environment to `main`, require a maintainer review and
store only a submitter token there. Approval credentials must never be in CI.

## Privacy

See [privacy and installer information](packaging/PRIVACY.txt). WinDesk does
not upload desktop layouts or telemetry. Update checks and optional downloads
contact GitHub only when requested. That policy is shown before installation
and included with distributed packages.

See [setup and release instructions](tools/RELEASE-SIGNING.md) and the
[Foundation conditions](https://signpath.org/terms.html). Acceptance is the
Foundation's decision. Signing identifies the publisher; it does not guarantee
that SmartScreen will immediately stop warning about a new release.
