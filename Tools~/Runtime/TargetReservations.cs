using System.Collections.Generic;
namespace CircuitBreaker
{
    internal static class TargetReservations
    {
        static readonly Dictionary<int,float> targets=new Dictionary<int,float>();
        public static bool TryReserve(int id,float now){
            if(targets.TryGetValue(id,out var expiry)&&now<expiry)return false;
            targets[id]=now+5;return true;
        }
        public static void Release(int id)=>targets.Remove(id);
        public static void Cleanup(float now){var expired=new List<int>();foreach(var entry in targets)if(now>=entry.Value)expired.Add(entry.Key);foreach(var id in expired)targets.Remove(id);}
        public static void Clear()=>targets.Clear();
    }
    internal static class SmartMineRules
    {
        public const float Radius=70, Lifetime=300, ScanInterval=.75f, LaunchHeight=80, TerminalSpeed=18;
        public static bool Eligible(bool groundVehicle,bool disabled,bool knownFactions,bool friendly,float distanceSquared)=>groundVehicle&&!disabled&&knownFactions&&!friendly&&distanceSquared<=Radius*Radius;
    }
}
