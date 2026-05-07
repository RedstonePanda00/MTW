using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace NCL
{
    public class CompProperties_AbilityCauseExplosion : CompProperties_AbilityEffect
    {
        public CompProperties_AbilityCauseExplosion()
        {
            this.compClass = typeof(CompAbilityEffect_Explosion);
        }

        public DamageDef damageDef;
        public int damageAmount;
        public float explosionRadius;

        // 新增可配置参数（可选）
        public float chanceToStartFire = 0f;
        public bool damageFalloff = false;
        public ThingDef preExplosionSpawnThingDef = null;
        public float preExplosionSpawnChance = 0f;
        public int preExplosionSpawnThingCount = 1;
    }

    public class CompAbilityEffect_Explosion : CompAbilityEffect
    {
        public new CompProperties_AbilityCauseExplosion Props =>
            (CompProperties_AbilityCauseExplosion)this.props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            GenExplosion.DoExplosion(
                center: target.Cell,
                map: this.parent.ConstantCaster.Map,
                radius: Props.explosionRadius,
                damType: Props.damageDef,
                instigator: this.parent.ConstantCaster,
                damAmount: Props.damageAmount,
                armorPenetration: -1f, // 使用damageDef默认值
                explosionSound: null,
                weapon: null,
                projectile: null,
                intendedTarget: null,
                postExplosionSpawnThingDef: null,
                postExplosionSpawnChance: 0f,
                postExplosionSpawnThingCount: 1,
                postExplosionGasType: null,
                applyDamageToExplosionCellsNeighbors: false,
                preExplosionSpawnThingDef: Props.preExplosionSpawnThingDef,
                preExplosionSpawnChance: Props.preExplosionSpawnChance,
                preExplosionSpawnThingCount: Props.preExplosionSpawnThingCount,
                chanceToStartFire: Props.chanceToStartFire,
                damageFalloff: Props.damageFalloff,
                direction: null,
                ignoredThings: new List<Thing> { this.parent.ConstantCaster },
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
 