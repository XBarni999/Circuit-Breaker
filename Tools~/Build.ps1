param([string]$GameDir='', [string]$BlueprinterProject='')
$ErrorActionPreference = 'Stop'
$circuitRoot = Split-Path $PSScriptRoot -Parent
$circuitRuntime = Join-Path $PSScriptRoot 'Runtime'
$circuitBundle = Join-Path $circuitRuntime 'Bundle\CircuitBreaker.nobp'
if (!(Test-Path -LiteralPath $circuitBundle)) { throw 'Build the Circuit Breaker Blueprinter bundle in Unity first.' }
$circuitBuildArguments=@('build',(Join-Path $circuitRuntime 'CircuitBreaker.csproj'),'-c','Release')
if($GameDir){$circuitBuildArguments += '-p:GameDir='+$GameDir}
if($BlueprinterProject){$circuitBuildArguments += '-p:BlueprinterProject='+$BlueprinterProject}
dotnet @circuitBuildArguments
if ($LASTEXITCODE -ne 0) { throw 'Release build failed.' }
$circuitDll = Join-Path $circuitRuntime 'bin\Release\net472\Circuit-Breaker.dll'
$circuitAssembly = [Reflection.Assembly]::Load([IO.File]::ReadAllBytes($circuitDll))
$circuitStream = $circuitAssembly.GetManifestResourceStream('CircuitBreaker.CircuitBreaker.nobp')
if ($null -eq $circuitStream) { throw 'Embedded bundle missing.' }
try {
    $circuitHash = [Security.Cryptography.SHA256]::Create()
    $circuitEmbeddedHash = [BitConverter]::ToString($circuitHash.ComputeHash($circuitStream)).Replace('-', '')
    $circuitSourceHash = (Get-FileHash -LiteralPath $circuitBundle -Algorithm SHA256).Hash
    if ($circuitEmbeddedHash -ne $circuitSourceHash) { throw 'Embedded bundle hash mismatch.' }
} finally { $circuitStream.Dispose(); $circuitHash.Dispose() }
$circuitDelivery = Join-Path $circuitRoot 'Delivery~'
New-Item -ItemType Directory -Force -Path $circuitDelivery | Out-Null
Copy-Item -LiteralPath $circuitDll -Destination (Join-Path $circuitDelivery 'Circuit-Breaker.dll') -Force
$circuitDllHash = (Get-FileHash -LiteralPath $circuitDll -Algorithm SHA256).Hash
$circuitReport = "DLL SHA256: $circuitDllHash`nBundle SHA256: $circuitSourceHash`nEmbedded bundle SHA256: $circuitEmbeddedHash`nSource and embedded bundle are byte-identical.`nLive mission and multiplayer validation: not performed.`n"
[IO.File]::WriteAllText((Join-Path $circuitDelivery 'SHA256.txt'), $circuitReport)
[IO.File]::WriteAllText((Join-Path $circuitRoot 'Validation~\package.txt'), $circuitReport)
Write-Output $circuitReport
Write-Output (Join-Path $circuitDelivery 'Circuit-Breaker.dll')
