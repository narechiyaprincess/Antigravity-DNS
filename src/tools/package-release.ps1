param(
 [string]$ExePath='',
 [switch]$WhatIf
)
# Makes one verified build THE release, repeatably: release\AntigravityDNS.exe (the only file there) and the shortcut
# "Открыть Antigravity DNS.lnk" in the project root, which opens it. The build output keeps the assembly name AntigravityDnsUnlocker.exe;
# nothing in the program depends on its file name. Afterwards the published file is checked: the same SHA-256 as the build and the .csproj version; any mismatch -> exit 1.
# No process is closed. A running copy keeps its file locked, but Windows lets a running exe be renamed: such a file (and anything else in release\)
# is moved to src\.work\replaced\ instead of being deleted, and removed there on a later run once nobody holds it.
# This file is saved with a BOM: Windows PowerShell 5.1 mangles non-ASCII text in a script without one.
$ErrorActionPreference='Stop'
$src=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$repo=(Resolve-Path (Join-Path $src '..')).Path
if(-not $ExePath){$ExePath=Join-Path $src '.work\build\AntigravityDnsUnlocker.exe'}
$exe=(Resolve-Path -LiteralPath $ExePath).Path
$csv=[regex]::Match((Get-Content -LiteralPath (Join-Path $src 'app\AntigravityDnsUnlocker.csproj') -Raw),'<Version>([^<]+)</Version>').Groups[1].Value
$ver=(& $exe --version | Select-Object -First 1).Trim()
if($ver -ne $csv){throw "version mismatch: the exe reports '$ver', the .csproj says '$csv'"}
$hash=(Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash
$rel=Join-Path $repo 'release'
$name='AntigravityDNS.exe'
$dst=Join-Path $rel $name
$aside=Join-Path $src '.work\replaced'
$lnk=Join-Path $repo 'Открыть Antigravity DNS.lnk'
"release build: $exe"
"  version $ver  sha256 $hash"
if($WhatIf){"WhatIf: would put it at $dst and point '$lnk' at it";return}

function MoveAside([string]$path){
 New-Item -ItemType Directory -Force $aside|Out-Null
 $leaf=Split-Path -Leaf $path
 $to=Join-Path $aside ([IO.Path]::GetFileNameWithoutExtension($leaf)+'.old-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+[guid]::NewGuid().ToString('N').Substring(0,6)+[IO.Path]::GetExtension($leaf))
 Move-Item -LiteralPath $path -Destination $to -Force
 "  moved aside: $leaf -> src\.work\replaced\$(Split-Path -Leaf $to)"
}

# leftovers of earlier swaps: removed once nobody holds them
if(Test-Path -LiteralPath $aside){Get-ChildItem -LiteralPath $aside -Force|ForEach-Object{try{Remove-Item -LiteralPath $_.FullName -Recurse -Force -ErrorAction Stop}catch{}}}
New-Item -ItemType Directory -Force $rel|Out-Null
# release\ holds only the exe
Get-ChildItem -LiteralPath $rel -Force|Where-Object{$_.Name -ne $name}|ForEach-Object{MoveAside $_.FullName}
if(-not ((Test-Path -LiteralPath $dst) -and ((Get-FileHash -LiteralPath $dst -Algorithm SHA256).Hash -eq $hash))){
 try{Copy-Item -LiteralPath $exe -Destination $dst -Force -ErrorAction Stop}
 catch{
  MoveAside $dst
  Copy-Item -LiteralPath $exe -Destination $dst -Force
  "  the previous $name was running: it keeps working until it is closed from its tray menu (Выход); open the program again to get this build"
 }
}

# the shortcut always opens release\AntigravityDNS.exe; shortcuts of earlier names go
$ws=New-Object -ComObject WScript.Shell
$s=$ws.CreateShortcut($lnk)
$s.TargetPath=$dst;$s.WorkingDirectory=$rel;$s.IconLocation=$dst+',0';$s.Description='Antigravity DNS';$s.Save()
Get-ChildItem -LiteralPath $repo -Filter '*.lnk' -Force|Where-Object{$_.Name -ne (Split-Path -Leaf $lnk) -and $_.Name -like 'Открыть *DNS.lnk'}|ForEach-Object{Remove-Item -LiteralPath $_.FullName -Force;"  removed old shortcut: $($_.Name)"}

# the checks
$bad=@()
if((Get-FileHash -LiteralPath $dst -Algorithm SHA256).Hash -ne $hash){$bad+='release exe'}
$relVer=(& $dst --version | Select-Object -First 1).Trim()
if($relVer -ne $csv){$bad+='release version'}
$target=$ws.CreateShortcut($lnk).TargetPath
if($target -ne $dst){$bad+=('shortcut -> '+$target)}
"sha256        $hash  release\$name"
"version       $relVer"
"shortcut      $lnk -> $target"
$running=@(Get-Process -Name AntigravityDNS,AntigravityDnsUnlocker -ErrorAction SilentlyContinue)
if($running.Count -gt 0){"note: $($running.Count) copy of the program is running now; it keeps the build it was started from until it is closed (tray menu -> Выход)"}
if($bad.Count -gt 0){"PACKAGE FAIL: "+($bad -join '; ');exit 1}
"PACKAGE PASS: release\$name is $ver, sha256 $hash"
