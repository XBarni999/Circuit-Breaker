namespace CircuitBreaker
{
    internal static class HpmRules
    {
        public static bool SuppressionActive(float now,float expiry){return now<expiry;}
        public static bool InActivationRange(bool launchTargetEligible,float launchTargetDistance,float range){return launchTargetEligible&&launchTargetDistance<=range;}
        public static bool Activates(bool aircraft,bool ship,bool missile,bool radar,bool airDefenseMissiles){return !aircraft&&!ship&&!missile&&(radar||airDefenseMissiles);}
        public static bool ExposedTarget(bool aircraft,bool missile,bool friendly){return !missile&&(aircraft||!friendly);}
    }
}
