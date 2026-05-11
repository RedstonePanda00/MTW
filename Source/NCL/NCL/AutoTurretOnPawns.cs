using HarmonyLib;
using Microsoft.SqlServer.Server;
using Mono.Cecil.Cil;
using RimWorld;
using RimWorld.Planet;
using RimWorld.Utility;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Linq.Expressions;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Remoting.Messaging;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Noise;
using Verse.Sound;
using static HarmonyLib.Code;
using static RimWorld.FoodUtility;
using static System.Net.Mime.MediaTypeNames;
using static UnityEngine.GraphicsBuffer;
using static UnityEngine.Scripting.GarbageCollector;


namespace NCL
{
    // Token: 0x02000012 RID: 18
    public class LTS_CompProperties_ToggleHediff : CompProperties_AbilityEffect
    {
        // Token: 0x0600003A RID: 58 RVA: 0x000038AE File Offset: 0x00001AAE
        public LTS_CompProperties_ToggleHediff()
        {
            this.compClass = typeof(LTS_CompAbilityEffect_ToggleHediff);
        }

        // Token: 0x0400001B RID: 27
        public HediffDef ToggleHediff;

        // Token: 0x0400001C RID: 28
        public float StartSeverity;

        // Token: 0x0400001D RID: 29
        public BodyPartDef location = null;
    }
}

namespace NCL
{
    // Token: 0x02000013 RID: 19
    public class LTS_CompAbilityEffect_ToggleHediff : CompAbilityEffect
    {
        // Token: 0x17000008 RID: 8
        // (get) Token: 0x0600003B RID: 59 RVA: 0x000038D0 File Offset: 0x00001AD0
        public new LTS_CompProperties_ToggleHediff Props
        {
            get
            {
                return (LTS_CompProperties_ToggleHediff)this.props;
            }
        }

        // Token: 0x0600003C RID: 60 RVA: 0x000038F0 File Offset: 0x00001AF0
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            bool flag = target.Pawn != null;
            if (flag)
            {
                Pawn_HealthTracker health = target.Pawn.health;
                object obj;
                if (health == null)
                {
                    obj = null;
                }
                else
                {
                    HediffSet hediffSet = health.hediffSet;
                    obj = ((hediffSet != null) ? hediffSet.GetFirstHediffOfDef(this.Props.ToggleHediff, false) : null);
                }
                bool flag2 = obj != null;
                if (flag2)
                {
                    target.Pawn.health.RemoveHediff(target.Pawn.health.hediffSet.GetFirstHediffOfDef(this.Props.ToggleHediff, false));
                }
                else
                {
                    target.Pawn.health.AddHediff(this.Props.ToggleHediff, this.location(target), null, null);
                    bool flag3 = target.Pawn.health.hediffSet.GetFirstHediffOfDef(this.Props.ToggleHediff, false).TryGetComp<HediffComp_Lactating>() != null;
                    if (flag3)
                    {
                        target.Pawn.health.hediffSet.GetFirstHediffOfDef(this.Props.ToggleHediff, false).TryGetComp<HediffComp_Lactating>().TryCharge(-0.124f);
                    }
                }
            }
        }

        // Token: 0x0600003D RID: 61 RVA: 0x00003A20 File Offset: 0x00001C20
        public BodyPartRecord location(LocalTargetInfo target)
        {
            bool flag = this.Props.location == null;
            BodyPartRecord result;
            if (flag)
            {
                result = null;
            }
            else
            {
                result = (from part in target.Pawn.health.hediffSet.GetNotMissingParts(BodyPartHeight.Undefined, BodyPartDepth.Undefined, null, null)
                          where part.def == this.Props.location
                          select part).ToList<BodyPartRecord>()[0];
            }
            return result;
        }
    }
}

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