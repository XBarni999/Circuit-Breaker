using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Mirage;
namespace CircuitBreaker
{
    public sealed class SmartMineController : MonoBehaviour
    {
        Missile missile; Vector3 previous; float boundAt,landedAt,nextScan; bool finished,visualFinished;
        static WeaponInfo strikeInfo;
        readonly List<Unit> candidates=new List<Unit>();
        public bool Grounded {get;private set;}
        public void Bind(Missile m){
            if(missile==m)return;missile=m;boundAt=Time.time;previous=transform.position;
            var a=m.GetComponentInChildren<Animation>(true);if(a)a.Play();
        }
        void FixedUpdate(){
            if(!missile||missile.disabled||finished||!missile.LocalSim||!missile.rb)return;
            if(!Grounded){
                if(Time.time-boundAt>120){Retire();return;}
                // Model +Y is its nose: the deployed feet face -Y throughout descent.
                missile.rb.useGravity=true;missile.rb.angularVelocity=Vector3.zero;missile.rb.MoveRotation(Quaternion.identity);
                Vector3 v=missile.rb.velocity;v.x*=Mathf.Exp(-.35f*Time.fixedDeltaTime);v.z*=Mathf.Exp(-.35f*Time.fixedDeltaTime);v.y=Mathf.Max(v.y,-SmartMineRules.TerminalSpeed);missile.rb.velocity=v;
                Vector3 step=transform.position-previous;
                if(Time.time-boundAt>.1f){
                    RaycastHit? nearest=null;
                    foreach(var hit in Physics.RaycastAll(previous+Vector3.up*.2f,step.sqrMagnitude>.001f?step.normalized:Vector3.down,step.magnitude+.4f,PhysicsLayers.StaticsMask,QueryTriggerInteraction.Ignore)){
                        if(hit.collider.GetComponentInParent<Unit>()==missile||hit.normal.y<=.2f)continue;
                        if(!nearest.HasValue||hit.distance<nearest.Value.distance)nearest=hit;
                    }
                    if(nearest.HasValue)Land(nearest.Value.point,nearest.Value.normal);
                }
                previous=transform.position;return;
            }
            if(Time.time-landedAt>=SmartMineRules.Lifetime){Retire();return;}
            if(Time.time<nextScan)return;nextScan=Time.time+SmartMineRules.ScanInterval;
            Scan();
        }
        void OnCollisionEnter(Collision collision){
            if(!missile||!missile.LocalSim||Grounded||finished||collision.contactCount==0||Time.time-boundAt<.1f)return;
            if((PhysicsLayers.StaticsMask&(1<<collision.gameObject.layer))==0)return;
            var contact=collision.GetContact(0);if(contact.normal.y>.2f)Land(contact.point,contact.normal);
        }
        void Land(Vector3 point,Vector3 normal){
            transform.SetPositionAndRotation(point+normal*.09f,Quaternion.FromToRotation(Vector3.up,normal));
            missile.rb.velocity=Vector3.zero;missile.rb.angularVelocity=Vector3.zero;missile.rb.useGravity=false;missile.rb.isKinematic=true;
            Grounded=true;landedAt=Time.time;nextScan=landedAt+4;
        }
        void Scan(){
            if(!missile.NetworkHQ)return;
            if(!strikeInfo)strikeInfo=Resources.FindObjectsOfTypeAll<WeaponInfo>().FirstOrDefault(w=>w.name=="Submunition1_info"&&w.weaponPrefab);
            if(!strikeInfo)return;
            candidates.Clear();BattlefieldGrid.GetUnitsInRangeNonAlloc(missile.GlobalPosition(),SmartMineRules.Radius,candidates);
            candidates.Sort((a,b)=>a&&b?((a.GlobalPosition()-missile.GlobalPosition()).sqrMagnitude.CompareTo((b.GlobalPosition()-missile.GlobalPosition()).sqrMagnitude)):0);
            TargetReservations.Cleanup(Time.time);
            foreach(var target in candidates){
                if(!target||!SmartMineRules.Eligible(target is GroundVehicle,target.disabled,target.NetworkHQ&&missile.NetworkHQ,target.NetworkHQ==missile.NetworkHQ,(target.GlobalPosition()-missile.GlobalPosition()).sqrMagnitude))continue;
                Vector3 start=transform.position+Vector3.up*.25f,line=target.transform.position+Vector3.up-start;
                bool blocked=false;
                foreach(var hit in Physics.RaycastAll(start,line.normalized,line.magnitude,PhysicsLayers.StaticsMask,QueryTriggerInteraction.Ignore)){
                    var unit=hit.collider.GetComponentInParent<Unit>();if(unit!=missile&&unit!=target){blocked=true;break;}
                }
                if(blocked||!TargetReservations.TryReserve(target.GetInstanceID(),Time.time))continue;
                try{
                    Vector3 port=transform.position+Vector3.up*SmartMineRules.LaunchHeight;
                    Vector3 aim=(target.transform.position+Vector3.up-port).normalized;
                    Unit owner=missile; if(missile.ownerID.TryGetUnit(out var launcher)&&launcher)owner=launcher;
                    var strike=NetworkSceneSingleton<Spawner>.i.SpawnMissile(strikeInfo.weaponPrefab,port,Quaternion.LookRotation(aim),aim*25,target,owner);
                    if(!strike){TargetReservations.Release(target.GetInstanceID());return;}
                    strike.NetworkHQ=missile.NetworkHQ;strike.NetworkownerID=missile.ownerID;
                    Retire();return;
                }catch(System.Exception e){TargetReservations.Release(target.GetInstanceID());Plugin.Diagnostic?.Invoke("Smart mine GS25 launch failed: "+e.Message);return;}
            }
        }
        void Retire(){if(finished)return;finished=true;missile.SetTarget(null);missile.Networkdisabled=true;FinishVisual();Destroy(missile.gameObject,2);}
        void FinishVisual(){
            if(visualFinished)return;visualFinished=true;
            foreach(var renderer in GetComponentsInChildren<Renderer>(true))renderer.enabled=false;
            foreach(var collider in GetComponentsInChildren<Collider>(true))collider.enabled=false;
            if(GameAssets.i&&GameAssets.i.rotorStrike_dirt){var puff=Instantiate(GameAssets.i.rotorStrike_dirt,transform.position,Quaternion.LookRotation(Vector3.up));puff.transform.localScale*=.12f;Destroy(puff,3);}
        }
        void Update(){if(missile&&missile.disabled)FinishVisual();}
    }
}
