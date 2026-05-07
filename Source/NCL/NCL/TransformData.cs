using Verse;

namespace NCL
{
    public class TransformData
    {
        public int transformCooldown;
        public ThingDef thingDef;
        public string label;
        public string description;
        public int revertAfterTicks;
        public SoundDef soundOnTransform;
        public ThingDef moteOnTransform;
        public ThingDef needApparel;
        public string iconPath = "";
        public float iconSize = 1f;
    }
}
