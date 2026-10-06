using UnityEngine;
using System.Linq;
using Mirage;
namespace CircuitBreaker
{
    public sealed class LocustDispenser : MonoBehaviour
    {
        Missile missile; bool opened; float openedAt; Animation animation;
        public void Bind(Missile m){missile=m;animation=m.GetComponentInChildren<Animation>(true);}
        void FixedUpdate(){
            if(!missile||missile.disabled||opened)return;
            if(missile.timeSinceSpawn<.3f||missile.rb.velocity.y>0)return;
            if(!Physics.Raycast(transform.position,Vector3.down,out var hit,10000,PhysicsLayers.StaticsMask,QueryTriggerInteraction.Ignore)||hit.distance>175)return;
            opened=true;openedAt=Time.time;if(animation)animation.Play();
            if(!missile.LocalSim)return;
            var info=Resources.FindObjectsOfTypeAll<WeaponInfo>().FirstOrDefault(w=>w.name=="WI_LocustMine");
            if(!info||!info.weaponPrefab){Debug.LogError("Locust mine prefab unavailable");return;}
            Vector3 forward=missile.rb.velocity;forward.y=0;forward.Normalize();if(forward.sqrMagnitude<.5f)forward=transform.forward;
            Vector3 right=Vector3.Cross(Vector3.up,forward);
            // 4 longitudinal pairs; ground footprint approx 150 x 40 m at 175 m release.
            for(int i=0;i<8;i++){
                float row=i/2;float vy=missile.rb.velocity.y;float t=Mathf.Max(.3f,(vy+Mathf.Sqrt(vy*vy+2*9.81f*hit.distance))/9.81f);
                Vector3 v=missile.rb.velocity+forward*((row-1.5f)*50f/t)+right*((i%2==0?-20f:20f)/t);
                NetworkSceneSingleton<Spawner>.i.SpawnMissile(info.weaponPrefab,transform.position+right*(i%2==0?-.6f:.6f),Quaternion.LookRotation(forward),v,null,missile);
            }
        }
        void Update(){if(!opened)return;if(Time.time-openedAt>.8f){foreach(var t in GetComponentsInChildren<Transform>(true))if(t.name=="CBU_InternalPayloadMines")t.gameObject.SetActive(false);}if(Time.time-openedAt>2&&missile&&missile.LocalSim&&!missile.disabled){missile.Networkdisabled=true;Destroy(missile.gameObject,4);}}
    }
    public sealed class LocustMine : MonoBehaviour
    {
        Missile missile; float landedAt,check; Vector3 previous; bool exploded; SphereCollider trigger;
        readonly Collider[] overlaps=new Collider[64];
        public bool Grounded {get;private set;}
        public void Bind(Missile m){missile=m;previous=transform.position;var a=m.GetComponentInChildren<Animation>(true);if(a)a.Play();}
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
