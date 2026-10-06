using UnityEngine;
namespace CircuitBreaker
{
    public sealed class BlackoutFlight : MonoBehaviour
    {
        Missile missile; Unit target; GlobalPosition point; bool designated,active,spent,unfolded;
        float start,nextPulse,nextFlash; int pulses; Vector3 runVector; ParticleSystem[] arcs;
        static readonly System.Reflection.FieldInfo altitude=HarmonyLib.AccessTools.Field(typeof(OpticalSeekerCruiseMissile),"altitudeTarget");
        bool gps;
        public void Bind(Missile m){missile=m;arcs=m.GetComponentsInChildren<ParticleSystem>(true).Length==0?new ParticleSystem[0]:System.Array.FindAll(m.GetComponentsInChildren<ParticleSystem>(true),p=>p.name.StartsWith("HPMArc"));}
        public void Designate(Unit t,GlobalPosition p){if(gps)return;target=t;point=t?t.GlobalPosition():p;designated=true;}
        public void DesignateGPS(GlobalPosition p){target=null;point=p;designated=true;gps=true;}
        public void Guide(OpticalSeekerCruiseMissile seeker)
        {
            if(!missile || !missile.LocalSim || missile.disabled)return;
            if(!designated)return;
            if(!unfolded && missile.timeSinceSpawn>1){missile.DeployFins();unfolded=true;}
            if(missile.timeSinceSpawn>2)missile.SetTangible(true);
            if(missile.timeSinceSpawn<1)return;
            if(target && !target.disabled)point=target.GlobalPosition();
            Vector3 delta=point-missile.GlobalPosition();delta.y=0;
            if(!active&&!spent&&delta.magnitude<Plugin.TriggerRange.Value){active=true;start=Time.time;nextPulse=start;runVector=missile.rb.velocity.normalized;runVector.y=0;runVector.Normalize();if(runVector.sqrMagnitude<.5f)runVector=transform.forward;}
            if(active){
                if(delta.magnitude<=Plugin.Radius.Value&&Time.time>=nextPulse&&pulses<Plugin.PulseCount.Value){Suppression.Pulse(missile,runVector);pulses++;nextPulse=Time.time+Plugin.PulseInterval.Value;}
                if(Time.time-start>=Plugin.RunDuration.Value){active=false;spent=true;missile.Arm();}
            }
            if(spent){missile.SetAimpoint(point,target?target.rb?.velocity??Vector3.zero:Vector3.zero);return;}
            // Keep the vanilla forward terrain lookahead and physical missile steering.
            var desired=active?missile.GlobalPosition()+runVector*1200:point;
            altitude.SetValue(seeker,active?45f:30f);
            missile.SetAimpoint(seeker.TerrainWaypoint(desired),Vector3.zero);
        }
        void Update(){if(!missile||missile.disabled)return;bool showing=active;
            // Clients infer the visual run from replicated position and their initial target.
            if(!missile.LocalSim&&designated){Vector3 d=point-missile.GlobalPosition();d.y=0;if(!spent&&d.magnitude<Plugin.TriggerRange.Value){if(!active){active=true;start=Time.time;}showing=Time.time-start<Plugin.RunDuration.Value;if(!showing){spent=true;active=false;}}}
            if(showing&&Time.time>=nextFlash){nextFlash=Time.time+2.5f;foreach(var p in arcs)if(p)p.Play(true);}}
    }
}
