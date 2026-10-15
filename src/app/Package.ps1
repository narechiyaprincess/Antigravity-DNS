# Packages the built exe as AntigravityDnsUnlocker-<version>-win-x64.zip + SHA256SUMS.txt.
# <version> is read from <Version> in AntigravityDnsUnlocker.csproj and must equal what the exe itself reports (--version): one version number everywhere.
# -ReadmePath adds that text file to the archive next to the exe (the release package: the portable folder).
# Nothing is published or uploaded; the output goes to -OutputDirectory (default src\.work\dist).
param(
    [string]$ExePath = (Join-Path $PSScriptRoot '..\.work\build\AntigravityDnsUnlocker.exe'),
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\.work\dist'),
    [string]$ReadmePath = ''
)
$ErrorActionPreference = 'Stop'
$csproj = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'AntigravityDnsUnlocker.csproj') -Raw
$m = [regex]::Match($csproj, '<Version>([^<]+)</Version>')
if (-not $m.Success) { throw 'No <Version> in AntigravityDnsUnlocker.csproj' }
$version = $m.Groups[1].Value
if (-not (Test-Path -LiteralPath $ExePath)) { throw "Exe not found: $ExePath (run Build.ps1 first)" }
$ExePath = (Resolve-Path -LiteralPath $ExePath).Path

$psi = New-Object Diagnostics.ProcessStartInfo $ExePath
$psi.Arguments = '--version'; $psi.UseShellExecute = $false; $psi.RedirectStandardOutput = $true; $psi.CreateNoWindow = $true
$p = [Diagnostics.Process]::Start($psi)
$reported = $p.StandardOutput.ReadToEnd().Trim()
if (-not $p.WaitForExit(30000)) { try { $p.Kill() } catch {}; throw 'The exe did not answer --version within 30 s' }
if ($reported -ne $version) { throw "Version mismatch: the .csproj says $version but the exe reports '$reported'. Rebuild first." }

[IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null
$OutputDirectory = (Resolve-Path -LiteralPath $OutputDirectory).Path
$zipName = "AntigravityDnsUnlocker-$version-win-x64.zip"
$zipPath = Join-Path $OutputDirectory $zipName
if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::Open($zipPath, [IO.Compression.ZipArchiveMode]::Create)
try {
    [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $ExePath, 'AntigravityDnsUnlocker.exe', [IO.Compression.CompressionLevel]::Optimal)
    if ($ReadmePath) { [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, (Resolve-Path -LiteralPath $ReadmePath).Path, (Split-Path -Leaf $ReadmePath), [IO.Compression.CompressionLevel]::Optimal) }
}
finally { $zip.Dispose() }

$zipHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
$exeHash = (Get-FileHash -LiteralPath $ExePath -Algorithm SHA256).Hash.ToLowerInvariant()
$sums = "$zipHash  $zipName`r`n$exeHash  AntigravityDnsUnlocker.exe`r`n"
[IO.File]::WriteAllText((Join-Path $OutputDirectory 'SHA256SUMS.txt'), $sums, (New-Object Text.UTF8Encoding($false)))
Write-Output "Packaged $zipPath"
Write-Output "SHA256SUMS.txt written ($zipName, AntigravityDnsUnlocker.exe)"
