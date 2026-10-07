using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using Blueprinter;
using UnityEngine;

[InitializeOnLoad]
public static class CircuitBreakerMineVariants
{
    const string R="Assets/Blueprinter/Mods/Iron Gate/";
    static bool busy;
    static CircuitBreakerMineVariants(){EditorApplication.update+=Poll;}
    static void Poll(){
        string request=R+"Tools~/mine-variants-request.txt";
        if(busy||!File.Exists(request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
        busy=true;EditorApplication.delayCall+=()=>{
            try{Separate();CircuitBreakerSetup.Build();File.WriteAllText(R+"Tools~/mine-variants-result.txt","OK contact Locust + Lawn Chair v0.1.18");}
            catch(Exception e){File.WriteAllText(R+"Tools~/mine-variants-result.txt",e.ToString());Debug.LogException(e);}
            finally{File.Delete(request);busy=false;}
        };
    }
    static string Rename(string text)=>text.Replace("LocustMine","ZhdanMine").Replace("Locust","LawnChair");
    static void Remap(UnityEngine.Object obj,Dictionary<UnityEngine.Object,UnityEngine.Object> mapping){
        var s=new SerializedObject(obj);var p=s.GetIterator();
        while(p.Next(true)){
            if(p.propertyType==SerializedPropertyType.ObjectReference&&p.objectReferenceValue&&mapping.TryGetValue(p.objectReferenceValue,out var replacement))p.objectReferenceValue=replacement;
            if(p.propertyType==SerializedPropertyType.String&&(p.name=="jsonKey"||p.name=="weaponJsonKey"))p.stringValue=Rename(p.stringValue);
            if(p.propertyType==SerializedPropertyType.String&&p.name=="mountName")p.stringValue=p.stringValue.Replace("CBU-82M Locust","CBU-82S Lawn Chair");
        }
        s.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(obj);
    }
    [MenuItem("Blueprinter/Circuit Breaker/Separate contact and sensor mine variants")]
    public static void Separate(){
        // Clone current user-edited racks; every transform and the existing Locust asset GUIDs stay intact.
        var paths=Directory.GetFiles(R).Select(p=>p.Replace('\\','/')).Where(p=>
            p.EndsWith(".prefab")&&(Path.GetFileName(p)=="Locust.prefab"||Path.GetFileName(p)=="LocustMine.prefab"||Path.GetFileName(p).StartsWith("CB_Locust_"))||
            p.EndsWith(".asset")&&(Path.GetFileName(p).StartsWith("WI_Locust")||Path.GetFileName(p).StartsWith("Def_Locust")||Path.GetFileName(p).StartsWith("WM_Locust_")||Path.GetFileName(p).StartsWith("Op_CB_Locust_"))).ToArray();
        var mapping=new Dictionary<UnityEngine.Object,UnityEngine.Object>();
        foreach(var source in paths){string destination=R+Rename(Path.GetFileName(source));if(!File.Exists(destination)&&!AssetDatabase.CopyAsset(source,destination))throw new Exception("Cannot clone "+source);mapping.Add(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(source),AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(destination));}
        foreach(var source in paths){
            string destination=R+Rename(Path.GetFileName(source));
            if(destination.EndsWith(".prefab")){
                var root=PrefabUtility.LoadPrefabContents(destination);
                try{root.name=Path.GetFileNameWithoutExtension(destination);foreach(var component in root.GetComponentsInChildren<MonoBehaviour>(true))if(component)Remap(component,mapping);PrefabUtility.SaveAsPrefabAsset(root,destination);}
                finally{PrefabUtility.UnloadPrefabContents(root);}
            }else{var asset=AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(destination);asset.name=Path.GetFileNameWithoutExtension(destination);Remap(asset,mapping);}
        }
        foreach(var key in new[]{"LawnChair","ZhdanMine"}){
            var info=AssetDatabase.LoadAssetAtPath<WeaponInfo>(R+"WI_"+key+".asset");var definition=AssetDatabase.LoadAssetAtPath<MissileDefinition>(R+"Def_"+key+".asset");
            var s=new SerializedObject(info);s.FindProperty("weaponName").stringValue=key=="LawnChair"?"CBU-82S Lawn Chair":"Zhdan Sensor Mine";s.FindProperty("shortName").stringValue=key=="LawnChair"?"LAWN CHAIR":"ZHDAN";s.ApplyModifiedPropertiesWithoutUndo();
            s=new SerializedObject(definition);s.FindProperty("unitName").stringValue=info.weaponName;s.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(info);EditorUtility.SetDirty(definition);
        }
        CircuitBreakerSmartMine.UpdateMine();
        var weapon=typeof(CircuitBreakerSetup).GetMethod("Weapon",BindingFlags.Static|BindingFlags.NonPublic);
        var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(R+"LocustMineDeploy.anim");
        weapon.Invoke(null,new object[]{"LocustMine","info_bomb_125_1","bomb_125_1","Submunition_Mine",16f,7f,0f,clip});
        string description="CCIP dispenser with eight contact mines, each containing 7 kg HE. Arms four seconds after landing; triggers within 3 m of vehicles and aircraft; self-destructs after 210 seconds.";
        foreach(var path in new[]{"WI_Locust.asset","Def_Locust.asset"}){var obj=AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(R+path);var s=new SerializedObject(obj);s.FindProperty("description").stringValue=description;s.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(obj);}
        AssetDatabase.SaveAssets();OpReferenceIndex.Refresh();
    }
}
