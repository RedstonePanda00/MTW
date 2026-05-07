using RimWorld;
using Verse;
using System.Collections.Generic;

namespace NCL
{
    // Hediff 属性配置
    public class HediffCompProperties_ExplodeOnRemove : HediffCompProperties
    {
        public string damageDefName; // 先用字符串存储Def名称
        public DamageDef damageDef;  // 不要直接赋默认值
        public int damageAmount = 50;
        public float explosionRadius = 3f;
        public bool affectFriendly = false;

        public HediffCompProperties_ExplodeOnRemove()
        {
            compClass = typeof(HediffComp_ExplodeOnRemove);
        }
    }

    // Hediff 组件实现
    public class HediffComp_ExplodeOnRemove : HediffComp
    {
        public HediffCompProperties_ExplodeOnRemove Props =>
            (HediffCompProperties_ExplodeOnRemove)this.props;

        // 在Hediff被完全移除后触发
        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();
            TryDoExplosion();
        }

        private void TryDoExplosion()
        {
            Pawn pawn = parent.pawn;

            // 严格的安全检查
            if (pawn == null ||
                pawn.Destroyed ||
                pawn.Map == null ||
                !pawn.Spawned ||
                !pawn.Position.IsValid)
                return;


            // 精确复制你原有技能的爆炸参数
            GenExplosion.DoExplosion(
                center: pawn.Position,
                map: pawn.Map,
                radius: Props.explosionRadius,
                damType: Props.damageDef,
                instigator: pawn,
                damAmount: Props.damageAmount,
                armorPenetration: -1f,
                explosionSound: null,
                weapon: null,
                projectile: null,
                intendedTarget: null,
                postExplosionSpawnThingDef: null,
                postExplosionSpawnChance: 0f,
                postExplosionSpawnThingCount: 1,
                postExplosionGasType: null,
                applyDamageToExplosionCellsNeighbors: false,
                preExplosionSpawnThingDef: null,
                preExplosionSpawnChance: 0f,
                preExplosionSpawnThingCount: 1,
                chanceToStartFire: 0f,
                damageFalloff: false,
                direction: null,
                affectedAngle: null,
                doVisualEffects: true,
                propagationSpeed: 1f,
                excludeRadius: 0f,
                doSoundEffects: true,
                postExplosionSpawnThingDefWater: null,
                screenShakeFactor: 1f,
                flammabilityChanceCurve: null,
                overrideCells: null
            );
        }
    }
}