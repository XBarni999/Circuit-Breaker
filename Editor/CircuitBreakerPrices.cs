using System;
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class CircuitBreakerPrices
{
    const string Root="Assets/Blueprinter/Mods/Iron Gate/";
    static bool busy;
    static CircuitBreakerPrices(){EditorApplication.update+=Poll;}
    static void Poll(){
        string request=Root+"Tools~/prices-request.txt";
        if(busy||!File.Exists(request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
        busy=true;EditorApplication.delayCall+=()=>{
            try{Price("Blackout",9);Price("Locust",.9f);AssetDatabase.SaveAssets();CircuitBreakerSetup.Build();File.WriteAllText(Root+"Tools~/prices-result.txt","OK v0.1.20 prices and bundle");}
            catch(Exception e){File.WriteAllText(Root+"Tools~/prices-result.txt",e.ToString());Debug.LogException(e);}
            finally{File.Delete(request);busy=false;}
        };
    }
    static void Price(string key,float price){
        foreach(var kind in new[]{"WI_","Def_"}){
            var asset=AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(Root+kind+key+".asset");
            var serialized=new SerializedObject(asset);serialized.FindProperty(kind=="WI_"?"costPerRound":"value").floatValue=price;
            serialized.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(asset);
        }
    }
}
