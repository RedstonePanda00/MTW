using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace NCL
{
    internal static class DiverUtility
    {
        internal const string DiverDefName = "MTW_Diver";

        internal static bool IsDiver(Pawn pawn)
        {
            return pawn?.def?.defName == DiverDefName;
        }
    }

    [HarmonyPatch(typeof(Pawn_DraftController), nameof(Pawn_DraftController.ShowDraftGizmo), MethodType.Getter)]
    internal static class Patch_Diver_ShowDraftGizmo
    {
        private static void Postfix(Pawn_DraftController __instance, ref bool __result)
        {
            if (__result || !DiverUtility.IsDiver(__instance?.pawn))
            {
                return;
            }

            __result = true;
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.SpawnSetup))]
    [HarmonyPriority(500)]
    internal static class Patch_Diver_EnsureEquipmentTracker
    {
        private static void Postfix(Pawn __instance, Map map)
        {
            if (map == null || __instance.Destroyed || !DiverUtility.IsDiver(__instance))
            {
                return;
            }

            if (__instance.equipment == null && __instance.RaceProps.intelligence >= Intelligence.ToolUser)
            {
                __instance.equipment = new Pawn_EquipmentTracker(__instance);
            }

            if (__instance.apparel == null && __instance.RaceProps.intelligence >= Intelligence.ToolUser)
            {
                __instance.apparel = new Pawn_ApparelTracker(__instance);
            }

            if (__instance.story == null)
            {
                __instance.story = new Pawn_StoryTracker(__instance);
            }

            if (__instance.story.bodyType == null)
            {
                __instance.story.bodyType = BodyTypeDefOf.Male;
            }

            if (__instance.story.headType == null)
            {
                __instance.story.headType = DefDatabase<HeadTypeDef>.AllDefsListForReading
                    .FirstOrDefault(h => h != HeadTypeDefOf.Skull && h != HeadTypeDefOf.Stump)
                    ?? HeadTypeDefOf.Skull;
            }

            if (!__instance.story.skinColorOverride.HasValue)
            {
                __instance.story.skinColorOverride = Color.white;
            }

        }
    }

    [HarmonyPatch(typeof(PawnRenderNode_Body), nameof(PawnRenderNode_Body.GraphicFor))]
    internal static class Patch_Diver_BodyGraphicFor
    {
        private static readonly Vector2 BodyDrawSize = new Vector2(1f, 1f);

        private static bool Prefix(PawnRenderNode_Body __instance, Pawn pawn, ref Graphic __result)
        {
            if (!DiverUtility.IsDiver(pawn))
            {
                return true;
            }

            __result = GraphicDatabase.Get<Graphic_Multi>("Diver/DiverBody", ShaderDatabase.Cutout, BodyDrawSize, Color.white);
            return false;
        }
    }

    [HarmonyPatch(typeof(PawnRenderNode_Head), nameof(PawnRenderNode_Head.GraphicFor))]
    internal static class Patch_Diver_HeadGraphicFor
    {
        private static readonly Vector2 HeadDrawSize = new Vector2(1f, 1f);

        private static bool Prefix(Pawn pawn, ref Graphic __result)
        {
            if (!DiverUtility.IsDiver(pawn))
            {
                return true;
            }

            __result = GraphicDatabase.Get<Graphic_Multi>("Diver/DiverHead", ShaderDatabase.Cutout, HeadDrawSize, Color.white);
            return false;
        }
    }

    [HarmonyPatch(typeof(PawnRenderNode_Stump), nameof(PawnRenderNode_Stump.GraphicFor))]
    internal static class Patch_Diver_StumpGraphicFor
    {
        private static bool Prefix(Pawn pawn, ref Graphic __result)
        {
            if (!DiverUtility.IsDiver(pawn))
            {
                return true;
            }

            __result = null;
            return false;
        }
    }

    [HarmonyPatch(typeof(PawnRenderNode_Hair), nameof(PawnRenderNode_Hair.GraphicFor))]
    internal static class Patch_Diver_HairGraphicFor
    {
        private static bool Prefix(Pawn pawn, ref Graphic __result)
        {
            if (!DiverUtility.IsDiver(pawn))
            {
                return true;
            }

            __result = null;
            return false;
        }
    }

    [HarmonyPatch(typeof(PawnRenderNode_Beard), nameof(PawnRenderNode_Beard.GraphicFor))]
    internal static class Patch_Diver_BeardGraphicFor
    {
        private static bool Prefix(Pawn pawn, ref Graphic __result)
        {
            if (!DiverUtility.IsDiver(pawn))
            {
                return true;
            }

            __result = null;
            return false;
        }
    }

    [HarmonyPatch(typeof(MechanitorUtility), nameof(MechanitorUtility.CanDraftMech))]
    internal static class Patch_Diver_CanDraftMech
    {
        private static void Postfix(Pawn mech, ref AcceptanceReport __result)
        {
            if (__result.Accepted || !DiverUtility.IsDiver(mech))
            {
                return;
            }

            if (mech.needs?.energy != null && mech.needs.energy.IsLowEnergySelfShutdown)
            {
                return;
            }

            __result = true;
        }
    }

    [HarmonyPatch(typeof(FloatMenuOptionProvider_DraftedMove), nameof(FloatMenuOptionProvider_DraftedMove.PawnCanGoto))]
    internal static class Patch_Diver_PawnCanGoto
    {
        private static bool Prefix(Pawn pawn, IntVec3 gotoLoc, ref AcceptanceReport __result)
        {
            if (!DiverUtility.IsDiver(pawn))
            {
                return true;
            }

            if (!pawn.CanReach(gotoLoc, PathEndMode.OnCell, Danger.Deadly))
            {
                __result = "CannotGoNoPath".Translate();
                return false;
            }

            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(MechanitorUtility), nameof(MechanitorUtility.InMechanitorCommandRange))]
    internal static class Patch_Diver_InMechanitorCommandRange
    {
        private static void Postfix(Pawn mech, ref bool __result)
        {
            if (__result || !DiverUtility.IsDiver(mech))
            {
                return;
            }

            if (mech.Faction == Faction.OfPlayer)
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(MechanitorUtility), nameof(MechanitorUtility.EverControllable))]
    internal static class Patch_Diver_EverControllable
    {
        private static void Postfix(Pawn mech, ref bool __result)
        {
            if (__result || !DiverUtility.IsDiver(mech))
            {
                return;
            }

            if (mech.Faction == Faction.OfPlayer)
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.IsColonyMech), MethodType.Getter)]
    internal static class Patch_Diver_IsColonyMech
    {
        private static void Postfix(Pawn __instance, ref bool __result)
        {
            if (__result || !DiverUtility.IsDiver(__instance))
            {
                return;
            }

            if (__instance.Faction == Faction.OfPlayer && __instance.MentalStateDef == null)
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.IsColonyMechPlayerControlled), MethodType.Getter)]
    internal static class Patch_Diver_IsColonyMechPlayerControlled
    {
        private static void Postfix(Pawn __instance, ref bool __result)
        {
            if (__result || !DiverUtility.IsDiver(__instance))
            {
                return;
            }

            if (__instance.Faction == Faction.OfPlayer && __instance.MentalStateDef == null)
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(ITab_Pawn_Character), nameof(ITab_Pawn_Character.IsVisible), MethodType.Getter)]
    internal static class Patch_Diver_HideCharacterInspectTab
    {
        private static void Postfix(ref bool __result)
        {
            if (!__result)
            {
                return;
            }

            Pawn selectedPawn = Find.Selector?.SingleSelectedThing as Pawn;
            if (DiverUtility.IsDiver(selectedPawn))
            {
                __result = false;
            }
        }
    }

    [HarmonyPatch(typeof(CompOverseerSubject), nameof(CompOverseerSubject.State), MethodType.Getter)]
    internal static class Patch_Diver_OverseerState
    {
        private static void Postfix(CompOverseerSubject __instance, ref OverseerSubjectState __result)
        {
            if (!DiverUtility.IsDiver(__instance?.parent as Pawn))
            {
                return;
            }

            Pawn pawn = __instance.parent as Pawn;
            if (pawn?.Faction == Faction.OfPlayer)
            {
                __result = OverseerSubjectState.Overseen;
            }
        }
    }

    [HarmonyPatch(typeof(MechanitorUtility), nameof(MechanitorUtility.IsColonyMechRequiringMechanitor))]
    internal static class Patch_Diver_IsColonyMechRequiringMechanitor
    {
        private static void Postfix(Pawn mech, ref bool __result)
        {
            if (!__result || !DiverUtility.IsDiver(mech))
            {
                return;
            }

            if (mech.Faction == Faction.OfPlayer)
            {
                __result = false;
            }
        }
    }

    [HarmonyPatch(typeof(DynamicPawnRenderNodeSetup_Apparel), nameof(DynamicPawnRenderNodeSetup_Apparel.GetDynamicNodes))]
    internal static class Patch_Diver_ApparelSetupScopeGuard
    {
        private static bool Prefix(Pawn pawn, ref IEnumerable<(PawnRenderNode node, PawnRenderNode parent)> __result)
        {
            // Guardrail for compatibility:
            // if another patch makes apparel setup run for non-humanlike pawns globally,
            // keep this behavior scoped to Diver only.
            if (pawn != null && !pawn.RaceProps.Humanlike && !DiverUtility.IsDiver(pawn))
            {
                __result = Enumerable.Empty<(PawnRenderNode node, PawnRenderNode parent)>();
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(PawnRenderTree), nameof(PawnRenderTree.ShouldAddNodeToTree))]
    internal static class Patch_Diver_ShouldAddNodeToTree
    {
        private static readonly AccessTools.FieldRef<PawnRenderTree, Pawn> PawnFieldRef =
            AccessTools.FieldRefAccess<PawnRenderTree, Pawn>("pawn");

        private static void Postfix(PawnRenderTree __instance, PawnRenderNodeProperties props, ref bool __result)
        {
            if (__result || props == null)
            {
                return;
            }

            Pawn pawn = PawnFieldRef(__instance);
            if (!DiverUtility.IsDiver(pawn))
            {
                return;
            }

            if (props.pawnType == PawnRenderNodeProperties.RenderNodePawnType.HumanlikeOnly)
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(PawnRenderTree), "SetupDynamicNodes")]
    internal static class Patch_Diver_SetupDynamicNodes_AddApparelNodes
    {
        private static readonly AccessTools.FieldRef<PawnRenderTree, Pawn> PawnFieldRef =
            AccessTools.FieldRefAccess<PawnRenderTree, Pawn>("pawn");

        private static readonly DynamicPawnRenderNodeSetup_Apparel ApparelSetup = new DynamicPawnRenderNodeSetup_Apparel();

        private static readonly System.Reflection.MethodInfo AddChildMethod =
            AccessTools.Method(typeof(PawnRenderTree), "AddChild");

        private static void Postfix(PawnRenderTree __instance)
        {
            Pawn pawn = PawnFieldRef(__instance);
            if (!DiverUtility.IsDiver(pawn) || pawn?.apparel == null || pawn.apparel.WornApparelCount == 0)
            {
                return;
            }

            foreach (var (node, parent) in ApparelSetup.GetDynamicNodes(pawn, __instance))
            {
                if (node == null)
                {
                    continue;
                }

                AddChildMethod?.Invoke(__instance, new object[] { node, parent });
            }
        }
    }

    [HarmonyPatch(typeof(StatWorker), nameof(StatWorker.IsDisabledFor))]
    internal static class Patch_Diver_StatWorkerIsDisabledFor
    {
        private static bool Prefix(Thing thing, ref bool __result)
        {
            if (!DiverUtility.IsDiver(thing as Pawn))
            {
                return true;
            }

            Pawn pawn = thing as Pawn;
            if (pawn?.skills == null)
            {
                __result = false;
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(FloatMenuOptionProvider_Mechanitor), nameof(FloatMenuOptionProvider_Mechanitor.GetOptionsFor), typeof(Pawn), typeof(FloatMenuContext))]
    internal static class Patch_Diver_MechanitorFloatMenu
    {
        private static bool Prefix(Pawn clickedPawn, FloatMenuContext context, ref IEnumerable<FloatMenuOption> __result)
        {
            if (!DiverUtility.IsDiver(clickedPawn))
            {
                return true;
            }

            __result = GetRepairOptions(clickedPawn, context);
            return false;
        }

        private static IEnumerable<FloatMenuOption> GetRepairOptions(Pawn clickedPawn, FloatMenuContext context)
        {
            if (!ModsConfig.BiotechActive || !MechanitorUtility.IsMechanitor(context.FirstSelectedPawn))
            {
                yield break;
            }

            if (!clickedPawn.IsColonyMech || !MechRepairUtility.CanRepair(clickedPawn))
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
