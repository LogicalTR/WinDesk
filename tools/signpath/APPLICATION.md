# SignPath Foundation application draft

Not submitted. Complete the public source transition and account settings in
[RELEASE-SIGNING.md](../RELEASE-SIGNING.md) before submitting this information.

- Project: WinDesk.
- Maintainer / source owner: [LogicalTR](https://github.com/LogicalTR).
- Repository: https://github.com/LogicalTR/WinDesk (public).
- License: MIT for WinDesk source, upstream notices; no commercial dual licensing.
- Description: Windows 10/11 desktop organizer written in C# and WPF. Desktop
  shortcut groups, hover panels, grouping, positioning and user-requested updates.
- Existing releases: https://github.com/LogicalTR/WinDesk/releases — EXE, portable
  ZIP and Inno Setup installer. v0.7.11 was released before this application.
- Source: full source is being moved from release ZIPs to the public repository
  tree. Supply the merged commit URL before applying.
- Build: GitHub-hosted `windows-2025`, SDK 10.0.402, runtime 10.0.13, Inno 6.7.3;
  immutable action references and pinned installer package checksum.
- Artifacts: WinDesk plus generated installer runtime/uninstaller in the first
  request; final installer in the second. Product WinDesk, matching four-part
  product version. Upstream libraries are not re-signed.
- Policy: https://github.com/LogicalTR/WinDesk/blob/main/CODE_SIGNING.md
  (must exist publicly before application).
- Privacy: https://github.com/LogicalTR/WinDesk/blob/main/packaging/PRIVACY.txt
  (must exist publicly before application). No telemetry/layout uploads. GitHub
  is contacted for user-requested updates. Optional ByBrowser download is off
  by default; it is external, not bundled or re-signed.
- Roles: LogicalTR is committer, reviewer and release approver. Confirm MFA on
  both real accounts before declaring this complete on the form.
- Review item: offline Inno signing covers temporary setup copies, uninstaller
  and outer setup. Confirm exact metadata matching for Inno's padded version
  strings in the first service trial.

The maintainer must supply accurate contact details, identity, adoption evidence
and declarations. No fabricated figures or claimed acceptance are included.
