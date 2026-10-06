using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Blueprinter;

[InitializeOnLoad]
public static class CircuitBreakerSetup
{
    const string R="Assets/Blueprinter/Mods/Iron Gate/";
    const string D="Assets/Blueprinter/_donotship/";
    const string Version="0.1.2";
    static CircuitBreakerSetup(){EditorApplication.update+=Poll;}
    static bool busy;
    static void Poll(){if(busy||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;string p=R+"Tools~/request.txt";if(!File.Exists(p))return;var action=File.ReadAllText(p).Trim();busy=true;EditorApplication.delayCall+=()=>{try{if(action=="create")Create();else if(action=="build")Build();else Inspect();File.WriteAllText(R+"Tools~/result.txt","OK "+action);}catch(Exception e){File.WriteAllText(R+"Tools~/result.txt",e.ToString());Debug.LogException(e);}finally{busy=false;if(File.Exists(p)&&File.ReadAllText(p).Trim()==action)File.Delete(p);}};}
    static T Load<T>(string p) where T:UnityEngine.Object => AssetDatabase.LoadAssetAtPath<T>(p) ?? throw new Exception("Missing "+p);
    public static void Inspect()
    {
        Directory.CreateDirectory(R+"Validation~");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var b=new StringBuilder();
        foreach(var f in Directory.GetFiles(R+"Models","*.fbx")) {
            var g=Load<GameObject>(f.Replace('\\','/'));b.AppendLine("MODEL "+f);
            foreach(var t in g.GetComponentsInChildren<Transform>(true)) b.AppendLine(t.name+" pos="+t.localPosition+" rot="+t.localEulerAngles+" scale="+t.localScale);
            foreach(var r in g.GetComponentsInChildren<Renderer>(true))b.AppendLine("BOUNDS "+r.name+" "+r.bounds+" materials="+string.Join(",",r.sharedMaterials.Select(m=>m?m.name:"NULL"))+" renderer="+r.GetType().Name+(r is SkinnedMeshRenderer?" bones="+string.Join(",",((SkinnedMeshRenderer)r).bones.Select(t=>t?t.name:"null")):""));
            foreach(var c in AssetDatabase.LoadAllAssetsAtPath(f.Replace('\\','/')).OfType<AnimationClip>()) {
                b.AppendLine("CLIP "+c.name+" length="+c.length);
                foreach(var binding in AnimationUtility.GetCurveBindings(c)) b.AppendLine("CURVE "+binding.path+" "+binding.propertyName);
            }
        }
        foreach(var n in new[]{"CruiseMissile1","bomb_cluster_400","bomb_125_1","CruiseMissile1_internal","bomb_cluster1_single","FastBomber1","Darkreach"}) {
            var g=Load<GameObject>(D+"GameObject/"+n+"_PLACEHOLDER.prefab");b.AppendLine("DONOR "+n);
            foreach(var c in g.GetComponentsInChildren<MonoBehaviour>(true)) {
                if(!c)continue;b.AppendLine("COMP "+c.GetType().Name+" "+c.name);
                if(c is Missile||c is MissileSeeker||c is MountedMissile){var it=new SerializedObject(c).GetIterator();while(it.NextVisible(true))b.AppendLine(it.propertyPath+" = "+Value(it));}
            }
        }
        File.WriteAllText(R+"Validation~/inspection.txt",b.ToString());Debug.Log("CIRCUIT_INSPECT_OK");
    }
    static string Value(SerializedProperty p){switch(p.propertyType){case SerializedPropertyType.ObjectReference:return p.objectReferenceValue?p.objectReferenceValue.name:"null";case SerializedPropertyType.Float:return p.floatValue.ToString();case SerializedPropertyType.Integer:return p.intValue.ToString();case SerializedPropertyType.Boolean:return p.boolValue.ToString();default:return p.propertyType.ToString();}}
    static SerializedProperty P(SerializedObject s,string n)=>s.FindProperty(n)??throw new Exception(s.targetObject.name+" missing "+n);
    static void Edit(UnityEngine.Object o,Action<SerializedObject> a){var s=new SerializedObject(o);a(s);s.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(o);}
    static T Copy<T>(string src,string name) where T:UnityEngine.Object{string p=R+name+".asset";if(!File.Exists(p)&&!AssetDatabase.CopyAsset(src,p))throw new Exception(src);var o=Load<T>(p);o.name=name;return o;}
    static GameObject Clone(string p){var g=UnityEngine.Object.Instantiate(Load<GameObject>(p));g.name=g.name.Replace("(Clone)","");return g;}
    static void Hide(GameObject g){foreach(var r in g.GetComponentsInChildren<Renderer>(true))r.enabled=false;foreach(var l in g.GetComponentsInChildren<LODGroup>(true))UnityEngine.Object.DestroyImmediate(l);}
    static AnimationClip Clip(string model,string name,string[] nodes)
    {
        var clips=AssetDatabase.LoadAllAssetsAtPath(R+"Models/"+model+".fbx").OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__")).ToArray();
        var result=new AnimationClip{name=name,legacy=true,frameRate=30,wrapMode=WrapMode.ClampForever};
        foreach(var node in nodes){
            var source=clips.FirstOrDefault(c=>c.name==node+"|"+node+"Action")??clips.FirstOrDefault(c=>c.name.EndsWith("|"+node+"Action"));
            if(!source && (model=="CHAMP"||model=="Submunition_Mine")){
                // The refreshed FBX has corrected hinges but no baked actions. Retarget only the
                // previous rotation motion; keep every current pivot position and scale intact.
                var previous=Load<AnimationClip>(R+name+".anim");var current=Load<GameObject>(R+"Models/"+model+".fbx");var hinge=current.GetComponentsInChildren<Transform>(true).Single(t=>t.name==node);
                var bindings=AnimationUtility.GetCurveBindings(previous).Where(b=>b.path.EndsWith(node)&&b.propertyName.StartsWith("m_LocalRotation.")).ToArray();
                if(bindings.Length!=4)throw new Exception("No retained rotation animation for "+node);
                var curves=new[]{"x","y","z","w"}.Select(axis=>AnimationUtility.GetEditorCurve(previous,bindings.Single(b=>b.propertyName=="m_LocalRotation."+axis))).ToArray();
                Func<float,Quaternion> rotation=time=>new Quaternion(curves[0].Evaluate(time),curves[1].Evaluate(time),curves[2].Evaluate(time),curves[3].Evaluate(time));
                var correction=hinge.localRotation*Quaternion.Inverse(rotation(previous.length));string hingePath=AnimationUtility.CalculateTransformPath(hinge,current.transform);
                for(int axis=0;axis<4;axis++){var keys=new List<Keyframe>();for(int frame=0;frame<=Mathf.CeilToInt(previous.length*30);frame++){float time=Mathf.Min(frame/30f,previous.length);var q=correction*rotation(time);keys.Add(new Keyframe(time,q[axis]));}result.SetCurve(hingePath,typeof(Transform),"m_LocalRotation."+new[]{"x","y","z","w"}[axis],new AnimationCurve(keys.ToArray()));}
                for(int axis=0;axis<3;axis++){result.SetCurve(hingePath,typeof(Transform),"m_LocalPosition."+new[]{"x","y","z"}[axis],AnimationCurve.Constant(0,previous.length,hinge.localPosition[axis]));result.SetCurve(hingePath,typeof(Transform),"m_LocalScale."+new[]{"x","y","z"}[axis],AnimationCurve.Constant(0,previous.length,hinge.localScale[axis]));}
                continue;
            }
            if(!source)throw new Exception("Missing animation "+model+" / "+node);
            foreach(var binding in AnimationUtility.GetCurveBindings(source).Where(b=>b.type==typeof(Transform)&&b.path.EndsWith(node))){var curve=AnimationUtility.GetEditorCurve(source,binding);AnimationUtility.SetEditorCurve(result,binding,curve);}
        }
        result.EnsureQuaternionContinuity();string path=R+name+".anim";var old=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);if(old){EditorUtility.CopySerialized(result,old);UnityEngine.Object.DestroyImmediate(result);return old;}AssetDatabase.CreateAsset(result,path);return result;
    }
    static GameObject Visual(Transform parent,string model,AnimationClip clip=null)
    {
        var pivot=new GameObject("CircuitVisual");pivot.transform.SetParent(parent,false);
        if(model=="CHAMP"||model=="CBU_Dispenser"||model=="ClusterBomb")pivot.transform.localRotation=Quaternion.Euler(0,180,0); // Source tail is at +Z; gameplay nose must face +Z.
        var g=Clone(R+"Models/"+model+".fbx");g.transform.SetParent(pivot.transform,false);g.transform.localPosition=Vector3.zero;
        if(model=="ClusterBomb")foreach(var t in g.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Submunition_Mine").ToArray())UnityEngine.Object.DestroyImmediate(t.gameObject); // Export includes a separate demonstration mine beside the dispenser.
        foreach(var c in g.GetComponentsInChildren<Animator>(true))UnityEngine.Object.DestroyImmediate(c);
        foreach(var c in g.GetComponentsInChildren<Camera>(true))UnityEngine.Object.DestroyImmediate(c.gameObject);
        foreach(var c in g.GetComponentsInChildren<Light>(true))UnityEngine.Object.DestroyImmediate(c.gameObject);
        foreach(var t in g.GetComponentsInChildren<Transform>(true))t.gameObject.layer=parent.gameObject.layer;
        foreach(var renderer in g.GetComponentsInChildren<Renderer>(true)){
            renderer.sharedMaterials=renderer.sharedMaterials.Select(src=>{
                string p=R+"Materials/"+src.name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(p);if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));mat.name=src.name;AssetDatabase.CreateAsset(mat,p);}
                string texture=model=="CHAMP"?"CHAMP_Albedo":model=="Submunition_Mine"||src.name.IndexOf("Mine",StringComparison.OrdinalIgnoreCase)>=0||renderer.name.IndexOf("Mine",StringComparison.OrdinalIgnoreCase)>=0?"Submunition_Mine_Albedo":"CBU_Albedo";
                mat.shader=Shader.Find("Universal Render Pipeline/Lit");mat.SetColor("_BaseColor",Color.white);mat.SetTexture("_BaseMap",Load<Texture2D>(R+"Textures/"+texture+".png"));mat.SetFloat("_Metallic",.1f);mat.SetFloat("_Smoothness",.3f);EditorUtility.SetDirty(mat);return mat;
            }).ToArray();
        }
        if(clip){var a=g.AddComponent<Animation>();a.AddClip(clip,clip.name);a.clip=clip;a.playAutomatically=false;clip.SampleAnimation(g,0);}
        return g;
    }
    static void Fins(Missile missile,GameObject model,AnimationClip clip,string[] names)
    {
        clip.SampleAnimation(model,0);var fins=names.Select(n=>model.GetComponentsInChildren<Transform>(true).Single(t=>t.name==n)).ToArray();var folded=fins.Select(t=>t.localEulerAngles).ToArray();
        clip.SampleAnimation(model,clip.length);var deployed=fins.Select(t=>t.localEulerAngles).ToArray();clip.SampleAnimation(model,0);
        Edit(missile,s=>{var a=P(s,"foldingFins");a.arraySize=fins.Length;for(int i=0;i<fins.Length;i++){var f=a.GetArrayElementAtIndex(i);f.FindPropertyRelative("fin").objectReferenceValue=fins[i];f.FindPropertyRelative("foldAngle").vector3Value=folded[i];f.FindPropertyRelative("deployAngle").vector3Value=folded[i]+new Vector3(Mathf.DeltaAngle(folded[i].x,deployed[i].x),Mathf.DeltaAngle(folded[i].y,deployed[i].y),Mathf.DeltaAngle(folded[i].z,deployed[i].z));f.FindPropertyRelative("deploySpeed").floatValue=1/Mathf.Max(clip.length,.1f);}});
    }
    static void Arcs(GameObject g)
    {
        var mat=AssetDatabase.LoadAssetAtPath<Material>(R+"Materials/HPMArc.mat");if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));mat.SetColor("_BaseColor",new Color(.3f,.8f,1,1));AssetDatabase.CreateAsset(mat,R+"Materials/HPMArc.mat");}
        for(int i=0;i<6;i++){
            var a=new GameObject("HPMArc"+i);a.transform.SetParent(g.transform,false);a.transform.localPosition=new Vector3(Mathf.Cos(i*Mathf.PI/3)*.4f,Mathf.Sin(i*Mathf.PI/3)*.4f,1.5f);
            var p=a.AddComponent<ParticleSystem>();var main=p.main;main.loop=false;main.playOnAwake=false;main.duration=.22f;main.startLifetime=.18f;main.startSpeed=8;main.startSize=.09f;main.startColor=new Color(.65f,.92f,1);main.maxParticles=32;
            var emission=p.emission;emission.rateOverTime=0;emission.SetBursts(new[]{new ParticleSystem.Burst(0,12)});var shape=p.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=.08f;
            var trails=p.trails;trails.enabled=true;trails.ratio=1;trails.lifetime=.12f;trails.widthOverTrail=new ParticleSystem.MinMaxCurve(.045f);
            var renderer=p.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=mat;renderer.trailMaterial=mat;p.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
    static WeaponInfo Weapon(string key,string donor,string prefabDonor,string model,float mass,float yield,float cost,AnimationClip clip)
    {
        var info=Copy<WeaponInfo>(D+"MonoBehaviour/"+donor+"_PLACEHOLDER.asset","WI_"+key);
        string defDonor=key=="Blackout"?"CruiseMissile1":key=="Locust"?"Bomb_cluster1":"Bomb_125_1";
        var def=Copy<MissileDefinition>(D+"MonoBehaviour/"+defDonor+"_PLACEHOLDER.asset","Def_"+key);
        string display=key=="Blackout"?"AGM-180 Blackout":key=="Locust"?"CBU-82M Locust":"Locust Mine";
        Edit(info,s=>{P(s,"weaponName").stringValue=display;P(s,"shortName").stringValue=key.ToUpperInvariant();P(s,"massPerRound").floatValue=mass;P(s,"costPerRound").floatValue=cost;P(s,"blastDamage").floatValue=yield;P(s,"pierceDamage").floatValue=key=="Blackout"?5:0;P(s,"nuclear").boolValue=false;P(s,"airburstHeight").floatValue=0;P(s,"description").stringValue=key=="Blackout"?"High-power microwave cruise missile. Four directed pulses suppress enemy radar, lasers and turret acquisition. Minimal impact charge.":key=="Locust"?"CCIP dispenser with eight remotely delivered mines, each containing a 10 kg HE charge. Arms four seconds after landing; self-destructs after 210 seconds.":"Ground contact mine. 10 kg HE charge. Vehicle and aircraft trigger, 3 m radius.";P(s,"hideInDisplay").boolValue=key=="LocustMine";});
        Edit(def,s=>{P(s,"jsonKey").stringValue="CircuitBreaker_"+key;P(s,"unitName").stringValue=display;P(s,"mass").floatValue=mass;P(s,"value").floatValue=cost;P(s,"disabled").boolValue=false;});
        var g=Clone(D+"GameObject/"+prefabDonor+"_PLACEHOLDER.prefab");g.name=key;Hide(g);
        foreach(var disp in g.GetComponentsInChildren<SubmunitionDispenser>(true))UnityEngine.Object.DestroyImmediate(disp);
        if(key!="Blackout")foreach(var seeker in g.GetComponents<MissileSeeker>())UnityEngine.Object.DestroyImmediate(seeker); // Unguided CCIP ballistics and terrain contact, no vanilla optical submunition search.
        var visual=Visual(g.transform,model,clip);var missile=g.GetComponent<Missile>();
        Edit(missile,s=>{P(s,"info").objectReferenceValue=info;P(s,"definition").objectReferenceValue=def;P(s,"mass").floatValue=mass;P(s,"blastYield").floatValue=yield;P(s,"pierceDamage").floatValue=key=="Blackout"?5:0;P(s,"impactFuse").boolValue=key=="Blackout";P(s,"warhead.Armed").boolValue=false;P(s,"foldingFins").arraySize=0;if(key!="Blackout")P(s,"motors").arraySize=0;});
        if(key=="Blackout"){
            Fins(missile,visual,clip,new[]{"CHAMP_Wing_L","CHAMP_Wing_R","CHAMP_Fin_L","CHAMP_Fin_R"});Arcs(g);
            Edit(missile.GetComponent<OpticalSeekerCruiseMissile>(),s=>{P(s,"altitudeTarget").floatValue=30;P(s,"terminalRange").floatValue=4000;});
            Edit(info,s=>{P(s,"targetRequirements.minRange").floatValue=0;P(s,"targetRequirements.maxRange").floatValue=150000;P(s,"targetRequirements.minAlignment").floatValue=180;P(s,"targetRequirements.lineOfSight").boolValue=false;});
        }
        if(key=="LocustMine"){
            foreach(var collider in g.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(collider);
            var capsule=g.AddComponent<CapsuleCollider>();capsule.radius=.08f;capsule.height=.16f;capsule.isTrigger=true;
            Edit(def,s=>{P(s,"length").floatValue=.16f;P(s,"width").floatValue=.16f;P(s,"height").floatValue=.06f;P(s,"mass").floatValue=16;});
            Edit(missile,s=>P(s,"finArea").floatValue=.015f);
        }
        // HPM and mines reuse vanilla small-HE visuals; damage magnitude remains the serialized native yield.
        if(key!="Locust"){
            var donorMissile=Load<GameObject>(D+"GameObject/bomb_125_1_PLACEHOLDER.prefab").GetComponent<Missile>();
            var ds=new SerializedObject(donorMissile);Edit(missile,s=>{foreach(var n in new[]{"airEffect","armorEffect","terrainEffect","waterSurfaceEffect","underwaterEffect"})P(s,"warhead."+n).objectReferenceValue=P(ds,"warhead."+n).objectReferenceValue;P(s,"warhead.airEffect").objectReferenceValue=Load<GameObject>(D+"GameObject/explosion_10kg_PLACEHOLDER.prefab");P(s,"warhead.armorEffect").objectReferenceValue=Load<GameObject>(D+"GameObject/explosion_10kg_armor_PLACEHOLDER.prefab");P(s,"warhead.terrainEffect").objectReferenceValue=Load<GameObject>(D+"GameObject/explosion_10kg_dusty_PLACEHOLDER.prefab");});
        }
        PrefabUtility.SaveAsPrefabAsset(g,R+key+".prefab");UnityEngine.Object.DestroyImmediate(g);var prefab=Load<GameObject>(R+key+".prefab");Edit(info,s=>P(s,"weaponPrefab").objectReferenceValue=prefab);Edit(def,s=>P(s,"unitPrefab").objectReferenceValue=prefab);return info;
    }
    static void Mount(string key,WeaponInfo info,string donor,string model,AnimationClip clip)
    {
        var mount=Copy<WeaponMount>(D+"MonoBehaviour/"+donor+"_PLACEHOLDER.asset","WM_"+key+"_"+donor);string json="CB_"+key+"_"+donor;
        var g=Clone(D+"GameObject/"+donor+"_PLACEHOLDER.prefab");g.name=json;Hide(g);
        var stations=g.GetComponentsInChildren<MountedMissile>(true);
        // Keep the donor's physical rack visible; only the donor ammunition is hidden.
        foreach(var renderer in g.GetComponentsInChildren<Renderer>(true))if(!renderer.GetComponentInParent<MountedMissile>(true))renderer.enabled=true;
        if(key=="Blackout"&&stations.Length>3)throw new Exception("Blackout rack exceeds three missiles");
        if(key=="Blackout"&&(donor=="AGM_heavy_triple"||donor=="AGM_heavy_double")){
            // Enlarge the native support and its rail spacing together, retaining each rail's
            // attachment relationship. Compensate the ammunition scale so the FBX stays full size.
            foreach(var rail in g.transform.Cast<Transform>()){rail.localPosition*=1.8f;rail.localScale*=1.8f;}
            foreach(var station in stations)station.transform.localScale/=1.8f;
        }
        if(key=="Blackout"&&donor=="AGM_heavy_single")stations[0].transform.localPosition+=Vector3.down*.17f;
        if(key=="Locust"&&donor=="bomb_500_double"){
            // Widen the native adapter; keep both ejector rails and their ammunition together.
            var adapter=g.transform.Find("doubleAdapter");var scale=adapter.localScale;scale.x*=1.6f;adapter.localScale=scale;
            foreach(var rail in adapter.Cast<Transform>()){scale=rail.localScale;scale.x/=1.6f;rail.localScale=scale;}
            foreach(var station in stations)station.transform.localPosition+=Vector3.down*.08f;
        }
        if(key=="Locust"&&donor=="bomb_250_triple"){
            // Preserve the vanilla triangular arrangement and roll toward each ejector face.
            // Expand its cross-section to clear the wider CBU bodies, retaining full-size bombs.
            foreach(var child in g.transform.Cast<Transform>()){
                var position=child.localPosition;position.x*=1.8f;position.y*=1.8f;child.localPosition=position;
                if(!child.GetComponent<MountedMissile>()){var scale=child.localScale;scale.x*=1.8f;scale.y*=1.8f;child.localScale=scale;}
            }
        }
        if(key=="Locust"&&donor=="bomb_cluster1_dual_internal"){
            foreach(var station in stations){var position=station.transform.localPosition;position.x=Mathf.Sign(position.x)*.36f;station.transform.localPosition=position;}
        }
        foreach(var station in g.GetComponentsInChildren<MountedMissile>(true)){Visual(station.transform,model,clip);Edit(station,s=>P(s,"info").objectReferenceValue=info);var collider=station.GetComponent<CapsuleCollider>();if(collider){collider.height=key=="Blackout"?5.5f:2.4f;collider.radius=key=="Blackout"?.32f:.3f;}}
        PrefabUtility.SaveAsPrefabAsset(g,R+json+".prefab");UnityEngine.Object.DestroyImmediate(g);
        Edit(mount,s=>{P(s,"jsonKey").stringValue=json;P(s,"mountName").stringValue=info.weaponName+(mount.ammo>1?" x"+mount.ammo:"");P(s,"info").objectReferenceValue=info;P(s,"prefab").objectReferenceValue=Load<GameObject>(R+json+".prefab");P(s,"mass").floatValue=info.massPerRound*mount.ammo;P(s,"disabled").boolValue=false;});
        var op=ScriptableObject.CreateInstance<OpAddWeaponToHardpoint>();op.weaponJsonKey=json;
        var source=Load<WeaponMount>(D+"MonoBehaviour/"+donor+"_PLACEHOLDER.asset");
        foreach(var path in Directory.GetFiles(D+"MonoBehaviour","*_PLACEHOLDER.asset")){
            var aircraft=AssetDatabase.LoadAssetAtPath<AircraftDefinition>(path.Replace('\\','/'));if(!aircraft||!aircraft.unitPrefab)continue;
            var manager=aircraft.unitPrefab.GetComponentInChildren<WeaponManager>(true);if(!manager)continue;
            var indices=new List<int>();for(int i=0;i<manager.hardpointSets.Length;i++)if(manager.hardpointSets[i].weaponOptions.Any(w=>w&&w.jsonKey==source.jsonKey))indices.Add(i);
            if(indices.Count>0)op.aircraft.Add(new OpAddWeaponToHardpoint.AircraftTarget{aircraftJsonKey=aircraft.jsonKey,hardpointIndices=indices});
        }
        if(key=="Blackout"&&(donor=="AGM_heavy_single"||donor=="AGM_heavy_double"||donor=="AGM_heavy_triple")){
            op.aircraft.Clear();op.aircraft.Add(new OpAddWeaponToHardpoint.AircraftTarget{aircraftJsonKey="FastBomber1",hardpointIndices=new List<int>{3}});
            op.aircraft.Add(new OpAddWeaponToHardpoint.AircraftTarget{aircraftJsonKey="Multirole1",hardpointIndices=new List<int>{4,5}});
        }
        if(key=="Blackout"&&donor.StartsWith("CruiseMissile1_internal")){
            op.aircraft.Clear();op.aircraft.Add(new OpAddWeaponToHardpoint.AircraftTarget{aircraftJsonKey="Darkreach",hardpointIndices=new List<int>{1,2,3}});
        }
        op.name="Op_"+json;
        string opPath=R+"Op_"+json+".asset";var old=AssetDatabase.LoadAssetAtPath<OpAddWeaponToHardpoint>(opPath);if(old){EditorUtility.CopySerialized(op,old);UnityEngine.Object.DestroyImmediate(op);}else AssetDatabase.CreateAsset(op,opPath);
    }
    [MenuItem("Blueprinter/Circuit Breaker/Create assets")]
    public static void Create()
    {
        Directory.CreateDirectory(R+"Materials");
        var champ=Clip("CHAMP","BlackoutDeploy",new[]{"CHAMP_Wing_L","CHAMP_Wing_R","CHAMP_Fin_L","CHAMP_Fin_R"});
        var doors=Clip("ClusterBomb","LocustOpen",new[]{"CBU_Door_L","CBU_Door_R"});
        var fins=Clip("Submunition_Mine","LocustMineDeploy",new[]{"Mine_VaneFin_1","Mine_VaneFin_2","Mine_VaneFin_3","Mine_VaneFin_4"});
        var blackout=Weapon("Blackout","info_CruiseMissile1","CruiseMissile1","CHAMP",900,2,12,champ);
        var locust=Weapon("Locust","info_Bomb_cluster1","bomb_cluster_400","ClusterBomb",400,0,1.5f,doors);
        Weapon("LocustMine","info_bomb_125_1","bomb_125_1","Submunition_Mine",16,10,0,fins);
        foreach(var file in Directory.GetFiles(R+"Models","*.fbx")){
            string modelPath=file.Replace('\\','/');var importer=(ModelImporter)AssetImporter.GetAtPath(modelPath);
            foreach(var material in Load<GameObject>(modelPath).GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Distinct()){
                var mapped=AssetDatabase.LoadAssetAtPath<Material>(R+"Materials/"+material.name+".mat");if(mapped)importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),material.name),mapped);
            }
            importer.SaveAndReimport();
        }
        foreach(var n in new[]{"CruiseMissile1_internal","CruiseMissile1_internalx2","CruiseMissile1_internalx3","AGM_heavy_single","AGM_heavy_double","AGM_heavy_triple"})Mount("Blackout",blackout,n,"CHAMP",champ);
        foreach(var n in new[]{"bomb_cluster1_single","bomb_cluster1_single_internal","bomb_cluster1_dual_internal"})Mount("Locust",locust,n,"ClusterBomb",doors);
        foreach(var n in new[]{"bomb_500_double","bomb_250_triple"})Mount("Locust",locust,n,"ClusterBomb",doors);
        ArchiveUnusedRacks();AssetDatabase.SaveAssets();OpReferenceIndex.Refresh();Validate();Preview();Debug.Log("CIRCUIT_CREATE_OK");
    }
    static void ArchiveUnusedRacks()
    {
        string archive=R+"Tools~/Archive-v0.1.0/UnusedRacks/";Directory.CreateDirectory(archive);
        foreach(var donor in new[]{"CruiseMissile1_internalx4","CruiseMissile1_internalx6"})foreach(var name in new[]{"CB_Blackout_"+donor+".prefab","WM_Blackout_"+donor+".asset","Op_CB_Blackout_"+donor+".asset"}){
            string path=R+name;if(!File.Exists(path))continue;File.Copy(path,archive+name,true);if(File.Exists(path+".meta"))File.Copy(path+".meta",archive+name+".meta",true);if(!AssetDatabase.DeleteAsset(path))throw new Exception("Could not archive "+path);
        }
    }
    public static void Validate()
    {
        Directory.CreateDirectory(R+"Validation~");
        foreach(var pair in new[]{new[]{"Missile","StartMissile"},new[]{"Missile","ServerFixedUpdate"},new[]{"Missile","DetectCollisions"},new[]{"OpticalSeekerCruiseMissile","SlowChecks"},new[]{"Radar","TargetSearch"},new[]{"Radar","CanSeeRadarReturn"},new[]{"TargetDetector","DetectTarget"},new[]{"TargetDetector","IsOperational"},new[]{"Turret","AssessTargetPriority"},new[]{"Laser","Fire"},new[]{"Laser","FixedUpdate"}}){var type=typeof(Missile).Assembly.GetType(pair[0]);if(type.GetMethod(pair[1],System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic)==null)throw new Exception("Missing runtime target "+string.Join(".",pair));}
        foreach(var name in new[]{"fireCommanded","beamRenderer"})if(typeof(Laser).GetField(name,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)==null)throw new Exception("Missing Laser."+name);
        if(typeof(Turret).GetMethod("AimTurret",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic,null,new[]{typeof(WeaponStation)},null)==null)throw new Exception("Missing gun aim overload");
        var b=new StringBuilder();foreach(var path in Directory.GetFiles(R,"*.prefab")){
            var go=Load<GameObject>(path.Replace('\\','/'));foreach(var t in go.GetComponentsInChildren<Transform>(true))if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)>0)throw new Exception("Missing script "+path);
            foreach(var c in go.GetComponentsInChildren<MonoBehaviour>(true)){var it=new SerializedObject(c).GetIterator();while(it.Next(true))if(it.propertyType==SerializedPropertyType.ObjectReference&&it.objectReferenceValue==null&&it.objectReferenceInstanceIDValue!=0)throw new Exception("Broken reference "+path+" "+it.propertyPath);}
            foreach(var renderer in go.GetComponentsInChildren<Renderer>(true).Where(r=>r.enabled&&!(r is ParticleSystemRenderer)))foreach(var mat in renderer.sharedMaterials)if(!mat||(AssetDatabase.GetAssetPath(mat).StartsWith(R)&&!mat.GetTexture("_BaseMap")))throw new Exception("Untextured visible material "+path+" / "+renderer.name);
            b.AppendLine("OK "+path);
        }
        var mine=Load<GameObject>(R+"LocustMine.prefab").GetComponent<Missile>();if(mine.GetYield()!=10)throw new Exception("Mine must contain exactly 10 kg HE");
        if(Load<WeaponInfo>(R+"WI_Locust.asset").gravMult!=1||!Load<WeaponInfo>(R+"WI_Locust.asset").bomb)throw new Exception("Native CCIP flags changed");
        var index=OpReferenceIndex.Load();foreach(var path in Directory.GetFiles(R,"Op_*.asset")){var op=Load<OpAddWeaponToHardpoint>(path.Replace('\\','/'));if(op.name!=Path.GetFileNameWithoutExtension(path)||op.aircraft.Count==0)throw new Exception("Invalid Op name or empty carriers: "+path);if(!index||!index.Weapons.Contains(op.weaponJsonKey))throw new Exception("Weapon not indexed: "+op.weaponJsonKey);b.AppendLine(op.weaponJsonKey+" carriers="+string.Join(",",op.aircraft.Select(a=>a.aircraftJsonKey+":"+string.Join("/",a.hardpointIndices))));}
        foreach(var path in Directory.GetFiles(R,"WM_*.asset")){var mount=Load<WeaponMount>(path.Replace('\\','/'));int rounds=mount.prefab.GetComponentsInChildren<MountedMissile>(true).Length;if(mount.ammo<1||(path.Contains("Blackout")&&mount.ammo>3)||rounds!=mount.ammo)throw new Exception("Invalid rack ammunition: "+path);b.AppendLine(mount.jsonKey+" ammo="+mount.ammo+" stations="+rounds);}
        foreach(var path in Directory.GetFiles(R,"WM_Blackout_AGM*.asset")){
            var mount=Load<WeaponMount>(path.Replace('\\','/'));int supports=mount.prefab.GetComponentsInChildren<MeshRenderer>(true).Count(r=>r.enabled&&!r.GetComponentInParent<MountedMissile>(true));if(supports==0)throw new Exception("External rack has no visible native pylon: "+path);
            foreach(var station in mount.prefab.GetComponentsInChildren<MountedMissile>(true))if((station.transform.lossyScale-Vector3.one).sqrMagnitude>.01f)throw new Exception("External missile scale differs from source model: "+path);
            b.AppendLine(mount.jsonKey+" visible native supports="+supports+" ammunition scale=1");
        }
        foreach(var path in Directory.GetFiles(R,"WM_Locust_bomb_*double.asset").Concat(Directory.GetFiles(R,"WM_Locust_bomb_*triple.asset"))){
            var mount=Load<WeaponMount>(path.Replace('\\','/'));var stations=mount.prefab.GetComponentsInChildren<MountedMissile>(true);
            foreach(var station in stations)if((station.transform.lossyScale-Vector3.one).sqrMagnitude>.01f)throw new Exception("Bomb scale differs from source model: "+path);
            if(path.Contains("500_double")&&stations.Any(s=>Mathf.Abs(s.transform.localPosition.x)>.001f))throw new Exception("Bomb is detached from its ejector rail: "+path);
            if(path.Contains("250_triple")&&(stations.Count(s=>Mathf.Abs(s.transform.localPosition.x)<.001f)!=1||stations.Select(s=>Mathf.Round(s.transform.localPosition.y*1000)).Distinct().Count()!=2))throw new Exception("Triple bombs must follow the triangular native rack: "+path);
            b.AppendLine(mount.jsonKey+" native attachment layout and ammunition scale verified");
        }
        foreach(var key in new[]{"Blackout","Locust","LocustMine"}){var g=Load<GameObject>(R+key+".prefab");var a=g.GetComponentInChildren<Animation>(true);if(!a||AnimationUtility.GetCurveBindings(a.clip).Length==0)throw new Exception("Missing deployment curves "+key);b.AppendLine(key+" animation="+a.clip.name+" length="+a.clip.length);}
        File.WriteAllText(R+"Validation~/assets.txt",b.ToString()+"Runtime method and field compatibility passed. Live mission and multiplayer tests pending.\n");
    }
    [MenuItem("Blueprinter/Circuit Breaker/Build bundle")]
    public static void Build(){AssetDatabase.SaveAssets();Validate();Directory.CreateDirectory(R+"Delivery~");Directory.CreateDirectory(R+"Tools~/Runtime/Bundle");var started=DateTime.UtcNow;ModBuilder.Build("Iron Gate","Circuit Breaker",Version,R+"Delivery~");string path=R+"Delivery~/Circuit Breaker_"+Version+".nobp";if(!File.Exists(path)||File.GetLastWriteTimeUtc(path)<started.AddSeconds(-1))throw new Exception("Fresh bundle was not produced");File.Copy(path,R+"Tools~/Runtime/Bundle/CircuitBreaker.nobp",true);Debug.Log("CIRCUIT_BUNDLE_OK");}
    public static void Preview()
    {
        Directory.CreateDirectory(R+"Validation~");
        foreach(var name in new[]{"Blackout","Locust","LocustMine","CB_Blackout_AGM_heavy_single","CB_Blackout_AGM_heavy_double","CB_Blackout_AGM_heavy_triple","CB_Blackout_CruiseMissile1_internal","CB_Blackout_CruiseMissile1_internalx2","CB_Blackout_CruiseMissile1_internalx3","CB_Locust_bomb_cluster1_dual_internal","CB_Locust_bomb_500_double","CB_Locust_bomb_250_triple"})foreach(bool deployed in new[]{false,true}){
            var preview=new PreviewRenderUtility();GameObject g=null;
            try{
                preview.BeginStaticPreview(new Rect(0,0,900,420));g=UnityEngine.Object.Instantiate(Load<GameObject>(R+name+".prefab"));preview.AddSingleGO(g);
                foreach(var c in g.GetComponentsInChildren<Camera>(true))c.enabled=false;
                foreach(var p in g.GetComponentsInChildren<ParticleSystem>(true))p.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                foreach(var animation in g.GetComponentsInChildren<Animation>(true))animation.clip.SampleAnimation(animation.gameObject,deployed?animation.clip.length:0);
                var rs=g.GetComponentsInChildren<Renderer>(true).Where(r=>r.enabled&&!(r is ParticleSystemRenderer)).ToArray();var bounds=rs[0].bounds;foreach(var r in rs.Skip(1))bounds.Encapsulate(r.bounds);
                float size=bounds.extents.magnitude;preview.cameraFieldOfView=30;preview.camera.transform.position=bounds.center+new Vector3(1.2f,.65f,1.5f).normalized*size*4;preview.camera.transform.LookAt(bounds.center);preview.camera.nearClipPlane=.01f;preview.camera.farClipPlane=100;
                preview.lights[0].intensity=1.3f;preview.lights[0].transform.rotation=Quaternion.Euler(40,30,0);preview.lights[1].intensity=.8f;preview.ambientColor=new Color(.35f,.35f,.35f);preview.camera.backgroundColor=new Color(.12f,.15f,.19f);preview.camera.clearFlags=CameraClearFlags.SolidColor;
                preview.Render(true);var image=preview.EndStaticPreview();File.WriteAllBytes(R+"Validation~/"+name+(deployed?"-deployed":"-folded")+".png",image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
            }finally{preview.Cleanup();}
        }
    }
}
