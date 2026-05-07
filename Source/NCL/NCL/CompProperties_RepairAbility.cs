using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using Verse;
using Verse.Sound;

namespace NCL
{
    // Token: 0x0200000E RID: 14
    public class CompProperties_RepairAbility : CompProperties_AbilityEffect
    {
        // Token: 0x06000052 RID: 82 RVA: 0x000042B3 File Offset: 0x000024B3
        public CompProperties_RepairAbility()
        {
            this.compClass = typeof(CompAbilityEffect_Repair);
        }
    }
}

namespace NCL
{
    // Token: 0x0200000D RID: 13
    public class CompAbilityEffect_Repair : CompAbilityEffect
    {
        // Token: 0x1700000F RID: 15
        // (get) Token: 0x06000048 RID: 72 RVA: 0x00003FE0 File Offset: 0x000021E0
        public new CompProperties_RepairAbility Props
        {
            get
            {
                return this.props as CompProperties_RepairAbility;
            }
        }

        // Token: 0x06000049 RID: 73 RVA: 0x00003FFD File Offset: 0x000021FD
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            this.RepairSelf(this.parent.pawn);
        }

        // Token: 0x0600004A RID: 74 RVA: 0x00004012 File Offset: 0x00002212
        public override void Apply(GlobalTargetInfo target)
        {
            this.Apply(null, null);
        }

        // Token: 0x0600004B RID: 75 RVA: 0x00004028 File Offset: 0x00002228
        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
        {
            return true;
        }

        // Token: 0x0600004C RID: 76 RVA: 0x0000403C File Offset: 0x0000223C
        public override bool CanApplyOn(GlobalTargetInfo target)
        {
            return this.CanApplyOn(null, null);
        }

        // Token: 0x0600004D RID: 77 RVA: 0x00004060 File Offset: 0x00002260
        private void RepairSelf(Pawn pawn)
        {
            CompAbilityEffect_Repair.tmpHediffs.Clear();
            CompAbilityEffect_Repair.tmpHediffs.AddRange(pawn.health.hediffSet.hediffs);
            CompAbilityEffect_Repair.tmpHediffs.SortBy((Hediff injury) => -injury.Severity);
            int num = 0;
            while (num < CompAbilityEffect_Repair.tmpHediffs.Count && num < 10)
            {
                Hediff hediff = CompAbilityEffect_Repair.tmpHediffs[num];
                bool flag = hediff != null && (hediff is Hediff_Injury || hediff is Hediff_MissingPart);
                if (flag)
                {
                    pawn.health.RemoveHediff(hediff);
                }
                num++;
            }
            CompAbilityEffect_Repair.tmpHediffs.Clear();

        }

        // Token: 0x0600004E RID: 78 RVA: 0x00004188 File Offset: 0x00002388
        public bool CanTargetNow()
        {
            Pawn pawn = this.parent.pawn;
            List<Pawn> list = pawn.Map.mapPawns.AllPawns.FindAll((Pawn p) => p.Faction != null && p.Faction.HostileTo(pawn.Faction));
            list.SortBy((Pawn m) => m.Position.DistanceTo(pawn.Position));
            bool flag = list[0].Position.DistanceTo(pawn.Position) <= 18.9f;
            bool result;
            if (flag)
            {
                result = false;
            }
            else
            {
                List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
                for (int i = 0; i < hediffs.Count; i++)
                {
                    Hediff_Injury hediff_Injury = hediffs[i] as Hediff_Injury;
                    bool flag2 = hediffs[i] is Hediff_MissingPart || hediff_Injury != null;
                    if (flag2)
                    {
                        return true;
                    }
                }
                result = false;
            }
            return result;
        }

        // Token: 0x0600004F RID: 79 RVA: 0x00004288 File Offset: 0x00002488
        public override bool GizmoDisabled(out string reason)
        {
            reason = null;
            return false;
        }

        // Token: 0x0400002B RID: 43
        private static List<Hediff> tmpHediffs = new List<Hediff>();
    }
}


namespace NCL
{
    public class CompProperties_GroupRepairAbility : CompProperties_AbilityEffect
    {
        public CompProperties_GroupRepairAbility()
        {
            this.compClass = typeof(CompAbilityEffect_GroupRepair);
        }
    }

    public class CompAbilityEffect_GroupRepair : CompAbilityEffect
    {
        private static List<Hediff> tmpHediffs = new List<Hediff>();

        // 定义特效常量（如果EffecterDefOf中没有）
        private const string MechRepairEffectDefName = "MechResurrected";

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            if (!target.Cell.IsValid) return;

            Map map = parent.pawn.Map;
            Faction faction = parent.pawn.Faction;
            const float repairRadius = 25f; // 25格范围

            // 修复范围内所有友方机械族
            foreach (Thing thing in GenRadial.RadialDistinctThingsAround(
                target.Cell, map, repairRadius, true))
            {
                if (thing is Pawn pawn &&
                    pawn.Faction == faction &&
                    pawn.RaceProps.IsMechanoid)
                {
                    RepairMech(pawn);

                    // 生成指定特效（安全访问）
                    SpawnRepairEffect(pawn.Position, map);
                }
            }
        }

        // 安全生成特效
        private void SpawnRepairEffect(IntVec3 position, Map map)
        {
            // 1. 尝试直接获取特效定义
            EffecterDef effecterDef = DefDatabase<EffecterDef>.GetNamedSilentFail(MechRepairEffectDefName);

            // 2. 如果找到定义，使用它
            if (effecterDef != null)
            {
                Effecter effecter = effecterDef.Spawn();
                effecter.Trigger(new TargetInfo(position, map), TargetInfo.Invalid);
                effecter.Cleanup();
                return;
            }

            // 3. 如果未找到，创建临时特效作为后备
            FleckMaker.ThrowLightningGlow(position.ToVector3(), map, 1.5f);
            FleckMaker.ThrowSmoke(position.ToVector3(), map, 1f);
        }

        // 原有的修复逻辑保持不变
        private void RepairMech(Pawn pawn)
        {
            tmpHediffs.Clear();
            tmpHediffs.AddRange(pawn.health.hediffSet.hediffs);
            tmpHediffs.SortBy((Hediff injury) => -injury.Severity);
            int num = 0;
            while (num < tmpHediffs.Count && num < 5)
            {
                Hediff hediff = tmpHediffs[num];
                if (hediff != null && (hediff is Hediff_Injury || hediff is Hediff_MissingPart))
                {
                    pawn.health.RemoveHediff(hediff);
                }
                num++;
            }
            tmpHediffs.Clear();
        }
    }
}
