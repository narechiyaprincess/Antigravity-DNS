[CmdletBinding()]
param(
 [ValidateSet('status','diagnose','enable','restore')][string]$Action='status',
 [switch]$TrustProxy,[switch]$Plain,[switch]$LibraryOnly
)
Set-StrictMode -Version 2
$ErrorActionPreference='Stop'
[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false)
$script:Targets=@('daily-cloudcode-pa.googleapis.com','generativelanguage.googleapis.com')
$script:Servers=@('179.254.166.171','164.5.224.81')
$script:Template='https://dns.malw.link/dns-query'
$script:Root=Join-Path $env:ProgramData 'AntigravityDnsUnlocker'
$script:StatePath=Join-Path $script:Root 'state.json'
$script:Exe=Join-Path $PSScriptRoot 'AntigravityDnsUnlocker.exe'

function Test-Admin {
 return ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}
function Get-Rules { return @(Get-DnsClientNrptRule -ErrorAction Stop | Select-Object Name,Namespace,NameServers,Comment,DisplayName,DirectAccessDnsServers,DirectAccessEnabled,DirectAccessProxyName,DirectAccessProxyType,DirectAccessQueryIPsecEncryption,DirectAccessQueryIPsecRequired,DnsSecEnabled,DnsSecQueryIPsecEncryption,DnsSecQueryIPsecRequired,DnsSecValidationRequired,IPsecCARestriction,NameEncoding) }
function Get-Effective { return @(Get-DnsClientNrptPolicy -Effective -ErrorAction Stop | Select-Object Namespace,NameServers) }
function Get-Doh { return @(Get-DnsClientDohServerAddress -ErrorAction Stop | Select-Object ServerAddress,DohTemplate,AutoUpgrade,AllowFallbackToUdp) }
function Split-Servers($Value) { return @((@($Value) -join ',') -split '[,;\s]+' | Where-Object {$_}) }
function Same-Set($Left,$Right) { return ((@($Left | Sort-Object -Unique) -join ',') -eq (@($Right | Sort-Object -Unique) -join ',')) }
function Test-Overlap([string]$Namespace,[string]$Domain) {
 $ns=$Namespace.ToLowerInvariant();$domain=$Domain.ToLowerInvariant()
 return ($ns -eq '.' -or $ns -eq $domain -or ($ns.StartsWith('.') -and ($domain -eq $ns.Substring(1) -or $domain.EndsWith($ns))) -or $ns.StartsWith('*') -or $ns.Contains('/'))
}
function Get-Conflicts($Rules,$Owner) {
 return @($Rules | Where-Object {
  $rule=$_; if($null -eq $rule){return $false}; $owned=($Owner -and $rule.PSObject.Properties['Comment'] -and $rule.Comment -eq ('AgDns:'+ $Owner) -and $rule.DisplayName -eq ('AgDns '+$Owner))
  !$owned -and @($script:Targets | Where-Object { $t=$_; @($rule.Namespace | Where-Object {Test-Overlap $_ $t}).Count -gt 0 }).Count -gt 0
 })
}
function Read-State {
 if(!(Test-Path -LiteralPath $script:StatePath)){return $null}
 try {
  $s=Get-Content -LiteralPath $script:StatePath -Raw -Encoding UTF8 | ConvertFrom-Json
  if($s.Schema -ne 1 -or $s.Owner -notmatch '^[a-f0-9]{32}$' -or $s.Phase -notin @('Applying','Active','Restoring','Restored','Failed') -or $s.Protocol -notin @('DoH','Plain DNS')){throw 'schema'}
  if((!(Same-Set @($s.Targets) $script:Targets) -and !(Same-Set @($s.Targets) @('daily-cloudcode-pa.googleapis.com'))) -or @($s.Servers).Count -lt 1 -or @($s.Servers | Where-Object {$_ -notin $script:Servers}).Count -gt 0){throw 'scope'}
  if(@($s.Rules).Count -ne @($s.Targets).Count -or !(Same-Set @($s.Rules | ForEach-Object {$_.Namespace}) $s.Targets)){throw 'rules scope'}
  if(($s.Protocol -eq 'DoH' -and !(Same-Set @($s.Doh | ForEach-Object {$_.Server}) $s.Servers)) -or ($s.Protocol -eq 'Plain DNS' -and @($s.Doh).Count -ne 0)){throw 'doh scope'}
  foreach($r in @($s.Rules)) {
   if($r.Namespace -notin $script:Targets -or ($r.Name -and $r.Name -notmatch '^\{?[a-fA-F0-9-]{36}\}?$') -or ($r.Fingerprint -and ($r.Fingerprint -isnot [string] -or $r.Fingerprint.Length -gt 8192))){throw 'rules'}
  }
  foreach($d in @($s.Doh)) {
   if($d.Server -notin $s.Servers -or $d.Template -ne $script:Template -or $d.Created -isnot [bool] -or $d.Ownership -notin @('Planned','Pending','Owned','Shared','Relinquished')){throw 'doh'}
  }
  return $s
 }catch {throw 'STATE_INVALID: journal unreadable; no system settings changed. Preserve state.json for recovery.'}
}
function Initialize-StateDirectory {
 if(!(Test-Path -LiteralPath $script:Root)){[IO.Directory]::CreateDirectory($script:Root) | Out-Null}
 $item=Get-Item -LiteralPath $script:Root
 if($item.Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'STATE_PATH_REPARSE_POINT'}
 $acl=New-Object Security.AccessControl.DirectorySecurity
 $acl.SetAccessRuleProtection($true,$false)
 foreach($sid in @('S-1-5-18','S-1-5-32-544')) {
  $rule=New-Object Security.AccessControl.FileSystemAccessRule ([Security.Principal.SecurityIdentifier]::new($sid),'FullControl','ContainerInherit,ObjectInherit','None','Allow'); $acl.AddAccessRule($rule)
 }
 $acl.AddAccessRule((New-Object Security.AccessControl.FileSystemAccessRule ([Security.Principal.SecurityIdentifier]::new('S-1-5-32-545'),'ReadAndExecute','ContainerInherit,ObjectInherit','None','Allow')))
 $acl.SetOwner([Security.Principal.SecurityIdentifier]::new('S-1-5-32-544'));Set-Acl -LiteralPath $script:Root -AclObject $acl
}
function Save-State($State) {
 $State.UpdatedUtc=[DateTime]::UtcNow.ToString('o')
 $temp=Join-Path $script:Root ('state-'+[guid]::NewGuid().ToString('N')+'.tmp')
 [IO.File]::WriteAllText($temp,($State | ConvertTo-Json -Depth 10),[Text.UTF8Encoding]::new($false))
 if(Test-Path -LiteralPath $script:StatePath){[IO.File]::Replace($temp,$script:StatePath,(Join-Path $script:Root 'state.previous.json'))}else{[IO.File]::Move($temp,$script:StatePath)}
}
function Get-Network {
 $raw=& $script:Exe --network
 if($LASTEXITCODE -ne 0){throw 'NETWORK_PROBE_FAILED'}
 return ($raw | ConvertFrom-Json)
}
function Get-SystemAnswers([string]$Domain,[string]$Type='A') {
 try { $raw=& $script:Exe --system-dns $Domain $Type;if($LASTEXITCODE -ne 0){return @()};$reply=$raw | ConvertFrom-Json;return @($reply.Addresses) }catch{return @()}
}
function Test-HostsConflict {
 $lines=Get-Content -LiteralPath (Join-Path $env:WINDIR 'System32\drivers\etc\hosts') -ErrorAction Stop
 foreach($line in $lines) { $names=@(($line -split '#')[0].Trim() -split '\s+' | Select-Object -Skip 1);foreach($t in $script:Targets){if($t -in $names){return $true}} }
 return $false
}
function Get-Status {
 $s=Read-State; $rules=@(Get-Rules); $effective=@(Get-Effective)
 $conflicts=@(Get-Conflicts (@($rules)+@($effective)) $(if($s){$s.Owner}else{''}))
 # Effective policies have no ownership field; remove only policies matching our journal.
 if($s){$conflicts=@($conflicts | Where-Object { $policy=$_; @($s.Targets | Where-Object {Same-Set $policy.Namespace @($_)}).Count -ne 1 -or !(Same-Set (Split-Servers $policy.NameServers) $s.Servers) -or $policy.PSObject.Properties['Name'] })}
 $active=$false;$state='Not configured';$code='NOT_CONFIGURED'
 if($s -and $s.Phase -ne 'Restored'){
  $owned=@($rules | Where-Object {$_.Comment -eq ('AgDns:'+$s.Owner)})
  $active=($s.Phase -eq 'Active' -and @($s.Targets | Where-Object {$t=$_;@($owned | Where-Object {Same-Set $_.Namespace @($t)}).Count -eq 1}).Count -eq $s.Targets.Count)
  $state='Failed (restorable)';$code='RECOVERY_REQUIRED'
  if($active -and $s.Phase -eq 'Active'){
   try {
    if(!(Same-Set $s.Targets $script:Targets)){throw 'RESTORE_BEFORE_SCOPE_UPGRADE'}
    foreach($intent in $s.Rules){$r=@($owned | Where-Object {$_.Name -eq $intent.Name});if($r.Count -ne 1){throw 'RECOVERY_REQUIRED'};Assert-OwnedRule $r[0] $s $intent.Namespace}
    if($s.Protocol -eq 'DoH'){foreach($d in $s.Doh){$registration=@(Get-Doh | Where-Object {$_.ServerAddress -eq $d.Server});if($registration.Count -ne 1 -or $registration[0].DohTemplate -ne $d.Template -or !$registration[0].AutoUpgrade -or $registration[0].AllowFallbackToUdp){throw 'DOH_REGISTRATION_CONFLICT'}}}
    foreach($t in $s.Targets){$policy=@($effective | Where-Object {Same-Set $_.Namespace @($t)} | Where-Object {Same-Set (Split-Servers $_.NameServers) $s.Servers});if($policy.Count -ne 1){throw 'NRPT_NOT_EFFECTIVE'}}
    $state='DNS active';$code='DNS_CONFIGURED_APPLICATION_NOT_TESTED'
   }catch{$active=$false;$code=$_.Exception.Message}
  }
 }
 if($conflicts.Count -gt 0 -or (Test-HostsConflict)){$active=$false;$state='Conflict';$code='NRPT_OR_HOSTS_CONFLICT'}
 [pscustomobject]@{State=$state;Code=$code;Resolver='dns.malw.link';Protocol=$(if($s -and $s.Phase -ne 'Restored'){$s.Protocol}else{'Not configured'});Targets=$script:Targets;Servers=@(if($s -and $s.Phase -ne 'Restored'){$s.Servers});Rules=@(if($s){$s.Rules});JournalPhase=$(if($s){$s.Phase}else{'Absent'});Conflicts=@($conflicts | Select-Object Namespace,NameServers);TransportProof='Not verified on wire';Application='AUTH_REQUIRED_FOR_E2E_TEST';Active=$active}
}
function Diagnose {
 $status=Get-Status;$network=Get-Network;$effective=@(Get-Effective);$s=Read-State
 $answers=@(foreach($t in $script:Targets){[pscustomobject]@{Host=$t;A=@(Get-SystemAnswers $t 'A');AAAA=@(Get-SystemAnswers $t 'AAAA')}})
 $nrpt='Not configured'
 if($status.Active){
  $policyOk=(@($script:Targets | Where-Object {$t=$_;@($effective | Where-Object {Same-Set $_.Namespace @($t)} | Where-Object {Same-Set (Split-Servers $_.NameServers) $s.Servers}).Count -eq 1}).Count -eq $script:Targets.Count)
  $answerOk=@($answers | Where-Object {$entry=$_;$t=$entry.Host;$expected=@($network.Candidates | Where-Object {$_.Server -in $s.Servers} | ForEach-Object {$_.TargetAnswers.PSObject.Properties[$t].Value});$entry.A.Count -lt 1 -or @($entry.A | Where-Object {$_ -notin $expected}).Count -gt 0}).Count -eq 0
  $nrpt=if($policyOk -and $answerOk){'Policy + Windows resolver answers match (wire destination unverified)'}else{'NRPT_NOT_EFFECTIVE'}
  if(!$policyOk -or !$answerOk){$status.Active=$false;$status.State='Failed (restorable)';$status.Code='NRPT_NOT_EFFECTIVE'}
 }
 [pscustomobject]@{State=$status.State;Code=$status.Code;Status=$status;Network=$network;SystemResolver=$answers;NrptEffective=$nrpt;DoHEncrypted='Direct probe: see Network; Windows resolver wire encryption: NOT VERIFIED';ControlAnswers=@(Get-SystemAnswers 'www.microsoft.com');VpnPresent=@(Get-NetAdapter | Where-Object {$_.Status -eq 'Up' -and $_.InterfaceDescription -match 'VPN|Tunnel|tun'} | Select-Object -ExpandProperty InterfaceDescription);Application='AUTH_REQUIRED_FOR_E2E_TEST';Note='Provider uses DNS + third-party SNI proxy. TLS verification stays enabled. DNS answers do not prove account eligibility or model response.'}
}
function Assert-Capabilities([bool]$Encrypted) {
 foreach($cmd in @('Get-DnsClientNrptRule','Get-DnsClientNrptPolicy','Add-DnsClientNrptRule','Remove-DnsClientNrptRule')){if(!(Get-Command $cmd -ErrorAction SilentlyContinue)){throw 'NRPT_UNSUPPORTED_BY_OS'}}
 if($Encrypted){foreach($cmd in @('Get-DnsClientDohServerAddress','Add-DnsClientDohServerAddress','Remove-DnsClientDohServerAddress')){if(!(Get-Command $cmd -ErrorAction SilentlyContinue)){throw 'DOH_UNSUPPORTED_BY_OS'}}}
}
function Assert-OwnedRule($Rule,$State,$Namespace) {
 if($Rule.Comment -ne ('AgDns:'+$State.Owner) -or $Rule.DisplayName -ne ('AgDns '+$State.Owner) -or !(Same-Set $Rule.Namespace @($Namespace)) -or !(Same-Set (Split-Servers $Rule.NameServers) $State.Servers)){throw 'OWNED_RULE_CHANGED: retained, no automatic overwrite'}
 $intent=@($State.Rules | Where-Object {$_.Namespace -eq $Namespace})[0]
 $expected=$intent.Fingerprint
 if(!$expected){$expected=Rule-Fingerprint ([pscustomobject]@{Comment=('AgDns:'+$State.Owner);DisplayName=('AgDns '+$State.Owner);Namespace=@($Namespace);NameServers=$State.Servers;NameEncoding='Disable'})}
 if((Rule-Fingerprint $Rule) -ne $expected){throw 'OWNED_RULE_CHANGED: security/encoding/auxiliary policy edited; retained'}
}
function Rule-Fingerprint($Rule) {
 $data=[ordered]@{}
 foreach($name in @('Comment','DisplayName','Namespace','NameServers','DirectAccessDnsServers','DirectAccessEnabled','DirectAccessProxyName','DirectAccessProxyType','DirectAccessQueryIPsecEncryption','DirectAccessQueryIPsecRequired','DnsSecEnabled','DnsSecQueryIPsecEncryption','DnsSecQueryIPsecRequired','DnsSecValidationRequired','IPsecCARestriction','NameEncoding')){
  $value=$null;if($Rule.PSObject.Properties[$name]){$value=$Rule.$name}
  if($name -in @('Namespace','NameServers','DirectAccessDnsServers')){$data[$name]=@((Split-Servers $value) | ForEach-Object {$_.ToLowerInvariant()} | Sort-Object -Unique)}
  elseif($name -in @('DirectAccessEnabled','DirectAccessQueryIPsecRequired','DnsSecEnabled','DnsSecQueryIPsecRequired','DnsSecValidationRequired')){$data[$name]=[bool]$value}
  elseif($name -in @('Comment','DisplayName')){$data[$name]=[string]$value}
  else{$data[$name]=([string]$value).ToLowerInvariant()}
 }
 return ($data | ConvertTo-Json -Depth 4 -Compress)
}
function Restore-State($s) {
 if(!$s -or $s.Phase -eq 'Restored'){return [pscustomobject]@{State='Not configured';Code='ROLLED_BACK';Result='No-op'}}
 $s.Phase='Restoring';Save-State $s
 foreach($intent in @($s.Rules)) {
  $rules=@(Get-Rules)
  $candidates=@($rules | Where-Object {($intent.Name -and $_.Name -eq $intent.Name) -or (!$intent.Name -and $_.Comment -eq ('AgDns:'+$s.Owner) -and (Same-Set $_.Namespace @($intent.Namespace)))})
  foreach($r in $candidates){Assert-OwnedRule $r $s $intent.Namespace; Remove-DnsClientNrptRule -Name $r.Name -Force -ErrorAction Stop | Out-Null}
 }
 if(@(Get-Rules | Where-Object {$_.Comment -eq ('AgDns:'+$s.Owner)}).Count -gt 0){throw 'OWNED_RULES_REMAIN'}
 $retained=@();$ambiguous=@()
 foreach($d in @($s.Doh | Where-Object {$_.Created})) {
  $current=@(Get-Doh | Where-Object {$_.ServerAddress -eq $d.Server})
  if($current.Count -eq 0){continue}
  if($d.Ownership -in @('Planned','Shared','Relinquished')){continue}
  if($d.Ownership -eq 'Pending'){$ambiguous+= $d.Server;continue}
  if($current.Count -ne 1 -or $current[0].DohTemplate -ne $d.Template -or !$current[0].AutoUpgrade -or $current[0].AllowFallbackToUdp){throw 'DOH_CHANGED_INDEPENDENTLY: registration retained'}
  $refs=@(Get-Rules | Where-Object {$d.Server -in (Split-Servers $_.NameServers)})+@(Get-Effective | Where-Object {$d.Server -in (Split-Servers $_.NameServers)})+@(Get-DnsClientServerAddress | Where-Object {$d.Server -in $_.ServerAddresses})
  if($refs.Count -gt 0){$retained+= $d.Server;$d.Ownership='Relinquished';Save-State $s;continue}
  Remove-DnsClientDohServerAddress -ServerAddress $d.Server -ErrorAction Stop | Out-Null
  if(@(Get-Doh | Where-Object {$_.ServerAddress -eq $d.Server}).Count -gt 0){throw 'DOH_RESTORE_FAILED'}
 }
 if($ambiguous.Count -gt 0){throw ('DOH_OWNERSHIP_UNCERTAIN: pending registration retained for administrator review: '+($ambiguous -join ','))}
 $s.Phase='Restored';Save-State $s
 return [pscustomobject]@{State='Not configured';Code=$(if($retained.Count -gt 0){'ROLLED_BACK_SHARED_DOH_RETAINED'}else{'ROLLED_BACK'});Result='Owned policies removed; adapters/hosts/proxy unchanged';RetainedSharedDoh=$retained}
}
function Enable-State([bool]$Trust,[bool]$Unencrypted) {
 if(!$Trust){throw 'TRUST_DECISION_REQUIRED: dns.malw.link can send HTTPS through third-party SNI proxy'}
 Assert-Capabilities (!$Unencrypted)
 $s=Read-State
 if($s -and $s.Phase -eq 'Active'){
  if(!(Same-Set $s.Targets $script:Targets)){throw 'RESTORE_BEFORE_SCOPE_UPGRADE'}
  if($s.Protocol -ne $(if($Unencrypted){'Plain DNS'}else{'DoH'})){throw 'RESTORE_BEFORE_PROTOCOL_CHANGE'}
  foreach($intent in $s.Rules){$r=@(Get-Rules | Where-Object {$_.Name -eq $intent.Name});if($r.Count -ne 1){throw 'RECOVERY_REQUIRED'};Assert-OwnedRule $r[0] $s $intent.Namespace}
  if($s.Protocol -eq 'DoH'){foreach($d in $s.Doh){$current=@(Get-Doh | Where-Object {$_.ServerAddress -eq $d.Server});if($current.Count -ne 1 -or $current[0].DohTemplate -ne $d.Template -or !$current[0].AutoUpgrade -or $current[0].AllowFallbackToUdp){throw 'DOH_REGISTRATION_CONFLICT'}}}
  foreach($t in $s.Targets){$p=@(Get-Effective | Where-Object {Same-Set $_.Namespace @($t)} | Where-Object {Same-Set (Split-Servers $_.NameServers) $s.Servers});if($p.Count -ne 1){throw 'NRPT_NOT_EFFECTIVE'}}
  $status=Get-Status;if($status.State -ne 'DNS active'){throw 'NRPT_CONFLICT'};return $status
 }
 if($s -and $s.Phase -ne 'Restored'){throw 'RECOVERY_REQUIRED: Restore unfinished journal first'}
 if(@(Get-Conflicts @(Get-Rules) '').Count -gt 0 -or @(Get-Conflicts @(Get-Effective) '').Count -gt 0 -or (Test-HostsConflict)){throw 'NRPT_CONFLICT'}
 $net=Get-Network
 $selected=@($net.Candidates | Where-Object {if($Unencrypted){$_.PlainValidated}else{$_.DohValidated}} | Select-Object -ExpandProperty Server)
 if($selected.Count -lt 1){throw $(if($Unencrypted){'DNS_TIMEOUT_OR_UNREACHABLE_OR_TLS_FAILURE'}else{'DOH_NO_VALIDATED_ENDPOINT: plaintext fallback disabled'})}
 $doh=@(if(!$Unencrypted){Get-Doh})
 foreach($ip in $selected){$old=@($doh | Where-Object {$_.ServerAddress -eq $ip});if($old.Count -gt 0 -and ($old.Count -ne 1 -or $old[0].DohTemplate -ne $script:Template -or !$old[0].AutoUpgrade -or $old[0].AllowFallbackToUdp)){throw 'DOH_REGISTRATION_CONFLICT'}}
 Initialize-StateDirectory
 $owner=[guid]::NewGuid().ToString('N')
 $s=[pscustomobject]@{Schema=1;Owner=$owner;Phase='Applying';Protocol=$(if($Unencrypted){'Plain DNS'}else{'DoH'});Targets=$script:Targets;Servers=$selected;Rules=@(foreach($t in $script:Targets){[pscustomobject]@{Namespace=$t;Name=$null;Fingerprint=$null}});Doh=@(if(!$Unencrypted){foreach($ip in $selected){$new=(@($doh | Where-Object {$_.ServerAddress -eq $ip}).Count -eq 0);[pscustomobject]@{Server=$ip;Template=$script:Template;Created=$new;Ownership=$(if($new){'Planned'}else{'Shared'})}}});UpdatedUtc=''}
 Save-State $s
 try {
  foreach($d in @($s.Doh | Where-Object {$_.Created})) {
   $d.Ownership='Pending';Save-State $s
   Add-DnsClientDohServerAddress -ServerAddress $d.Server -DohTemplate $d.Template -AllowFallbackToUdp $false -AutoUpgrade $true -ErrorAction Stop | Out-Null
   $check=@(Get-Doh | Where-Object {$_.ServerAddress -eq $d.Server});if($check.Count -ne 1 -or $check[0].DohTemplate -ne $d.Template -or !$check[0].AutoUpgrade -or $check[0].AllowFallbackToUdp){throw 'DOH_ADD_VERIFICATION_FAILED'}
   $d.Ownership='Owned';Save-State $s
  }
  foreach($intent in $s.Rules) {
   $r=Add-DnsClientNrptRule -Namespace $intent.Namespace -NameServers $s.Servers -NameEncoding Disable -DAProxyType '' -DAIPsecEncryptionType '' -DnsSecIPsecEncryptionType '' -DAEnable:$false -DAIPsecRequired:$false -DnsSecEnable:$false -DnsSecValidationRequired:$false -DnsSecIPsecRequired:$false -DisplayName ('AgDns '+$owner) -Comment ('AgDns:'+$owner) -PassThru -ErrorAction Stop
   $intent.Name=[string]$r.Name;$intent.Fingerprint=Rule-Fingerprint $r;Save-State $s
   Assert-OwnedRule $r $s $intent.Namespace
  }
  $effective=@(Get-Effective)
  foreach($t in $s.Targets){$match=@($effective | Where-Object {Same-Set $_.Namespace @($t)} | Where-Object {Same-Set (Split-Servers $_.NameServers) $s.Servers});if($match.Count -ne 1){throw 'NRPT_NOT_EFFECTIVE'}}
  # Resolve through Windows APIs, never an explicit -Server query as NRPT proof.
  foreach($t in $s.Targets){$answer=@(Get-SystemAnswers $t);$expected=@($net.Candidates | Where-Object {$_.Server -in $s.Servers} | ForEach-Object {$_.TargetAnswers.PSObject.Properties[$t].Value});if($answer.Count -lt 1 -or @($answer | Where-Object {$_ -notin $expected}).Count -gt 0){throw 'NRPT_NOT_EFFECTIVE: Windows resolver differs; cache/TUN/app resolver may interfere'}}
  $s.Phase='Active';Save-State $s;return Get-Status
 }catch {
  $failure=$_.Exception.Message
  try {$rollback=Restore-State $s;throw ('APPLY_FAILED_ROLLED_BACK: '+$failure)}catch{if($_.Exception.Message -like 'APPLY_FAILED_ROLLED_BACK:*'){throw};$s.Phase='Failed';Save-State $s;throw ('FAILED_RESTORABLE: '+$failure+'; restore: '+$_.Exception.Message)}
 }
}
function Invoke-Engine {
 if($Action -in @('enable','restore') -and !(Test-Admin)){throw 'ADMIN_REQUIRED: approve Windows UAC for this operation'}
 $mutex=$null;$held=$false
 try {
  if($Action -in @('enable','restore')){$mutex=New-Object Threading.Mutex($false,'Global\AntigravityDnsUnlockerMutation');try{$held=$mutex.WaitOne(0)}catch [Threading.AbandonedMutexException]{$held=$true};if(!$held){throw 'OPERATION_IN_PROGRESS'};Initialize-StateDirectory}
  switch($Action){'status'{Get-Status};'diagnose'{Diagnose};'restore'{Restore-State (Read-State)};'enable'{Enable-State ([bool]$TrustProxy) ([bool]$Plain)}}
 }finally{if($held){$mutex.ReleaseMutex()};if($mutex){$mutex.Dispose()}}
}
if(!$LibraryOnly){
 try {Invoke-Engine | ConvertTo-Json -Depth 12;exit 0}catch{[pscustomobject]@{State='Failed (restorable)';Code=$_.Exception.Message;SystemChanges='Use --restore if an ownership journal is present'} | ConvertTo-Json -Depth 3;exit 1}
}
