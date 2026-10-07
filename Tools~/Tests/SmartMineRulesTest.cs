using System;
using CircuitBreaker;
class SmartMineRulesTest
{
    static void Check(bool value,string name){if(!value)throw new Exception(name);}
    static void Main(){
        TargetReservations.Clear();
        Check(TargetReservations.TryReserve(1,0),"First mine claims target");
        Check(!TargetReservations.TryReserve(1,0),"Simultaneous second mine cannot claim target");
        Check(TargetReservations.TryReserve(2,0),"Different target remains available");
        Check(!TargetReservations.TryReserve(1,4.99f),"Reservation remains for full five seconds");
        Check(TargetReservations.TryReserve(1,5),"Reservation expires at five seconds");
        TargetReservations.Release(1);Check(TargetReservations.TryReserve(1,5),"Failed spawn releases target");
        TargetReservations.Renew(1,9);Check(!TargetReservations.TryReserve(1,13.99f),"Jump renews the reservation through the strike");
        Check(TargetReservations.TryReserve(1,14),"Renewed reservation expires five seconds after the hop");
        TargetReservations.Clear();
        TargetReservations.Cleanup(10);Check(TargetReservations.TryReserve(1,10),"Expired entries can be claimed after cleanup");
        Check(SmartMineRules.Eligible(true,false,true,false,4900),"Enemy ground vehicle at boundary");
        Check(!SmartMineRules.Eligible(true,false,true,false,4901),"Outside 70 m excluded");
        Check(!SmartMineRules.Eligible(false,false,true,false,10),"Aircraft and buildings excluded");
        Check(!SmartMineRules.Eligible(true,true,true,false,10),"Disabled vehicle excluded");
        Check(!SmartMineRules.Eligible(true,false,true,true,10),"Friendly excluded");
        Check(!SmartMineRules.Eligible(true,false,false,false,10),"Unknown faction excluded");
        Check(SmartMineRules.JumpHeight(0)==0,"Jump starts at the mine, without teleportation");
        Check(Math.Abs(SmartMineRules.JumpHeight(SmartMineRules.JumpApexTime)-80)<.001f,"Ballistic jump reaches 80 m");
        float previous=0;
        for(float time=.02f;time<=SmartMineRules.JumpApexTime;time+=.02f){float height=SmartMineRules.JumpHeight(time);Check(height>=previous&&height-previous<1,"Continuous upward steps below one metre");previous=height;}
        Check(SmartMineRules.JumpHeight(100)<=80.001f,"Jump clamps at its apex");
        TargetReservations.Clear();
        Console.WriteLine("SMART_MINE_RULES_PASSED: IFF, vehicle filtering, range boundary, simultaneous claims, expiry and spawn rollback.");
    }
}
