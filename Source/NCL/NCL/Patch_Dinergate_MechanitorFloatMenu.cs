using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace NCL
{
    // Dinergate has no overseer: vanilla mechanitor menu only offers control/disconnect-style entries and skips repair UX.
    // Replace the mechanitor branch for Dinergate with repair-only options (hide control / disconnect / disassemble).
    [HarmonyPatch(typeof(FloatMenuOptionProvider_Mechanitor), nameof(FloatMenuOptionProvider_Mechanitor.GetOptionsFor), typeof(Pawn), typeof(FloatMenuContext))]
    internal static class Patch_Dinergate_MechanitorFloatMenu
    {
        private static bool Prefix(Pawn clickedPawn, FloatMenuContext context, ref IEnumerable<FloatMenuOption> __result)
        {
            if (!DinergateDraftUtility.IsTargetDinergate(clickedPawn))
            {
                return true;
            }

            __result = OptionsForDinergate(clickedPawn, context);
            return false;
        }

        private static IEnumerable<FloatMenuOption> OptionsForDinergate(Pawn clickedPawn, FloatMenuContext context)
        {
            if (!ModsConfig.BiotechActive)
            {
                yield break;
            }

            if (!MechanitorUtility.IsMechanitor(context.FirstSelectedPawn))
            {
                yield break;
            }

            if (!clickedPawn.IsColonyMech)
            {
                yield break;
            }

            if (!MechRepairUtility.CanRepair(clickedPawn))
            {
                yield break;
            }

            if (!context.FirstSelectedPawn.Drafted)
            {
                TaggedString reason = "IsNotDrafted".Translate(context.FirstSelectedPawn.LabelShort, context.FirstSelectedPawn);
                yield return new FloatMenuOption(
                    "RepairThing".Translate(clickedPawn.LabelShort) + ": " + reason.CapitalizeFirst(),
                    null);
                yield break;
            }

            if (!context.FirstSelectedPawn.CanReach(clickedPawn, PathEndMode.Touch, Danger.Deadly))
            {
                yield return new FloatMenuOption(
                    "CannotRepairMech".Translate(clickedPawn.LabelShort) + ": " + "NoPath".Translate().CapitalizeFirst(),
                    null);
                yield break;
            }

            yield return FloatMenuUtility.DecoratePrioritizedTask(
                new FloatMenuOption(
                    "RepairThing".Translate(clickedPawn.LabelShort),
                    delegate
                    {
                        Job job = JobMaker.MakeJob(JobDefOf.RepairMech, clickedPawn);
                        context.FirstSelectedPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                    }),
                context.FirstSelectedPawn,
                new LocalTargetInfo(clickedPawn));
        }
    }
}
