using System;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;

namespace CircuitBreaker
{
    [BepInPlugin("ua.ncmod.circuitbreaker", "Circuit Breaker", "0.1.2")]
    [BepInDependency("com.nikkorap.blueprinter", "2.0.1")]
    public sealed class Plugin : BaseUnityPlugin
    {
        internal static ConfigEntry<float> Radius, Duration, RunDuration, TriggerRange, PulseInterval, MineLife;
        internal static ConfigEntry<int> PulseCount;
        Harmony harmony;
        struct LaunchPoint { public Unit owner; public GlobalPosition point; public float expiry; }
        static readonly List<LaunchPoint> launchPoints=new List<LaunchPoint>();
        internal static bool Is(Missile m,string key)=>m && m.GetWeaponInfo() && m.GetWeaponInfo().name==key;
        internal static readonly System.Reflection.FieldInfo SeekerMissile=AccessTools.Field(typeof(MissileSeeker),"missile");
        void Awake()
        {
            Radius=Config.Bind("Blackout","Radius",2000f,new ConfigDescription("Enemy electronics suppression radius (m).",new AcceptableValueRange<float>(1500,2500)));
            Duration=Config.Bind("Blackout","SuppressionSeconds",18f,new ConfigDescription("Recovery time after each pulse.",new AcceptableValueRange<float>(15,20)));
            RunDuration=Config.Bind("Blackout","RunSeconds",18f,new ConfigDescription("Horizontal HPM run duration.",new AcceptableValueRange<float>(15,20)));
            TriggerRange=Config.Bind("Blackout","ActivationRange",4000f,"Distance to the designated point (m).");
            PulseInterval=Config.Bind("Blackout","PulseInterval",2.5f,new ConfigDescription("Seconds between directed pulses.",new AcceptableValueRange<float>(2,3)));
            PulseCount=Config.Bind("Blackout","PulseCount",4,new ConfigDescription("Directed electronic suppression pulses per missile.",new AcceptableValueRange<int>(3,4)));
            MineLife=Config.Bind("Locust","Lifetime",210f,new ConfigDescription("Seconds after touchdown before native self detonation.",new AcceptableValueRange<float>(180,240)));
            harmony=new Harmony("ua.ncmod.circuitbreaker");
            Patch(typeof(Missile),"StartMissile",nameof(Started),false);
            Patch(typeof(MountedMissile),"Fire",nameof(Launch));
            harmony.Patch(AccessTools.Method(typeof(Spawner),"SpawnMissile",new[]{typeof(GameObject),typeof(Vector3),typeof(Quaternion),typeof(Vector3),typeof(Unit),typeof(Unit)}),postfix:new HarmonyMethod(typeof(Plugin),nameof(Spawned)));
            Patch(typeof(OpticalSeekerCruiseMissile),"Initialize",nameof(Initialized),false);
            Patch(typeof(OpticalSeekerCruiseMissile),"Seek",nameof(Seek));
            Patch(typeof(OpticalSeekerCruiseMissile),"SlowChecks",nameof(SlowChecks));
            Patch(typeof(Missile),"ServerFixedUpdate",nameof(ServerStep));
            Patch(typeof(Missile),"Steering",nameof(Unguided));
            Patch(typeof(Missile),"DetectCollisions",nameof(Collisions));
            Patch(typeof(TargetDetector),"DetectTarget",nameof(Detection));
            Patch(typeof(TargetDetector),"IsOperational",nameof(Operational),false);
            Patch(typeof(Radar),"IsJammed",nameof(Jammed),false);
            Patch(typeof(Radar),"TargetSearch",nameof(RadarSearch));
            Patch(typeof(Radar),"CanSeeRadarReturn",nameof(RadarReturn),false);
            Patch(typeof(Laser),"Fire",nameof(LaserFire));
            Patch(typeof(Laser),"FixedUpdate",nameof(LaserStep));
            Patch(typeof(Turret),"AssessTargetPriority",nameof(Assess));
            harmony.Patch(AccessTools.Method(typeof(Turret),"AimTurret",new[]{typeof(WeaponStation)}),postfix:new HarmonyMethod(typeof(Plugin),nameof(Aim)));
            Logger.LogInfo("Circuit Breaker: embedded Blueprinter pack, Blackout HPM and Locust 8 x 10 kg mines. Host authoritative simulation.");
        }
        void Patch(Type t,string method,string handler,bool prefix=true){var m=AccessTools.Method(t,method)??throw new MissingMethodException(t.Name,method);var h=new HarmonyMethod(typeof(Plugin),handler);harmony.Patch(m,prefix:prefix?h:null,postfix:prefix?null:h);}
        static void Launch(MountedMissile __instance,Unit owner,Unit target,GlobalPosition aimpoint){if(!owner||!owner.IsServer||target||!__instance.info||__instance.info.name!="WI_Blackout"||(bool)AccessTools.Field(typeof(MountedMissile),"fired").GetValue(__instance))return;launchPoints.RemoveAll(p=>!p.owner||p.expiry<Time.time);launchPoints.Add(new LaunchPoint{owner=owner,point=aimpoint,expiry=Time.time+10});}
        static void Spawned(Missile __result,Unit owner,Unit target){if(target||!Is(__result,"WI_Blackout"))return;int i=launchPoints.FindIndex(p=>p.owner==owner&&p.expiry>=Time.time);if(i<0)return;var f=__result.GetComponent<BlackoutFlight>()??__result.gameObject.AddComponent<BlackoutFlight>();f.Bind(__result);f.DesignateGPS(launchPoints[i].point);launchPoints.RemoveAt(i);}
        static void Started(Missile __instance)
        {
            if(Is(__instance,"WI_Blackout"))(__instance.GetComponent<BlackoutFlight>()??__instance.gameObject.AddComponent<BlackoutFlight>()).Bind(__instance);
            if(Is(__instance,"WI_Locust"))(__instance.GetComponent<LocustDispenser>()??__instance.gameObject.AddComponent<LocustDispenser>()).Bind(__instance);
            if(Is(__instance,"WI_LocustMine"))(__instance.GetComponent<LocustMine>()??__instance.gameObject.AddComponent<LocustMine>()).Bind(__instance);
        }
        static void Initialized(OpticalSeekerCruiseMissile __instance,Unit target,GlobalPosition aimpoint)
        {
            var m=SeekerMissile.GetValue(__instance) as Missile;if(!Is(m,"WI_Blackout"))return;
            var f=m.GetComponent<BlackoutFlight>()??m.gameObject.AddComponent<BlackoutFlight>();f.Bind(m);f.Designate(target,aimpoint);
        }
        static bool Seek(OpticalSeekerCruiseMissile __instance){var m=SeekerMissile.GetValue(__instance) as Missile;if(!Is(m,"WI_Blackout"))return true;m.GetComponent<BlackoutFlight>()?.Guide(__instance);return false;}
        static bool SlowChecks(OpticalSeekerCruiseMissile __instance){var m=SeekerMissile.GetValue(__instance) as Missile;return !Is(m,"WI_Blackout");}
        static bool ServerStep(Missile __instance)=>!Is(__instance,"WI_LocustMine") || !(__instance.GetComponent<LocustMine>()?.Grounded??false);
        static bool Unguided(Missile __instance)=>!Is(__instance,"WI_Locust")&&!Is(__instance,"WI_LocustMine");
        static bool Collisions(Missile __instance)=>!Is(__instance,"WI_LocustMine");
        static bool Detection(TargetDetector __instance)=>!Suppression.Active(__instance);
        static void Operational(TargetDetector __instance,ref bool __result){if(Suppression.Active(__instance))__result=false;}
        static void Jammed(Radar __instance,ref bool __result){if(Suppression.Active(__instance))__result=true;}
        static bool RadarSearch(Radar __instance)=>!Suppression.Active(__instance);
        static void RadarReturn(Radar __instance,ref bool __result){if(Suppression.Active(__instance))__result=false;}
        static bool LaserFire(Laser __instance)=>!Suppression.Active(__instance);
        static bool LaserStep(Laser __instance)=>!Suppression.Active(__instance);
        static bool Assess(Turret __instance)=>!Suppression.Active(__instance)||(__instance.GetWeaponStation()?.WeaponInfo?.gun??false);
        static void Aim(Turret __instance,ref bool __result){
            if(!Suppression.Active(__instance))return;
            if(!(__instance.GetWeaponStation()?.WeaponInfo?.gun??false)){__result=false;return;}
            // Gun CIWS still fires, but its stabilized barrel angle wanders. Native target/range checks remain.
            float phase=Time.time*7+__instance.GetInstanceID();__instance.transform.Rotate(0,Mathf.Sin(phase)*9,0,Space.Self);
            var elevation=AccessTools.Field(typeof(Turret),"elevationTransform").GetValue(__instance) as Transform;
            if(elevation)elevation.Rotate(Mathf.Cos(phase*.73f)*7,0,0,Space.Self);
        }
        void Update()=>Suppression.Cleanup();
        void OnDestroy(){harmony?.UnpatchSelf();Suppression.Clear();launchPoints.Clear();}
    }
}
