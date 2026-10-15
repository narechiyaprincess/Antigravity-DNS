[🇷🇺 Русский](../TECHNICAL.md) • [🇬🇧 English](TECHNICAL.md)

# Technical details

[Back to the project](README.md) · [Build and verification](../BUILDING.md) · [Troubleshooting](TROUBLESHOOTING.md) · [Security policy](../../SECURITY.md)

## Requirements

- **Windows 11 x64** with NRPT and native DoH commands, Windows PowerShell 5.1, and administrator rights for connection/restoration.
- The executable includes its .NET runtime; no separate runtime installation is required.
- Windows 10 compatibility is unverified. Being able to run .NET does not establish native DoH support. See [Microsoft's DoH requirements](https://learn.microsoft.com/en-us/windows-server/networking/dns/doh-client-support) and the [.NET 10 OS support table](https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md).

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

## Settings and compatibility

Login autostart launches the UI without elevation. Update the autostart setting if you move the executable.

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

## Architecture

C# WinForms provides the GUI and network probes. `Engine.ps1` applies/restores DNS policy; `ScriptStore.cs` extracts embedded resources with SHA-256 and ACL checks. The provider catalog is embedded in the EXE. The project has no third-party `PackageReference` dependencies; the .NET SDK restores runtime/build packs.
