# Build, verify, package

[Русский README](../README.md) · [English README](EN/README.md)

Commands below run from the repository root in PowerShell. They build and check the current source without changing live DNS policy. Interactive connection/restoration and `src/app/Integration.ps1` are separate operations; the latter exercises live system configuration and is not part of offline verification.

## Prerequisites

- Windows x64, an interactive desktop for UI checks, Windows PowerShell 5.1 (`powershell.exe`).
- [.NET SDK 10](https://dotnet.microsoft.com/en-us/download/dotnet/10.0). Check with `dotnet --info`.
- NuGet access for the first restore of runtime, linker, and ReadyToRun build packs. There are no third-party `PackageReference` entries.
- At least 10 GiB free on the source and build-temp drives, enforced by `Build.ps1`.

Python is optional and is only used by `src/tools/mutate.py`; the application, build, and normal verification do not need it.

## Build

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\src\app\Build.ps1
& .\src\.work\build\AntigravityDnsUnlocker.exe --version
& .\src\.work\build\AntigravityDnsUnlocker.exe --self-test
& .\src\.work\build\AntigravityDnsUnlocker.exe --verify-scripts
```

The project targets `net10.0-windows` and publishes a self-contained `win-x64` single file. `Engine.ps1`, `Integration.ps1`, `EmergencyRestore.ps1`, and `profiles.json` are embedded. Users need only the EXE. The assembly keeps the internal name `AntigravityDnsUnlocker`; local packaging can safely rename the executable to `AntigravityDNS.exe`.

Build output defaults to `src/.work/build`. Intermediate files are placed in an isolated temporary build directory. To use an SDK that is not on PATH:

```powershell
$sdk = (Resolve-Path .\tools-local\dotnet\dotnet.exe).Path # replace with your installed SDK
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\src\app\Build.ps1 -Dotnet $sdk
```

For the verification wrapper, pass `-Dotnet $sdk` or set `$env:AGDNS_DOTNET = $sdk` in the current PowerShell session. `dev.ps1` and the optional mutation tool also respect `AGDNS_DOTNET`. No personal SDK path is stored in the repository.

`<Version>` in [the project file](../src/app/AntigravityDnsUnlocker.csproj) is the application version. Historical tier names such as `t8_release_1_6.ps1` identify previous development milestones, not the current release number.

### Existing WinForms trimming tradeoff

The supplied project intentionally enables trimming, single-file compression, and ReadyToRun. It bypasses the SDK's WinForms trimming guard through `_SuppressWinFormsTrimError`, retains the application assembly, and enables reflection JSON and built-in COM support. This is an existing packaging choice, **not officially supported WinForms trimming**. Microsoft's [trimming incompatibility guide](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/incompatibilities#windows-forms) explains the limitation.

Run the actual published binary through UI and standalone checks after changing source or SDK. These checks reduce observed compatibility risk; they do not establish support on every Windows release. An untrimmed packaging change requires its own review and verification.

## Verify current source

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\src\tools\verify.ps1 -Tag local -NoPublish
```

This builds, runs the self-test, offline E2E **tiers 5–14**, UI smoke/states/stress/surfaces, four scale factors (96/120/144/192 DPI) in Russian and English, and the executable alone in an empty directory. Output and receipts stay in `src/.work/`; the wrapper log directory is `src/.work/logs/local/`.

Use an existing build:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\src\tools\verify.ps1 -Tag local -NoBuild -NoPublish
```

To run a single offline tier:

```powershell
$env:AGDNS_EXE = (Resolve-Path .\src\.work\build\AntigravityDnsUnlocker.exe).Path
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\src\tests\run_offline_e2e_tests.ps1 -Tier 14
```

`Current` is the default tier set. The offline runner mocks DNS-policy writes and uses synthetic network responses; it checks that real NRPT, DoH registrations, adapter DNS, the live journal, and autostart remain unchanged. It creates scratch files and uses the dedicated `HKCU:\Software\AgDnsTest` test key, which it removes afterwards. Do not store personal data in that test namespace. UI checks require an interactive Windows session and may briefly display dialogs.

`verify.ps1` without `-NoPublish` also promotes a passing build to the **local** `release/AntigravityDNS.exe` and updates a root shortcut. It does not upload to GitHub. Keep `-NoPublish` for ordinary checks. Use unique `-Tag` names when retaining previous receipts.

### Historical tiers 1–4

The legacy contract tests remain available through `-Tier All` or `-Tier 1` through `4`. They require the original `src/tests/fixtures/baseline-1.1.0/AntigravityDnsUnlocker.exe`; that compiled fixture and its PDB are intentionally not tracked. The original fixture EXE has SHA-256:

```text
bf6c99533fa520be03a8397c3f0e681b128594a1188d9883c6a9e3e61e9f7364
```

Obtain it only from the maintainer's original archive and check the hash. The adjacent historical PowerShell fixtures remain in source. Tier 1 also asserts the historical local-release layout, a root launcher pointing to `release/AntigravityDNS.exe`, and the old README's local-launch instructions; a clean public source checkout does not satisfy those layout assertions. These historical tests are not a public-clone acceptance gate. The runner fails early if a requested legacy binary is absent; it does not silently skip it or count it as passing.

## Local packaging

After verification, this command prepares an ignored local release and launcher:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\src\tools\package-release.ps1 -WhatIf
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\src\tools\package-release.ps1
```

This helper manages `release/` as an application output directory: it moves other contents aside, can remove previous `.work/replaced` leftovers, and updates the shortcut. Keep personal files out of these output directories. `-WhatIf` previews the destination before replacement. It verifies version and SHA-256 and does not stop running copies.

The separate [app/Package.ps1](../src/app/Package.ps1) helper creates an archive named `AntigravityDnsUnlocker-<version>-win-x64.zip` with `AntigravityDnsUnlocker.exe` and optional text instructions in `src/.work/dist/`. It also writes checksums. It is an alternative packaging format, not an additional runtime dependency.

For a public single-file asset, copy the verified EXE to an external release staging folder as `Antigravity-DNS-v1.0.0-win-x64.exe`, generate `SHA256SUMS.txt` with `Get-FileHash`, and keep release notes beside it. Do not commit executable files, generated ZIPs, or signing material. Publishing a tag/release and uploading assets are separate maintainer actions.
