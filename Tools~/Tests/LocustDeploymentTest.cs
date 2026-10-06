using System;
using CircuitBreaker;
class LocustDeploymentTest
{
    static void Check(bool value,string name){if(!value)throw new Exception(name);}
    static void Main(){
        Check(!LocustDeploymentRules.ShouldOpen(.1f,-50,325,.82f),"Do not open beside the launching aircraft");
        Check(!LocustDeploymentRules.ShouldOpen(5,20,325,.82f),"Do not open during the upward release phase");
        Check(!LocustDeploymentRules.ShouldOpen(5,-50,1000,.82f),"Wait until terrain-relative release height");
        Check(LocustDeploymentRules.ShouldOpen(5,-50,350,.82f),"A targetless CCIP drop opens at release height");
        Check(LocustDeploymentRules.ShouldOpen(5,-200,500,.82f),"A fast drop starts doors early enough");
        Check(LocustDeploymentRules.ShouldOpen(5,-50,325,0),"An animationless fallback still deploys");
        Console.WriteLine("LOCUST_DEPLOYMENT_PASSED: target-independent timing, launch clearance, descending-only gate and fast-drop door lead.");
    }
}
