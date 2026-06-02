using Verse;

namespace NCL
{
    public class CompProperties_FatCatMissileLoft : CompProperties
    {
        public int climbTicks = 30;
        public float climbAngleDegrees = 45f;
        public float climbSpeed = 0.45f;
        public float diveAcceleration = 0.035f;
        public float diveMaxSpeed = 1.35f;
        public int predictTicks = 90;
        public float hitRadius = 1.5f;
        public int trailIntervalTicks = 1;

        public CompProperties_FatCatMissileLoft()
        {
            compClass = typeof(CompFatCatMissileLoft);
        }
    }

    public class CompFatCatMissileLoft : ThingComp
    {
        public CompProperties_FatCatMissileLoft Props => (CompProperties_FatCatMissileLoft)props;
    }
}
