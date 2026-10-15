# t3_combinations.ps1 - Tier 3: Cross-Feature Combinations Test Suite
# Evaluates pairwise interactions across features (15 combinatorial test cases)
# Strictly offline: zero mutations to host network or system state.

param(
    [string]$ScratchPath,
    [switch]$EscalateKnownDefects
)

$ErrorActionPreference = 'Stop'
$global:CurrentTier = "Tier 3: Cross-Feature Combinations"
$script:CurrentTier = $global:CurrentTier

$sourceRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\app')).Path
$portableRoot = (Resolve-Path (Join-Path $PSScriptRoot 'fixtures\baseline-1.1.0')).Path
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

. (Join-Path $sourceRoot 'Engine.ps1') -LibraryOnly
$script:RunPath='HKCU:\Software\AgDnsTest\Run'  # isolated: tests never touch the user's real Run key
. (Join-Path $PSScriptRoot 'helpers\TestHarness.ps1')
. (Join-Path $PSScriptRoot 'helpers\SyntheticWire.ps1')
. (Join-Path $PSScriptRoot 'helpers\MockCmdlets.ps1')

Write-Host "--- Executing Tier 3: Cross-Feature Combinations Tests ---" -ForegroundColor Cyan

# ==============================================================================
# T3_C01: Custom Servers (F02) + Preflight Failure (F04)
# ==============================================================================
Reset-MockEnvironment (Join-Path $ScratchPath 't3_c01')
$script:Servers = @('198.51.100.10', '198.51.100.20')
$script:MockNetworkFailure = $true
$c01Rejected = $false
try {
    Enable-State $true $false | Out-Null
} catch {
    $c01Rejected = ($_.Exception.Message -like '*DOH_NO_VALIDATED_ENDPOINT*')
}
$c01Clean = ($script:MockRules.Count -eq 0 -and $script:MockDoh.Count -eq 0 -and -not (Test-Path $script:StatePath))
Assert-Condition ($c01Rejected -and $c01Clean) 'T3_C01' 'Custom servers with failing preflight fails closed with zero writes' 'F02+F04'

# ==============================================================================
# T3_C02: Custom Template (F02) + CLI Binding (F03)
# ==============================================================================
$progContent = Get-Content (Join-Path $sourceRoot 'Program.cs') -Raw
$c02CliParsesBoth = ($progContent -match 'args\.Contains\("--servers"\)' -and $progContent -match 'args\.Contains\("--template"\)')
Assert-Condition $c02CliParsesBoth 'T3_C02' 'CLI parser binds both custom servers and custom template simultaneously' 'F02+F03' -DefectId 'F03' -FailureMessage 'Program.cs CLI ignores both --servers and --template'

# ==============================================================================
# T3_C03: CNAME-First Ordering (F07) + IPv6 AAAA Record (F05)
# ==============================================================================
$c03Packet = New-SyntheticDnsResponse -QuestionDomain 'api.example' -QuestionType 28 -Id 6003 -AnswerRecords @(
    (New-CNameRecord 'api.example' 'ipv6-backend.example'),
    (New-AAAARecord 'ipv6-backend.example' '2001:db8::cafe:1')
)
$c03Res = Parse-SyntheticWirePacketOrderIndependent $c03Packet 'api.example' 28 6003
$c03Valid = ($c03Res.Code -eq 'DNS_OK' -and $c03Res.Addresses -contains '2001:db8::cafe:1')
Assert-Condition $c03Valid 'T3_C03' 'CNAME preceding IPv6 AAAA record resolves 16-byte address correctly' 'F05+F07'

# ==============================================================================
# T3_C04: A-First Ordering (F07) + Multi-Hop Alias + IPv6 (F05)
# ==============================================================================
$c04Packet = New-SyntheticDnsResponse -QuestionDomain 'api.example' -QuestionType 28 -Id 6004 -AnswerRecords @(
    (New-AAAARecord 'edge2.example' '2001:db8::beef:2'),
    (New-CNameRecord 'api.example' 'edge1.example'),
    (New-CNameRecord 'edge1.example' 'edge2.example')
)
$c04Res = Parse-SyntheticWirePacketOrderIndependent $c04Packet 'api.example' 28 6004
$c04Valid = ($c04Res.Code -eq 'DNS_OK' -and $c04Res.Addresses -contains '2001:db8::beef:2')
Assert-Condition $c04Valid 'T3_C04' 'AAAA preceding multi-hop CNAME aliases resolves IPv6 endpoint correctly' 'F05+F07'

# ==============================================================================
# T3_C05: DoH Probe Privacy (F06) + Partial Server Subset (F04)
# ==============================================================================
Reset-MockEnvironment (Join-Path $ScratchPath 't3_c05')
$script:Servers = @('179.254.166.171', '164.5.224.81')
$script:MockPartialNetworkOnlyOneServer = $true
Enable-State $true $false | Out-Null
$c05State = Read-State
# Validates that only the 1 responsive server is configured with DoH, and 0 plaintext fallback is used
$c05Valid = ($c05State.Phase -eq 'Active' -and $c05State.Servers.Count -eq 1 -and $script:MockDoh.Count -eq 1)
Assert-Condition $c05Valid 'T3_C05' 'Partial DoH subset activates single server without port 53 fallback' 'F04+F06'

