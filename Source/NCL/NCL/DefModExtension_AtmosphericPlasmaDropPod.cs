using Verse;

namespace NCL
{
    // Optional: sit at landed DrawPos for this many ticks before Skyfaller.Impact (despawn, inner cargo, spawnThing).
    public class DefModExtension_AtmosphericPlasmaDropPod : DefModExtension
    {
        public int ticksPauseOnGround = 90;
        public float touchdownCameraShake = 0.22f;
        public string cruiseMoteDefName = "PM_Mote_Flame";
        public float cruiseMoteZOffset = 0f;
        public FloatRange cruiseMoteProgressRange = new FloatRange(0.2f, 0.9f);
    }
}
