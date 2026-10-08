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
        Unit attackTarget; int reservationId; bool jumping,strikeLaunched; float jumpTime;
        readonly List<Unit> candidates=new List<Unit>();
        public bool Grounded {get;private set;}
        public void Bind(Missile m){
            if(!Plugin.InMission||missile==m)return;missile=m;boundAt=Time.time;previous=transform.position;
            var a=m.GetComponentInChildren<Animation>(true);if(a)a.Play();
        }
        void FixedUpdate(){
            if(!Plugin.InMission||!missile||missile.disabled||finished||!missile.LocalSim||!missile.rb)return;
            if(jumping){Jump();return;}
            if(!Grounded){
                if(Time.time-boundAt>120){Retire();return;}
                // Model +Y is its nose: the deployed feet face -Y throughout descent.
                missile.rb.useGravity=true;missile.rb.angularVelocity=Vector3.zero;missile.rb.MoveRotation(Quaternion.identity);
                Vector3 v=missile.rb.velocity;v.x*=Mathf.Exp(-.35f*Time.fixedDeltaTime);v.z*=Mathf.Exp(-.35f*Time.fixedDeltaTime);v.y=Mathf.Max(v.y,-SmartMineRules.TerminalSpeed);missile.rb.velocity=v;
                Vector3 step=transform.position-previous;
                if(Time.time-boundAt>.1f){
                    RaycastHit? nearest=null;
                    foreach(var hit in Physics.RaycastAll(previous+Vector3.up*.2f,step.sqrMagnitude>.001f?step.normalized:Vector3.down,step.magnitude+.4f,PhysicsLayers.Everything,QueryTriggerInteraction.Ignore)){
                        var surface=hit.collider.GetComponentInParent<Unit>();
                        if(surface is Missile||surface is GroundVehicle||surface is Aircraft||surface is Ship||hit.normal.y<=.2f)continue;
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
            if(!Plugin.InMission||!missile||!missile.LocalSim||Grounded||jumping||finished||collision.contactCount==0||Time.time-boundAt<.1f)return;
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
            if(Obstructed(transform.position+Vector3.up*.25f,Vector3.up,SmartMineRules.LaunchHeight))return;
            candidates.Clear();BattlefieldGrid.GetUnitsInRangeNonAlloc(missile.GlobalPosition(),SmartMineRules.Radius,candidates);
            candidates.Sort((a,b)=>a&&b?((a.GlobalPosition()-missile.GlobalPosition()).sqrMagnitude.CompareTo((b.GlobalPosition()-missile.GlobalPosition()).sqrMagnitude)):0);
            TargetReservations.Cleanup(Time.time);
            foreach(var target in candidates){
                if(!target||!SmartMineRules.Eligible(target is GroundVehicle,target.disabled,target.NetworkHQ&&missile.NetworkHQ,target.NetworkHQ==missile.NetworkHQ,(target.GlobalPosition()-missile.GlobalPosition()).sqrMagnitude))continue;
                if(!GroundSightClear(target))continue;
                Vector3 apex=transform.position+Vector3.up*SmartMineRules.LaunchHeight;
                if(!AttackPathClear(apex,target)||!TargetReservations.TryReserve(target.GetInstanceID(),Time.time))continue;
                attackTarget=target;reservationId=target.GetInstanceID();jumping=true;Grounded=false;jumpTime=0;
                missile.rb.useGravity=false;missile.rb.isKinematic=true;
                return;
            }
        }
        bool GroundSightClear(Unit target){
            Vector3 from=transform.position+Vector3.up*.25f;
            Vector3 line=target.transform.position+Vector3.up-from;
            foreach(var hit in Physics.RaycastAll(from,line.normalized,line.magnitude,PhysicsLayers.Everything,QueryTriggerInteraction.Ignore)){
                var unit=hit.collider.GetComponentInParent<Unit>();
                if(unit!=missile&&unit!=target)return false;
            }
            return true;
        }
        bool Obstructed(Vector3 start,Vector3 direction,float distance){
            foreach(var collider in Physics.OverlapSphere(start,.18f,PhysicsLayers.Everything,QueryTriggerInteraction.Ignore))if(!collider.GetComponentInParent<Missile>())return true;
            foreach(var hit in Physics.SphereCastAll(start,.18f,direction,distance,PhysicsLayers.Everything,QueryTriggerInteraction.Ignore)){
                // Other airborne ordnance is not a ceiling; bridge/road/vehicle colliders are.
                if(hit.collider.GetComponentInParent<Missile>())continue;
                return true;
            }
            return false;
        }
        bool AttackPathClear(Vector3 from,Unit target){
            foreach(var collider in Physics.OverlapSphere(from,.18f,PhysicsLayers.Everything,QueryTriggerInteraction.Ignore))if(!collider.GetComponentInParent<Missile>())return false;
            Vector3 line=target.transform.position+Vector3.up-from;
            foreach(var hit in Physics.SphereCastAll(from,.18f,line.normalized,line.magnitude,PhysicsLayers.Everything,QueryTriggerInteraction.Ignore)){
                var unit=hit.collider.GetComponentInParent<Unit>();if(unit==target||unit is Missile)continue;return false;
            }
            return true;
        }
        void Jump(){
            if(!attackTarget||attackTarget.disabled||attackTarget.NetworkHQ==missile.NetworkHQ){Retire();return;}
            TargetReservations.Renew(reservationId,Time.time);
            float nextTime=Mathf.Min(jumpTime+Time.fixedDeltaTime,SmartMineRules.JumpApexTime);
            float rise=SmartMineRules.JumpHeight(nextTime)-SmartMineRules.JumpHeight(jumpTime);
            Vector3 next=transform.position+Vector3.up*rise;
            if(Obstructed(transform.position+Vector3.up*.25f,Vector3.up,rise+.05f)){Retire();return;}
            missile.rb.MovePosition(next);
            missile.rb.MoveRotation(Quaternion.Euler(0,nextTime*220,Mathf.Sin(nextTime/SmartMineRules.JumpApexTime*Mathf.PI)*8));
            jumpTime=nextTime;
            if(jumpTime<SmartMineRules.JumpApexTime)return;
            Vector3 line=attackTarget.transform.position+Vector3.up-next;
            if(!AttackPathClear(next,attackTarget)){Retire();return;}
            try{
                Unit owner=missile;if(missile.ownerID.TryGetUnit(out var launcher)&&launcher)owner=launcher;
                var strike=NetworkSceneSingleton<Spawner>.i.SpawnMissile(strikeInfo.weaponPrefab,next,Quaternion.LookRotation(line),line.normalized*25,attackTarget,owner);
                if(strike){strike.NetworkHQ=missile.NetworkHQ;strike.NetworkownerID=missile.ownerID;strikeLaunched=true;}
            }catch(System.Exception e){Plugin.Diagnostic?.Invoke("Smart mine GS25 launch failed: "+e.Message);}
            Retire();
        }
        void OnDestroy(){if(reservationId!=0&&!strikeLaunched)TargetReservations.Release(reservationId);}
        void Retire(){if(finished)return;finished=true;if(reservationId!=0&&!strikeLaunched)TargetReservations.Release(reservationId);missile.SetTarget(null);missile.Networkdisabled=true;FinishVisual();Destroy(missile.gameObject,2);}
        void FinishVisual(){
            if(visualFinished)return;visualFinished=true;
            foreach(var renderer in GetComponentsInChildren<Renderer>(true))renderer.enabled=false;
            foreach(var collider in GetComponentsInChildren<Collider>(true))collider.enabled=false;
            if(!strikeLaunched&&GameAssets.i&&GameAssets.i.rotorStrike_dirt){var puff=Instantiate(GameAssets.i.rotorStrike_dirt,transform.position,Quaternion.LookRotation(Vector3.up));puff.transform.localScale*=.12f;Destroy(puff,3);}
        }
        void Update(){if(Plugin.InMission&&missile&&missile.disabled)FinishVisual();}
    }
}
