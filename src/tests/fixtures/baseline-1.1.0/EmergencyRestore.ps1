$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
$admin=([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if(!$admin){
 try {$argsText='-NoProfile -ExecutionPolicy Bypass -File "'+$PSCommandPath+'"';$p=Start-Process -FilePath (Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe') -ArgumentList $argsText -Verb RunAs -WindowStyle Hidden -PassThru;$p.WaitForExit();exit $p.ExitCode}catch{[Windows.Forms.MessageBox]::Show('UAC не подтверждён. Сетевые настройки не изменены.','Restore Antigravity DNS') | Out-Null;exit 1}
}
try {
 . (Join-Path $PSScriptRoot 'Engine.ps1') -LibraryOnly
 $Action='restore'
 $result=Invoke-Engine
 [Windows.Forms.MessageBox]::Show(($result | ConvertTo-Json -Depth 5),'Restore Antigravity DNS') | Out-Null
 exit 0
}catch{[Windows.Forms.MessageBox]::Show($_.Exception.Message,'Restore Antigravity DNS — требуется проверка') | Out-Null;exit 1}
