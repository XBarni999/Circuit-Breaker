using UnityEngine;
namespace CircuitBreaker
{
    // Terrain probes set a flight waypoint; native aerodynamic and turn limits remain in control.
    internal sealed class TerrainPreview
    {
        float nextScan,slope;bool sampled;
        public float Slope(Missile missile,Vector3 course,float fallback)
        {
            if(sampled&&Time.time<nextScan)return slope;
            sampled=true;nextScan=Time.time+.1f;
            Vector3 origin=missile.transform.position;float speed=Mathf.Max(missile.speed,100);
            float horizon=Mathf.Clamp(speed*6,600,2400),best=float.NegativeInfinity;
            int mask=PhysicsLayers.StaticsMask|PhysicsLayers.ExclusionZonesMask;
            Vector3 motion=missile.rb.velocity;motion.y=0;motion=motion.sqrMagnitude>1?motion.normalized:course;
            foreach(var direction in new[]{course,motion}){
                for(int i=0;i<=12;i++){
                    float distance=horizon*i/12;Vector3 sample=origin+direction*distance;
                    // Read the ground and building roofs below the anticipated route, not only its endpoint.
                    if(Physics.Raycast(sample+Vector3.up*1500,Vector3.down,out var ground,20000,mask,QueryTriggerInteraction.Ignore)){
                        float floor=Mathf.Max(ground.point.y,Datum.LocalSeaY)+35;
                        best=Mathf.Max(best,(floor-origin.y)/Mathf.Max(distance,speed*2));
                    }
                }
                // A swept corridor catches narrow structures falling between the downward samples.
                foreach(var hit in Physics.SphereCastAll(origin,3,direction,horizon,mask,QueryTriggerInteraction.Ignore)){
                    if(hit.collider is TerrainCollider||hit.collider.sharedMaterial==GameAssets.i.terrainMaterial)continue;
                    if(hit.collider.GetComponentInParent<Missile>()==missile)continue;
                    float roof=hit.collider.bounds.max.y+35;
                    best=Mathf.Max(best,(roof-origin.y)/Mathf.Max(hit.distance,speed*2));
                }
            }
            slope=float.IsNegativeInfinity(best)?fallback:best;
            return slope;
        }
    }
}
