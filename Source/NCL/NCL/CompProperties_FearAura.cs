using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace NCL
{
    public class CompProperties_FearAura : CompProperties
    {
        public float radius = 6f;
        public int checkInterval = 60;
        public float fleeChance = 1f;
        public bool affectAnimals = true;
        public bool affectHumans = true;
        public bool affectMechanoids = true;

        // 新增：是否影响盟友单位
        public bool affectAllies = false;

        public CompProperties_FearAura()
        {
            compClass = typeof(CompFearAura);
        }
    }

    public class CompFearAura : ThingComp
    {
        private CompProperties_FearAura Props => (CompProperties_FearAura)props;
        private int nextCheckTick = 0;

        public override void CompTick()
        {
            base.CompTick();

            if (Find.TickManager.TicksGame < nextCheckTick ||
                !parent.Spawned ||
                parent.Map == null)
                return;

            nextCheckTick = Find.TickManager.TicksGame + Props.checkInterval;

            foreach (Pawn pawn in GetNearbyPawns())
            {
                if (ShouldFlee(pawn) && Rand.Value < Props.fleeChance)
                {
                    MakePawnFlee(pawn);
                }
            }
        }

        private IEnumerable<Pawn> GetNearbyPawns()
        {
            return GenRadial
                .RadialDistinctThingsAround(parent.Position, parent.Map, Props.radius, true)
                .OfType<Pawn>()
                .Where(p => p != parent &&
                       !p.Dead &&
                       !p.Downed &&
                       p.Awake());
        }

        private bool ShouldFlee(Pawn pawn)
        {
            // 新增：检查同阵营免疫
            if (!AffectsPawnBasedOnFaction(pawn))
                return false;

            if (pawn.RaceProps.Humanlike)
                return Props.affectHumans;
            if (pawn.RaceProps.Animal)
                return Props.affectAnimals;
            if (pawn.RaceProps.IsMechanoid)
                return Props.affectMechanoids;
            return false;
        }

        // 新增：基于阵营关系的免疫检查
        private bool AffectsPawnBasedOnFaction(Pawn pawn)
        {
            // 获取阵营信息
            Faction parentFaction = parent.Faction;
            Faction pawnFaction = pawn.Faction;

            // 1. 处理无阵营或相同实体的情况
            if (parentFaction == null || pawnFaction == null)
                return true;

            // 2. 处理同一阵营的情况（避免自检错误）
            if (parentFaction == pawnFaction)
                return false; // 同阵营免疫

            // 3. 处理相同派系ID但不同实例的比较
            if (parentFaction.def == pawnFaction.def && parentFaction.loadID == pawnFaction.loadID)
                return false;

            // 4. 检查盟友免疫设置
            if (!Props.affectAllies)
            {
                try
                {
                    // 安全地获取阵营关系
                    return parentFaction.HostileTo(pawnFaction);
                }
                catch // 防止意外异常
                {
                    return true; // 出错时默认影响
                }
            }

            return true;
        }


        private void MakePawnFlee(Pawn pawn)
        {
            if (pawn.CurJob != null && pawn.CurJob.def == JobDefOf.Flee)
                return;

            IntVec3 direction = pawn.Position - parent.Position;
            float length = direction.LengthHorizontal;

            if (length > 0)
            {
                direction.x = Mathf.RoundToInt(direction.x / length);
                direction.z = Mathf.RoundToInt(direction.z / length);
            }
            else
            {
                direction = new IntVec3(Rand.Range(-1, 2), 0, Rand.Range(-1, 2));
            }

            IntVec3 fleeDest = pawn.Position + direction * 10;
            fleeDest = CellFinder.RandomClosewalkCellNear(fleeDest, pawn.Map, 5);

            if (fleeDest.IsValid && pawn.CanReach(fleeDest, PathEndMode.OnCell, Danger.Deadly))
            {
                pawn.jobs.StartJob(
                    JobMaker.MakeJob(JobDefOf.Flee, fleeDest, parent),
                    JobCondition.InterruptForced
                );

                if (pawn.IsColonist)
                    Messages.Message($"{pawn.LabelShort} is fleeing in terror!", pawn, MessageTypeDefOf.NegativeEvent);

                FleckMaker.ThrowAirPuffUp(pawn.DrawPos, pawn.Map);
            }
        }

        public override string CompInspectStringExtra()
        {
            if (!parent.Spawned)
                return null;

            string baseText = $"Fear radius: {Props.radius}m";

            // 新增：显示阵营免疫信息
            if (!Props.affectAllies)
                baseText += "\nAllies are immune";

            return baseText;
        }
    }
}
