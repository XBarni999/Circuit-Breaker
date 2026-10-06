using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class CircuitBreakerGallery
{
    const string Root="Assets/Blueprinter/Mods/Iron Gate/";
    static CircuitBreakerGallery(){EditorApplication.update+=Poll;}
    static void Poll(){
        string request=Root+"Tools~/gallery-request.txt";
        if(!File.Exists(request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
        File.Delete(request);
        try{Render();File.WriteAllText(Root+"Validation~/gallery-result.txt","OK");}
        catch(Exception e){File.WriteAllText(Root+"Validation~/gallery-result.txt",e.ToString());Debug.LogException(e);}
    }
    [MenuItem("Blueprinter/Circuit Breaker/Render gallery")]
    public static void Render(){
        Directory.CreateDirectory(Root+"Validation~/Gallery");
        Shot("Blackout","blackout",true,new Vector3(1.8f,.85f,1));
        Shot("Locust","locust",false,new Vector3(1.8f,.85f,1));
        Shot("LocustMine","locust-mine",true,new Vector3(1.6f,1.6f,1));
        Shot("CB_Blackout_AGM_heavy_triple","blackout-rack",false,new Vector3(1.8f,1.1f,1));
        Shot("CB_Locust_bomb_250_triple","locust-rack",false,new Vector3(1.8f,1.1f,1));
        Debug.Log("CIRCUIT_GALLERY_OK");
    }
    static void Shot(string prefab,string output,bool deployed,Vector3 view){
        var preview=new PreviewRenderUtility();
        try{
            preview.BeginStaticPreview(new Rect(0,0,1600,900));
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Root+prefab+".prefab");
            if(!asset)throw new Exception("Missing "+prefab);
            var model=UnityEngine.Object.Instantiate(asset);preview.AddSingleGO(model);
            foreach(var camera in model.GetComponentsInChildren<Camera>(true))camera.enabled=false;
            foreach(var particles in model.GetComponentsInChildren<ParticleSystem>(true))particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach(var animation in model.GetComponentsInChildren<Animation>(true))if(animation.clip)animation.clip.SampleAnimation(animation.gameObject,deployed?animation.clip.length:0);
            var renderers=model.GetComponentsInChildren<Renderer>(true).Where(r=>r.enabled&&!(r is ParticleSystemRenderer)).ToArray();
            var bounds=renderers[0].bounds;foreach(var renderer in renderers.Skip(1))bounds.Encapsulate(renderer.bounds);
            var cam=preview.camera;cam.orthographic=true;cam.nearClipPlane=.01f;cam.farClipPlane=200;
            cam.transform.position=bounds.center+view.normalized*(bounds.size.magnitude*3+5);cam.transform.LookAt(bounds.center);
            float halfWidth=0,halfHeight=0;
            for(int i=0;i<8;i++){
                Vector3 corner=bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                Vector3 local=cam.transform.InverseTransformPoint(corner);halfWidth=Mathf.Max(halfWidth,Mathf.Abs(local.x));halfHeight=Mathf.Max(halfHeight,Mathf.Abs(local.y));
            }
            cam.orthographicSize=Mathf.Max(halfHeight,halfWidth/(1600f/900f))*1.22f;
            cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.055f,.075f,.095f);
            preview.lights[0].intensity=2.2f;preview.lights[0].transform.rotation=Quaternion.Euler(35,-45,0);
            preview.lights[1].intensity=1.4f;preview.lights[1].transform.rotation=Quaternion.Euler(145,135,0);
            preview.ambientColor=new Color(.4f,.42f,.46f);
            preview.Render(true);var image=preview.EndStaticPreview();
            File.WriteAllBytes(Root+"Validation~/Gallery/"+output+".png",image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
        }finally{preview.Cleanup();}
    }
}
