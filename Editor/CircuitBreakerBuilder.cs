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
    const string Version="0.1.13";
    static CircuitBreakerSetup(){EditorApplication.update+=Poll;}
    static bool busy;
    static void Poll(){if(busy||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;string p=R+"Tools~/request.txt";if(!File.Exists(p))return;var action=File.ReadAllText(p).Trim();busy=true;EditorApplication.delayCall+=()=>{try{if(action=="create")Create();else if(action=="build")Build();else if(action=="refreshbuild"){Create();Build();}else if(action=="heatbuild"){UpdateHeatOnly();Build();}else if(action=="flightbuild"){UpdateAltitudeOnly();Build();}else Inspect();File.WriteAllText(R+"Tools~/result.txt","OK "+action);}catch(Exception e){File.WriteAllText(R+"Tools~/result.txt",e.ToString());Debug.LogException(e);}finally{busy=false;if(File.Exists(p)&&File.ReadAllText(p).Trim()==action)File.Delete(p);}};}
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
                bool champ=model=="CHAMP";bool foldedWing=champ&&node.StartsWith("CHAMP_Wing_");var folded=hinge.localRotation;var deployed=Quaternion.Euler(hinge.localEulerAngles.x,hinge.localEulerAngles.y,0);
                if(champ&&!foldedWing){deployed=hinge.localRotation;folded=Quaternion.AngleAxis(node.EndsWith("_L")?90:-90,Vector3.forward)*deployed;}
                float travel=Mathf.Max(.01f,Quaternion.Angle(rotation(0),rotation(previous.length)));
                for(int axis=0;axis<4;axis++){var keys=new List<Keyframe>();for(int frame=0;frame<=Mathf.CeilToInt(previous.length*30);frame++){float time=Mathf.Min(frame/30f,previous.length);var q=champ?Quaternion.Slerp(folded,deployed,Mathf.Clamp01(Quaternion.Angle(rotation(0),rotation(time))/travel)):correction*rotation(time);keys.Add(new Keyframe(time,q[axis]));}result.SetCurve(hingePath,typeof(Transform),"m_LocalRotation."+new[]{"x","y","z","w"}[axis],new AnimationCurve(keys.ToArray()));}
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
                mat.shader=Shader.Find("Universal Render Pipeline/Lit");mat.SetColor("_BaseColor",Color.white);mat.SetTexture("_BaseMap",Load<Texture2D>(R+"Textures/"+texture+".png"));mat.SetFloat("_Metallic",.1f);mat.SetFloat("_Smoothness",.3f);
                if(model=="CHAMP"){mat.SetTexture("_BumpMap",Load<Texture2D>(R+"Textures/CHAMP_Normal.png"));mat.SetFloat("_BumpScale",1);mat.EnableKeyword("_NORMALMAP");mat.SetTexture("_MetallicGlossMap",Load<Texture2D>(R+"Textures/CHAMP_MetallicSmoothness.png"));mat.EnableKeyword("_METALLICSPECGLOSSMAP");mat.SetFloat("_Smoothness",1);}
                EditorUtility.SetDirty(mat);return mat;
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
    static void Heat(GameObject g,GameObject visual)
    {
        var renderers=visual.GetComponentsInChildren<Renderer>(true);var bounds=renderers[0].bounds;foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);
        var a=new GameObject("BlackoutHeat");a.transform.SetParent(g.transform,false);a.transform.localPosition=g.transform.InverseTransformPoint(new Vector3(bounds.center.x,bounds.center.y,bounds.min.z+.05f));a.transform.localRotation=Quaternion.Euler(0,180,0);
        var ps=a.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);var main=ps.main;main.playOnAwake=false;main.loop=true;main.startLifetime=.35f;main.startSpeed=18;main.startSize=.25f;main.startColor=new Color(1,1,1,.9f);main.simulationSpace=ParticleSystemSimulationSpace.Local;main.maxParticles=80;
        var emission=ps.emission;emission.rateOverTime=45;var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=3;shape.radius=.07f;
        var size=ps.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,.6f,1,1.8f));
        var color=ps.colorOverLifetime;color.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.12f),new GradientAlphaKey(0,1)});color.color=gradient;
        var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=Load<Material>(D+"Material/HeatDistortion_PLACEHOLDER.mat");renderer.enabled=false;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
    }
    public static void UpdateHeatOnly()
    {
        // Edit the existing nozzle only: retain the user's placement and every rack asset.
        const int resolution=128;
        var mask=new Texture2D(resolution,resolution,TextureFormat.RGBA32,false,true){name="BlackoutHeatMask",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
        var normal=new Texture2D(resolution,resolution,TextureFormat.RGBA32,false,true){name="BlackoutHeatNormal",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
        for(int y=0;y<resolution;y++)for(int x=0;x<resolution;x++){
            float u=(x+.5f)/resolution*2-1,v=(y+.5f)/resolution*2-1,r=Mathf.Sqrt(u*u+v*v);
            float fade=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.35f,1,r));fade*=fade;
            mask.SetPixel(x,y,new Color(1,1,1,fade));
            var n=new Vector3(Mathf.Sin(v*7+u*3)*.25f*fade,Mathf.Cos(u*8-v*2)*.25f*fade,1).normalized;
            normal.SetPixel(x,y,new Color(n.x*.5f+.5f,n.y*.5f+.5f,n.z*.5f+.5f,1));
        }
        mask.Apply();normal.Apply();
        foreach(var texture in new[]{mask,normal}){
            string path=R+"Textures/"+texture.name+".asset";var old=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if(old){EditorUtility.CopySerialized(texture,old);UnityEngine.Object.DestroyImmediate(texture);EditorUtility.SetDirty(old);}else AssetDatabase.CreateAsset(texture,path);
        }
        string materialPath=R+"Materials/BlackoutHeat.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if(!material){material=new Material(Load<Material>(D+"Material/HeatDistortion_PLACEHOLDER.mat")){name="BlackoutHeat"};AssetDatabase.CreateAsset(material,materialPath);}
        material.SetTexture("_BaseMap",Load<Texture2D>(R+"Textures/BlackoutHeatMask.asset"));material.SetTexture("_BumpMap",Load<Texture2D>(R+"Textures/BlackoutHeatNormal.asset"));
        material.SetFloat("_DistortionStrength",4);material.SetFloat("_DistortionStrengthScaled",.4f);material.SetFloat("_DistortionBlend",1);EditorUtility.SetDirty(material);
        string prefabPath=R+"Blackout.prefab";var root=PrefabUtility.LoadPrefabContents(prefabPath);
        try{
            var ps=root.GetComponentsInChildren<ParticleSystem>(true).Single(p=>p.name=="BlackoutHeat");ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;main.startSpeed=28;main.startLifetime=.28f;main.maxParticles=256;
            var emission=ps.emission;emission.rateOverTime=240;
            var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;renderer.renderMode=ParticleSystemRenderMode.Stretch;renderer.velocityScale=.025f;renderer.lengthScale=2;renderer.sortMode=ParticleSystemSortMode.Distance;
            PrefabUtility.SaveAsPrefabAsset(root,prefabPath);
        }finally{PrefabUtility.UnloadPrefabContents(root);}
        const string description="High-power microwave cruise missile. Continuous 20-second emission suppresses enemy electronics within 10 km before terminal attack. Minimal impact charge.";
        Edit(Load<WeaponInfo>(R+"WI_Blackout.asset"),s=>P(s,"description").stringValue=description);
        Edit(Load<MissileDefinition>(R+"Def_Blackout.asset"),s=>P(s,"description").stringValue=description);
        AssetDatabase.SaveAssets();Debug.Log("CIRCUIT_HEAT_ONLY_OK");
    }
    public static void UpdateAltitudeOnly()
    {
        string path=R+"Blackout.prefab";var root=PrefabUtility.LoadPrefabContents(path);
        try{
            Edit(root.GetComponent<OpticalSeekerCruiseMissile>(),s=>P(s,"altitudeTarget").floatValue=35);
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }finally{PrefabUtility.UnloadPrefabContents(root);}
        AssetDatabase.SaveAssets();Debug.Log("CIRCUIT_ALTITUDE_ONLY_OK");
    }
    static WeaponInfo Weapon(string key,string donor,string prefabDonor,string model,float mass,float yield,float cost,AnimationClip clip)
    {
        var info=Copy<WeaponInfo>(D+"MonoBehaviour/"+donor+"_PLACEHOLDER.asset","WI_"+key);
        string defDonor=key=="Blackout"?"CruiseMissile1":key=="Locust"?"Bomb_cluster1":"Bomb_125_1";
        var def=Copy<MissileDefinition>(D+"MonoBehaviour/"+defDonor+"_PLACEHOLDER.asset","Def_"+key);
        string display=key=="Blackout"?"AGM-180 Blackout":key=="Locust"?"CBU-82M Locust":"Locust Mine";
        Edit(info,s=>{P(s,"weaponName").stringValue=display;P(s,"shortName").stringValue=key.ToUpperInvariant();P(s,"massPerRound").floatValue=mass;P(s,"costPerRound").floatValue=cost;P(s,"blastDamage").floatValue=yield;P(s,"pierceDamage").floatValue=key=="Blackout"?5:0;P(s,"nuclear").boolValue=false;P(s,"airburstHeight").floatValue=0;P(s,"description").stringValue=key=="Blackout"?"High-power microwave cruise missile. Four directed pulses suppress enemy radar, lasers and turret acquisition. Minimal impact charge.":key=="Locust"?"CCIP dispenser with eight remotely delivered mines, each containing a 7 kg HE charge. Arms four seconds after landing; self-destructs after 210 seconds.":"Ground contact mine. 7 kg HE charge. Vehicle and aircraft trigger, 3 m radius.";P(s,"hideInDisplay").boolValue=key=="LocustMine";});
        Edit(def,s=>{P(s,"jsonKey").stringValue="CircuitBreaker_"+key;P(s,"unitName").stringValue=display;P(s,"description").stringValue=info.description;P(s,"mass").floatValue=mass;P(s,"value").floatValue=cost;P(s,"disabled").boolValue=false;});
        var g=Clone(D+"GameObject/"+prefabDonor+"_PLACEHOLDER.prefab");g.name=key;Hide(g);
        foreach(var disp in g.GetComponentsInChildren<SubmunitionDispenser>(true))UnityEngine.Object.DestroyImmediate(disp);
        var visual=Visual(g.transform,model,clip);var missile=g.GetComponent<Missile>();
        if(key!="Blackout"){
            foreach(var seeker in g.GetComponents<MissileSeeker>())UnityEngine.Object.DestroyImmediate(seeker);
            var passive=g.AddComponent<MissileSeeker>();Edit(passive,s=>P(s,"missile").objectReferenceValue=missile);passive.triggerMissileWarning=false;passive.proximityFuse=false;
        }
        Edit(missile,s=>{P(s,"info").objectReferenceValue=info;P(s,"definition").objectReferenceValue=def;P(s,"mass").floatValue=mass;P(s,"blastYield").floatValue=yield;P(s,"pierceDamage").floatValue=key=="Blackout"?5:0;P(s,"impactFuse").boolValue=key=="Blackout";P(s,"warhead.Armed").boolValue=false;P(s,"foldingFins").arraySize=0;if(key!="Blackout")P(s,"motors").arraySize=0;});
        if(key=="Blackout"){
            Edit(def,s=>P(s,"radarSize").floatValue=.004f); Edit(missile,s=>{P(s,"gLimit").floatValue=3.5f;P(s,"maxTurnRate").floatValue=8;P(s,"torque").floatValue=.6f;});
            Edit(missile,s=>P(s,"motors").GetArrayElementAtIndex(0).FindPropertyRelative("thrust").floatValue=7000);
            Fins(missile,visual,clip,new[]{"CHAMP_Wing_L","CHAMP_Wing_R","CHAMP_Fin_L","CHAMP_Fin_R"});Heat(g,visual);
            Edit(missile.GetComponent<OpticalSeekerCruiseMissile>(),s=>{P(s,"altitudeTarget").floatValue=35;P(s,"terminalRange").floatValue=10000;});
            Edit(info,s=>{P(s,"targetRequirements.minRange").floatValue=0;P(s,"targetRequirements.maxRange").floatValue=150000;P(s,"targetRequirements.minAlignment").floatValue=180;P(s,"targetRequirements.lineOfSight").boolValue=false;});
        }
        if(key!="LocustMine"){
            if(key=="Blackout")clip.SampleAnimation(visual,clip.length);
            var renderers=visual.GetComponentsInChildren<Renderer>(true);var bounds=renderers[0].bounds;foreach(var renderer in renderers.Skip(1))bounds.Encapsulate(renderer.bounds);
            Edit(def,s=>{P(s,"length").floatValue=bounds.size.z;P(s,"width").floatValue=bounds.size.x;P(s,"height").floatValue=bounds.size.y;});clip.SampleAnimation(visual,0);
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
    public static void Create()=>Create(false);
    [MenuItem("Blueprinter/Circuit Breaker/Rebuild racks (overwrites mount edits)")]
    public static void RebuildRacks()=>Create(true);
    static void Create(bool rebuildRacks)
    {
        Directory.CreateDirectory(R+"Materials");
        PrepareBlackoutTextures();
        var champ=Clip("CHAMP","BlackoutDeploy",new[]{"CHAMP_Wing_L","CHAMP_Wing_R","CHAMP_Fin_L","CHAMP_Fin_R"});
        var doors=Clip("ClusterBomb","LocustOpen",new[]{"CBU_Door_L","CBU_Door_R"});
        var fins=Clip("Submunition_Mine","LocustMineDeploy",new[]{"Mine_VaneFin_1","Mine_VaneFin_2","Mine_VaneFin_3","Mine_VaneFin_4"});
        var blackout=Weapon("Blackout","info_CruiseMissile1","CruiseMissile1","CHAMP",900,2,12,champ);
        var locust=Weapon("Locust","info_Bomb_cluster1","bomb_cluster_400","ClusterBomb",400,0,1.5f,doors);
        Weapon("LocustMine","info_bomb_125_1","bomb_125_1","Submunition_Mine",16,7,0,fins);
        foreach(var file in Directory.GetFiles(R+"Models","*.fbx")){
            string modelPath=file.Replace('\\','/');var importer=(ModelImporter)AssetImporter.GetAtPath(modelPath);
            foreach(var material in Load<GameObject>(modelPath).GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Distinct()){
                var mapped=AssetDatabase.LoadAssetAtPath<Material>(R+"Materials/"+material.name+".mat");if(mapped)importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),material.name),mapped);
            }
            importer.SaveAndReimport();
        }
        if(rebuildRacks||!File.Exists(R+"WM_Blackout_AGM_heavy_single.asset")){
            foreach(var n in new[]{"CruiseMissile1_internal","CruiseMissile1_internalx2","CruiseMissile1_internalx3","AGM_heavy_single","AGM_heavy_double","AGM_heavy_triple"})Mount("Blackout",blackout,n,"CHAMP",champ);
            foreach(var n in new[]{"bomb_cluster1_single","bomb_cluster1_single_internal","bomb_cluster1_dual_internal"})Mount("Locust",locust,n,"ClusterBomb",doors);
            foreach(var n in new[]{"bomb_500_double","bomb_250_triple"})Mount("Locust",locust,n,"ClusterBomb",doors);
        }
        else RefreshBlackoutVisuals(champ);
        ArchiveUnusedRacks();AssetDatabase.SaveAssets();OpReferenceIndex.Refresh();Validate();Preview();Debug.Log("CIRCUIT_CREATE_OK");
    }
    static void PrepareBlackoutTextures(){
        var normal=(TextureImporter)AssetImporter.GetAtPath(R+"Textures/CHAMP_Normal.png");if(normal.textureType!=TextureImporterType.NormalMap){normal.textureType=TextureImporterType.NormalMap;normal.SaveAndReimport();}
        var metallic=new Texture2D(2,2);var roughness=new Texture2D(2,2);metallic.LoadImage(File.ReadAllBytes(R+"Textures/CHAMP_Metallic.png"));roughness.LoadImage(File.ReadAllBytes(R+"Textures/CHAMP_Roughness.png"));
        if(metallic.width!=roughness.width||metallic.height!=roughness.height)throw new Exception("CHAMP PBR map dimensions differ");
        var colors=metallic.GetPixels();var rough=roughness.GetPixels();for(int i=0;i<colors.Length;i++)colors[i]=new Color(colors[i].r,0,0,1-rough[i].r);metallic.SetPixels(colors);metallic.Apply();string path=R+"Textures/CHAMP_MetallicSmoothness.png";File.WriteAllBytes(path,metallic.EncodeToPNG());UnityEngine.Object.DestroyImmediate(metallic);UnityEngine.Object.DestroyImmediate(roughness);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);var importer=(TextureImporter)AssetImporter.GetAtPath(path);if(importer.sRGBTexture){importer.sRGBTexture=false;importer.SaveAndReimport();}
    }
    static Dictionary<string,string> MountTransforms(GameObject root){
        var result=new Dictionary<string,string>();foreach(var t in root.GetComponentsInChildren<Transform>(true)){
            bool inside=false;for(var parent=t.parent;parent&&parent!=root.transform;parent=parent.parent)if(parent.name=="CircuitVisual")inside=true;if(inside)continue;
            string path="";for(var node=t;node&&node!=root.transform;node=node.parent)path="/"+node.name+"["+node.GetSiblingIndex()+"]"+path;
            result[path]=t.localPosition.ToString("F8")+t.localRotation.ToString("F8")+t.localScale.ToString("F8");
        }return result;
    }
    static void RefreshBlackoutVisuals(AnimationClip clip){
        var report=new StringBuilder();foreach(var path in Directory.GetFiles(R,"CB_Blackout_*.prefab")){
            var root=PrefabUtility.LoadPrefabContents(path.Replace('\\','/'));try{
                var before=MountTransforms(root);
                foreach(var station in root.GetComponentsInChildren<MountedMissile>(true)){
                    var old=station.transform.Find("CircuitVisual");if(!old)throw new Exception("Missing mounted CHAMP visual: "+path);
                    var position=old.localPosition;var rotation=old.localRotation;var scale=old.localScale;int index=old.GetSiblingIndex();
                    var oldModel=old.GetChild(0);var modelPosition=oldModel.localPosition;var modelRotation=oldModel.localRotation;var modelScale=oldModel.localScale;
                    UnityEngine.Object.DestroyImmediate(old.gameObject);var model=Visual(station.transform,"CHAMP",clip);var pivot=model.transform.parent;pivot.SetSiblingIndex(index);pivot.localPosition=position;pivot.localRotation=rotation;pivot.localScale=scale;model.transform.localPosition=modelPosition;model.transform.localRotation=modelRotation;model.transform.localScale=modelScale;
                }
                var after=MountTransforms(root);if(before.Count!=after.Count||before.Any(p=>!after.ContainsKey(p.Key)||after[p.Key]!=p.Value))throw new Exception("Mount transform changed: "+path);
                PrefabUtility.SaveAsPrefabAsset(root,path.Replace('\\','/'));report.AppendLine(path+": support, station and visual pivot transforms unchanged; CHAMP model refreshed");
            }finally{PrefabUtility.UnloadPrefabContents(root);}
        }Directory.CreateDirectory(R+"Validation~");File.WriteAllText(R+"Validation~/mount-model-refresh.txt",report.ToString());
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
        var mine=Load<GameObject>(R+"LocustMine.prefab").GetComponent<Missile>();if(mine.GetYield()!=7)throw new Exception("Mine must contain exactly 7 kg HE");
        foreach(var key in new[]{"Blackout","Locust","LocustMine"}){
            var definition=Load<MissileDefinition>(R+"Def_"+key+".asset");var weapon=Load<WeaponInfo>(R+"WI_"+key+".asset");var missile=Load<GameObject>(R+key+".prefab").GetComponent<Missile>();
            if(definition.description!=weapon.description||missile.GetMass()!=weapon.massPerRound)throw new Exception("Encyclopedia stats differ from weapon: "+key);
            if(key!="Blackout"&&(missile.GetComponent<MissileSeeker>()?.GetType()!=typeof(MissileSeeker)||missile.GetTotalBurnTime()!=0))throw new Exception("Passive seeker or unpowered state missing: "+key);
            b.AppendLine(key+" encyclopedia description/mass synchronized; native seeker API present");
        }
        var blackout=Load<GameObject>(R+"Blackout.prefab");if(blackout.GetComponentsInChildren<Transform>(true).Any(t=>t.name.StartsWith("HPMWave")||t.name.StartsWith("HPMArc")))throw new Exception("Microwave emission must be invisible");
        if(!blackout.GetComponentsInChildren<ParticleSystem>(true).Any(p=>p.name=="BlackoutHeat"))throw new Exception("Native exhaust heat effect missing");
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
            b.AppendLine(mount.jsonKey+" existing attachment transforms preserved; ammunition scale verified");
        }
        foreach(var key in new[]{"Blackout","Locust","LocustMine"}){var g=Load<GameObject>(R+key+".prefab");var a=g.GetComponentInChildren<Animation>(true);if(!a||AnimationUtility.GetCurveBindings(a.clip).Length==0)throw new Exception("Missing deployment curves "+key);b.AppendLine(key+" animation="+a.clip.name+" length="+a.clip.length);}
        File.WriteAllText(R+"Validation~/assets.txt",b.ToString()+"Runtime method and field compatibility passed. Live mission and multiplayer tests pending.\n");
    }
    [MenuItem("Blueprinter/Circuit Breaker/Build bundle")]
    public static void Build(){AssetDatabase.SaveAssets();Validate();Directory.CreateDirectory(R+"Delivery~");Directory.CreateDirectory(R+"Tools~/Runtime/Bundle");var started=DateTime.UtcNow;ModBuilder.Build("Iron Gate","Circuit Breaker",Version,R+"Delivery~");string path=R+"Delivery~/Circuit Breaker_"+Version+".nobp";if(!File.Exists(path)||File.GetLastWriteTimeUtc(path)<started.AddSeconds(-1))throw new Exception("Fresh bundle was not produced");File.Copy(path,R+"Tools~/Runtime/Bundle/CircuitBreaker.nobp",true);Debug.Log("CIRCUIT_BUNDLE_OK");}
    public static void Preview()
    {
        Directory.CreateDirectory(R+"Validation~");
        foreach(var name in new[]{"Blackout","Blackout-HPM","Locust","LocustMine","CB_Blackout_AGM_heavy_single","CB_Blackout_AGM_heavy_double","CB_Blackout_AGM_heavy_triple","CB_Blackout_CruiseMissile1_internal","CB_Blackout_CruiseMissile1_internalx2","CB_Blackout_CruiseMissile1_internalx3","CB_Locust_bomb_cluster1_dual_internal","CB_Locust_bomb_500_double","CB_Locust_bomb_250_triple"})foreach(bool deployed in new[]{false,true}){
            var preview=new PreviewRenderUtility();GameObject g=null;
            try{
                preview.BeginStaticPreview(new Rect(0,0,900,420));g=UnityEngine.Object.Instantiate(Load<GameObject>(R+(name=="Blackout-HPM"?"Blackout":name)+".prefab"));preview.AddSingleGO(g);
                foreach(var c in g.GetComponentsInChildren<Camera>(true))c.enabled=false;
                foreach(var p in g.GetComponentsInChildren<ParticleSystem>(true))p.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                foreach(var animation in g.GetComponentsInChildren<Animation>(true))animation.clip.SampleAnimation(animation.gameObject,deployed?animation.clip.length:0);
                if(name=="Blackout-HPM")foreach(var renderer in g.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.name.StartsWith("HPMWave"))){int i=int.Parse(renderer.name.Substring(7));float phase=(.6f-i*.14f)/1.15f,radius=Mathf.Lerp(.65f,3.4f,phase);renderer.enabled=true;renderer.transform.localScale=new Vector3(radius,radius,radius*.6f);renderer.transform.localPosition=new Vector3(0,0,1.2f-phase*.7f);var props=new MaterialPropertyBlock();props.SetFloat("_Phase",phase);props.SetFloat("_Opacity",Mathf.Sin(phase*Mathf.PI)*.12f);renderer.SetPropertyBlock(props);}
                var rs=g.GetComponentsInChildren<Renderer>(true).Where(r=>r.enabled&&!(r is ParticleSystemRenderer)).ToArray();var bounds=rs[0].bounds;foreach(var r in rs.Skip(1))bounds.Encapsulate(r.bounds);
                float size=bounds.extents.magnitude;preview.cameraFieldOfView=30;preview.camera.transform.position=bounds.center+new Vector3(1.2f,.65f,1.5f).normalized*size*4;preview.camera.transform.LookAt(bounds.center);preview.camera.nearClipPlane=.01f;preview.camera.farClipPlane=100;
                preview.lights[0].intensity=1.3f;preview.lights[0].transform.rotation=Quaternion.Euler(40,30,0);preview.lights[1].intensity=.8f;preview.ambientColor=new Color(.35f,.35f,.35f);preview.camera.backgroundColor=new Color(.12f,.15f,.19f);preview.camera.clearFlags=CameraClearFlags.SolidColor;
                preview.Render(true);var image=preview.EndStaticPreview();File.WriteAllBytes(R+"Validation~/"+name+(deployed?"-deployed":"-folded")+".png",image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
            }finally{preview.Cleanup();}
        }
    }
}