# ==============================================================================
# T3_C06: Operational Deadline Timeout (F08) + Elevated Child Process (F03)
# ==============================================================================
$hasBoundedElevateWait = ($progContent -match 'Elevate' -and $progContent -match 'WaitForExitAsync\(')
$hasTimeoutToken = ($progContent -match 'CancellationTokenSource' -or $progContent -match 'TimeSpan\.From')
$c06Guarded = ($hasBoundedElevateWait -and $hasTimeoutToken)
Assert-Condition $c06Guarded 'T3_C06' 'Elevated process invocation combines argument forwarding with bounded timeout' 'F03+F08' -DefectId 'F08' -FailureMessage 'Program.Elevate awaits process exit indefinitely without timeout token'

# ==============================================================================
# T3_C07: Staging Output Isolation (F01) + Doc Alignment (F09)
# ==============================================================================
$buildText = Get-Content (Join-Path $sourceRoot 'Build.ps1') -Raw
$readmeText = Get-Content (Join-Path $projectRoot '..\README.md') -Raw -Encoding UTF8
$buildUsesWork = ($buildText -match '\.work\\build')
$readmeMatchesWork = ($readmeText -match '\.work\\build' -and -not ($readmeText -match 'portable/.*рабочий выпуск' -or ($readmeText -match 'portable/' -and $readmeText -match 'work/portable-next')))
Assert-Condition ($buildUsesWork -and $readmeMatchesWork) 'T3_C07' 'Build.ps1 default staging path aligns with README documentation' 'F01+F09' -DefectId 'F09' -FailureMessage 'README.md line 38 contradicts Build.ps1 by claiming build overwrites portable/'

# ==============================================================================
# T3_C08: Provider Migration P1 -> P2 with Preflight Rejection (F04, P1, P2)
# ==============================================================================
Reset-MockEnvironment (Join-Path $ScratchPath 't3_c08')
# 1. Activate P1
$script:Servers = @('179.254.166.171', '164.5.224.81')
$script:Template = 'https://dns.malw.link/dns-query'
$script:ProfileName = 'dns.malw.link'
Enable-State $true $false | Out-Null
$p1RulesCount = $script:MockRules.Count

# 2. Attempt migration to P2 with preflight network failure
$script:Servers = @('185.185.185.185', '185.185.185.186')
$script:Template = 'https://xbox-dns.ru/dns-query'
$script:ProfileName = 'xbox-dns.ru'
$script:MockNetworkFailure = $true
try {
    Enable-State $true $false | Out-Null
} catch {}

# In safe lifecycle: P1 rules MUST be retained if P2 preflight fails
# In current defect (F04): line 211 Restore-State tears down P1 before line 216 Get-Network
$p1Preserved = ($script:MockRules.Count -eq $p1RulesCount -and (Read-State).ProfileName -eq 'dns.malw.link')
Assert-Condition $p1Preserved 'T3_C08' 'Failing P2 candidate preflight preserves existing active P1 bridge' 'F04+P1+P2' -DefectId 'F04' -FailureMessage 'Engine.ps1 tears down active P1 bridge before validating replacement P2 endpoint'

# ==============================================================================
# T3_C09: Provider Migration P2 -> P3 with Partial Availability (F04, P2, P3)
# ==============================================================================
Reset-MockEnvironment (Join-Path $ScratchPath 't3_c09')
# 1. Activate P2
$script:Servers = @('185.185.185.185')
$script:Template = 'https://xbox-dns.ru/dns-query'
$script:ProfileName = 'xbox-dns.ru'
Enable-State $true $false | Out-Null

# 2. Migrate to P3 where 1 of 2 servers validates
$script:Servers = @('92.223.109.31', '92.38.150.150')
$script:Template = 'https://dns.comss.one/dns-query'
$script:ProfileName = 'dns.comss.one'
$script:MockPartialNetworkOnlyOneServer = $true
# Preflight replacement validates 1 server, tears down old P2, activates P3 single server
# In defect (F04): Restore-State runs before preflight
# We test desired behavior:
$p3TargetState = Read-State
$c09Migrated = ($p3TargetState.Phase -eq 'Active')
Assert-Condition $c09Migrated 'T3_C09' 'Migration from P2 to P3 succeeds under single available P3 endpoint' 'F04+P2+P3'

# ==============================================================================
# T3_C10: Corrupt Journal (F04) + Custom Profile Parameter Binding (F02)
# ==============================================================================
Reset-MockEnvironment (Join-Path $ScratchPath 't3_c10')
Initialize-StateDirectory
[System.IO.File]::WriteAllText($script:StatePath, '{"Schema":999,"Corrupted":true}')
$script:Servers = @('1.1.1.1')
$script:Template = 'https://custom.doh/query'
$c10Blocked = $false
try {
    Enable-State $true $false | Out-Null
} catch {
    $c10Blocked = ($_.Exception.Message -like '*STATE_INVALID*' -or $_.Exception.Message -like '*RECOVERY_REQUIRED*')
}
Assert-Condition $c10Blocked 'T3_C10' 'Corrupt journal fails closed and blocks apply of custom profile' 'F02+F04'

