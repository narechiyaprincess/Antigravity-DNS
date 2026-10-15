param([string]$Tag='run',[switch]$NoBuild,[switch]$NoPublish,[string]$OutDir='.work\build',[string]$ExeOverride='',[string]$Dotnet='', [ValidateSet('Current','All','1','2','3','4','5','6','7','8','9','10','11','12','13','14')][string]$Tier='Current')
# Build, offline tests, UI checks and standalone EXE verification.
# Logs: src\.work\logs\<Tag>. A passing run updates release\ unless -NoPublish is set.
$ErrorActionPreference='Continue'
$root=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $root
$work=Join-Path $root '.work'
# SDK selection: -Dotnet, AGDNS_DOTNET, then PATH.
$dn=if($Dotnet){$Dotnet}elseif($env:AGDNS_DOTNET){$env:AGDNS_DOTNET}else{'dotnet'}
$log=Join-Path $work "logs\$Tag"
New-Item -ItemType Directory -Force $log|Out-Null
$outFull=if([IO.Path]::IsPathRooted($OutDir)){$OutDir}else{Join-Path $root $OutDir}
$exeP=if($ExeOverride){$ExeOverride}else{Join-Path $outFull 'AntigravityDnsUnlocker.exe'}
$env:AGDNS_EXE=$exeP
function RunP($file,$argl,$out,$to){
 $p=New-Object Diagnostics.Process;$p.StartInfo.FileName=$file;$p.StartInfo.Arguments=$argl;$p.StartInfo.UseShellExecute=$false;$p.StartInfo.RedirectStandardOutput=$true;$p.StartInfo.RedirectStandardError=$true;$p.StartInfo.WorkingDirectory=$root
 [void]$p.Start()
 $t1=$p.StandardOutput.ReadToEndAsync();$t2=$p.StandardError.ReadToEndAsync()
 $txt=''
 if(-not $p.WaitForExit($to*1000)){try{$p.Kill()}catch{};$txt='TIMEOUT'+"`r`n"}else{$p.WaitForExit()}
 try{[void][Threading.Tasks.Task]::WaitAll(@($t1,$t2),5000)}catch{}
 $txt+=$t1.Result+$t2.Result
 Set-Content -Encoding UTF8 $out $txt
 return $p.ExitCode
}
if(-not $NoBuild){
 $b=RunP 'powershell.exe' "-NoProfile -File `"$root\app\Build.ps1`" -Dotnet `"$dn`" -OutputDirectory `"$outFull`"" (Join-Path $log 'build.txt') 600
 "BUILD exit=$b"
 if($b -ne 0){exit 1}
}
$s=RunP $exeP '--self-test' (Join-Path $log 'selftest.txt') 300
"SELFTEST exit=$s"
$e=RunP 'powershell.exe' "-NoProfile -ExecutionPolicy Bypass -File `"$root\tests\run_offline_e2e_tests.ps1`" -Tier $Tier" (Join-Path $log 'e2e.txt') 1500
"E2E exit=$e"
$u=RunP $exeP "--ui-smoke `"$work\smoke.png`"" (Join-Path $log 'ui.txt') 300
"UISMOKE exit=$u"
New-Item -ItemType Directory -Force (Join-Path $work 'states')|Out-Null
$u2=RunP $exeP "--ui-smoke `"$work\states\smoke.png`" --states" (Join-Path $log 'ui_states.txt') 300
"UISMOKE_STATES exit=$u2"
$u3=RunP $exeP '--ui-stress' (Join-Path $log 'ui_stress.txt') 240
"UISTRESS exit=$u3"
$u4=RunP $exeP '--ui-surfaces' (Join-Path $log 'ui_surfaces.txt') 120
"UISURFACES exit=$u4"
# the main page at 100 / 125 / 150 / 200 % in both languages, each in a fresh process (snapshots go to src\.work\states-dpi)
New-Item -ItemType Directory -Force (Join-Path $work 'states-dpi')|Out-Null
$scaleBad=0
foreach($d in 96,120,144,192){foreach($en in '','--en'){
 $r=RunP $exeP "--ui-smoke `"$work\states-dpi\s$d$en.png`" --states --dpi $d $en" (Join-Path $log "ui_scale_$d$en.txt") 300
 if($r -ne 0){$scaleBad++}
}}
"UISCALE failures=$scaleBad"
# the exe alone in an empty folder: nothing next to it is needed
$empty=Join-Path ([IO.Path]::GetTempPath()) ('agdns-empty-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $empty|Out-Null
$emptyBad=0
try{
 # under the name it is published with (release\AntigravityDNS.exe): nothing in the program depends on its file name
 $lone=Join-Path $empty 'AntigravityDNS.exe'
 Copy-Item -LiteralPath $exeP -Destination $lone
 $csv=[regex]::Match((Get-Content -LiteralPath (Join-Path $root 'app\AntigravityDnsUnlocker.csproj') -Raw),'<Version>([^<]+)</Version>').Groups[1].Value
 $r1=RunP $lone '--version' (Join-Path $log 'empty_version.txt') 60
 $ver=((Get-Content -LiteralPath (Join-Path $log 'empty_version.txt') -Raw) -replace '\s','')
 if($r1 -ne 0 -or $ver -ne $csv){$emptyBad++}
 $r2=RunP $lone '--self-test' (Join-Path $log 'empty_selftest.txt') 300
 if($r2 -ne 0){$emptyBad++}
 $r3=RunP $lone '--verify-scripts' (Join-Path $log 'empty_verify_scripts.txt') 120
 if($r3 -ne 0 -or -not ((Get-Content -LiteralPath (Join-Path $log 'empty_verify_scripts.txt') -Raw) -match '"Verified": true')){$emptyBad++}
 "EMPTYDIR version=$ver (csproj $csv) failures=$emptyBad"
}finally{Remove-Item -LiteralPath $empty -Recurse -Force -ErrorAction SilentlyContinue}

# Update the release and shortcut only after all checks pass.
$allOk=($s -eq 0 -and $e -eq 0 -and $u -eq 0 -and $u2 -eq 0 -and $u3 -eq 0 -and $u4 -eq 0 -and $scaleBad -eq 0 -and $emptyBad -eq 0)
$pub=0
if($allOk -and -not $NoPublish){
 & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root 'tools\package-release.ps1') -ExePath $exeP *>&1 | Tee-Object -FilePath (Join-Path $log 'publish.txt')
 $pub=$LASTEXITCODE
 "PUBLISH exit=$pub"
}else{"PUBLISH skipped ("+$(if($NoPublish){'-NoPublish'}else{'a check was red'})+")"}
# the runner's scratch folders carry a protected ACL (that is what they test): take them back and remove them so nothing piles up
Get-ChildItem -LiteralPath $work -Directory -Filter 'scratch_*' -ErrorAction SilentlyContinue | ForEach-Object{ & takeown.exe /f $_.FullName /r /d y *>$null; & icacls.exe $_.FullName /grant "$($env:USERNAME):(OI)(CI)F" /t /c *>$null; Remove-Item -LiteralPath $_.FullName -Recurse -Force -ErrorAction SilentlyContinue }
if(-not $allOk -or $pub -ne 0){exit 1}
