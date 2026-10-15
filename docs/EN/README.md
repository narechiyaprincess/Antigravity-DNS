[🇷🇺 Русский](../../README.md) • 🇬🇧 English

# Antigravity DNS

**A Windows DNS bridge for Antigravity, Gemini Desktop, and Google services.**

[![Windows x64](https://img.shields.io/badge/Windows-x64-0078D4?style=flat-square)](#installation)
[![.NET 10](https://img.shields.io/badge/.NET-10-512BD4?style=flat-square)](#building-from-source)
[![MIT](https://img.shields.io/github/license/narechiyaprincess/Antigravity-DNS?style=flat-square)](../../LICENSE)
[![Release](https://img.shields.io/github/v/release/narechiyaprincess/Antigravity-DNS?style=flat-square)](https://github.com/narechiyaprincess/Antigravity-DNS/releases/latest)

Test DoH providers, choose a working profile, and route DNS queries for selected domains through it. The provider returns addresses of its SNI proxy; Windows applies the configuration through NRPT rules and DNS over HTTPS registrations. A single executable with Russian and English UI.

[Downloads](https://github.com/narechiyaprincess/Antigravity-DNS/releases) · [Report a problem](https://github.com/narechiyaprincess/Antigravity-DNS/issues/new/choose) · [Build and verification guide](../BUILDING.md)

<p>
  <img src="../../assets/screenshots/bridge.png" width="320" alt="Bridge page with profile and DNS scope selection">
  <img src="../../assets/screenshots/benchmark.png" width="320" alt="Provider test: availability, median, P95, and TLS timings">
</p>

*Screenshots use the UI test mode. Measurements are illustrative; run a fresh test on your own network.*

## Features

- Six built-in profiles, pinned-address checks, and provider address discovery when required.
- Tests covering availability, answer stability, median and P95 DoH request times, and TLS connection time. Returned service addresses receive separate TLS checks.
- Ranked results and **Pick the best**. This selects a profile; connecting is a separate action.
- Three scopes: Antigravity, **+ Gemini Desktop**, and **+ Browser**.
- Custom DoH profiles with an HTTPS hostname URL, optional IP addresses, and a display name.
- Bridge health checks, restoration of owned entries, and alternative-provider offers that require confirmation.
- System tray, notifications, login autostart, measurement and operation history, logs, and support-report preview/export.

## Services and scope

| Mode | DNS names |
|---|---|
| Antigravity | `daily-cloudcode-pa.googleapis.com`, `generativelanguage.googleapis.com` |
| + Gemini Desktop | Core names plus `gemini.google.com`, `geminiweb-pa.clients6.google.com` |
| + Browser | Antigravity/Gemini names plus `.google.com`, `.googleapis.com`, `.gstatic.com`, `.googleusercontent.com`, `.google` suffixes |

Suffix rules cover matching Google names. The benchmark checks specific Antigravity/Gemini hosts, rather than every service under those suffixes. Other compatible applications can use the bridge when they resolve these names through Windows DNS.

The catalog contains `dns.malw.link`, `xbox-dns.ru`, `dns.comss.one`, `geohide.ru`, `dns.dns-ai.ru`, and `dns.bezmezhau.com`. `xbox-dns.ru` and `dns.dns-ai.ru` are experimental. See [profiles.json](../../src/app/profiles.json); dated health notes record earlier observations and do not guarantee current availability.

## How it works

1. The application probes DoH responses and TLS, discovering provider addresses when necessary. Measuring does not change DNS policy.
2. Connecting requires provider trust confirmation and UAC approval. The engine repeats preflight, creates NRPT rules for the selected names, and registers Windows DoH with UDP fallback disabled.
3. Applications using the system resolver follow these rules. HTTPS connections to the service traverse the provider's infrastructure while certificate validation remains enabled.
4. Disconnecting removes verified owned entries using an ownership journal. Shared entries or entries changed by other software may be retained; the UI reports the result.

The engine changes domain-specific DNS policy. It does not tunnel all traffic, rewrite adapter DNS, modify `hosts` or the system proxy, or configure a VPN.

## Installation

- **Windows 11 x64** with NRPT and native DoH commands, Windows PowerShell 5.1, and administrator rights for connection/restoration.
- The executable includes its .NET runtime; no separate runtime installation is required.
- Windows 10 compatibility is unverified. Being able to run .NET does not establish native DoH support. See [Microsoft's DoH requirements](https://learn.microsoft.com/en-us/windows-server/networking/dns/doh-client-support) and the [.NET 10 OS support table](https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md).

1. Open [Releases](https://github.com/narechiyaprincess/Antigravity-DNS/releases) and download `Antigravity-DNS-v1.0.0-win-x64.exe` and `SHA256SUMS.txt` once the release is published. If no release exists yet, build from source.
2. Compare the downloaded file's SHA-256 with the release checksum:

   ```powershell
   Get-FileHash .\Antigravity-DNS-v1.0.0-win-x64.exe -Algorithm SHA256
   ```

3. Place the EXE in a permanent folder and run it. There is no installer and no required companion file. The binary is currently unsigned; Windows may display an unknown-publisher warning. Check the download source and checksum.

## Usage

1. Open **Test → Measure**, wait for results, and choose **Pick the best** or select a profile manually.
2. On **Bridge**, choose a scope. Start with **Antigravity**; add Gemini or browser coverage when needed.
3. Select **Connect bridge**, review the provider trust information, and approve UAC.
4. Test sign-in and the required action in Antigravity/Gemini itself. A successful DNS check does not prove account eligibility or a model response.
5. Use **Disconnect** / **Restore** to revert the bridge. Closing the window may leave the app in the tray. **Exit** ends the process; it does not replace disconnecting.

Login autostart is configured in **Settings**. It starts the UI when you sign in; it is not a service and cannot approve UAC. If you move the executable, update the autostart setting.

### Antigravity

The bridge covers the listed API domains. It does not modify Antigravity files or validate account restrictions. **Info** links to the independent [Open Antigravity Patcher](https://sourceforge.net/projects/open-antigravity-patcher/). The patcher is not bundled or launched automatically; review its compatibility and behavior separately.

### Gemini Desktop and Google in a browser

**+ Gemini Desktop** adds the two Gemini domains. **+ Browser** extends the rules to the Google suffixes and offers diagnostics for the selected browser. A browser's own Secure DNS/DoH, a VPN, or a TUN can bypass the system resolver. The app provides guidance without rewriting browser settings. DNS changes cannot guarantee removal of regional or account restrictions.

### Custom DoH

Go to **Settings → Custom DoH provider**, enter an address such as `https://dns.example/dns-query`, and optionally provide IP addresses. The URL must contain a hostname, not an IP literal. Discovery and TLS/DoH checks use the same pipeline as built-in profiles. Saving a profile alone does not establish service compatibility.

## Security and local data

- A third-party provider handles DNS requests and supplies the SNI proxy. Passing TLS checks is not an audit of its infrastructure or data-retention policy.
- Certificate names and chains are checked; explicit revocation is rejected. Unavailable revocation services use soft-fail behavior. The app does not install root certificates.
- The engine tests direct DoH requests and compares Windows resolver answers. It **does not independently verify system DNS encryption through packet capture**.
- Settings, history, and UI errors live under `%LocalAppData%\AntigravityDnsUnlocker`. The ownership journal and staged PowerShell resources live under `%ProgramData%\AntigravityDnsUnlocker`. Deleting the EXE does not remove this data or disconnect the bridge.
- Support reports include a preview and personal-data redaction. Review the result before sharing; do not upload raw settings, journals, tokens, or private DoH URLs.

## Troubleshooting

| Symptom | Next step |
|---|---|
| No provider works | Run another measurement on your network and inspect TLS/DoH failures in Logs. Keep certificate validation enabled. |
| Connected, but the service still fails | Check scope, application/browser DNS settings, VPN/TUN routing, and account restrictions. |
| `DOH_UNSUPPORTED_BY_OS` | Check that Windows provides the native DoH commands; update the OS. |
| NRPT/DoH conflicts or changed rules | Open Details and diagnostics. Do not blindly delete other software's rules. |
| WMI timeout or unconfirmed operation | Wait for completion, inspect diagnostics, and use the offered recovery. Do not manually delete the journal. |
| Reporting a bug | Generate a support report in Logs, review its preview, and use the [issue form](https://github.com/narechiyaprincess/Antigravity-DNS/issues/new/choose). |

## Building from source

Use Windows x64, [.NET SDK 10](https://dotnet.microsoft.com/en-us/download/dotnet/10.0), and Windows PowerShell 5.1. From the repository root:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\src\app\Build.ps1
& .\src\.work\build\AntigravityDnsUnlocker.exe --self-test
& .\src\.work\build\AntigravityDnsUnlocker.exe --verify-scripts
```

Run current-application verification, including offline E2E tiers 5–14, UI checks, and standalone executable checks:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\src\tools\verify.ps1 -Tag local -NoPublish
```

Build output and logs stay in ignored `src/.work/`. `-NoPublish` prevents replacing the local release and shortcut. Historical tiers 1–4 require separate legacy fixtures and are not included in this mode. See [BUILDING.md](../BUILDING.md) for SDK overrides, packaging, and the WinForms trimming limitation.

## Architecture and layout

C# WinForms provides the GUI and network probes. `Engine.ps1` applies/restores DNS policy; `ScriptStore.cs` extracts embedded resources with SHA-256 and ACL checks. The provider catalog is embedded in the EXE. The project has no third-party `PackageReference` dependencies; the .NET SDK restores runtime/build packs.

```text
.github/                  issue forms and pull request template
assets/screenshots/       documentation images
docs/EN/README.md         English
docs/BUILDING.md          builds, checks, and packaging
src/app/                  C#, PowerShell, catalog, resources
src/tests/                offline E2E and synthetic fixtures
src/tools/                build/verify/packaging and mutation tooling
LICENSE                   MIT
README.md                 Russian
```

Compiled executables and archives belong in [GitHub Releases](https://github.com/narechiyaprincess/Antigravity-DNS/releases); Git contains source.

## License and credits

[MIT](../../LICENSE) · [narechiyaprincess](https://github.com/narechiyaprincess) · [source and issues](https://github.com/narechiyaprincess/Antigravity-DNS)

Built with [.NET](https://github.com/dotnet/runtime) and [Windows Forms](https://github.com/dotnet/winforms). [Open Antigravity Patcher](https://sourceforge.net/projects/open-antigravity-patcher/) is a separate project by avencores. Antigravity, Gemini, and Google belong to their respective owners; this repository is not an official Google project.
