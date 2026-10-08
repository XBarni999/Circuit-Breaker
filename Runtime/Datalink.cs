using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace CircuitBreaker
{
    internal static class Datalink
    {
        static readonly Dictionary<Behaviour,bool> hidden=new Dictionary<Behaviour,bool>();
        static float nextCleanup;
        public static Aircraft LocalAircraft=>SceneSingleton<CombatHUD>.i?SceneSingleton<CombatHUD>.i.aircraft:null;
        public static bool CanLaunch(Aircraft receiver,Unit target,WeaponInfo info){
            var prefab=info?info.weaponPrefab:null;
            bool radarGuided=prefab&&(prefab.GetComponent<ARHSeeker>()||prefab.GetComponent<SARHSeeker>());
            return !radarGuided||CanContact(receiver,target);
        }
        public static bool CanContact(Aircraft receiver,Unit target){
            if(!Suppression.ActiveAircraft(receiver)||!target||target==receiver)return true;
            // Local optical observations remain usable; shared faction tracks are unavailable.
            foreach(var detector in receiver.GetComponentsInChildren<TargetDetector>(true))if(!(detector is Radar)&&detector!=receiver.radar&&detector.detectedTargets.Contains(target))return true;
            return false;
        }
        public static void Show(Behaviour image,bool blocked){
            if(!image)return;
            if(blocked){if(!hidden.ContainsKey(image))hidden[image]=image.enabled;image.enabled=false;}
            else if(hidden.TryGetValue(image,out var enabled)){image.enabled=enabled;hidden.Remove(image);}
        }
        public static void Clear(){foreach(var pair in hidden)if(pair.Key)pair.Key.enabled=pair.Value;hidden.Clear();}
        public static void Cleanup(){if(Time.time<nextCleanup)return;nextCleanup=Time.time+.5f;if(!Suppression.ActiveAircraft(LocalAircraft)){Clear();return;}foreach(var image in hidden.Keys.Where(image=>!image).ToArray())hidden.Remove(image);}
    }
}