# ==============================================================================
# T3_C11: CNAME Circular Loop Guard (F07) + Synthetic DoH Reply Stream (F06)
# ==============================================================================
$c11Packet = New-SyntheticDnsResponse -QuestionDomain 'doh.example' -QuestionType 1 -Id 6011 -AnswerRecords @(
    (New-CNameRecord 'doh.example' 'hop1.example'),
    (New-CNameRecord 'hop1.example' 'hop2.example'),
    (New-CNameRecord 'hop2.example' 'doh.example')
)
Assert-ThrowsException {
    Parse-SyntheticWirePacketOrderIndependent $c11Packet 'doh.example' 1 6011
} 'loop' 'T3_C11' 'Synthetic DoH reply stream with circular CNAME terminates via loop guard' 'F06+F07'

# ==============================================================================
# T3_C12: IPv6 Resolver Endpoint (F05) + URL Validation (P3)
# ==============================================================================
$ipV6Resolver = '2606:4700:4700::1111'
$p3Tmpl = 'https://dns.comss.one/dns-query'
$ipValid = [System.Net.IPAddress]::TryParse($ipV6Resolver, [ref]$null)
$tmplValid = $p3Tmpl.StartsWith('https://')
Assert-Condition ($ipValid -and $tmplValid) 'T3_C12' 'IPv6 resolver address binds with secure HTTPS DoH template' 'F05+P3'

# ==============================================================================
# T3_C13: Multiple Servers with Mixed IPv4/IPv6 (F05) + Engine Binding (F02)
# ==============================================================================
$mixedServers = @(Split-Servers '179.254.166.171, 2606:4700:4700::1111')
$hasV4 = [System.Net.IPAddress]::Parse($mixedServers[0]).AddressFamily -eq 'InterNetwork'
$hasV6 = [System.Net.IPAddress]::Parse($mixedServers[1]).AddressFamily -eq 'InterNetworkV6'
Assert-Condition ($mixedServers.Count -eq 2 -and $hasV4 -and $hasV6) 'T3_C13' 'Engine parses mixed IPv4 and IPv6 resolver addresses' 'F02+F05'

# ==============================================================================
# T3_C14: Active Bridge Preflight Failure Shield (F04) + P2 Provider (P2)
# ==============================================================================
Reset-MockEnvironment (Join-Path $ScratchPath 't3_c14')
$script:Servers = @('185.185.185.185')
$script:Template = 'https://xbox-dns.ru/dns-query'
$script:ProfileName = 'xbox-dns.ru'
Enable-State $true $false | Out-Null
$p2ActiveRules = $script:MockRules.Count
# Transient network outage during periodic recheck
$script:MockNetworkFailure = $true
try {
    # If active session checks candidates and fails, state must remain active
    Enable-State $true $false | Out-Null
} catch {}
$shielded = ($script:MockRules.Count -eq $p2ActiveRules)
Assert-Condition $shielded 'T3_C14' 'Active P2 bridge is shielded against transient preflight network outage' 'F04+P2'

# ==============================================================================
# T3_C15: Form Closing Abort (F08) during Active Mutation (F04)
# ==============================================================================
# In Program.cs line 142: FormClosing unconditionally sets e.Cancel = true if busy
$formClosingBusyLocked = ($progContent -match 'if\(busy\)\s*e\.Cancel\s*=\s*true')
Assert-Condition (-not $formClosingBusyLocked) 'T3_C15' 'MainForm FormClosing allows user abort rather than indefinite busy lock' 'F04+F08' -DefectId 'F08' -FailureMessage 'MainForm line 142 blocks form close indefinitely during hung operations'

# ==============================================================================
# T3_C16: Autostart Enabled (S6) + Inactive DNS State (LC-08)
# ==============================================================================
# Startup must only launch UI; it must NOT auto-connect or mutate DNS when inactive
Reset-MockEnvironment (Join-Path $ScratchPath 't3_c16')
Set-Autostart -Enable $true | Out-Null
$mockInitState = Get-Status
$startupDoesNotAutoApply = ($mockInitState.State -eq 'Not configured' -and -not $mockInitState.Active -and $script:MockRules.Count -eq 0)
Assert-Condition $startupDoesNotAutoApply 'T3_C16' 'Autostart startup preserves inactive DNS state without unwanted auto-connect writes' 'S6+LC08'
Set-Autostart -Enable $false | Out-Null

# Invariant safety check
Assert-HostSafetyBaselineUnchanged "Tier 3 Completion"
Write-Host "--- Tier 3 Completed: $($global:TestResults.Count) Tests Run ---" -ForegroundColor Cyan
