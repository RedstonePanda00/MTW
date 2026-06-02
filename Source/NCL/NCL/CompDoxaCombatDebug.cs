using System.Collections.Generic;
using RimWorld;
using Verse;

namespace NCL
{
    public class CompProperties_DoxaCombatDebug : CompProperties
    {
        public CompProperties_DoxaCombatDebug()
        {
            compClass = typeof(CompDoxaCombatDebug);
        }
    }

    public class CompDoxaCombatDebug : ThingComp
    {
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (!Prefs.DevMode || parent is not Pawn pawn || pawn.def.defName != DoxaCombatFireUtility.DoxaDefName)
            {
                yield break;
            }

            yield return new Command_Action
            {
                defaultLabel = "DEV: Log Doxa combat",
                action = () => DoxaCombatFireUtility.LogCombatState(pawn)
            };
        }
    }
}
