# TestHarness.ps1 - Core test harness, assertion engine, and safety verification
# Strictly offline: zero mutations to host network or system state.

$ErrorActionPreference = 'Stop'

# Global suite counters and records
if ($null -eq $global:TestResults) {
    $global:TestResults = [System.Collections.Generic.List[PSObject]]::new()
}
$script:TestResults = $global:TestResults
if ($null -eq $global:TotalCount) { $global:TotalCount = 0 }
if ($null -eq $global:PassCount) { $global:PassCount = 0 }
if ($null -eq $global:FailCount) { $global:FailCount = 0 }
if ($null -eq $global:DefectCount) { $global:DefectCount = 0 }
if ($null -eq $global:CurrentTier) { $global:CurrentTier = "" }
$script:CurrentTier = $global:CurrentTier

# Host invariant state capture.
# Read-only and WMI-free: the Get-DnsClient* CIM cmdlets can hang when the WMI service is unresponsive, so the baseline reads the
# registry stores they front (NRPT rules, DoH registrations, per-interface DNS servers) plus state.json and the HKCU Run entry.
function Get-RegistryValues([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { return @() }
    @(Get-ChildItem -LiteralPath $Path -ErrorAction SilentlyContinue | Sort-Object PSChildName | ForEach-Object {
        $v = Get-ItemProperty -LiteralPath $_.PSPath -ErrorAction SilentlyContinue
        $props = @($v.PSObject.Properties | Where-Object { $_.Name -notlike 'PS*' } | Sort-Object Name | ForEach-Object { "$($_.Name)=$($_.Value -join ',')" })
        "$($_.PSChildName)|$($props -join ';')"
    })
}

function Get-HostFingerprint {
    [CmdletBinding()]
    param()
    $o = [ordered]@{}
    $stateFile = Join-Path $env:ProgramData 'AntigravityDnsUnlocker\state.json'
    $o.StateJsonSha256 = if (Test-Path -LiteralPath $stateFile) { (Get-FileHash -LiteralPath $stateFile -Algorithm SHA256).Hash } else { 'NONE' }
    $o.NrptLocal = @(Get-RegistryValues 'HKLM:\SYSTEM\CurrentControlSet\Services\Dnscache\Parameters\DnsPolicyConfig')
    $o.NrptPolicy = @(Get-RegistryValues 'HKLM:\SOFTWARE\Policies\Microsoft\Windows NT\DNSClient\DnsPolicyConfig')
    $o.Doh = @(Get-RegistryValues 'HKLM:\SYSTEM\CurrentControlSet\Services\Dnscache\Parameters\DohWellKnownServers')
    $o.AdapterDns = @(Get-RegistryValues 'HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces' | ForEach-Object { ($_ -split '\|')[0] + '|' + ((($_ -split '\|', 2)[1] -split ';') | Where-Object { $_ -match '^(NameServer|DhcpNameServer|DohFlags)=' }) -join ';' })
    $o.AdapterDnsV6 = @(Get-RegistryValues 'HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip6\Parameters\Interfaces' | ForEach-Object { ($_ -split '\|')[0] + '|' + ((($_ -split '\|', 2)[1] -split ';') | Where-Object { $_ -match '^(NameServer|DhcpNameServer)=' }) -join ';' })
    $run = Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -ErrorAction SilentlyContinue
    $o.HkcuRunAntigravity = if ($run -and $run.PSObject.Properties['AntigravityDnsUnlocker']) { [string]$run.AntigravityDnsUnlocker } else { 'ABSENT' }
    return $o
}

function Initialize-HostSafetyBaseline {
    [CmdletBinding()]
    param()

    if ($null -ne $global:HostBaselineFingerprint) {
        return
    }
    $fp = Get-HostFingerprint
    $global:HostBaselineFingerprint = $fp
    $global:HostBaselineFingerprintJson = ($fp | ConvertTo-Json -Depth 6 -Compress)
    $global:HostBaselineStateJsonHash = $fp.StateJsonSha256
    $global:HostBaselineNrptRuleCount = @($fp.NrptLocal).Count + @($fp.NrptPolicy).Count
    $global:HostBaselineDohCount = @($fp.Doh).Count
}

if ($null -eq $global:HostBaselineFingerprint) {
    Initialize-HostSafetyBaseline
}

function Assert-HostSafetyBaselineUnchanged {
    [CmdletBinding()]
    param([string]$Context = "General Invariant")

    $now = Get-HostFingerprint
    if ($now.StateJsonSha256 -ne $global:HostBaselineStateJsonHash) {
        throw "CRITICAL HOST SAFETY VIOLATION [$Context]: %ProgramData%\AntigravityDnsUnlocker\state.json was modified! Initial: $($global:HostBaselineStateJsonHash), Current: $($now.StateJsonSha256)"
    }
    foreach ($section in @('NrptLocal', 'NrptPolicy', 'Doh', 'AdapterDns', 'AdapterDnsV6', 'HkcuRunAntigravity')) {
        $before = (@($global:HostBaselineFingerprint[$section]) -join "`n")
        $after = (@($now[$section]) -join "`n")
        if ($before -ne $after) {
            throw "CRITICAL HOST SAFETY VIOLATION [$Context]: live $section changed during the run"
        }
    }
}

function Record-TestResult {
    param(
        [string]$TestId,
        [string]$Description,
        [bool]$Passed,
        [string]$Tier,
        [string]$FeatureId,
        [string]$Details = "",
        [string]$DefectId = ""
    )
    
    $global:TotalCount++
    if ($Passed) {
        $global:PassCount++
        $status = "PASS"
    } else {
        $global:FailCount++
        if ($DefectId) {
            $global:DefectCount++
            $status = "FAIL (DEFECT $DefectId)"
        } else {
            $status = "FAIL"
        }
    }
    
    $tierName = if ($Tier) { $Tier } elseif ($global:CurrentTier) { $global:CurrentTier } else { "General" }
    $record = [PSCustomObject]@{
        TestId      = $TestId
        Description = $Description
        Passed      = $Passed
        Status      = $status
        Tier        = $tierName
        FeatureId   = $FeatureId
        DefectId    = $DefectId
        Details     = $Details
        Timestamp   = [DateTime]::UtcNow.ToString("o")
    }
    $global:TestResults.Add($record)
    
    $color = if ($Passed) { "Green" } elseif ($DefectId) { "Yellow" } else { "Red" }
    Write-Host "  [$status] $TestId - $Description" -ForegroundColor $color
    if (-not $Passed -and $Details) {
        Write-Host "      Details: $Details" -ForegroundColor DarkGray
    }
}

function Assert-Condition {
    param(
        [bool]$Condition,
        [string]$TestId,
        [string]$Description,
        [string]$FeatureId,
        [string]$DefectId = "",
        [string]$FailureMessage = ""
    )
    Record-TestResult -TestId $TestId -Description $Description -Passed $Condition -Tier $global:CurrentTier -FeatureId $FeatureId -Details $FailureMessage -DefectId $DefectId
}

function Assert-EqualValues {
    param(
        $Actual,
        $Expected,
        [string]$TestId,
        [string]$Description,
        [string]$FeatureId,
        [string]$DefectId = ""
    )
    $cond = ($Actual -eq $Expected)
    $msg = if (-not $cond) { "Expected: '$Expected', Actual: '$Actual'" } else { "" }
    Record-TestResult -TestId $TestId -Description $Description -Passed $cond -Tier $global:CurrentTier -FeatureId $FeatureId -Details $msg -DefectId $DefectId
}

function Assert-ThrowsException {
    param(
        [ScriptBlock]$Action,
        [string]$ExpectedPattern,
        [string]$TestId,
        [string]$Description,
        [string]$FeatureId,
        [string]$DefectId = ""
    )
    $caught = $false
    $details = ""
    try {
        & $Action | Out-Null
    } catch {
        $caught = $true
        $msg = $_.Exception.Message
        if ($ExpectedPattern -and ($msg -notlike "*$ExpectedPattern*")) {
            $caught = $false
            $details = "Caught unexpected exception: '$msg', expected pattern: '*$ExpectedPattern*'"
        }
    }
    if (-not $caught -and -not $details) {
        $details = "Expected exception matching '*$ExpectedPattern*' but no exception was thrown."
    }
    Record-TestResult -TestId $TestId -Description $Description -Passed $caught -Tier $global:CurrentTier -FeatureId $FeatureId -Details $details -DefectId $DefectId
}

function New-ScratchDirectory {
    param([string]$BaseDir)
    if (-not $BaseDir) {
        $BaseDir = Join-Path $PSScriptRoot '..\..\.work'
    }
    $guid = [Guid]::NewGuid().ToString('N')
    $scratch = Join-Path $BaseDir ("scratch_" + $guid)
    [System.IO.Directory]::CreateDirectory($scratch) | Out-Null
    return $scratch
}

function Remove-ScratchDirectory {
    param([string]$ScratchPath)
    if ($ScratchPath -and (Test-Path -LiteralPath $ScratchPath)) {
        try {
            Remove-Item -LiteralPath $ScratchPath -Recurse -Force -ErrorAction SilentlyContinue
        } catch {
            # Best effort cleanup
        }
    }
}
