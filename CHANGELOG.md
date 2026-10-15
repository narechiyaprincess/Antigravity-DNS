# Changelog

Notable changes are recorded here using a [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) style. Historical test-tier names are not application releases.

## [Unreleased]

### Added

- Windows CI and tag-build artifact workflows, a shared offline technical check, and a pinned .NET SDK.
- Security policy, contribution guide, and Russian/English troubleshooting guides.
- Dependabot checks for the GitHub Actions used by these workflows.

### Changed

- README and build-guide navigation, CI coverage documentation, and source packaging of repository guidance.
- Standalone engine test setup now creates a mismatched pending DoH registration for the existing ambiguity-preservation assertion, consistent with current interrupted-apply recovery tests.

## [1.0.0] - 2026-10-15

### Added

- Public Windows x64 portable release of Antigravity DNS, with Russian and English UI.
- DNS/DoH provider measurement and selection, domain-scoped bridge configuration for Antigravity, Gemini Desktop, and Google browser use, and restoration using an ownership journal.
- Support-report preview and local diagnostics.

This entry describes the published [v1.0.0 release](https://github.com/narechiyaprincess/Antigravity-DNS/releases/tag/v1.0.0) and the source prepared in commit [`dcb571b`](https://github.com/narechiyaprincess/Antigravity-DNS/commit/dcb571b). Earlier development milestones have not been reconstructed into a release history.

[Unreleased]: https://github.com/narechiyaprincess/Antigravity-DNS/compare/v1.0.0...main
[1.0.0]: https://github.com/narechiyaprincess/Antigravity-DNS/releases/tag/v1.0.0
