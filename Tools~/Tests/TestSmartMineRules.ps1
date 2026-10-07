$ErrorActionPreference='Stop'
$testPath=Join-Path $PSScriptRoot 'SmartMineCheck~'
New-Item -ItemType Directory -Force $testPath | Out-Null
$project='<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net472</TargetFramework><OutputType>Exe</OutputType><LangVersion>latest</LangVersion><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup><ItemGroup><Compile Include="..\SmartMineRulesTest.cs"/><Compile Include="..\..\Runtime\TargetReservations.cs"/></ItemGroup></Project>'
[IO.File]::WriteAllText((Join-Path $testPath 'SmartMineCheck.csproj'),$project)
dotnet build (Join-Path $testPath 'SmartMineCheck.csproj') -c Release
if($LASTEXITCODE){throw 'Smart mine test build failed'}
& (Join-Path $testPath 'bin/Release/net472/SmartMineCheck.exe')
if($LASTEXITCODE){throw 'Smart mine rules failed'}
