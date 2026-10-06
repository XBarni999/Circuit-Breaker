using System;
namespace CircuitBreaker
{
    internal static class DescentProfile
    {
        public static float ApproachPitch(float pitch,float slope,float maximumDescent,float dt)
        {
            double target=Math.Max(-maximumDescent,Math.Min(25,Math.Atan(slope)*180/Math.PI));
            double step=(target>pitch?5:3)*Math.Max(0,Math.Min(.1,dt));
            return (float)(pitch+Math.Max(-step,Math.Min(step,target-pitch)));
        }
        // Limit descent only. A higher terrain waypoint always retains priority.
        public static float LimitHeight(float height,float terrainHeight,float horizontalDistance,float elapsed,float degrees,float rampSeconds)
        {
            double t=Math.Max(0,Math.Min(1,elapsed/Math.Max(1,rampSeconds)));
            double ease=t*t*(3-2*t);
            double drop=Math.Max(0,horizontalDistance)*Math.Tan(Math.Max(0,degrees)*ease*Math.PI/180);
            return Math.Max(terrainHeight,height-(float)drop);
        }
    }
}
