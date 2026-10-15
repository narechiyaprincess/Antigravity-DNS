[🇷🇺 Русский](../../README.md) • 🇬🇧 English

# Antigravity DNS

**A Windows DNS bridge for Antigravity, Gemini Desktop, and Google services.**

[![Windows x64](https://img.shields.io/badge/Windows-x64-0078D4?style=flat-square)](#installation)
[![CI](https://github.com/narechiyaprincess/Antigravity-DNS/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/narechiyaprincess/Antigravity-DNS/actions/workflows/ci.yml)
[![MIT](https://img.shields.io/github/license/narechiyaprincess/Antigravity-DNS?style=flat-square)](../../LICENSE)
[![Latest release](https://img.shields.io/github/v/release/narechiyaprincess/Antigravity-DNS?style=flat-square)](https://github.com/narechiyaprincess/Antigravity-DNS/releases/latest)

Test DNS/DoH providers, choose a working profile, and configure Windows to reach selected domains through the provider's DNS/SNI proxy. A single portable executable with Russian and English UI.

[Latest Release](https://github.com/narechiyaprincess/Antigravity-DNS/releases/latest) · [Issues](https://github.com/narechiyaprincess/Antigravity-DNS/issues) · [Telegram](https://t.me/sexelysium) · [Build](../BUILDING.md)

<p>
  <img src="../../assets/screenshots/bridge.png" width="320" alt="Bridge: provider profile and scope selection">
  <img src="../../assets/screenshots/benchmark.png" width="320" alt="Provider test: availability, median, P95, and TLS timings">
</p>

*Measurement results depend on your network.*

## Features

- Automatic tests of built-in DNS/DoH profiles: availability, latency, median, P95, and TLS; ranked results and best-profile selection.
- **Antigravity**, **+ Gemini Desktop**, and **+ Browser** scopes for Google domains; custom DoH profiles.
- Preflight checks, an ownership journal, and restoration that preserves shared entries and entries owned by other software.
- System tray, login autostart, notifications, logs, measurement and operation history, and support reports.

## How it works

1. **Measure** — test the providers on your network.
2. **Select** — compare results or choose “Pick the best”.
3. **Connect** — confirm the provider and approve UAC, then open your application.

Antigravity DNS is not a VPN and does not tunnel all traffic. It applies DNS rules for selected domains without rewriting adapter DNS, `hosts`, or the system proxy.

## Installation

Requires **Windows 11 x64**, native DoH, and Windows PowerShell 5.1. Administrator rights are required to connect and restore. The EXE includes its .NET runtime; no installer is needed. Windows 10 compatibility is unverified.

Download the EXE from the [latest Release](https://github.com/narechiyaprincess/Antigravity-DNS/releases/latest), compare its SHA-256 with the release checksum, and run it from a permanent folder.

## Usage

Open **Test → Measure**, then choose **Pick the best** or select a profile manually. On **Bridge**, choose a scope and select **Connect bridge**. Selecting a profile alone does not connect it.

**+ Gemini Desktop** adds Gemini domains; **+ Browser** extends coverage to Google domains. Custom DoH and autostart are in **Settings**. Use **Disconnect / Restore** to remove the bridge; closing the window or exiting the tray leaves its rules in place.

## Notes

- The provider handles DNS queries and supplies the SNI proxy. TLS checks do not replace trust in that provider; passing a test cannot guarantee account access or a model response.
- Browser Secure DNS and VPN/TUN routing can bypass the system resolver. The app does not rewrite browser settings.
- See [domains, security, local data, and troubleshooting](TECHNICAL.md). Review the support-report preview before sharing it.

If the bridge or a service fails, see [Troubleshooting](TROUBLESHOOTING.md). Follow [SECURITY.md](../../SECURITY.md) for suspected vulnerabilities without posting public details.

## Development

C# WinForms and PowerShell, with the provider catalog and engine embedded in the EXE. Application source, offline tests, and tooling live in `src/`. See [BUILDING.md](../BUILDING.md) for build and verification commands and packaging limitations.

[Contributing](../../CONTRIBUTING.md) · [Changelog](../../CHANGELOG.md) · [Release preparation](../RELEASING.md). CI checks the build, self-test, embedded scripts, mocked engine tests, and offline tier 14; full local verification and live checks are documented separately.

## License and links

[MIT](../../LICENSE) · [Source](https://github.com/narechiyaprincess/Antigravity-DNS) · [Open Antigravity Patcher](https://sourceforge.net/projects/open-antigravity-patcher/)

The patcher is an independent project linked from the app. Antigravity DNS is not an official Google project.
