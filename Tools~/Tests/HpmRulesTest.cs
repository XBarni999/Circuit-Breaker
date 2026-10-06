using System;
using CircuitBreaker;
class HpmRulesTest
{
    static void Check(bool value,string name){if(!value)throw new Exception(name);}
    static void Main(){
        Check(!HpmRules.InActivationRange(true,10001,10000),"A launch-selected target outside 10 km must not activate HPM, even if other radars are nearby");
        Check(HpmRules.InActivationRange(true,10000,10000),"The captured launch target activates HPM at 10 km");
        Check(!HpmRules.InActivationRange(false,500,10000),"An invalid or destroyed launch target cannot activate HPM");
        Check(!HpmRules.Activates(false,false,false,false,false),"Ordinary tank must not expend the charge");
        Check(HpmRules.Activates(false,false,false,true,false),"Standalone ground radar activates emission");
        Check(HpmRules.Activates(false,false,false,false,true),"IR SAM without radar activates emission");
        Check(HpmRules.Activates(false,false,false,true,true),"Radar SAM activates emission");
        Check(!HpmRules.Activates(true,false,false,true,true),"An aircraft must not be a ground activator");
        Check(!HpmRules.Activates(false,true,false,true,true),"A ship must not be a ground activator");
        Check(HpmRules.ExposedTarget(true,false,true),"Friendly aircraft are affected");
        Check(HpmRules.ExposedTarget(true,false,false),"Hostile aircraft are affected");
        Check(!HpmRules.ExposedTarget(false,false,true),"Friendly ground electronics remain excluded");
        Check(!HpmRules.ExposedTarget(false,true,false),"Airborne missiles remain excluded");
        Console.WriteLine("HPM_RULES_PASSED: ground radar/SAM activation, ordinary tank exclusion, aircraft friendly fire, interceptor exclusion.");
    }
}
