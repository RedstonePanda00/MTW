using RimWorld;
using Verse;

namespace NCL
{
    public class CompProperties_AssaultTrooper : CompProperties
    {
        public AbilityDef c4Ability;

        public CompProperties_AssaultTrooper()
        {
            compClass = typeof(CompAssaultTrooper);
        }
    }

    // Per-pawn state shared by the assault trooper think nodes.
    public class CompAssaultTrooper : ThingComp
    {
        // Set when a burst is ordered and cleared once the pawn has backed off, so the kite node
        // alternates between firing and opening range instead of walking away before the first shot.
        private bool firedSinceReposition;

        public CompProperties_AssaultTrooper Props => (CompProperties_AssaultTrooper)props;

        public bool FiredSinceReposition => firedSinceReposition;

        public Ability C4Ability
        {
            get
            {
                Pawn pawn = parent as Pawn;
                if (pawn?.abilities == null || Props.c4Ability == null)
                {
                    return null;
                }

                return pawn.abilities.GetAbility(Props.c4Ability);
            }
        }

        public void Notify_BurstOrdered()
        {
            firedSinceReposition = true;
        }

        public void Notify_Repositioned()
        {
            firedSinceReposition = false;
        }
    }
}
