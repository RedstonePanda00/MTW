using HarmonyLib;
using RimWorld;
using System;
using UnityEngine;
using Verse;

[HarmonyPatch(typeof(HediffComp_ReactOnDamage), nameof(HediffComp_ReactOnDamage.Notify_PawnPostApplyDamage))]
class HediffComp_ReactOnDamage_Notify_PawnPostApplyDamage_Patch_EMP //add a chance equal to EMPResistance to not apply brain shock or vomiting
{
    [HarmonyPrefix]
    static bool HediffComp_ReactOnDamage_Notify_PawnPostApplyDamage_Prefix(DamageInfo dinfo, float totalDamageDealt, HediffComp_ReactOnDamage __instance)
    {
        Pawn pawn = __instance.Pawn;
        if (pawn == null)
        {
            return true;
        }

        float empResistance = 0f;
        StatDef brainShockResStat = DefDatabase<StatDef>.GetNamedSilentFail("NCL_BrainShockResistance");
        if (brainShockResStat != null)
        {
            empResistance = pawn.GetStatValue(brainShockResStat);
        }

        if (dinfo.Def == DamageDefOf.EMP && new System.Random().Next(0, 100) < empResistance * 100f)
        {
            if (pawn.Map != null)
            {
                MoteMaker.ThrowText(
                    new Vector3((float)pawn.Position.x + 1f, pawn.Position.y, (float)pawn.Position.z + 1f),
                    text: "Resisted".Translate(),
                    map: pawn.Map,
                    color: Color.white);
            }

            return false;
        }

        return true;
    }
}
