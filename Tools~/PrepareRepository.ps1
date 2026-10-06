param([string]$PublishPath)
$ErrorActionPreference='Stop'
if(!$PublishPath){throw 'Pass an existing isolated publishing checkout with -PublishPath.'}
$circuitRoot=Split-Path $PSScriptRoot -Parent
$circuitPublish=[IO.Path]::GetFullPath($PublishPath)
if(!(Test-Path -LiteralPath (Join-Path $circuitPublish '.git'))){throw 'Publishing directory must be a Git checkout.'}
$circuitExpected=@('README.md','CHANGELOG.md','.gitignore','.gitattributes')
$circuitCopies=@{}
foreach($file in Get-ChildItem -LiteralPath (Join-Path $circuitRoot 'Editor') -File | Where-Object Extension -In '.cs','.asmdef'){$circuitCopies['Editor/'+$file.Name]=$file.FullName}
[xml]$project=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'Runtime/CircuitBreaker.csproj')
foreach($include in $project.Project.ItemGroup.Compile.Include){if($include){$circuitCopies['Tools~/Runtime/'+$include]=Join-Path $PSScriptRoot ('Runtime/'+$include)}}
$circuitCopies['Tools~/Runtime/CircuitBreaker.csproj']=Join-Path $PSScriptRoot 'Runtime/CircuitBreaker.csproj'
foreach($file in Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'Tests') -File -Filter '*.cs'){$circuitCopies['Tools~/Tests/'+$file.Name]=$file.FullName}
foreach($name in @('Build.ps1','AuditBundle.py','PrepareRepository.ps1','RepositoryREADME.md','RepositoryCHANGELOG.md')){$circuitCopies['Tools~/'+$name]=Join-Path $PSScriptRoot $name}
$circuitCopies['README.md']=Join-Path $PSScriptRoot 'RepositoryREADME.md'
$circuitCopies['CHANGELOG.md']=Join-Path $PSScriptRoot 'RepositoryCHANGELOG.md'
$circuitExpected+=@($circuitCopies.Keys)
$circuitTracked=git -C $circuitPublish ls-files
if($LASTEXITCODE){throw 'Cannot read tracked files.'}
# Removing tracked asset files here is recoverable through Git; the working Unity project is never changed.
foreach($relative in $circuitTracked){
    if($relative -in $circuitExpected){continue}
    $absolute=[IO.Path]::GetFullPath((Join-Path $circuitPublish $relative))
    if(!$absolute.StartsWith($circuitPublish+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Path escaped publishing checkout.'}
    if(Test-Path -LiteralPath $absolute){Remove-Item -LiteralPath $absolute}
}
foreach($entry in $circuitCopies.GetEnumerator()){
    $destination=Join-Path $circuitPublish $entry.Key
    New-Item -ItemType Directory -Force -Path (Split-Path $destination -Parent) | Out-Null
    $text=[IO.File]::ReadAllText($entry.Value).Replace("`r`n","`n").TrimEnd()+"`n"
    [IO.File]::WriteAllText($destination,$text)
}
[IO.File]::WriteAllText((Join-Path $circuitPublish '.gitignore'),"**/bin/`n**/obj/`n**/Bundle/`nDelivery~/`nValidation~/`nTools~/Archive*/`nTools~/Decompiled/`n*.nobp`n*.dll`n*.exe`n*.log`n*.meta`n*.prefab`n*.asset`n*.anim`n*.mat`n*.fbx`n*.blend`n*.png`n*.jpg`n*.jpeg`n*.ogg`n")
[IO.File]::WriteAllText((Join-Path $circuitPublish '.gitattributes'),"* text=auto eol=lf`n")
Write-Output ('Prepared code and text only: '+$circuitPublish)
