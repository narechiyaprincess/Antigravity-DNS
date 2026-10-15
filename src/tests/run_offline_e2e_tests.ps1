# run_offline_e2e_tests.ps1 - Master Offline E2E Test Suite Runner
# 100% OFFLINE verification with zero host network mutations.
# Supports 5-tier systematic methodology (tier 8: Release 1.6 resilience): Tier 1-4 (1.2.0 contracts) and Tier 5 (1.3.0: single exe, catalog, benchmark, UI) and Tier 6 (1.4 work items) and Tier 7 (1.4 review and 1.5 work items).

[CmdletBinding()]
param(
    [ValidateSet('Current', 'All', '1', '2', '3', '4', '5', '6', '7', '8', '9', '10', '11', '12', '13', '14')][string]$Tier = 'Current',
    [string]$Feature = '',
    
    [string]$CustomScratchPath = ''
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$workRoot = Join-Path $projectRoot '.work'

# Current runs tiers 5-14, which use the current application. Compiled historical
# fixtures are deliberately excluded from the public source tree.
if ($Tier -in @('All','1','2','3','4')) {
    $baselineExe = Join-Path $PSScriptRoot 'fixtures\baseline-1.1.0\AntigravityDnsUnlocker.exe'
    if (-not (Test-Path -LiteralPath $baselineExe)) {
        throw 'Legacy tiers 1-4 require the original baseline-1.1.0 executable. See docs/BUILDING.md. Use -Tier Current for source-checkout verification.'
    }
}

Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host " Antigravity DNS - Systematic Offline E2E Test Runner (tiers 1-14)" -ForegroundColor Cyan
Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host "Timestamp (UTC) : $([DateTime]::UtcNow.ToString('yyyy-MM-dd HH:mm:ssZ'))" -ForegroundColor Gray
Write-Host "Target Tier     : $Tier" -ForegroundColor Gray
Write-Host "Target Feature  : $(if($Feature){$Feature}else{'All Features (F01-F09, P1-P3, R13)'})" -ForegroundColor Gray
Write-Host "Safety Mode     : 100% Offline (Synthetic Wire & Mock Cmdlets)" -ForegroundColor Gray
Write-Host ""

# Import helpers
. (Join-Path $PSScriptRoot 'helpers\TestHarness.ps1')
. (Join-Path $PSScriptRoot 'helpers\SyntheticWire.ps1')
. (Join-Path $PSScriptRoot 'helpers\MockCmdlets.ps1')

$global:TotalCount = 0
$global:PassCount = 0
$global:FailCount = 0
$global:DefectCount = 0
$global:TestResults.Clear()

# 1. Establish Host Safety Baseline (Read-Only)
Write-Host "[Safety Pre-Flight] Capturing host network baseline snapshot..." -ForegroundColor DarkCyan
Initialize-HostSafetyBaseline
$global:HostBaselineRunValue = [string](Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name AntigravityDnsUnlocker -ErrorAction SilentlyContinue).AntigravityDnsUnlocker
Write-Host "  Live state.json SHA256: $global:HostBaselineStateJsonHash" -ForegroundColor DarkGray
Write-Host "  Live NRPT rules (registry): $global:HostBaselineNrptRuleCount; DoH registrations: $global:HostBaselineDohCount" -ForegroundColor DarkGray
Write-Host "  Host Safety Baseline established successfully." -ForegroundColor Green
Write-Host ""

# 2. Prepare Disposable Scratch Directory
$scratch = if ($CustomScratchPath) { 
    $CustomScratchPath 
} else { 
    New-ScratchDirectory -BaseDir $workRoot 
}
Write-Host "[Environment] Disposable Scratch: $scratch" -ForegroundColor DarkCyan
Write-Host ""

$startTime = [System.Diagnostics.Stopwatch]::StartNew()
$fatalError = $null
$hostUnchanged = $true

try {
    # 3. Execute Tiers
    if ($Tier -eq 'All' -or $Tier -eq '1') {
        & (Join-Path $PSScriptRoot 't1_feature_coverage.ps1') -ScratchPath (Join-Path $scratch 't1')
        Write-Host ""
    }
    
    if ($Tier -eq 'All' -or $Tier -eq '2') {
        & (Join-Path $PSScriptRoot 't2_boundary_corner.ps1') -ScratchPath (Join-Path $scratch 't2')
        Write-Host ""
    }
    
    if ($Tier -eq 'All' -or $Tier -eq '3') {
        & (Join-Path $PSScriptRoot 't3_combinations.ps1') -ScratchPath (Join-Path $scratch 't3')
        Write-Host ""
    }
    
    if ($Tier -eq 'All' -or $Tier -eq '4') {
        & (Join-Path $PSScriptRoot 't4_scenarios.ps1') -ScratchPath (Join-Path $scratch 't4')
        Write-Host ""
    }

    if ($Tier -in @('All','Current','5')) {
        & (Join-Path $PSScriptRoot 't5_release_1_3.ps1') -ScratchPath (Join-Path $scratch 't5')
        Write-Host ""
    }

    if ($Tier -in @('All','Current','6')) {
        & (Join-Path $PSScriptRoot 't6_release_1_4.ps1') -ScratchPath (Join-Path $scratch 't6')
        Write-Host ""
    }

    if ($Tier -in @('All','Current','7')) {
        & (Join-Path $PSScriptRoot 't7_release_1_5.ps1') -ScratchPath (Join-Path $scratch 't7')
        Write-Host ""
    }

    if ($Tier -in @('All','Current','8')) {
        & (Join-Path $PSScriptRoot 't8_release_1_6.ps1') -ScratchPath (Join-Path $scratch 't8')
        Write-Host ""
    }

    if ($Tier -in @('All','Current','9')) {
        & (Join-Path $PSScriptRoot 't9_gemini.ps1') -ScratchPath (Join-Path $scratch 't9')
        Write-Host ""
    }

    if ($Tier -in @('All','Current','10')) {
        & (Join-Path $PSScriptRoot 't10_speed.ps1') -ScratchPath (Join-Path $scratch 't10')
        Write-Host ""
    }

    if ($Tier -in @('All','Current','11')) {
        & (Join-Path $PSScriptRoot 't11_google_lang.ps1') -ScratchPath (Join-Path $scratch 't11')
        Write-Host ""
    }

    if ($Tier -in @('All','Current','12')) {
        & (Join-Path $PSScriptRoot 't12_ui_v3.ps1') -ScratchPath (Join-Path $scratch 't12')
        Write-Host ""
    }

    if ($Tier -in @('All','Current','13')) {
        & (Join-Path $PSScriptRoot 't13_ui_v4.ps1') -ScratchPath (Join-Path $scratch 't13')
        Write-Host ""
    }

    if ($Tier -in @('All','Current','14')) {
        & (Join-Path $PSScriptRoot 't14_release_final.ps1') -ScratchPath (Join-Path $scratch 't14')
        Write-Host ""
    }
    
} catch {
    $fatalError = $_
    Write-Host "[ERROR] Test execution aborted: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "        at $($_.InvocationInfo.ScriptName):$($_.InvocationInfo.ScriptLineNumber)  $($_.InvocationInfo.Line.Trim())" -ForegroundColor DarkRed
} finally {
    # 4. Enforce Host Safety Invariant Post-Flight Check
    Write-Host "[Safety Post-Flight] Verifying host network state integrity..." -ForegroundColor DarkCyan
    try {
        Assert-HostSafetyBaselineUnchanged "Final Runner Invariant"
        $liveRun = Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name AntigravityDnsUnlocker -ErrorAction SilentlyContinue
        if ([string]$liveRun.AntigravityDnsUnlocker -ne $global:HostBaselineRunValue) { throw "CRITICAL HOST SAFETY VIOLATION: live HKCU Run entry changed" }
        Write-Host "  CERTIFIED: Zero mutations occurred on Windows host network adapter settings," -ForegroundColor Green
        Write-Host "             live NRPT rules, DoH registrations, or %ProgramData%\state.json." -ForegroundColor Green
    } catch {
        $hostUnchanged = $false
        Write-Host "  $($_.Exception.Message)" -ForegroundColor Red
        $fatalError = $_
    }
    
    Remove-Item -LiteralPath 'HKCU:\Software\AgDnsTest' -Recurse -Force -ErrorAction SilentlyContinue

    # 5. Scratch Cleanup
    if (-not $CustomScratchPath) {
        Write-Host "[Cleanup] Removing disposable scratch directory: $scratch" -ForegroundColor DarkGray
        Remove-ScratchDirectory $scratch
    }
}

$startTime.Stop()

# 6. Reporting and Defect Catalogue Breakdown
Write-Host ""
Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host " Test Execution Summary & Verification Report                         " -ForegroundColor Cyan
Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host "Total Tests Run : $($global:TotalCount)" -ForegroundColor White
Write-Host "Passed Tests    : $($global:PassCount)" -ForegroundColor Green
Write-Host "Defect Findings : $($global:DefectCount) (Known defects reproduced and isolated)" -ForegroundColor Yellow
Write-Host "Failed (Other)  : $($global:FailCount - $global:DefectCount)" -ForegroundColor $(if($global:FailCount - $global:DefectCount -gt 0){"Red"}else{"Green"})
Write-Host "Execution Time  : $($startTime.Elapsed.TotalSeconds.ToString('F2')) seconds" -ForegroundColor Gray
Write-Host ""

if ($global:DefectCount -gt 0) {
    Write-Host "--- Known Implementation Defects Identified for Milestone Remediation ---" -ForegroundColor Yellow
    $defects = $global:TestResults | Where-Object { $_.DefectId } | Group-Object DefectId
    foreach ($d in $defects) {
        Write-Host "  Defect $($d.Name) ($($d.Count) test cases reproduced):" -ForegroundColor Yellow
        foreach ($item in $d.Group) {
            Write-Host "    - [$($item.TestId)] $($item.Description)" -ForegroundColor DarkYellow
            if ($item.Details) {
                Write-Host "      Cause: $($item.Details)" -ForegroundColor DarkGray
            }
        }
    }
    Write-Host ""
}

# Write test receipt JSON artifact
$receiptDir = Join-Path $projectRoot '.work'
if (-not (Test-Path $receiptDir)) { [System.IO.Directory]::CreateDirectory($receiptDir) | Out-Null }
$receiptPath = Join-Path $receiptDir 'offline_e2e_receipt.json'
$receiptObj = [PSCustomObject]@{
    TimestampUtc    = [DateTime]::UtcNow.ToString('o')
    DurationSeconds = $startTime.Elapsed.TotalSeconds
    TotalTests      = $global:TotalCount
    PassCount       = $global:PassCount
    DefectCount     = $global:DefectCount
    FailOtherCount  = ($global:FailCount - $global:DefectCount)
    HostUnchanged   = $hostUnchanged
    Results         = $global:TestResults
}
$receiptJson = $receiptObj | ConvertTo-Json -Depth 6
[System.IO.File]::WriteAllText($receiptPath, $receiptJson, [System.Text.Encoding]::UTF8)
Write-Host "Receipt saved to: $receiptPath" -ForegroundColor DarkCyan
Write-Host ""

if ($fatalError -or $global:FailCount -gt 0) {
    if ($global:DefectCount -gt 0) { Write-Host "Known defect tests failed: treated as FAILURE." -ForegroundColor Red }
    exit 1
}

# If only known defects failed, exit 0 with defect warnings for milestone fix track
if (($global:FailCount - $global:DefectCount) -eq 0) {
    Write-Host "Offline test suite executed cleanly without unhandled errors." -ForegroundColor Green
    exit 0
} else {
    exit 1
}
