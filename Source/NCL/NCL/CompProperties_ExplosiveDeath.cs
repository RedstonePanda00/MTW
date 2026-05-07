using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace NCL
{
    public class CompProperties_FragmentedExplosive : CompProperties
    {
        public float explosionRadius = 5f;
        public int fragmentCount = 10; // 碎片数量
        public float guaranteedCenterFraction = 0.5f; // 落在中心的碎片比例
        public ThingDef fragmentProjectileDef;
        public float fireGlowSize = 3.5f;
        public float smokeSize = 5.5f;
        public float heatGlowSize = 3.5f;

        public CompProperties_FragmentedExplosive()
        {
            compClass = typeof(Comp_FragmentedExplosive);
        }
    }

    public class Comp_FragmentedExplosive : ThingComp
    {
        private CompProperties_FragmentedExplosive Props => (CompProperties_FragmentedExplosive)props;

        // 存储原始发射者
        private Thing originalLauncher;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.Look(ref originalLauncher, "originalLauncher");
        }

        // 在子弹生成时捕获发射者
        public override void PostPostMake()
        {
            base.PostPostMake();

            // 如果是子弹，尝试从父级获取发射者
            if (parent is Projectile projectile)
            {
                originalLauncher = projectile.Launcher;
            }
        }

        // 公开的爆炸方法
        public void TriggerExplosion()
        {
            if (parent.Map != null)
            {
                Detonate(parent.Map);
            }
        }

        // 公开的销毁方法
        public void TriggerExplosion(Map map)
        {
            if (map != null)
            {
                Detonate(map);
            }
        }

        private void TryTriggerByDamage(DamageInfo dinfo)
        {
            if (parent.HitPoints <= 0)
            {
                TriggerExplosion();
            }
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);
            TriggerExplosion(previousMap);
        }

        // 改为受保护的访问级别
        protected void Detonate(Map map)
        {
            if (map == null) return;

            // 创建视觉效果
            Vector3 position = parent.DrawPos;
            FleckMaker.ThrowFireGlow(position, map, Props.fireGlowSize);
            FleckMaker.ThrowSmoke(position, map, Props.smokeSize);
            FleckMaker.ThrowHeatGlow(parent.Position, map, Props.heatGlowSize);

            // 生成碎片
            CreateFragments(map);
        }

        private void CreateFragments(Map map)
        {
            IntVec3 center = parent.Position;
            float radius = Props.explosionRadius;

            ThingDef fragmentDef = GetFragmentProjectileDef();
            if (fragmentDef == null) return;

            // 使用原始发射者
            Thing launcher = originalLauncher;

            // 计算保证落在中心的碎片数量
            int guaranteedCenterCount = Mathf.CeilToInt(Props.fragmentCount * Props.guaranteedCenterFraction);
            int randomCount = Props.fragmentCount - guaranteedCenterCount;

            // 生成保证落在中心的碎片
            for (int i = 0; i < guaranteedCenterCount; i++)
            {
                SpawnAndLaunchFragment(map, center, center, launcher, fragmentDef);
            }

            // 生成随机位置的碎片
            if (radius > 0 && randomCount > 0)
            {
                List<IntVec3> targetCells = GetTargetCells(center, radius, map);

                for (int i = 0; i < randomCount; i++)
                {
                    IntVec3 targetCell = targetCells.Count > 0 ?
                        targetCells.RandomElement() :
                        center;

                    SpawnAndLaunchFragment(map, center, targetCell, launcher, fragmentDef);
                }
            }
        }

        private List<IntVec3> GetTargetCells(IntVec3 center, float radius, Map map)
        {
            List<IntVec3> targetCells = new List<IntVec3>();

            // 添加爆炸半径内的所有有效单元格
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, radius, true))
            {
                if (cell.InBounds(map))
                {
                    targetCells.Add(cell);
                }
            }

            return targetCells;
        }

        private void SpawnAndLaunchFragment(Map map, IntVec3 center, IntVec3 target, Thing launcher, ThingDef fragmentDef)
        {
            Projectile projectile = (Projectile)ThingMaker.MakeThing(fragmentDef);
            if (projectile == null) return;

            GenSpawn.Spawn(projectile, center, map);
            LaunchFragment(projectile, center, target, launcher);
        }

        private void LaunchFragment(Projectile projectile, IntVec3 origin, IntVec3 target, Thing launcher)
        {
            // 如果原始发射者不可用，使用安全的替代方案
            if (launcher == null || launcher.Destroyed)
            {
                // 使用父物体作为发射者
                projectile.Launch(
                    launcher: parent,
                    origin: origin.ToVector3Shifted(),
                    usedTarget: new LocalTargetInfo(target),
                    intendedTarget: new LocalTargetInfo(target),
                    hitFlags: ProjectileHitFlags.All,
                    preventFriendlyFire: false,
                    equipment: null,
                    targetCoverDef: null
                );
            }
            else
            {
                // 使用原始发射者
                projectile.Launch(
                    launcher: launcher,
                    origin: origin.ToVector3Shifted(),
                    usedTarget: new LocalTargetInfo(target),
                    intendedTarget: new LocalTargetInfo(target),
                    hitFlags: ProjectileHitFlags.All,
                    preventFriendlyFire: false,
                    equipment: null,
                    targetCoverDef: null
                );
            }
        }

        private ThingDef GetFragmentProjectileDef()
        {
            return Props.fragmentProjectileDef ??
                DefDatabase<ThingDef>.GetNamedSilentFail("Bullet_NCL");
        }
    }

    // 自定义子弹类
    public class Projectile_Fragmented : Projectile
    {
        private Comp_FragmentedExplosive fragComp;

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            fragComp = this.TryGetComp<Comp_FragmentedExplosive>();
        }

        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            base.Impact(hitThing, blockedByShield);

            // 触发爆炸组件
            if (fragComp != null)
            {
                fragComp.TriggerExplosion(base.Map);
            }
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            // 确保在销毁前触发爆炸
            if (fragComp != null && !this.Destroyed)
            {
                fragComp.TriggerExplosion(base.Map);
            }
            base.Destroy(mode);
        }
    }
}
