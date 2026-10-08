using UnityEngine;
namespace CircuitBreaker
{
    public sealed class ExhaustHeat : MonoBehaviour
    {
        Missile missile; ParticleSystem plume; Material material;
        public void Bind(Missile m){
            if(missile)return;missile=m;
            foreach(var p in m.GetComponentsInChildren<ParticleSystem>(true))if(p.name=="BlackoutHeat"){
                plume=p;var renderer=p.GetComponent<ParticleSystemRenderer>();
                material=new Material(renderer.sharedMaterial);renderer.sharedMaterial=material;renderer.enabled=true;break;
            }
        }
        void Update(){if(!plume)return;bool burning=missile&&!missile.disabled&&missile.timeSinceSpawn>=1.3f&&missile.timeSinceSpawn<601.3f;if(burning&&!plume.isPlaying)plume.Play();else if(!burning&&plume.isPlaying)plume.Stop(true,ParticleSystemStopBehavior.StopEmitting);}
        void OnDestroy(){if(material)Destroy(material);}
    }
}
