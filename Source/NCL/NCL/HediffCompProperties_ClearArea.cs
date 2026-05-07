using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace NCL
{
    // Hediff主体
    public class Hediff_TotalClear : HediffWithComps
    {
        // 基础功能已通过HediffWithComps实现
    }

    // 清除组件
    public class HediffComp_TotalClear : HediffComp
    {
        public HediffCompProperties_TotalClear Props =>
            (HediffCompProperties_TotalClear)props;

        private int ticksUntilClear;

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            if (Pawn.Map == null || !Pawn.Spawned) return;

            if (--ticksUntilClear <= 0)
            {
                PerformTotalClear();
                ticksUntilClear = Props.clearInterval;
            }
        }

        private void PerformTotalClear()
        {
            CellRect rect = CellRect.CenteredOn(Pawn.Position, 3);
            List<Thing> things = new List<Thing>();

            foreach (IntVec3 cell in rect)
            {
                if (cell.InBounds(Pawn.Map))
                    things.AddRange(cell.GetThingList(Pawn.Map));
            }

            foreach (Thing thing in things)
            {
                if (ShouldClear(thing))
                    ExecuteClear(thing);
            }
        }

        private bool ShouldClear(Thing thing)
        {
            // 白名单检查（满足任意条件即豁免）
            if (thing is Pawn pawn)
            {
                // 派系白名单
                if (Props.whiteFactions.Contains(pawn.Faction?.def))
                    return false;

                // 种族白名单
                if (Props.whiteRaces.Contains(pawn.def))
                    return false;

                // 具体生物种类白名单
                if (Props.whitePawnKinds.Contains(pawn.kindDef))
                    return false;
            }

            // 物品白名单
            if (Props.whiteThings.Contains(thing.def))
                return false;

            return true; // 不在白名单中的全部清除
        }

        private void ExecuteClear(Thing thing)
        {
            if (thing.Destroyed) return;

            // 生物清除方式
            if (thing is Pawn targetPawn)
            {
                if (!targetPawn.Dead)
                {
                    DamageInfo dinfo = new DamageInfo(
                        DamageDefOf.Vaporize,
                        99999f,
                        instigator: Pawn
                    );
                    targetPawn.Kill(dinfo);
                }
            }
            // 物品清除方式
            else
            {
                if (thing.def.destroyable)
                    thing.Destroy();
                else
                    thing.DeSpawn(); // 处理不可销毁对象
            }

            // 清除特效
            FleckMaker.ThrowLightningGlow(thing.DrawPos, thing.Map, 2f);
        }
    }

    public class HediffCompProperties_TotalClear : HediffCompProperties
    {
        public List<FactionDef> whiteFactions = new List<FactionDef>();
        public List<ThingDef> whiteRaces = new List<ThingDef>();
        public List<PawnKindDef> whitePawnKinds = new List<PawnKindDef>();
        public List<ThingDef> whiteThings = new List<ThingDef>();
        public int clearInterval = 60;

        public HediffCompProperties_TotalClear()
        {
            compClass = typeof(HediffComp_TotalClear); // 关键绑定
        }
    }
}