@{
    RootModule        = 'DnsClient.psm1'
    ModuleVersion     = '9.9.9.9'
    GUID              = '5d6a0a8e-3b0b-4c9e-9a53-0d4e6a2f7c11'
    Author            = 'AgDns offline tests'
    Description       = 'Fake DnsClient used only by offline tests'
    PowerShellVersion = '5.1'
    FunctionsToExport = @('Get-DnsClientNrptRule', 'Get-DnsClientNrptPolicy', 'Get-DnsClientDohServerAddress', 'Get-DnsClientServerAddress', 'Add-DnsClientNrptRule', 'Remove-DnsClientNrptRule', 'Add-DnsClientDohServerAddress', 'Remove-DnsClientDohServerAddress')
    CmdletsToExport   = @()
    VariablesToExport = @()
    AliasesToExport   = @()
}
