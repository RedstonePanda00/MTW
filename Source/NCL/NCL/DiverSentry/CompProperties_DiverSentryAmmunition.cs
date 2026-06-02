using Verse;

namespace NCL
{
    public class CompProperties_DiverSentryAmmunition : CompProperties
    {
        public int ammoCapacity = 100;

        [MustTranslate]
        public string ammoLabel = "ammunition";

        [MustTranslate]
        public string ammoGizmoLabel = "ammunition";

        [MustTranslate]
        public string outOfAmmoMessage = "out of ammunition";

        public bool showAmmoGizmo = true;

        public float depletionEffectRadius = 3.9f;

        public CompProperties_DiverSentryAmmunition()
        {
            compClass = typeof(Comp_DiverSentryAmmunition);
        }
    }
}
