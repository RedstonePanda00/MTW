using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace NCL
{

    public class CompProperties_AbilityShowRange : CompProperties_AbilityEffect
    {

        public CompProperties_AbilityShowRange()
        {
            this.compClass = typeof(CompAbilityShowRange);
        }

        public float range = 0f;


        public float minRange = 0f;
    }
}



namespace NCL
{

    public class CompAbilityShowRange : CompAbilityEffect
    {

        public new CompProperties_AbilityShowRange Props
        {
            get
            {
                return (CompProperties_AbilityShowRange)this.props;
            }
        }


        public override bool AICanTargetNow(LocalTargetInfo target)
        {
            return true;
        }


        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            List<IntVec3> list = new List<IntVec3>();
            foreach (IntVec3 intVec in GenRadial.RadialCellsAround(target.Cell, this.Props.range, true))
            {
                bool flag = intVec.IsValid || intVec.InBounds(this.parent.pawn.Map);
                if (flag)
                {
                    list.Add(intVec);
                }
            }
            bool flag2 = this.Props.minRange != 0f;
            if (flag2)
            {
                foreach (IntVec3 intVec2 in GenRadial.RadialCellsAround(target.Cell, this.Props.minRange, true))
                {
                    bool flag3 = intVec2.IsValid || intVec2.InBounds(this.parent.pawn.Map);
                    if (flag3)
                    {
                        list.Add(intVec2);
                    }
                }
            }
            GenDraw.DrawFieldEdges(list);
        }
    }
}
