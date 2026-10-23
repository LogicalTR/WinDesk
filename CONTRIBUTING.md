# Contributing

Contributions to WinDesk are accepted under the repository's MIT license.
Only submit code and assets you are entitled to license this way. Preserve
third-party notices and document any new dependencies and network behavior.
Do not add proprietary bundled components or commercial dual licensing.

Use a pull request targeting `main`. The maintainer reviews changes before
merging. New contributors do not receive direct write or signing access.
Maintainers with GitHub or SignPath access must use MFA. Changes to workflows,
build tools, packaging and signing policy need the same review as app code.

On Windows, install the exact SDK in `global.json`, then run:

```powershell
dotnet run --project tests/WinDesk.Core.Tests -c Release
pwsh -File tests/Test-ReleaseSigning.ps1
pwsh -File tests/Test-SignPath.ps1
pwsh -File build-release.ps1 -Unsigned
```

Use a clean test account or Windows VM for desktop/Explorer/installer tests;
they change the test account's shell state. PR CI uses GitHub-hosted Windows
runners without signing secrets. See [Code signing policy](CODE_SIGNING.md).
