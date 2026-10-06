using UnityEngine;
using System.Linq;
using Mirage;
namespace CircuitBreaker
{
    public sealed class LocustDispenser : MonoBehaviour
    {
        Missile missile; bool opened; float openedAt,releaseDelay,nextPair,finishedAt,boundAt; int released; Animation animation; WeaponInfo mineInfo;
        const float PairInterval=.08f;
        public void Bind(Missile m){if(missile==m)return;missile=m;boundAt=Time.time;animation=m.GetComponentInChildren<Animation>(true);releaseDelay=(animation&&animation.clip?animation.clip.length:.77f)+.05f;}
        float Age=>Mathf.Max(missile.timeSinceSpawn,Time.time-boundAt);
        public void Impact(Vector3 normal){if(!missile||!missile.LocalSim||missile.disabled)return;missile.Arm();missile.Detonate(normal,false,true);}
        public void CheckImpact(){
            if(!missile||!missile.LocalSim||missile.disabled||!missile.rb||Age<.3f)return;
            Vector3 velocity=missile.rb.velocity;
            foreach(var hit in Physics.RaycastAll(transform.position,velocity.normalized,velocity.magnitude*Time.fixedDeltaTime*1.1f,PhysicsLayers.StaticsMask|PhysicsLayers.ShipsMask,QueryTriggerInteraction.Ignore)){
                if(hit.collider.GetComponentInParent<Unit>()==missile)continue;Impact(hit.normal);return;
            }
        }
        void FixedUpdate(){
            if(!missile||missile.disabled||!missile.rb||released>=8)return;
            if(Age<.3f)return;
            if(missile.LocalSim&&!missile.IsTangible())missile.SetTangible(true);
            missile.UpdateRadarAlt();float height=Mathf.Max(0,missile.radarAlt);
            if(missile.LocalSim&&(height<=.25f||Age>120)){Impact(Vector3.up);return;}
            if(missile.rb.velocity.y>0)return;
            if(!opened){
                // Start the doors early enough for a fast, high-altitude drop; the mines release
                // near the intended altitude after the entire clip has completed.
                if(!LocustDeploymentRules.ShouldOpen(Age,missile.rb.velocity.y,height,releaseDelay))return;
                opened=true;openedAt=Time.time;nextPair=openedAt+releaseDelay;if(animation)animation.Play();
            }
            if(!missile.LocalSim||Time.time<nextPair)return;
            if(!mineInfo)mineInfo=Resources.FindObjectsOfTypeAll<WeaponInfo>().FirstOrDefault(w=>w.name=="WI_LocustMine");
            if(!mineInfo||!mineInfo.weaponPrefab){Debug.LogError("Locust mine prefab unavailable");released=8;finishedAt=Time.time;return;}
            Vector3 forward=missile.rb.velocity;forward.y=0;forward.Normalize();if(forward.sqrMagnitude<.5f)forward=transform.forward;
            Vector3 right=Vector3.Cross(Vector3.up,forward);
            // 4 longitudinal pairs; ground footprint approx 150 x 40 m at 325 m release.
            for(int pair=0;pair<2;pair++){
                int i=released++;
                float row=i/2;float vy=missile.rb.velocity.y;float t=Mathf.Max(.3f,(vy+Mathf.Sqrt(vy*vy+2*9.81f*height))/9.81f);
                Vector3 v=missile.rb.velocity+forward*((row-1.5f)*50f/t)+right*((i%2==0?-20f:20f)/t);
                Vector3 port=transform.TransformPoint(new Vector3(i%2==0?-.13f:.13f,-.32f,-.55f+row*.35f));
                NetworkSceneSingleton<Spawner>.i.SpawnMissile(mineInfo.weaponPrefab,port,Quaternion.LookRotation(forward),v,null,missile);
            }
            nextPair=Time.time+PairInterval;if(released==8)finishedAt=Time.time;
        }
        void Update(){if(!opened)return;float complete=releaseDelay+3*PairInterval;if(Time.time-openedAt>=complete){foreach(var t in GetComponentsInChildren<Transform>(true))if(t.name=="CBU_InternalPayloadMines")t.gameObject.SetActive(false);}if(released==8&&Time.time-finishedAt>.4f&&missile&&missile.LocalSim&&!missile.disabled){missile.Networkdisabled=true;Destroy(missile.gameObject,4);}}
    }
    public sealed class LocustMine : MonoBehaviour
    {
        Missile missile; float landedAt,check; Vector3 previous; bool exploded; SphereCollider trigger;
        readonly Collider[] overlaps=new Collider[64];
        public bool Grounded {get;private set;}
        public void Bind(Missile m){if(missile==m)return;missile=m;previous=transform.position;var a=m.GetComponentInChildren<Animation>(true);if(a)a.Play();}
        void FixedUpdate(){
            if(!missile||missile.disabled||exploded||!missile.LocalSim)return;
            if(!Grounded&&missile.timeSinceSpawn>Plugin.MineLife.Value+45){Explode();return;}
            if(!Grounded){
                Vector3 step=transform.position-previous;
                if(missile.timeSinceSpawn>.1f && Physics.Raycast(previous+Vector3.up*.2f,step.sqrMagnitude>.001f?step.normalized:Vector3.down,out var hit,step.magnitude+.4f,PhysicsLayers.StaticsMask,QueryTriggerInteraction.Ignore)){
                    transform.position=hit.point+hit.normal*.09f;transform.rotation=Quaternion.FromToRotation(Vector3.up,hit.normal);
                    missile.rb.velocity=Vector3.zero;missile.rb.angularVelocity=Vector3.zero;missile.rb.isKinematic=true;Grounded=true;landedAt=Time.time;
                    trigger=gameObject.AddComponent<SphereCollider>();trigger.radius=3;trigger.isTrigger=true;missile.Arm();
                }
                previous=transform.position;return;
            }
            if(Time.time-landedAt>=Plugin.MineLife.Value){Explode();return;}
            if(Time.time-landedAt<4||Time.time<check)return;check=Time.time+.1f;
            // Poll also catches a vehicle already overlapping at arming time.
            int count=Physics.OverlapSphereNonAlloc(transform.position,3,overlaps,PhysicsLayers.Everything,QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++)if(Vehicle(overlaps[i])){Explode();break;}
        }
        bool Vehicle(Collider c){var u=c.GetComponentInParent<Unit>();return u&&!u.disabled&&(u is GroundVehicle||u is Aircraft);}
        void OnTriggerEnter(Collider c){if(Grounded&&Time.time-landedAt>=4&&missile&&missile.LocalSim&&Vehicle(c))Explode();}
        void Explode(){if(exploded)return;exploded=true;missile.Arm();missile.Detonate(Vector3.up,true,true);}
    }
}
