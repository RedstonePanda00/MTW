using RimWorld;
using Verse;
using System;

namespace NCL
{
    public class CompProperties_ExplodeWhenStunned : CompProperties
    {
        public float explosionRadius = 5f;
        public int explosionDamage = 30;
        public DamageDef damageType;
        public float armorPenetration = 1f;
        public float chanceToStartFire = 0.5f;
        public HediffDef vulnerabilityHediff;
        public float increasedDamageFactor = 1.5f;
        public bool damageFalloff = true;
        public bool applyDamageToExplosionCellsNeighbors = true;
        public float cooldownTicks = 600; // 默认10秒冷却时间(60 ticks/秒)

        public CompProperties_ExplodeWhenStunned()
        {
            compClass = typeof(CompExplodeWhenStunned);
        }
    }

    public class CompExplodeWhenStunned : ThingComp
    {
        private int lastExplosionTick = -9999;

        public CompProperties_ExplodeWhenStunned Props => (CompProperties_ExplodeWhenStunned)props;

        public override void CompTick()
        {
            base.CompTick();

            // 这里使用parent而不是Parent（RimWorld通常使用小写）
            if (parent is Pawn pawn && pawn.stances.stunner.Stunned)
            {
                // 检查冷却时间是否已过
                if (Find.TickManager.TicksGame >= lastExplosionTick + Props.cooldownTicks)
                {
                    // 记录本次触发时间
                    lastExplosionTick = Find.TickManager.TicksGame;

                    // 触发爆炸效果
                    DoExplosion(pawn);

                    // 添加负面效果
                    ApplyVulnerability(pawn);
                }
            }
        }

        private void DoExplosion(Pawn pawn)
        {
            GenExplosion.DoExplosion(
                center: pawn.Position,
                map: pawn.Map,
                radius: Props.explosionRadius,
                damType: Props.damageType,
                instigator: pawn,
                damAmount: Props.explosionDamage,
                armorPenetration: Props.armorPenetration,
                weapon: null,
                projectile: null,
                intendedTarget: null,
                postExplosionSpawnThingDef: null,
                postExplosionSpawnChance: 0f,
                postExplosionSpawnThingCount: 0,
                applyDamageToExplosionCellsNeighbors: Props.applyDamageToExplosionCellsNeighbors,
                preExplosionSpawnThingDef: null,
                preExplosionSpawnChance: 0f,
                preExplosionSpawnThingCount: 0,
                chanceToStartFire: Props.chanceToStartFire,
                damageFalloff: Props.damageFalloff
            );
        }

        private void ApplyVulnerability(Pawn pawn)
        {
            if (Props.vulnerabilityHediff != null)
            {
                // 先移除已有的同类型Hediff（避免叠加）
                Hediff existingHediff = pawn.health.hediffSet.GetFirstHediffOfDef(Props.vulnerabilityHediff);
                if (existingHediff != null)
                {
                    pawn.health.RemoveHediff(existingHediff);
                }

                // 添加新的Hediff
                Hediff hediff = HediffMaker.MakeHediff(Props.vulnerabilityHediff, pawn);
                pawn.health.AddHediff(hediff);
            }
        }

        // 保存和加载冷却时间状态
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref lastExplosionTick, "lastExplosionTick", -9999);
        }
    }
}
