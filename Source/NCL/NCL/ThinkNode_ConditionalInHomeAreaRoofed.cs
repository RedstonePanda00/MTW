using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI;

namespace NCLvsTW
{
    public class ThinkNode_ConditionalInHomeAreaRoofed : ThinkNode_Conditional
    {
        public bool invert = false; // XML可配置是否反转条件

        protected override bool Satisfied(Pawn pawn)
        {
            if (pawn.Map == null)
                return false;

            // 检查是否在生活区
            bool inHomeArea = pawn.Map.areaManager.Home[pawn.Position];

            // 检查是否有屋顶
            bool roofed = pawn.Position.Roofed(pawn.Map);

            // 反转条件（当invert=true时，返回不在有顶生活区）
            return invert ? !(inHomeArea && roofed) : (inHomeArea && roofed);
        }
    }

    // 自定义生活区漫游行为
    public class JobGiver_WanderInHomeArea : JobGiver_Wander
    {
        public JobGiver_WanderInHomeArea()
        {
            wanderRadius = 7.5f;
            ticksBetweenWandersRange = new IntRange(125, 200);
        }

        protected override IntVec3 GetWanderRoot(Pawn pawn)
        {
            // 确保只在生活区生成漫游点
            if (pawn.Map.areaManager.Home.ActiveCells.TryRandomElement(out IntVec3 result))
            {
                return result;
            }
            return pawn.Position; // 回退到当前位置
        }

        protected override Job TryGiveJob(Pawn pawn)
        {
            // 确保漫游点有路径可达且在生活区
            if (pawn.Map.areaManager.Home.ActiveCells.Where(cell =>
                   pawn.CanReach(cell, PathEndMode.OnCell, Danger.None))
                .TryRandomElement(out IntVec3 wanderRoot))
            {
                return base.TryGiveJob(pawn);
            }
            return null;
        }
    }
}