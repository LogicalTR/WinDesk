# Windows 10 22H2 test VM

This fixture runs a full Windows 10 desktop using QEMU/KVM inside Docker. It is not a Windows container or Windows Sandbox. Only the local machine can reach its web console at <http://127.0.0.1:18006>.

The guest uses 4 CPU cores, 6 GB RAM and a sparse 64 GB disk. Its image is based on a pinned `dockurr/windows` digest. The Dockerfile retains guest UAC and the firewall; tests run as the interactive user without elevation. No host user data or complete repository is shared with the guest: `shared/bundle` contains only release files, published tests and their scripts.

## Local instance

The instance created for this workspace is stored under `C:\Users\Burak\WinDesk-VMs\win10-22h2`. Its `.env` contains `WINDESK_VM_ROOT` and a generated local VM password and is deliberately outside the repository. Keep the VM disk and ISO off the nearly full E: drive.

From the repository root:

```powershell
$vm = 'C:\Users\Burak\WinDesk-VMs\win10-22h2'
# Required after restarting Docker/WSL if /dev/kvm is absent:
wsl -d docker-desktop -- modprobe kvm_intel
docker compose --env-file "$vm\.env" -f .\tools\vm\compose.yml up -d
docker compose --env-file "$vm\.env" -f .\tools\vm\compose.yml stop
```

`stop` preserves the installation. `down` removes the container and network but preserves the bind-mounted VM files. Removing the storage directory is a separate, destructive step; retain the test reports first.

## Media and tests

The guest uses Microsoft's Windows 10 Enterprise Evaluation 22H2 x64 image, build 19045.2006. The installed guest currently reports an activation warning (`LicenseStatus=5`, zero grace time). It is retained as requested, but this is not an indefinitely licensed VM. Microsoft's [evaluation guidance](https://www.microsoft.com/en-us/evalcenter/evaluate-windows-10-enterprise) describes hourly shutdowns when the evaluation is not activated or has expired. Activation/licensing must be resolved for sustained reuse; no licensing or clock bypass was applied.

- [Microsoft ISO](https://software-static.download.prss.microsoft.com/dbazure/988969d5-f34g-4e03-ac9d-1f9786c66750/19045.2006.220908-0225.22h2_release_svc_refresh_CLIENTENTERPRISEEVAL_OEMRET_x64FRE_en-us.iso)
- SHA-256: `ef7312733a9f5d7d51cfa04ac497671995674ca5e1058d5164d6028f0938d668`

The ISO hash must match before startup. The installation's actual build is checked again inside the guest. No SDK or global .NET installation is needed: the release and test executables are self-contained.

The OEM bootstrap registers `guest-runner.ps1` as a limited, interactive scheduled task. A new identifier in `shared/run-request.txt` triggers a fresh copy of `shared/bundle` into the guest user profile. Results are written to `shared/results/<identifier>`, including the actual OS build, privilege level, Setup logs, individual test logs, an `evidence.zip` archive and a real desktop screenshot. The last completed request is persisted, so restarting the VM does not rerun the Explorer restart test automatically.

`guest-tests.ps1` checks Setup/launch/uninstall, the core and WPF test suites, the modern-menu Windows 10 guard, and real Explorer restart recovery for both visible and hidden panels. Explorer restart is restricted to the test session and requires both `--explorer-restart-test` and `WINDESK_ALLOW_EXPLORER_RESTART=1`; the normal test command never restarts Explorer.

Before rerunning after changes, republish the tests into `shared/bundle/tests/app` and `shared/bundle/tests/core`, copy the new release into `shared/bundle/dist/releases/<version>`, and update the copied scripts and app project file. Submit a new identifier with `./tools/vm/request-test.ps1 -RunId run-004`. The helper writes through Samba to invalidate the guest's read cache; directly overwriting `run-request.txt` on the host can encounter an open-file lock or leave cached content in the guest. Guest tests refuse to run on a machine other than `WINDESK-QA` or a build other than 19045 / 22H2. Do not reuse an old identifier.

See the root `RELEASE_NOTES.md` for observed results and remaining test limitations.
