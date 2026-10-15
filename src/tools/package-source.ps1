param([string]$OutputPath='')
$ErrorActionPreference='Stop'
$src=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$repo=(Resolve-Path (Join-Path $src '..')).Path
if(-not $OutputPath){$OutputPath=Join-Path $repo 'source code.zip'}
$OutputPath=[IO.Path]::GetFullPath($OutputPath)

$files=@(Get-Item -LiteralPath (Join-Path $repo 'README.md'),(Join-Path $repo '.gitignore'))
foreach($name in 'LICENSE','.gitattributes'){
 $path=Join-Path $repo $name
 if(Test-Path -LiteralPath $path){$files+=Get-Item -LiteralPath $path}
}
foreach($folder in 'docs','assets\screenshots','.github'){
 $path=Join-Path $repo $folder
 if(Test-Path -LiteralPath $path){
  $files+=Get-ChildItem -LiteralPath $path -Recurse -File -Force | Where-Object{$_.Extension -in '.md','.png','.yml','.yaml'}
 }
}
foreach($folder in 'app','tests','tools'){
 $files+=Get-ChildItem -LiteralPath (Join-Path $src $folder) -Recurse -File -Force | Where-Object{
  $_.Extension -in '.cs','.csproj','.ps1','.psm1','.psd1','.py','.json','.ico','.manifest' -and
  $_.FullName -notmatch '[\\/](bin|obj|\.vs|__pycache__)[\\/]'
 }
}
$files=@($files | Sort-Object FullName -Unique)
if(@($files | Where-Object{$_.FullName -eq $OutputPath}).Count){throw 'Output path overlaps a source file'}

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$parent=[IO.Path]::GetDirectoryName($OutputPath)
[IO.Directory]::CreateDirectory($parent)|Out-Null
$temp=Join-Path $parent ([IO.Path]::GetRandomFileName()+'.zip')
try{
 $zip=[IO.Compression.ZipFile]::Open($temp,[IO.Compression.ZipArchiveMode]::Create)
 try{
  foreach($file in $files){
   $entry=$file.FullName.Substring($repo.Length+1).Replace('\','/')
   [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,$file.FullName,$entry,[IO.Compression.CompressionLevel]::Optimal)
  }
 }finally{$zip.Dispose()}
 $check=[IO.Compression.ZipFile]::OpenRead($temp)
 try{if($check.Entries.Count -ne $files.Count){throw 'Archive entry count mismatch'}}finally{$check.Dispose()}
 [IO.File]::Copy($temp,$OutputPath,$true)
}finally{if([IO.File]::Exists($temp)){[IO.File]::Delete($temp)}}
'SOURCE_PACKAGE_PASS files='+$files.Count
'SHA256 '+(Get-FileHash -LiteralPath $OutputPath -Algorithm SHA256).Hash
$OutputPath
