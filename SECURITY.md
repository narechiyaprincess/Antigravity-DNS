# Security policy

For security fixes, only the latest published stable release is maintained (currently [v1.0.0](https://github.com/narechiyaprincess/Antigravity-DNS/releases/latest)). Older releases are not separate maintenance branches. Reports about current source are also welcome. Support is best effort; there is no guaranteed response or fix schedule.

## Reporting a vulnerability

**Do not post vulnerability details, exploit code, or sensitive diagnostics in public Issues or pull requests.** Use GitHub's [Report a vulnerability](https://github.com/narechiyaprincess/Antigravity-DNS/security/advisories/new) form if it is available. Private vulnerability reporting must be enabled by the maintainer; this document does not imply that it is enabled.

If the private form is unavailable, open a [public issue](https://github.com/narechiyaprincess/Antigravity-DNS/issues/new) asking only for a private security contact. Include no technical details until a private channel has been agreed. There is no dedicated security email published by this project. Please allow time to investigate and coordinate a fix before public disclosure.

Security-sensitive problems include privilege escalation or code execution through elevated scripts/staging, bypasses of ownership or script-integrity checks, unintended DNS-policy changes, certificate-validation bypasses, and disclosure of private data in support reports. Provider outages, browser DNS conflicts, and account restrictions normally belong in [troubleshooting](docs/EN/TROUBLESHOOTING.md).

## Safe diagnostics

After reviewing them, an app version, Windows version/build, selected built-in profile and scope, error code, and minimal reproduction with synthetic data are usually sufficient. Support reports have redaction and a preview, but **review the preview yourself**. Remove usernames, machine/network identifiers, personal paths, tokens, and private DoH URLs. Do not attach raw `state.json`, `settings.json`, user logs, credentials, or memory dumps to public reports.

See [technical security and data boundaries](docs/EN/TECHNICAL.md#security-and-local-data). DNS/SNI providers are independent third parties; a successful probe does not audit their infrastructure or establish their trustworthiness.
