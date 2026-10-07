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
        public static void Renew(int id,float now){if(targets.ContainsKey(id))targets[id]=now+5;}
        public static void Cleanup(float now){var expired=new List<int>();foreach(var entry in targets)if(now>=entry.Value)expired.Add(entry.Key);foreach(var id in expired)targets.Remove(id);}
        public static void Clear()=>targets.Clear();
    }
    internal static class SmartMineRules
    {
        public const float Radius=70, Lifetime=300, ScanInterval=.75f, LaunchHeight=80, TerminalSpeed=18;
        public const float JumpGravity=9.81f;
        public static float JumpApexTime=>(float)System.Math.Sqrt(2*LaunchHeight/JumpGravity);
        public static float JumpHeight(float time){float t=System.Math.Max(0,System.Math.Min(time,JumpApexTime));return JumpGravity*JumpApexTime*t-.5f*JumpGravity*t*t;}
        public static bool Eligible(bool groundVehicle,bool disabled,bool knownFactions,bool friendly,float distanceSquared)=>groundVehicle&&!disabled&&knownFactions&&!friendly&&distanceSquared<=Radius*Radius;
    }
}
