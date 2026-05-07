using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace NCL
{

    public class CompProperties_Detonate : CompProperties_AbilityEffect
    {

        public CompProperties_Detonate()
        {
            this.compClass = typeof(CompDetonate);
        }


        public float radius;


        public DamageDef damageType;

        public int damageAmount = -1;

        public float damagePenetration = -1f;

        public SoundDef soundCreated = null;

        public ThingDef thingCreated = null;

        public float thingCreatedChance = 0f;

        public float chanceToStartFire = 0f;

        public bool damageUser = true;

        public bool killUser = false;
    }
}



namespace NCL
{
    internal class CompDetonate : CompAbilityEffect
    {
        public new CompProperties_Detonate Props => (CompProperties_Detonate)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Detonate();
        }

        public void Detonate()
        {
            List<Thing> ignoredThings = new List<Thing>();
            if (!Props.damageUser)
            {
                ignoredThings.Add(parent.pawn);
            }

            GenExplosion.DoExplosion(
                center: parent.pawn.Position,
                map: parent.pawn.Map,
                radius: Props.radius,
                damType: Props.damageType,
                instigator: parent.pawn,
                damAmount: Props.damageAmount,
                armorPenetration: Props.damagePenetration,
                explosionSound: Props.soundCreated,
                weapon: null,
                projectile: null,
                intendedTarget: null,
                postExplosionSpawnThingDef: Props.thingCreated,
                postExplosionSpawnChance: Props.thingCreatedChance,
                postExplosionSpawnThingCount: 1,
                postExplosionGasType: null,
                applyDamageToExplosionCellsNeighbors: false,
                chanceToStartFire: Props.chanceToStartFire,
                damageFalloff: false,
                ignoredThings: ignoredThings,
                doVisualEffects: true
            );

            if (Props.killUser)
            {
                parent.pawn.Kill(null, null);
            }
        }
    }
}

