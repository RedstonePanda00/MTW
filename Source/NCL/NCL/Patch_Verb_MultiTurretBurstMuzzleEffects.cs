using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using RsPandaLibrary;
using UnityEngine;
using Verse;

namespace NCL
{
    // Vanilla TryCastNextBurstShot uses caster.Position for shot flash and sound. RsPanda multi-turret
    // projectiles already use TurretShootDrawWorld, but FleckMaker.Static(IntVec3, ...) snaps to cell
    // center — on large mechs the tail muzzle often shares the pawn's root cell, so the flash still
    // reads as "from body center". Use the Vector3 overload for the fleck only when multi-turret scope is active.
    [HarmonyPatch(typeof(Verb), "TryCastNextBurstShot")]
    public static class Patch_Verb_MultiTurretBurstMuzzleEffects
    {
        public const string SolarisPenetratorGunDefName = "NCL_Scorpion_SolarisPenetrator";

        // Burst muzzle fleck/sound redirect is always active; logging is opt-in / Solaris-only by default.
        public static bool LogMultiTurretBurstMuzzle = false;

        // When true with DevMode, also logs RT AutoGun burst lines (very spammy during sustained fire).
        public static bool LogMuzzleBurstIncludeRtGunsInDevMode = false;

        // When true with LogMultiTurretBurstMuzzle, logs Projectile.Launch origin for Solaris shots (DevMode only).
        public static bool LogSolarisProjectileLaunchOrigin = false;

        private static bool loggedTranspilerNoMatches;
        private static bool loggedTranspilerFleckOverloadMiss;

