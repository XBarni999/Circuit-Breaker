using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using HarmonyLib;
namespace CircuitBreaker
{
    internal static class Suppression
    {
        static readonly Dictionary<Component,float> affected=new Dictionary<Component,float>();
        static readonly Dictionary<Unit,float> affectedUnits=new Dictionary<Unit,float>();
        static readonly Dictionary<Aircraft,float> affectedAircraft=new Dictionary<Aircraft,float>();
        static readonly List<Unit> units=new List<Unit>();
        static float cleanup;
        static readonly Dictionary<Missile,float> nextReport=new Dictionary<Missile,float>();
        public static bool Active(Component c)=>c&&affected.TryGetValue(c,out var expiry)&&HpmRules.SuppressionActive(Time.time,expiry);
        public static bool ActiveUnit(Unit u)=>u&&affectedUnits.TryGetValue(u,out var expiry)&&HpmRules.SuppressionActive(Time.time,expiry);
        public static bool ActiveAircraft(Aircraft a)=>a&&affectedAircraft.TryGetValue(a,out var expiry)&&HpmRules.SuppressionActive(Time.time,expiry);
        public static bool IsActivator(Unit u){
            if(!u||u.disabled||u is Missile||u is Aircraft||u is Ship)return false;
            bool radar=u.radar is Radar attached&&attached.RadarParameters.maxRange>0;
            foreach(var r in u.GetComponentsInChildren<Radar>(true))radar|=r.RadarParameters.maxRange>0;
            bool sam=false;
            foreach(var station in u.weaponStations){var info=station.WeaponInfo;sam|=info&&info.missile&&!info.gun&&(info.effectiveness.antiAir>0||info.effectiveness.antiMissile>0);}
            return HpmRules.Activates(false,false,false,radar,sam);
        }
        static bool Exposed(Missile source,Unit u){
            Vector3 antenna=u.transform.position+Vector3.up*3;
            if(u.radar){var scanner=u.radar.GetScanPoint();if(scanner)antenna=scanner.position;}
            Vector3 origin=source.transform.position+Vector3.up*.3f;Vector3 line=antenna-origin;
            foreach(var hit in Physics.RaycastAll(origin,line.normalized,line.magnitude,PhysicsLayers.StaticsMask,QueryTriggerInteraction.Ignore)){
                var hitUnit=hit.collider.GetComponentInParent<Unit>();if(hitUnit==source||hitUnit==u)continue;return false;
            }
            return true;
        }
        public static void Emit(Missile source)
        {
            if(!source.LocalSim||!source.NetworkHQ)return;
            units.Clear();BattlefieldGrid.GetUnitsInRangeNonAlloc(source.GlobalPosition(),Plugin.Radius.Value,units);
            int hits=0,shielded=0;var hitNames=new List<string>();
            foreach(var u in units){
                if(!u||u.disabled||!u.NetworkHQ||!HpmRules.ExposedTarget(u is Aircraft,u is Missile,u.NetworkHQ==source.NetworkHQ))continue;
                Vector3 delta=u.GlobalPosition()-source.GlobalPosition();if(delta.sqrMagnitude>Plugin.Radius.Value*Plugin.Radius.Value)continue;
                // Continuous omnidirectional coverage; static terrain still shields electronics.
                if(!Exposed(source,u)){shielded++;continue;}
                Apply(u);
                hits++;
                if(hitNames.Count<12)hitNames.Add(u.definition?u.definition.jsonKey:u.name);
                // Native replicated event drives the game's map jamming indicators.
                u.Jam(new Unit.JamEventArgs{jammingUnit=source,jamAmount=2});
            }
            if(!nextReport.TryGetValue(source,out var reportAt)||Time.time>=reportAt){nextReport[source]=Time.time+2;Plugin.Diagnostic?.Invoke("HPM emission: scanned="+units.Count+", suppressed="+hits+", occluded="+shielded+", speed="+source.speed.ToString("F0")+" m/s; units="+string.Join(",",hitNames));}
        }
        public static void Apply(Unit u){
            if(!u||u.disabled||u is Missile)return;
            if(u is Aircraft aircraft){
                affectedAircraft[aircraft]=Time.time+Plugin.Duration.Value;
                foreach(var r in aircraft.GetComponentsInChildren<Radar>(true)){affected[r]=Time.time+Plugin.Duration.Value;r.detectedTargets.Clear();}
                if(aircraft.radar is Radar radar){affected[radar]=Time.time+Plugin.Duration.Value;radar.detectedTargets.Clear();}
                return;
            }
            affectedUnits[u]=Time.time+Plugin.Duration.Value;
            foreach(var detector in u.GetComponentsInChildren<TargetDetector>(true)){affected[detector]=Time.time+Plugin.Duration.Value;detector.detectedTargets.Clear();}
            foreach(var turret in u.GetComponentsInChildren<Turret>(true))affected[turret]=Time.time+Plugin.Duration.Value;
            foreach(var laser in u.GetComponentsInChildren<Laser>(true)){
                    affected[laser]=Time.time+Plugin.Duration.Value;
                    AccessTools.Field(typeof(Laser),"fireCommanded").SetValue(laser,false);
                    var beam=AccessTools.Field(typeof(Laser),"beamRenderer").GetValue(laser) as Renderer;if(beam)beam.enabled=false;
            }
        }
        public static void Cleanup(){if(Time.time<cleanup)return;cleanup=Time.time+1;foreach(var key in affected.Keys.Where(k=>!k||affected[k]<=Time.time).ToArray())affected.Remove(key);foreach(var key in affectedUnits.Keys.Where(k=>!k||affectedUnits[k]<=Time.time).ToArray())affectedUnits.Remove(key);foreach(var key in affectedAircraft.Keys.Where(k=>!k||affectedAircraft[k]<=Time.time).ToArray())affectedAircraft.Remove(key);foreach(var key in nextReport.Keys.Where(k=>!k||k.disabled).ToArray())nextReport.Remove(key);}
        public static void Clear(){affected.Clear();affectedUnits.Clear();affectedAircraft.Clear();nextReport.Clear();units.Clear();cleanup=0;}
    }
}
