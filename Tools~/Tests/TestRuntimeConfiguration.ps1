param([string]$GameDir='F:/Games/Nuclear.Option.v0.34.1')
$ErrorActionPreference='Stop'
$testRoot=Join-Path $PSScriptRoot 'ConfigurationCheck~'
New-Item -ItemType Directory -Force -Path $testRoot | Out-Null
$plugin=[IO.File]::ReadAllText((Join-Path (Split-Path $PSScriptRoot -Parent) 'Runtime/Plugin.cs'))
$bindings=[regex]::Matches($plugin,'(?m)^\s*(Radius|Duration|TriggerRange|EmissionDuration|MineLife|DescentAngle|DescentRamp)=Config\.Bind.*;\s*$')
if($bindings.Count -ne 7){throw 'Cannot extract all production configuration bindings.'}
$body=($bindings | ForEach-Object {$_.Value.Trim().Replace('Config.Bind','config.Bind')}) -join "`n"
$source=@'
using System;
using System.IO;
using BepInEx.Configuration;
class ConfigurationCheck {
    static void Main(string[] args){
        string path=Path.Combine(args[0],"test.cfg");
        File.WriteAllText(path,"[Blackout]\nSuppressionSeconds = 18\n");
        var config=new ConfigFile(path,false);config.SaveOnConfigSet=false;
        ConfigEntry<float> Radius,Duration,TriggerRange,EmissionDuration,MineLife,DescentAngle,DescentRamp;
        BINDINGS
        if(Duration.Value!=4)throw new Exception("Legacy recovery configuration was not clamped to four seconds");
        if(Radius.Value!=10000||TriggerRange.Value!=10000||EmissionDuration.Value!=20||MineLife.Value!=300)throw new Exception("Configuration defaults changed");
        config.Save();
        Console.WriteLine("RUNTIME_CONFIGURATION_PASSED: all seven production bindings execute using installed BepInEx; legacy recovery 18 -> 4.");
    }
}
'@
$source=$source.Replace('BINDINGS',$body)
$sourcePath=Join-Path $testRoot 'ConfigurationCheck.cs'
[IO.File]::WriteAllText($sourcePath,$source)
$bep=Join-Path $GameDir 'BepInEx/core/BepInEx.dll'
$exe=Join-Path $testRoot 'ConfigurationCheck.exe'
Copy-Item -LiteralPath $bep -Destination (Join-Path $testRoot 'BepInEx.dll') -Force
& 'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe' /nologo ('/reference:'+$bep) ('/out:'+$exe) $sourcePath
if($LASTEXITCODE){throw 'Configuration test compilation failed.'}
& $exe $testRoot
if($LASTEXITCODE){throw 'Actual BepInEx configuration execution failed.'}
