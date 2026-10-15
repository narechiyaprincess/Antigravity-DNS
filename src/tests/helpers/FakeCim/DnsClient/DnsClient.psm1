# Fake DnsClient module for offline tests of the "CIM hangs" case (1.4-1).
# Tests put the folder that contains this module FIRST on PSModulePath of a child process, so the REAL engine run by the REAL exe resolves
# Get-DnsClient* to these stubs. No production hook is involved and the real DnsClient module is never loaded.
#   AGDNS_FAKE_CIM = 'hang'  -> every read cmdlet sleeps for 5 minutes (a hung WMI service)
#   AGDNS_FAKE_CIM = 'ok'    -> every read cmdlet returns nothing immediately
# Any cmdlet that would change the system records itself in AGDNS_FAKE_CIM_LOG and throws; a test asserts the log stays empty.

function Wait-Fake { if ($env:AGDNS_FAKE_CIM -eq 'hang') { Start-Sleep -Seconds 300 } }
function Note-Mutation([string]$Name) {
    if ($env:AGDNS_FAKE_CIM_LOG) { Add-Content -LiteralPath $env:AGDNS_FAKE_CIM_LOG -Value $Name -ErrorAction SilentlyContinue }
    throw "FAKE_CIM_MUTATION_ATTEMPTED: $Name"
}

function Get-DnsClientNrptRule { [CmdletBinding()] param($Name) Wait-Fake; @() }
function Get-DnsClientNrptPolicy { [CmdletBinding()] param([switch]$Effective) Wait-Fake; @() }
function Get-DnsClientDohServerAddress { [CmdletBinding()] param($ServerAddress) Wait-Fake; @() }
function Get-DnsClientServerAddress { [CmdletBinding()] param() Wait-Fake; @() }
function Add-DnsClientNrptRule { [CmdletBinding()] param() Note-Mutation 'Add-DnsClientNrptRule' }
function Remove-DnsClientNrptRule { [CmdletBinding()] param() Note-Mutation 'Remove-DnsClientNrptRule' }
function Add-DnsClientDohServerAddress { [CmdletBinding()] param() Note-Mutation 'Add-DnsClientDohServerAddress' }
function Remove-DnsClientDohServerAddress { [CmdletBinding()] param() Note-Mutation 'Remove-DnsClientDohServerAddress' }

Export-ModuleMember -Function Get-DnsClientNrptRule, Get-DnsClientNrptPolicy, Get-DnsClientDohServerAddress, Get-DnsClientServerAddress, Add-DnsClientNrptRule, Remove-DnsClientNrptRule, Add-DnsClientDohServerAddress, Remove-DnsClientDohServerAddress
