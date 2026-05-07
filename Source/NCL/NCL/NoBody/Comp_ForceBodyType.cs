using Verse;

namespace NoBody
{
    public class Comp_ForceBodyType : ThingComp
    {
        public bool enableNoBody;

        public CompProperties_ForceBodyType Props => (CompProperties_ForceBodyType)props;

        public override void Notify_Equipped(Pawn pawn)
        {
            base.Notify_Equipped(pawn);
            if (pawn == null || pawn.story == null || pawn.def.defName != "Human")
                return;
            enableNoBody = true;
        }

        public override void Notify_Unequipped(Pawn pawn)
        {
            base.Notify_Unequipped(pawn);
            enableNoBody = false;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref enableNoBody, "enableNoBody", false);
        }
    }
}
