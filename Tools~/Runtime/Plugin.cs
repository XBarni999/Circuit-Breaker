using System;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;

namespace CircuitBreaker
{
    [BepInPlugin("ua.ncmod.circuitbreaker", "Circuit Breaker", "0.1.21")]
    [BepInDependency("com.nikkorap.blueprinter", "2.0.1")]
    public sealed class Plugin : BaseUnityPlugin
    {
        internal static ConfigEntry<float> Radius, Duration, TriggerRange, EmissionDuration, MineLife, DescentAngle, DescentRamp;
        Harmony harmony;
        internal static Action<string> Diagnostic;
        struct LaunchPoint { public Unit owner,target; public bool gps; public GlobalPosition point; public float expiry; }
        static readonly List<LaunchPoint> launchPoints=new List<LaunchPoint>();
        static float nextTargetNotice;
        internal static bool InMission=>GameManager.gameState==GameState.SinglePlayer||GameManager.gameState==GameState.Multiplayer;
        internal static bool Is(Missile m,string key)=>m && m.GetWeaponInfo() && m.GetWeaponInfo().name==key;
        internal static readonly System.Reflection.FieldInfo SeekerMissile=AccessTools.Field(typeof(MissileSeeker),"missile");
        void Awake()
        {
            Diagnostic=text=>Logger.LogInfo(text);
            Radius=Config.Bind("Blackout","Radius",10000f,new ConfigDescription("HPM radius (m): enemy ground electronics and aircraft of every faction.",new AcceptableValueRange<float>(1500,10000)));
            Duration=Config.Bind("Blackout","SuppressionSeconds",4f,new ConfigDescription("Recovery delay after the last HPM exposure (seconds).",new AcceptableValueList<float>(4f)));
            TriggerRange=Config.Bind("Blackout","ActivationRange",10000f,"Distance to the original selected ground radar / SAM that starts emission (m).");
            EmissionDuration=Config.Bind("Blackout","EmissionSeconds",20f,new ConfigDescription("Continuous emission duration after activation.",new AcceptableValueRange<float>(15,20)));
            MineLife=Config.Bind("Locust","Lifetime",210f,new ConfigDescription("Contact mine lifetime after touchdown (seconds).",new AcceptableValueRange<float>(180,240)));
            DescentAngle=Config.Bind("Blackout","MaximumDescentAngle",20f,new ConfigDescription("Maximum approach descent angle in degrees; terrain avoidance can still command a climb.",new AcceptableValueRange<float>(3,25)));
            DescentRamp=Config.Bind("Blackout","DescentRampSeconds",5f,new ConfigDescription("Time to gradually enter the approach descent after launch.",new AcceptableValueRange<float>(3,25)));
            var revision=Config.Bind("General","TuningRevision",0,"Default tuning migration revision.");
            if(revision.Value<2){if(Radius.Value==2000)Radius.Value=10000;if(TriggerRange.Value==4000)TriggerRange.Value=10000;if(DescentAngle.Value==8)DescentAngle.Value=20;if(DescentRamp.Value==12)DescentRamp.Value=5;revision.Value=2;Config.Save();}
            if(revision.Value<3){Duration.Value=4;revision.Value=3;Config.Save();}
            if(revision.Value<5){MineLife.Value=210;revision.Value=5;Config.Save();}
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
            Patch(typeof(MissileSeeker),"GetSeekerType",nameof(PassiveType),false);
            Patch(typeof(MissileSeeker),"GetMinSpeed",nameof(PassiveMinimum),false);
            Patch(typeof(TargetDetector),"DetectTarget",nameof(Detection));
            Patch(typeof(TargetDetector),"IsOperational",nameof(Operational),false);
            Patch(typeof(Radar),"IsJammed",nameof(Jammed),false);
            Patch(typeof(Unit),"UserCode_RpcJam_569024588",nameof(JamReceived));
            Patch(typeof(Radar),"TargetSearch",nameof(RadarSearch));
            Patch(typeof(Radar),"CanSeeRadarReturn",nameof(RadarReturn),false);
            Patch(typeof(Laser),"Fire",nameof(LaserFire));
            Patch(typeof(Laser),"FixedUpdate",nameof(LaserStep));
            Patch(typeof(Turret),"AssessTargetPriority",nameof(Assess));
            Patch(typeof(Turret),"FixedUpdate",nameof(TurretStep));
            Patch(typeof(WeaponStation),"Fire",nameof(StationFire));
            Patch(typeof(WeaponStation),"LaunchMount",nameof(MountAccess));
            Patch(typeof(WeaponManager),"AddTargetList",nameof(TargetAccess));
            Patch(typeof(CombatAI),"AnalyzeTarget",nameof(AnalyzeAccess));
            Patch(typeof(FactionHQ),"GetTargetsWithinCone",nameof(ConeAccess),false);
            Patch(typeof(FactionHQ),"GetTargetsWithinRange",nameof(RangeAccess),false);
            Patch(typeof(HUDUnitMarker),"UpdatePosition",nameof(HudContact),false);
            Patch(typeof(HUDUnitMarker),"UpdateVisibility",nameof(HudContact),false);
            Patch(typeof(UnitMapIcon),"UpdateIcon",nameof(MapContact),false);
            Patch(typeof(UnitMapIcon),"ClickIcon",nameof(MapAccess));
            Patch(typeof(CombatHUD),"SelectUnit",nameof(HudAccess));
            Patch(typeof(TargetListSelector),"CheckExclusions",nameof(ListAccess),false);
            harmony.Patch(AccessTools.Method(typeof(Turret),"AimTurret",new[]{typeof(WeaponStation)}),postfix:new HarmonyMethod(typeof(Plugin),nameof(Aim)));
            Logger.LogInfo("Circuit Breaker 0.1.21: immutable launch-target activation; aircraft radar and datalink suppression including allies. Host authoritative simulation.");
        }
        void Patch(Type t,string method,string handler,bool prefix=true){var m=AccessTools.Method(t,method)??throw new MissingMethodException(t.Name,method);var h=new HarmonyMethod(typeof(Plugin),handler);harmony.Patch(m,prefix:prefix?h:null,postfix:prefix?null:h);}
        static void Launch(MountedMissile __instance,Unit owner,Unit target,GlobalPosition aimpoint){if(!owner||!owner.IsServer||!__instance.info||__instance.info.name!="WI_Blackout"||(bool)AccessTools.Field(typeof(MountedMissile),"fired").GetValue(__instance))return;launchPoints.RemoveAll(p=>!p.owner||p.expiry<Time.time);launchPoints.Add(new LaunchPoint{owner=owner,target=target,gps=!target,point=target?target.GlobalPosition():aimpoint,expiry=Time.time+10});}
        static void Spawned(Missile __result,Unit owner,Unit target){if(!InMission)return;if(Is(__result,"WI_Locust")||Is(__result,"WI_LawnChair")||Is(__result,"WI_LocustMine")||Is(__result,"WI_ZhdanMine"))Started(__result);if(!Is(__result,"WI_Blackout"))return;int i=launchPoints.FindIndex(p=>p.owner==owner&&p.expiry>=Time.time);if(i<0)return;var f=__result.GetComponent<BlackoutFlight>()??__result.gameObject.AddComponent<BlackoutFlight>();f.Bind(__result);f.CaptureLaunch(launchPoints[i].target,launchPoints[i].point,launchPoints[i].gps);launchPoints.RemoveAt(i);}
        static void Started(Missile __instance)
        {
            if(!InMission)return;
            if(Is(__instance,"WI_Blackout"))(__instance.GetComponent<BlackoutFlight>()??__instance.gameObject.AddComponent<BlackoutFlight>()).Bind(__instance);
            if(Is(__instance,"WI_Locust")||Is(__instance,"WI_LawnChair"))(__instance.GetComponent<LocustDispenser>()??__instance.gameObject.AddComponent<LocustDispenser>()).Bind(__instance);
            if(Is(__instance,"WI_LocustMine"))(__instance.GetComponent<LocustMine>()??__instance.gameObject.AddComponent<LocustMine>()).Bind(__instance);
            if(Is(__instance,"WI_ZhdanMine"))(__instance.GetComponent<SmartMineController>()??__instance.gameObject.AddComponent<SmartMineController>()).Bind(__instance);
        }
        static bool Dispenser(Missile m)=>Is(m,"WI_Locust")||Is(m,"WI_LawnChair");
        static bool Mine(Missile m)=>Is(m,"WI_LocustMine")||Is(m,"WI_ZhdanMine");
        static void Initialized(OpticalSeekerCruiseMissile __instance,Unit target,GlobalPosition aimpoint)
        {
            var m=SeekerMissile.GetValue(__instance) as Missile;if(!Is(m,"WI_Blackout"))return;
            var f=m.GetComponent<BlackoutFlight>()??m.gameObject.AddComponent<BlackoutFlight>();f.Bind(m);f.Designate(target,aimpoint);
        }
        static bool Seek(OpticalSeekerCruiseMissile __instance){var m=SeekerMissile.GetValue(__instance) as Missile;if(!Is(m,"WI_Blackout"))return true;m.GetComponent<BlackoutFlight>()?.Guide(__instance);return false;}
        static bool SlowChecks(OpticalSeekerCruiseMissile __instance){var m=SeekerMissile.GetValue(__instance) as Missile;return !Is(m,"WI_Blackout");}
        static bool ServerStep(Missile __instance)=>!Is(__instance,"WI_ZhdanMine")&&(!Is(__instance,"WI_LocustMine")||!(__instance.GetComponent<LocustMine>()?.Grounded??false));
        static bool Unguided(Missile __instance){
            if(Mine(__instance))return false;
            if(Dispenser(__instance)&&__instance.rb&&__instance.rb.velocity.sqrMagnitude>1){
                // Passive tail stabilization: align the nose with air velocity, retaining gravity/CCIP.
                __instance.SetAimpoint(__instance.GlobalPosition()+__instance.rb.velocity.normalized*1000,Vector3.zero);
            }
            return true;
        }
        static void PassiveType(MissileSeeker __instance,ref string __result){var m=SeekerMissile.GetValue(__instance) as Missile;if(Dispenser(m))__result="Unguided / CCIP";else if(Is(m,"WI_LocustMine"))__result="Contact mine";else if(Is(m,"WI_ZhdanMine"))__result="Smart mine / GS25";}
        static void PassiveMinimum(MissileSeeker __instance,ref float __result){var m=SeekerMissile.GetValue(__instance) as Missile;if(Dispenser(m)||Mine(m))__result=0;}
        static bool Collisions(Missile __instance){if(Dispenser(__instance)){__instance.GetComponent<LocustDispenser>()?.CheckImpact();return false;}if(Mine(__instance))return false;if(Is(__instance,"WI_Blackout")){var flight=__instance.GetComponent<BlackoutFlight>();if(!flight)return true;flight.CheckImpact();return false;}return true;}
        static bool Detection(TargetDetector __instance)=>!Suppression.Active(__instance);
        static void Operational(TargetDetector __instance,ref bool __result){if(Suppression.Active(__instance))__result=false;}
        static void Jammed(Radar __instance,ref bool __result){if(Suppression.Active(__instance))__result=true;}
        static void JamReceived(Unit __instance,Unit.JamEventArgs args){if(args.jammingUnit is Missile source&&Is(source,"WI_Blackout"))Suppression.Apply(__instance);}
        static bool RadarSearch(Radar __instance)=>!Suppression.Active(__instance);
        static void RadarReturn(Radar __instance,ref bool __result){if(Suppression.Active(__instance))__result=false;}
        static bool LaserFire(Laser __instance)=>!Suppression.Active(__instance);
        static bool LaserStep(Laser __instance)=>!Suppression.Active(__instance);
        static bool Assess(Turret __instance)=>!(Suppression.Active(__instance)||Suppression.ActiveUnit(__instance.GetAttachedUnit()))||(__instance.GetWeaponStation()?.WeaponInfo?.gun??false);
        static bool TurretStep(Turret __instance){if(!(Suppression.Active(__instance)||Suppression.ActiveUnit(__instance.GetAttachedUnit()))||(__instance.GetWeaponStation()?.WeaponInfo?.gun??false))return true;AccessTools.Field(typeof(Turret),"target").SetValue(__instance,null);AccessTools.Field(typeof(Turret),"timeOnTarget").SetValue(__instance,0f);return false;}
        static bool BlackoutTargetAllowed(WeaponStation station,Unit owner,Unit target){
            if(!station.WeaponInfo||station.WeaponInfo.name!="WI_Blackout")return true;
            if(target&&Suppression.IsActivator(target)&&target.NetworkHQ&&target.NetworkHQ!=owner.NetworkHQ)return true;
            if(owner==Datalink.LocalAircraft&&Time.time>=nextTargetNotice){nextTargetNotice=Time.time+3;var report=SceneSingleton<AircraftActionsReport>.i;if(report)report.ReportText("Blackout: select an enemy ground radar / SAM.",3);}
            return false;
        }
        static bool StationFire(WeaponStation __instance,Unit owner,Unit target){if(!BlackoutTargetAllowed(__instance,owner,target))return false;if(owner is Aircraft aircraft&&!(__instance.WeaponInfo?.gun??false))return Datalink.CanLaunch(aircraft,target,__instance.WeaponInfo);return !Suppression.ActiveUnit(owner)||(__instance.WeaponInfo?.gun??false);}
        static bool MountAccess(WeaponStation __instance,Unit owner,Unit target)=>BlackoutTargetAllowed(__instance,owner,target)&&(!(owner is Aircraft aircraft)||Datalink.CanLaunch(aircraft,target,__instance.WeaponInfo));
        static bool TargetAccess(WeaponManager __instance,Unit target)=>Datalink.CanContact(__instance.GetComponentInParent<Aircraft>(),target);
        static bool AnalyzeAccess(WeaponStation weaponStation,Unit analyzer,TrackingInfo trackingInfo,ref OpportunityThreat __result){if(trackingInfo!=null&&trackingInfo.TryGetUnit(out var target)){
            bool wrongBlackoutTarget=weaponStation.WeaponInfo&&weaponStation.WeaponInfo.name=="WI_Blackout"&&!Suppression.IsActivator(target);
            if(wrongBlackoutTarget||(analyzer is Aircraft aircraft&&!Datalink.CanLaunch(aircraft,target,weaponStation.WeaponInfo))){__result=default;return false;}
        }return true;}
        static void ConeAccess(Transform fromTransform,List<Unit> __result){var aircraft=fromTransform?fromTransform.GetComponentInParent<Aircraft>():null;if(Suppression.ActiveAircraft(aircraft))__result.RemoveAll(target=>!Datalink.CanContact(aircraft,target));}
        static void RangeAccess(Transform fromTransform,List<TrackingInfo> __result){var aircraft=fromTransform?fromTransform.GetComponentInParent<Aircraft>():null;if(Suppression.ActiveAircraft(aircraft))__result.RemoveAll(track=>!track.TryGetUnit(out var target)||!Datalink.CanContact(aircraft,target));}
        static void HudContact(HUDUnitMarker __instance)=>Datalink.Show(__instance.image,!Datalink.CanContact(Datalink.LocalAircraft,__instance.unit));
        static void MapContact(UnitMapIcon __instance)=>Datalink.Show(__instance.iconImage,!Datalink.CanContact(Datalink.LocalAircraft,__instance.unit));
        static bool MapAccess(UnitMapIcon __instance)=>Datalink.CanContact(Datalink.LocalAircraft,__instance.unit);
        static bool HudAccess(Unit unit)=>Datalink.CanContact(Datalink.LocalAircraft,unit);
        static void ListAccess(Unit u,ref bool __result){if(!Datalink.CanContact(Datalink.LocalAircraft,u))__result=true;}
        static void Aim(Turret __instance,ref bool __result){
            if(!Suppression.Active(__instance))return;
            if(!(__instance.GetWeaponStation()?.WeaponInfo?.gun??false)){__result=false;return;}
            // Gun CIWS still fires, but its stabilized barrel angle wanders. Native target/range checks remain.
            float phase=Time.time*7+__instance.GetInstanceID();__instance.transform.Rotate(0,Mathf.Sin(phase)*9,0,Space.Self);
            var elevation=AccessTools.Field(typeof(Turret),"elevationTransform").GetValue(__instance) as Transform;
            if(elevation)elevation.Rotate(Mathf.Cos(phase*.73f)*7,0,0,Space.Self);
        }
        void Update(){Suppression.Cleanup();Datalink.Cleanup();}
        void OnDestroy(){harmony?.UnpatchSelf();Suppression.Clear();Datalink.Clear();TargetReservations.Clear();launchPoints.Clear();}
    }
}