        private static readonly FieldInfo FI_Verb_caster = AccessTools.Field(typeof(Verb), nameof(Verb.caster));
        private static readonly MethodInfo MI_Thing_get_Position = AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.Position));
        private static readonly FieldInfo FI_VerbEvaluatingUnit =
            AccessTools.Field(typeof(CompMultiTurretGun), "VerbEvaluatingUnit");

        private static readonly MethodInfo MI_MuzzleCellOrCaster = AccessTools.Method(
            typeof(Patch_Verb_MultiTurretBurstMuzzleEffects),
            nameof(MuzzleCellOrCaster));
        private static readonly MethodInfo MI_MuzzleWorldForBurstFleck = AccessTools.Method(
            typeof(Patch_Verb_MultiTurretBurstMuzzleEffects),
            nameof(MuzzleWorldForBurstFleck));
        private static readonly MethodInfo MI_Fleck_Static_IntVec3 = AccessTools.Method(
            typeof(FleckMaker),
            nameof(FleckMaker.Static),
            new[] { typeof(IntVec3), typeof(Map), typeof(FleckDef), typeof(float) });
        private static readonly MethodInfo MI_Fleck_Static_Vector3 = AccessTools.Method(
            typeof(FleckMaker),
            nameof(FleckMaker.Static),
            new[] { typeof(Vector3), typeof(Map), typeof(FleckDef), typeof(float) });

        private static bool ShouldLogMuzzle(Verb verb)
        {
            if (!LogMultiTurretBurstMuzzle || verb?.caster == null)
            {
                return false;
            }

            if (verb.EquipmentSource?.def?.defName == SolarisPenetratorGunDefName)
            {
                return true;
            }

            return Prefs.DevMode && LogMuzzleBurstIncludeRtGunsInDevMode &&
                   verb.caster is Pawn p && p.def.defName == "NCL_Mech_Scorpion";
        }

        private static string DescribePandaEvaluatingVerb(Verb verb)
        {
            if (FI_VerbEvaluatingUnit == null)
            {
                return "CompMultiTurretGun.VerbEvaluatingUnit field not found (RsPanda version mismatch?)";
            }

            object unit = FI_VerbEvaluatingUnit.GetValue(null);
            if (unit == null)
            {
                return "VerbEvaluatingUnit=null (TryCastNextBurstShot outside CompMultiTurretGun scope?)";
            }

            Verb scoped = Traverse.Create(unit).Field<Verb>("verb").Value;
            bool sameRef = ReferenceEquals(scoped, verb);
            string scopedGun = scoped?.EquipmentSource?.def?.defName ?? "null";
            string thisGun = verb?.EquipmentSource?.def?.defName ?? "null";
            return $"VerbEvaluatingUnit present scopedVerb==this:{sameRef} scopedEqDef:{scopedGun} thisEqDef:{thisGun}";
        }

        private static void MaybeLogMuzzle(string channel, Verb verb, bool tryGetOk, IntVec3 root, Vector3 draw, Vector3 returnedWorld)
        {
            if (!ShouldLogMuzzle(verb))
            {
                return;
            }

            string scope = DescribePandaEvaluatingVerb(verb);
            Vector3 casterDraw = verb.caster.DrawPos;
            float horizDelta = (draw - casterDraw).MagnitudeHorizontal();
            bool sameX = Mathf.Approximately(draw.x, casterDraw.x);
            bool sameZ = Mathf.Approximately(draw.z, casterDraw.z);
            Log.Message(
                $"[NCL MTW][MuzzleBurst][{channel}] tick={Find.TickManager.TicksGame} tryGet={tryGetOk} " +
                $"root={root} draw=({draw.x:F3},{draw.y:F3},{draw.z:F3}) ret=({returnedWorld.x:F3},{returnedWorld.y:F3},{returnedWorld.z:F3}) " +
                $"casterCell={verb.caster.Position} casterDraw=({casterDraw.x:F3},{casterDraw.y:F3},{casterDraw.z:F3}) " +
                $"horizDeltaDrawVsCaster={horizDelta:F3} sameXZ=({sameX},{sameZ}) {scope}");
        }

        public static IntVec3 MuzzleCellOrCaster(Verb verb)
        {
            if (verb?.caster == null)
            {
                return IntVec3.Invalid;
            }

            bool ok = CompMultiTurretGun.TryGetTurretShootFromVerb(verb, out IntVec3 root, out Vector3 draw);
            IntVec3 ret = ok ? root : verb.caster.Position;
            // Log with draw as reference for sound (IntVec3 root snaps Y in ToVector3Shifted for display).
            MaybeLogMuzzle("SoundCell", verb, ok, root, draw, ok ? draw : ret.ToVector3Shifted());
            return ret;
        }

        // Matches vanilla IntVec3 overload behavior for non–multi-turret (cell center); sub-cell muzzle when Panda scope is active.
        public static Vector3 MuzzleWorldForBurstFleck(Verb verb)
        {
            if (verb?.caster == null)
            {
                return Vector3.zero;
            }

            bool ok = CompMultiTurretGun.TryGetTurretShootFromVerb(verb, out IntVec3 root, out Vector3 draw);
            Vector3 ret = ok ? draw : verb.caster.Position.ToVector3Shifted();
            MaybeLogMuzzle("FleckWorld", verb, ok, root, draw, ret);
            return ret;
        }

        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
            List<int> matchStarts = new List<int>();
            for (int i = 0; i <= codes.Count - 3; i++)
            {
                if (codes[i].opcode != OpCodes.Ldarg_0)
                {
                    continue;
                }

                if (!codes[i + 1].LoadsField(FI_Verb_caster))
                {
                    continue;
                }

                if (codes[i + 2].opcode != OpCodes.Callvirt || codes[i + 2].operand is not MethodInfo mi ||
                    mi != MI_Thing_get_Position)
                {
                    continue;
                }

                matchStarts.Add(i);
            }

            if (matchStarts.Count == 0)
            {
                if (!loggedTranspilerNoMatches)
                {
                    loggedTranspilerNoMatches = true;
                    Log.Warning(
                        "[NCL MTW][MuzzleBurst][Transpiler] No ldarg.0 + caster + Position pattern in Verb.TryCastNextBurstShot — " +
                        "patch may not apply on this game version; burst muzzle IL unchanged.");
                }

                return codes;
            }

            int fleckMatchIndex = matchStarts.Min();

            for (int k = matchStarts.Count - 1; k >= 0; k--)
            {
                int i = matchStarts[k];
                bool isFleckSite = i == fleckMatchIndex;
                codes[i] = new CodeInstruction(OpCodes.Ldarg_0);
                codes[i + 1] = new CodeInstruction(
                    OpCodes.Call,
                    isFleckSite ? MI_MuzzleWorldForBurstFleck : MI_MuzzleCellOrCaster);
                codes.RemoveAt(i + 2);

                if (isFleckSite && MI_Fleck_Static_IntVec3 != null && MI_Fleck_Static_Vector3 != null)
                {
                    bool replacedFleckOverload = false;
                    int scanEnd = Mathf.Min(i + 40, codes.Count);
                    for (int j = i; j < scanEnd; j++)
                    {
                        if (codes[j].opcode == OpCodes.Call && Equals(codes[j].operand, MI_Fleck_Static_IntVec3))
                        {
                            codes[j].operand = MI_Fleck_Static_Vector3;
                            replacedFleckOverload = true;
                            break;
                        }
                    }

                    if (!replacedFleckOverload && !loggedTranspilerFleckOverloadMiss)
                    {
                        loggedTranspilerFleckOverloadMiss = true;
                        Log.Warning(
                            "[NCL MTW][MuzzleBurst][Transpiler] FleckMaker.Static(IntVec3,...) not found after muzzle load — " +
                            "Vector3 fleck overload swap skipped; check RimWorld / compiler IL.");
                    }
                }
            }

            return codes;
        }
    }

    // Confirms hellsphere projectile origin vs pawn DrawPos when equipment is Solaris (no DevMode required).
    [HarmonyPatch(typeof(Projectile), nameof(Projectile.Launch), new[]
    {
        typeof(Thing), typeof(Vector3), typeof(LocalTargetInfo), typeof(LocalTargetInfo), typeof(ProjectileHitFlags),
        typeof(bool), typeof(Thing), typeof(ThingDef)
    })]
    public static class Patch_Projectile_Launch_LogSolarisOrigin
    {
        [HarmonyPostfix]
        public static void Postfix(Projectile __instance, Thing launcher, Vector3 origin, Thing equipment)
        {
            if (!Patch_Verb_MultiTurretBurstMuzzleEffects.LogMultiTurretBurstMuzzle ||
                !Patch_Verb_MultiTurretBurstMuzzleEffects.LogSolarisProjectileLaunchOrigin)
            {
                return;
            }

            if (equipment?.def?.defName != Patch_Verb_MultiTurretBurstMuzzleEffects.SolarisPenetratorGunDefName)
            {
                return;
            }

            if (launcher == null)
            {
                return;
            }

            Vector3 launcherDraw = launcher.DrawPos;
            float horiz = (origin - launcherDraw).MagnitudeHorizontal();
            Log.Message(
                $"[NCL MTW][ProjectileLaunch][Solaris] tick={Find.TickManager.TicksGame} " +
                $"projDef={__instance?.def?.defName} " +
                $"origin=({origin.x:F3},{origin.y:F3},{origin.z:F3}) launcherDraw=({launcherDraw.x:F3},{launcherDraw.y:F3},{launcherDraw.z:F3}) " +
                $"horizDelta={horiz:F3}");
        }
    }
}
