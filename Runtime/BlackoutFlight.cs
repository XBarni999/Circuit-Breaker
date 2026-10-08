using UnityEngine;
namespace CircuitBreaker
{
    public sealed class BlackoutFlight : MonoBehaviour
    {
        Missile missile; Unit target; GlobalPosition point; bool designated,active,spent,unfolded,running;
        float emissionEnd,nextEmission; Vector3 runVector; bool wreckHidden,launchCaptured;
        readonly TerrainPreview terrain=new TerrainPreview();
        float pitch,lastGuide; bool pitchInitialized;
        readonly System.Collections.Generic.List<Unit> terminalCandidates=new System.Collections.Generic.List<Unit>();
        static readonly System.Reflection.FieldInfo altitude=HarmonyLib.AccessTools.Field(typeof(OpticalSeekerCruiseMissile),"altitudeTarget");
        bool gps;
        static readonly System.Reflection.FieldInfo trim=HarmonyLib.AccessTools.Field(typeof(OpticalSeekerCruiseMissile),"altitudeTrim");
        public void Bind(Missile m){missile=m;(m.GetComponent<ExhaustHeat>()??m.gameObject.AddComponent<ExhaustHeat>()).Bind(m);}
        public void Designate(Unit t,GlobalPosition p){if(designated)return;target=t;point=t?t.GlobalPosition():p;designated=true;gps=!t;}
        public void CaptureLaunch(Unit t,GlobalPosition p,bool coordinateDesignation){if(launchCaptured)return;launchCaptured=true;target=t;point=t?t.GlobalPosition():p;designated=true;gps=coordinateDesignation;}
        public bool CheckImpact(){
            if(!missile||!missile.LocalSim||missile.disabled||missile.timeSinceSpawn<2||!missile.rb)return false;
            if(transform.position.y<Datum.LocalSeaY){missile.Arm();missile.Detonate(Vector3.up,false,false);return true;}
            Vector3 velocity=missile.rb.velocity;
            if(velocity.sqrMagnitude<25){
                if(missile.timeSinceSpawn>3&&Physics.Raycast(transform.position,Vector3.down,out var restingSurface,3,PhysicsLayers.StaticsMask|PhysicsLayers.ShipsMask,QueryTriggerInteraction.Ignore)){missile.Arm();missile.Detonate(restingSurface.normal,false,true);return true;}
                return false;
            }
            RaycastHit? nearest=null;
            foreach(var hit in Physics.RaycastAll(transform.position,velocity.normalized,velocity.magnitude*Time.fixedDeltaTime*1.1f,~PhysicsLayers.ExclusionZonesMask,QueryTriggerInteraction.Ignore)){
                if(hit.collider.GetComponentInParent<Missile>()==missile)continue;
                if(!nearest.HasValue||hit.distance<nearest.Value.distance)nearest=hit;
            }
            if(!nearest.HasValue)return false;
            var impact=nearest.Value;missile.Arm();transform.position=impact.point-velocity.normalized*.2f;
            bool ground=impact.collider.sharedMaterial==GameAssets.i.terrainMaterial;
            missile.Detonate(impact.normal,!ground,ground);return true;
        }
        public void HideWreck(){
            if(wreckHidden)return;wreckHidden=true;
            foreach(var t in GetComponentsInChildren<Transform>(true))if(t.name=="CircuitVisual")foreach(var r in t.GetComponentsInChildren<Renderer>(true))r.enabled=false;
            if(missile&&missile.LocalSim)Destroy(gameObject,2);
        }
        void BeginTerminal(){
            spent=true;active=false;
            if(!target||target.disabled){
                target=null;float nearest=Plugin.Radius.Value*Plugin.Radius.Value;
                terminalCandidates.Clear();BattlefieldGrid.GetUnitsInRangeNonAlloc(missile.GlobalPosition(),Plugin.Radius.Value,terminalCandidates);
                foreach(var candidate in terminalCandidates){
                    if(!candidate||candidate.disabled||candidate is Missile||candidate is Aircraft||!candidate.NetworkHQ||candidate.NetworkHQ==missile.NetworkHQ)continue;
                    float distance=(candidate.GlobalPosition()-missile.GlobalPosition()).sqrMagnitude;
                    if(distance<nearest){nearest=distance;target=candidate;}
                }
                if(target)point=target.GlobalPosition();
            }
            missile.Arm();
        }
        void FixedUpdate(){
            if(missile&&missile.disabled){HideWreck();return;}
            if(!active||!missile||!missile.LocalSim||missile.disabled)return;
            if(Time.time>=emissionEnd){active=false;return;}
            if(Time.time<nextEmission)return;
            nextEmission=Time.time+.25f;
            Suppression.Emit(missile);
        }
        public void Guide(OpticalSeekerCruiseMissile seeker)
        {
            if(!missile||!missile.LocalSim||missile.disabled||!designated)return;
            if(!unfolded&&missile.timeSinceSpawn>1){missile.DeployFins();unfolded=true;}
            if(missile.timeSinceSpawn>2){missile.SetTangible(true);if(!missile.IsArmed())missile.Arm();}
            if(missile.timeSinceSpawn<1)return;
            if(target&&!target.disabled)point=target.GlobalPosition();
            Vector3 delta=point-missile.GlobalPosition();float designatedDistance=delta.magnitude;delta.y=0;
            if(emissionEnd==0&&!spent){
                bool eligible=gps||(target&&!target.disabled&&Suppression.IsActivator(target)&&target.NetworkHQ!=missile.NetworkHQ);
                if(HpmRules.InActivationRange(eligible,designatedDistance,Plugin.TriggerRange.Value)){
                    active=true;emissionEnd=Time.time+Plugin.EmissionDuration.Value;nextEmission=Time.time+.25f;Suppression.Emit(missile);
                    Plugin.Diagnostic?.Invoke("Blackout HPM active at launch designation: "+(target?target.definition.jsonKey:"GPS")+", distance="+designatedDistance.ToString("F0")+" m, seconds="+Plugin.EmissionDuration.Value);
                }
            }
            if(!spent&&!running&&delta.magnitude<=2000){running=true;runVector=missile.rb?missile.rb.velocity:transform.forward;runVector.y=0;if(runVector.sqrMagnitude<1){runVector=transform.forward;runVector.y=0;}runVector.Normalize();}
            if(emissionEnd>0&&!spent){
                if(Time.time>=emissionEnd)BeginTerminal();
            }
            if(spent){missile.SetAimpoint(point,target?target.rb?.velocity??Vector3.zero:Vector3.zero);return;}
            var desired=running?missile.GlobalPosition()+runVector*1200:point;
            altitude.SetValue(seeker,35f);
            trim.SetValue(seeker,0f);
            var waypoint=seeker.TerrainWaypoint(desired);Vector3 raw=waypoint-missile.GlobalPosition();Vector3 toWaypoint=raw;toWaypoint.y=0;
            if(toWaypoint.magnitude<600){Vector3 forward=desired-missile.GlobalPosition();forward.y=0;if(forward.sqrMagnitude<1)forward=transform.forward;toWaypoint=forward.normalized*1200;waypoint=missile.GlobalPosition()+toWaypoint;waypoint.y=missile.GlobalPosition().y+raw.y;}
            float slope=terrain.Slope(missile,toWaypoint.normalized,raw.y/toWaypoint.magnitude);
            if(!pitchInitialized){Vector3 velocity=missile.rb.velocity;pitch=Mathf.Atan2(velocity.y,new Vector2(velocity.x,velocity.z).magnitude)*Mathf.Rad2Deg;pitch=Mathf.Clamp(pitch,-Plugin.DescentAngle.Value,25);pitchInitialized=true;lastGuide=Time.time;}
            float dt=Mathf.Clamp(Time.time-lastGuide,0,.1f);lastGuide=Time.time;
            pitch=DescentProfile.ApproachPitch(pitch,slope,Plugin.DescentAngle.Value,dt);
            waypoint.y=missile.GlobalPosition().y+toWaypoint.magnitude*Mathf.Tan(pitch*Mathf.Deg2Rad);
            waypoint.y=DescentProfile.LimitHeight(missile.GlobalPosition().y,waypoint.y,toWaypoint.magnitude,missile.timeSinceSpawn-1,Plugin.DescentAngle.Value,Plugin.DescentRamp.Value);
            missile.SetAimpoint(waypoint,Vector3.zero);
        }
    }
}
