using System;
namespace CircuitBreaker
{
    internal static class LocustDeploymentRules
    {
        // Deployment deliberately has no target/lock parameter: CCIP drops are ballistic.
        public static bool ShouldOpen(float age,float verticalSpeed,float height,float doorDelay){
            float lead=doorDelay+3*.08f;
            float openingHeight=325+Math.Max(0,-verticalSpeed)*lead+4.905f*lead*lead;
            return age>=.3f&&verticalSpeed<=0&&height<=openingHeight;
        }
    }
}
