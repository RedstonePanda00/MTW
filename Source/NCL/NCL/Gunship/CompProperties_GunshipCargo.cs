using Verse;

namespace NCL
{
    public class CompProperties_GunshipCargo : CompProperties
    {
        public int maxSlots = 8;
        // 0 means unlimited nesting. Cycles are always rejected regardless of this value.
        public int maxNestDepth = 0;
        // Gunships load and unload in flight; the flag stays for carriers that should not.
        public bool requireGroundedToLoad = false;
        // Safety net only. Where an AI carrier unloads is decided by its think tree; 0 disables.
        public int autoUnloadAfterTicks = 20000;

        public CompProperties_GunshipCargo()
        {
            compClass = typeof(CompGunshipCargo);
        }
    }
}
