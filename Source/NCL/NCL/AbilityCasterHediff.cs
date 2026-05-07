using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NCL;
using RimWorld;
using Verse;

namespace NCL
{
    // Token: 0x02000015 RID: 21
    public class CompProperties_AbilityCasterHediff : CompProperties_AbilityEffect
    {
        // Token: 0x06000049 RID: 73 RVA: 0x000038FC File Offset: 0x00001AFC
        public CompProperties_AbilityCasterHediff()
        {
            this.compClass = typeof(CompAbilityEffect_CasterHediff);
        }

        // Token: 0x04000021 RID: 33
        public HediffDef casterHediff;

        // Token: 0x04000022 RID: 34
        public float initialSeverity = 0.5f;

        // Token: 0x04000023 RID: 35
        public bool ignoreIfExist;
    }
}

    // Token: 0x02000005 RID: 5
    public class CompAbilityEffect_CasterHediff : CompAbilityEffect
{
    // Token: 0x17000003 RID: 3
    // (get) Token: 0x06000009 RID: 9 RVA: 0x000022EE File Offset: 0x000004EE
    public new CompProperties_AbilityCasterHediff Props
    {
        get
        {
            return this.props as CompProperties_AbilityCasterHediff;
        }
    }

    // Token: 0x0600000A RID: 10 RVA: 0x000022FC File Offset: 0x000004FC
    public override void PostApplied(List<LocalTargetInfo> targets, Map map)
    {
        Hediff firstHediffOfDef = this.parent.pawn.health.hediffSet.GetFirstHediffOfDef(this.Props.casterHediff, false);
        bool flag = !this.Props.ignoreIfExist || firstHediffOfDef == null;
        if (flag)
        {
            Hediff hediff = this.parent.pawn.health.AddHediff(this.Props.casterHediff, null, null, null);
            hediff.Severity = this.Props.initialSeverity;
        }
    }
}

