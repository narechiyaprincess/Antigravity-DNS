# Preparing a release

[Build and verification](BUILDING.md) · [Changelog](../CHANGELOG.md)

The [Release artifacts workflow](../.github/workflows/release.yml) prepares files for a future release. It does **not** create a GitHub Release, upload release assets, sign code, or change repository settings. It uses `contents: read` and needs no additional secrets.

## Tag build

1. Review the intended source and update `<Version>` in `src/app/AntigravityDnsUnlocker.csproj` and the changelog for that release. Keep the SDK pin in [global.json](../global.json) deliberate and tested. Do not move or recreate an existing release tag.
2. Run the full local gate on a non-elevated Windows desktop:

   ```powershell
   powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\src\tools\verify.ps1 -Tag release-check -NoPublish
   ```

   Perform the intended Windows 11 manual UI and connection/restoration checks separately on a suitable test machine. Offline success does not prove live provider availability or ownership restoration on a particular installation.
3. After review, the maintainer commits/pushes the approved source and creates/pushes its matching `vX.Y.Z` tag. These are explicit maintainer actions. The tag must contain the workflow itself. The workflow rejects branch refs, prerelease-style tags, malformed versions, and tags that disagree with the project version. A manual workflow run must select an existing matching tag.
4. The job starts from a clean checkout on `windows-2025`, installs the exact SDK from `global.json`, and runs [verify-ci.ps1](../src/tools/verify-ci.ps1): build, reported-version check, self-test, embedded-resource verification, mocked engine tests, and offline tier 14. It does not run live integration or change runner DNS policy. See [hosted CI coverage](BUILDING.md#hosted-ci).

The workflow uploads an Actions artifact named `Antigravity-DNS-vX.Y.Z-win-x64`, retained for 14 days (subject to GitHub policy), containing:

- `Antigravity-DNS-vX.Y.Z-win-x64.exe` — the verified self-contained build under the existing public asset naming convention.
- `BUILDINFO.txt` — tag, source commit, actual SDK, and OS version.
- `SHA256SUMS.txt` — SHA-256 of the EXE and build information, in `hash  filename` format.

Verification logs and the offline receipt are a separate Actions artifact retained for 7 days. A failed check prevents candidate upload. If setup/build fails before logs are created, there may be no diagnostic artifact; inspect the job log.

The SDK pin and clean source make the build procedure repeatable. The hosted Windows image, restored build packs, ReadyToRun output, and artifact ZIP metadata are not locked to a byte-identical toolchain image. **Bit-for-bit reproducibility is not claimed.** Record the build information and verify the checksum of the exact candidate being distributed. Future SDK updates require changing and verifying the pin; Dependabot here only maintains Actions references.

## Manual publication on github.com

After a successful tag job, download and **extract** its candidate artifact from Actions. Compare the EXE hash with `SHA256SUMS.txt`:

```powershell
Get-FileHash -LiteralPath .\Antigravity-DNS-vX.Y.Z-win-x64.exe -Algorithm SHA256
```

Replace `vX.Y.Z` with the actual version. The sums refer to the files inside the downloaded artifact, not GitHub's outer ZIP. Validate that exact EXE on the intended Windows environment before publishing; do not rebuild it and keep the old checksum.

On github.com, prepare a release against the reviewed existing tag, write factual notes from the changelog, and attach the extracted EXE, `BUILDINFO.txt`, and `SHA256SUMS.txt`. Review the notes, tag, and assets before publishing. Preserve existing releases; this workflow does not retrofit or replace v1.0.0. If signing is added later, sign before generating final checksums and verify the signed candidate separately.

No branch-protection rule is assumed. Maintainers may enable Actions if disabled, require the `verify` check for PRs after observing its first successful hosted run, and enable private vulnerability reporting in the repository's security settings. These remain manual settings decisions, not workflow side effects.
