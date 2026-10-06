using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Blueprinter;

[InitializeOnLoad]
public static class CircuitBreakerCarriers
{
    const string Root="Assets/Blueprinter/Mods/Iron Gate/";
    const string Native="Assets/Blueprinter/_donotship/MonoBehaviour/";
    [Serializable] public class CarrierProfile { public CarrierOption[] entries; }
    [Serializable] public class CarrierOption { public string aircraftJsonKey,unitName,sourceWeaponKey; public int index,ammo; public bool internalMount; }
    static CircuitBreakerCarriers(){EditorApplication.update+=Poll;}
    static void Poll(){
        string request=Root+"Tools~/carriers-request.txt";
        if(!File.Exists(request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
        string mode=File.ReadAllText(request).Trim();File.Delete(request);
        try{Update(mode=="heavy");CircuitBreakerSetup.Build();File.WriteAllText(Root+"Validation~/carriers-result.txt","OK");}
        catch(Exception e){File.WriteAllText(Root+"Validation~/carriers-result.txt",e.ToString());Debug.LogException(e);}
    }
    public static void Update(bool includeHeavy){
        var report=new StringBuilder();
        string profilePath=Root+"Editor/InstalledAircraftCompatibility.json";
        var profile=File.Exists(profilePath)?JsonUtility.FromJson<CarrierProfile>(File.ReadAllText(profilePath)):null;
        var aircraft=Directory.GetFiles(Native,"*_PLACEHOLDER.asset").Select(p=>AssetDatabase.LoadAssetAtPath<AircraftDefinition>(p.Replace('\\','/'))).Where(a=>a&&a.unitPrefab).ToArray();
        foreach(var file in Directory.GetFiles(Root,"Op_*.asset")){
            var op=AssetDatabase.LoadAssetAtPath<OpAddWeaponToHardpoint>(file.Replace('\\','/'));
            bool blackout=op.weaponJsonKey.StartsWith("CB_Blackout_");
            bool locust=op.weaponJsonKey.StartsWith("CB_Locust_");if(!blackout&&!locust)continue;
            var mount=AssetDatabase.LoadAssetAtPath<WeaponMount>(Root+"WM_"+op.weaponJsonKey.Substring(3)+".asset");
            if(!mount)throw new Exception("Missing mount for "+op.weaponJsonKey);
            bool internalMount=op.weaponJsonKey.Contains("internal");
            foreach(var a in aircraft){
                var manager=a.unitPrefab.GetComponentInChildren<WeaponManager>(true);if(!manager)continue;
                for(int i=0;i<manager.hardpointSets.Length;i++){
                    bool compatible=manager.hardpointSets[i].weaponOptions.Any(w=>{
                        if(!w||w.ammo<mount.ammo)return false;
                        string key=w.jsonKey??"";
                        bool bay=key.Contains("internal")||key.Contains("rotary");
                        if(bay!=internalMount)return false;
                        return locust?key.StartsWith("bomb_250_"):(key.StartsWith("CruiseMissile1_internal")||(includeHeavy&&key.StartsWith("AGM_heavy_")));
                    });
                    if(!compatible)continue;
                    var entry=op.aircraft.FirstOrDefault(t=>t.aircraftJsonKey==a.jsonKey);
                    if(entry==null){entry=new OpAddWeaponToHardpoint.AircraftTarget{aircraftJsonKey=a.jsonKey};op.aircraft.Add(entry);}
                    if(!entry.hardpointIndices.Contains(i))entry.hardpointIndices.Add(i);
                }
            }
            if(profile!=null)foreach(var option in profile.entries){
                if(option.aircraftJsonKey.IndexOf("mig29",StringComparison.OrdinalIgnoreCase)>=0||option.aircraftJsonKey.IndexOf("mig-29",StringComparison.OrdinalIgnoreCase)>=0)continue;
                if(option.internalMount!=internalMount||option.ammo<mount.ammo)continue;
                bool compatible=locust?option.sourceWeaponKey.StartsWith("bomb_250_"):(option.sourceWeaponKey.StartsWith("CruiseMissile1_internal")||(includeHeavy&&option.sourceWeaponKey.StartsWith("AGM_heavy_")));
                if(!compatible)continue;
                var entry=op.aircraft.FirstOrDefault(t=>t.aircraftJsonKey==option.aircraftJsonKey);
                if(entry==null){entry=new OpAddWeaponToHardpoint.AircraftTarget{aircraftJsonKey=option.aircraftJsonKey};op.aircraft.Add(entry);}
                if(!entry.hardpointIndices.Contains(option.index))entry.hardpointIndices.Add(option.index);
            }
            foreach(var entry in op.aircraft)entry.hardpointIndices.Sort();
            EditorUtility.SetDirty(op);
            report.AppendLine(op.weaponJsonKey+" "+string.Join(", ",op.aircraft.Select(a=>a.aircraftJsonKey+":"+string.Join("/",a.hardpointIndices))));
        }
        AssetDatabase.SaveAssets();Directory.CreateDirectory(Root+"Validation~");File.WriteAllText(Root+"Validation~/carriers.txt",report.ToString());
    }
}
