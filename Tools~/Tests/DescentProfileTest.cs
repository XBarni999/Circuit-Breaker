using System;
using CircuitBreaker;
class DescentProfileTest
{
    static void Check(bool condition,string label){if(!condition)throw new Exception(label);}
    static void Main(){
        Check(DescentProfile.LimitHeight(4000,30,1800,0,20,5)==4000,"High launch begins level");
        Check(DescentProfile.LimitHeight(4000,4200,1800,12,20,5)==4200,"Terrain climb retains priority");
        Check(DescentProfile.LimitHeight(50,30,1800,12,20,5)==30,"Cruise floor is retained");
        double height=4000,maxSlope=0;const double dt=.02,speed=300,span=1800;
        for(int frame=0;frame<3000;frame++){
            double time=frame*dt;float waypoint=DescentProfile.LimitHeight((float)height,30,(float)span,(float)time,20,5);
            double slope=(height-waypoint)/span;maxSlope=Math.Max(maxSlope,slope);height-=slope*speed*dt;
            if(frame==50)Check(height>3995,"First second cannot produce a dive");
            if(frame==500)Check(height>3000,"Descent ramps gradually over the first five seconds");
        }
        Check(maxSlope<=Math.Tan(20*Math.PI/180)+.0001,"Twenty degree approach ceiling");
        Check(height<250&&height>=30,"Four kilometre launch requires a sustained descent, not a plunge");
        float pitch=0;double previous=0;
        for(int frame=0;frame<1000;frame++){
            float slope=frame<500?1000:-1000;previous=pitch;
            pitch=DescentProfile.ApproachPitch(pitch,slope,20,.02f);
            Check(pitch>=-20&&pitch<=25,"Obstacle clearance cannot command a vertical or inverted flight path");
            Check(Math.Abs(pitch-previous)<=.10001,"Abrupt obstacle changes retain the 5 degree/s pitch ramp");
        }
        Check(DescentProfile.ApproachPitch(0,1000,20,30)<=.50001,"Long frame gap cannot jump to a steep climb");
        Check(DescentProfile.ApproachPitch(10,-1000,20,.02f)>9.93,"Leaving an obstacle returns gently to cruise");
        Console.WriteLine("OBSTACLE_PITCH_PASSED: bounded 25 degree climb, 20 degree descent, 5/3 degree/s ramps, no frame-gap jump.");
        Console.WriteLine("DESCENT_PROFILE_PASSED: 4 km launch, 20 degree ceiling, gradual 5 s entry, terrain climb priority.");
        Console.WriteLine("Idealized 300 m/s profile after 60 s: {0:F0} m. This is a numerical profile check, not a live flight test.",height);
    }
}
