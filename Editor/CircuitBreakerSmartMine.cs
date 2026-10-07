using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class CircuitBreakerSmartMine
{
    const string R="Assets/Blueprinter/Mods/Iron Gate/";
    static bool busy;
    static CircuitBreakerSmartMine(){EditorApplication.update+=Poll;}
    static void Poll(){
        string request=R+"Tools~/smart-mine-request.txt";
        if(busy||!File.Exists(request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
        busy=true;EditorApplication.delayCall+=()=>{
            try{UpdateMine();CircuitBreakerSetup.Build();File.WriteAllText(R+"Tools~/smart-mine-result.txt","OK smart mine v0.1.17");}
            catch(Exception e){File.WriteAllText(R+"Tools~/smart-mine-result.txt",e.ToString());Debug.LogException(e);}
            finally{File.Delete(request);busy=false;}
        };
    }
    static void Description(string key,string text){
        foreach(var path in new[]{"WI_"+key+".asset","Def_"+key+".asset"}){
            var obj=AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(R+path);var s=new SerializedObject(obj);s.FindProperty("description").stringValue=text;
            if(key=="ZhdanMine"&&s.FindProperty("blastDamage")!=null)s.FindProperty("blastDamage").floatValue=0;
            s.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(obj);
        }
    }
    [MenuItem("Blueprinter/Circuit Breaker/Update smart mine only")]
    public static void UpdateMine(){
        var root=PrefabUtility.LoadPrefabContents(R+"ZhdanMine.prefab");
        try{
            var missile=root.GetComponent<Missile>();var serialized=new SerializedObject(missile);
            serialized.FindProperty("foldingFins").arraySize=0;serialized.FindProperty("blastYield").floatValue=0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            foreach(var t in root.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="CircuitVisual").ToArray())UnityEngine.Object.DestroyImmediate(t.gameObject);
            var pivot=new GameObject("CircuitVisual");pivot.transform.SetParent(root.transform,false);
            var model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(R+"Models/Zhdan_Mine.fbx"));model.name="Zhdan_Mine";model.transform.SetParent(pivot.transform,false);
            foreach(var animator in model.GetComponentsInChildren<Animator>(true))UnityEngine.Object.DestroyImmediate(animator);
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(R+"Textures/Zhdan_Mine_Albedo.png");if(!texture)throw new Exception("Zhdan texture is missing");
            string materialPath=R+"Materials/MAT_ZhdanMine.mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,materialPath);}
            mat.SetTexture("_BaseMap",texture);mat.SetColor("_BaseColor",Color.white);mat.SetFloat("_Metallic",.1f);mat.SetFloat("_Smoothness",.25f);EditorUtility.SetDirty(mat);
            foreach(var renderer in model.GetComponentsInChildren<Renderer>(true))renderer.sharedMaterials=renderer.sharedMaterials.Select(_=>mat).ToArray();
            foreach(var t in model.GetComponentsInChildren<Transform>(true))t.gameObject.layer=root.layer;
            var clip=new AnimationClip{name="ZhdanMineDeploy",legacy=true,frameRate=30,wrapMode=WrapMode.ClampForever};
            foreach(var source in AssetDatabase.LoadAllAssetsAtPath(R+"Models/Zhdan_Mine.fbx").OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__"))){
                string actionFin=Enumerable.Range(1,4).Select(i=>"Zhdan_Fin_"+i).FirstOrDefault(n=>source.name.Contains(n));
                foreach(var binding in AnimationUtility.GetCurveBindings(source).Where(b=>b.path.Contains("Zhdan_Fin_")&&(actionFin==null||b.path.Contains(actionFin))))AnimationUtility.SetEditorCurve(clip,binding,AnimationUtility.GetEditorCurve(source,binding));
            }
            if(AnimationUtility.GetCurveBindings(clip).Length==0)throw new Exception("Zhdan fin animation was not imported");
            clip.EnsureQuaternionContinuity();var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(R+"ZhdanMineDeploy.anim");
            if(existing){EditorUtility.CopySerialized(clip,existing);UnityEngine.Object.DestroyImmediate(clip);clip=existing;}else AssetDatabase.CreateAsset(clip,R+"ZhdanMineDeploy.anim");
            var animation=model.AddComponent<Animation>();animation.AddClip(clip,clip.name);animation.clip=clip;animation.playAutomatically=false;
            clip.SampleAnimation(model,clip.length);
            // Infer the rear from the four fin hinge positions, independent of FBX axis conversion.
            var fins=model.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Zhdan_Fin_")).ToArray();if(fins.Length!=4)throw new Exception("Expected four Zhdan fin hinges");
            Vector3 rear=Vector3.zero;foreach(var fin in fins)rear+=pivot.transform.InverseTransformPoint(fin.position);rear/=fins.Length;
            pivot.transform.localRotation=Quaternion.FromToRotation(rear.normalized,Vector3.down);
            var renderers=model.GetComponentsInChildren<Renderer>(true);var bounds=renderers[0].bounds;foreach(var renderer in renderers.Skip(1))bounds.Encapsulate(renderer.bounds);
            model.transform.position+=new Vector3(root.transform.position.x-bounds.center.x,root.transform.position.y-.09f-bounds.min.y,root.transform.position.z-bounds.center.z);
            var report=new StringBuilder();report.AppendLine("Zhdan deployed bounds: "+bounds.size);report.AppendLine("Four fin hinges face down; lowest deployed mesh point is -0.09 m relative to mine origin.");
            clip.SampleAnimation(model,0);PrefabUtility.SaveAsPrefabAsset(root,R+"ZhdanMine.prefab");
            Directory.CreateDirectory(R+"Validation~");File.WriteAllText(R+"Validation~/smart-mine-model.txt",report.ToString());
        }finally{PrefabUtility.UnloadPrefabContents(root);}
        Description("LawnChair","CCIP dispenser deploying eight Zhdan sensor mines at approximately 325 m. Each mine arms after four seconds, scans enemy ground vehicles within 70 m and launches one vanilla GS25 from 80 m above the mine. Five-minute lifetime; coordinated five-second target reservations.");
        Description("ZhdanMine","Zhdan sensor mine: enemy ground vehicles only, 70 m detection radius, one vanilla GS25 top attack, five-minute lifetime. No contact explosive charge.");
        AssetDatabase.SaveAssets();
    }
}
