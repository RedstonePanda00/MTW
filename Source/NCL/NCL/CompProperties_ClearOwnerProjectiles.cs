using System.Collections.Generic;
using RimWorld;
using Verse;

namespace NCL
{
    public class CompProperties_ClearOwnerProjectiles : CompProperties
    {
        public CompProperties_ClearOwnerProjectiles()
        {
            compClass = typeof(CompClearOwnerProjectiles);
        }
    }

    public class CompClearOwnerProjectiles : ThingComp
    {
        private bool projectilesCleared = false;

        public override void CompTick()
        {
            base.CompTick();

            // 如果实体已销毁且投射物尚未清除
            if (parent.Destroyed && !projectilesCleared)
            {
                ClearOwnerProjectiles();
                projectilesCleared = true;
            }
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);

            // 确保在实体被销毁时清除投射物
            if (!projectilesCleared)
            {
                ClearOwnerProjectiles();
                projectilesCleared = true;
            }
        }

        private void ClearOwnerProjectiles()
        {
            // 获取实体所在的地图
            Map map = parent.MapHeld;
            if (map == null) return;

            // 获取地图上所有投射物
            List<Thing> projectiles = map.listerThings.ThingsInGroup(ThingRequestGroup.Projectile);

            // 使用反向遍历安全移除元素
            for (int i = projectiles.Count - 1; i >= 0; i--)
            {
                Thing thing = projectiles[i];
                if (thing is Projectile projectile)
                {
                    // 检查所有者关系
                    if (projectile.Launcher == parent)
                    {
                        try
                        {
                            // 使用安全方法销毁投射物
                            SafeDestroyProjectile(projectile);
                        }
                        catch (System.Exception ex)
                        {
                            Log.Warning($"Failed to safely destroy projectile {projectile}: {ex.Message}");
                        }
                    }
                }
            }
        }

        private void SafeDestroyProjectile(Projectile projectile)
        {
            // 检查投射物是否仍然存在
            if (projectile.DestroyedOrNull() || !projectile.Spawned)
                return;

            // 检查投射物是否已经被标记为销毁
            if (projectile.Map == null)
                return;

            // 尝试移除投射物
            if (!projectile.Destroyed)
            {
                projectile.DeSpawn();

            }
        }
    }
}
