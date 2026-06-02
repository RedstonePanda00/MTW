using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace NCL
{
    public class Comp_DiverSentryAmmunition : ThingComp
    {
        private int ammoRemaining;

        private bool pendingDepletion;

        public CompProperties_DiverSentryAmmunition Props => (CompProperties_DiverSentryAmmunition)props;

        public int AmmoRemaining => ammoRemaining;

        public float AmmoPercent => Props.ammoCapacity <= 0 ? 0f : ammoRemaining / (float)Props.ammoCapacity;

        public bool HasAmmo => ammoRemaining > 0;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (!respawningAfterLoad)
            {
                ammoRemaining = Props.ammoCapacity;
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref ammoRemaining, "ammoRemaining", Props.ammoCapacity);
            Scribe_Values.Look(ref pendingDepletion, "pendingDepletion", false);
        }

        public override void CompTick()
        {
            base.CompTick();
            if (pendingDepletion)
            {
                pendingDepletion = false;
                DepleteAndDestroy();
            }
        }

        public override string CompInspectStringExtra()
        {
            string text = Props.ammoLabel + ": " + ammoRemaining + " / " + Props.ammoCapacity;
            if (!HasAmmo && !Props.outOfAmmoMessage.NullOrEmpty())
            {
                text += "\n" + ("CannotShoot".Translate() + ": " + Props.outOfAmmoMessage).Resolve();
            }

            return text;
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (Props.showAmmoGizmo && parent.Faction == Faction.OfPlayer &&
                Find.Selector.SelectedObjects.Count == 1)
            {
                yield return new Gizmo_DiverSentryAmmunition(this);
            }

            if (!DebugSettings.ShowDevGizmos)
            {
                yield break;
            }

            Command_Action setFull = new Command_Action
            {
                defaultLabel = "DEV: Set ammo full",
                action = () => ammoRemaining = Props.ammoCapacity
            };
            yield return setFull;

            Command_Action setEmpty = new Command_Action
            {
                defaultLabel = "DEV: Set ammo empty",
                action = DepleteAndDestroy
            };
            yield return setEmpty;
        }

        public void Notify_ShotFired()
        {
            if (!HasAmmo)
            {
                return;
            }

            ammoRemaining--;
            if (ammoRemaining <= 0)
            {
                ammoRemaining = 0;
                pendingDepletion = true;
            }
        }

        private void DepleteAndDestroy()
        {
            if (!parent.Spawned)
            {
                return;
            }

            PlayDepletionEffect();
            parent.Destroy();
        }

        private void PlayDepletionEffect()
        {
            Map map = parent.Map;
            if (map == null)
            {
                return;
            }

            IntVec3 center = parent.Position;
            float radius = Props.depletionEffectRadius;
            Vector3 drawPos = parent.DrawPos;

            SoundDef explosionSound = SoundDef.Named("Explosion_Bomb");
            ThingDef smokeFilth = DefDatabase<ThingDef>.GetNamedSilentFail("Filth_Smoke");

            GenExplosion.DoExplosion(
                center,
                map,
                radius,
                DamageDefOf.Bomb,
                null,
                damAmount: 0,
                armorPenetration: 0f,
                explosionSound: explosionSound,
                chanceToStartFire: 0f,
                applyDamageToExplosionCellsNeighbors: false,
                postExplosionSpawnThingDef: smokeFilth,
                postExplosionSpawnChance: smokeFilth != null ? 0.65f : 0f,
                doVisualEffects: true,
                doSoundEffects: explosionSound != null);

            for (int i = 0; i < 10; i++)
            {
                Vector3 offset = Rand.InsideUnitCircleVec3 * Rand.Range(0.5f, radius * 0.85f);
                offset.y = 0f;
                FleckMaker.ThrowSmoke(drawPos + offset, map, Rand.Range(1.8f, 3.2f));
            }
        }
    }
}
