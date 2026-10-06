using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using HarmonyLib;
namespace CircuitBreaker
{
    internal static class Suppression
    {
        static readonly Dictionary<Component,float> affected=new Dictionary<Component,float>();
        static readonly List<Unit> units=new List<Unit>();
        static float cleanup;
        public static bool Active(Component c)=>c&&affected.TryGetValue(c,out var expiry)&&Time.time<expiry;
        public static void Pulse(Missile source,Vector3 forward)
        {
            if(!source.LocalSim||!source.NetworkHQ)return;
            units.Clear();BattlefieldGrid.GetUnitsInRangeNonAlloc(source.GlobalPosition(),Plugin.Radius.Value,units);
            foreach(var u in units){
                if(!u||u.disabled||u is Missile||u is Aircraft||!u.NetworkHQ||u.NetworkHQ==source.NetworkHQ)continue;
                Vector3 delta=u.GlobalPosition()-source.GlobalPosition();if(delta.sqrMagnitude>Plugin.Radius.Value*Plugin.Radius.Value)continue;
                // Broad down-facing fan: forward and lateral electronics, with terrain shielding.
                if(Vector3.Dot(delta.normalized,forward)<-.35f)continue;
                if(Physics.Linecast(source.transform.position,u.transform.position+Vector3.up*3,out var blocker,PhysicsLayers.StaticsMask)&&blocker.collider.GetComponentInParent<Unit>()!=u)continue;
                foreach(var detector in u.GetComponentsInChildren<TargetDetector>(true)){affected[detector]=Time.time+Plugin.Duration.Value;detector.detectedTargets.Clear();}
                foreach(var turret in u.GetComponentsInChildren<Turret>(true))affected[turret]=Time.time+Plugin.Duration.Value;
                foreach(var laser in u.GetComponentsInChildren<Laser>(true)){
                    affected[laser]=Time.time+Plugin.Duration.Value;
                    AccessTools.Field(typeof(Laser),"fireCommanded").SetValue(laser,false);
                    var beam=AccessTools.Field(typeof(Laser),"beamRenderer").GetValue(laser) as Renderer;if(beam)beam.enabled=false;
                }
            }
        }
        public static void Cleanup(){if(Time.time<cleanup)return;cleanup=Time.time+1;foreach(var key in affected.Keys.Where(k=>!k||affected[k]<=Time.time).ToArray())affected.Remove(key);}
        public static void Clear(){affected.Clear();units.Clear();cleanup=0;}
    }
}
