using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace NCL
{
    public class AbilityExtension_PlantC4 : DefModExtension
    {
        public int installTicks = 300;
        public int fuseTicks = 180;
        public ThingDef chargeDef;
        public SoundDef plantedSound;
        public SoundDef explosionSound;
        public float explosionFlashScale = 5f;
    }

    // Goes next to the target building, attaches the charge over installTicks and starts the ability
    // cooldown only when the charge is actually attached. Any movement of the planter or loss of
    // adjacency to the target aborts the placement.
    public class JobDriver_PlantC4 : JobDriver
    {
        private IntVec3 installCell = IntVec3.Invalid;

        private Thing Target => job.GetTarget(TargetIndex.A).Thing;

        private AbilityExtension_PlantC4 Extension => job.ability?.def.GetModExtension<AbilityExtension_PlantC4>();

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref installCell, "installCell", IntVec3.Invalid);
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => job.ability == null || Extension?.chargeDef == null);

            if (job.targetB.IsValid && !job.targetB.HasThing)
            {
                yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);
            }
            else
            {
                yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            }

            Toil install = ToilMaker.MakeToil("PlantC4");
            install.initAction = delegate
            {
                pawn.pather.StopDead();
                installCell = pawn.Position;
                if (!pawn.Position.AdjacentTo8WayOrInside(Target))
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                pawn.rotationTracker.FaceTarget(Target);
            };
            install.tickAction = delegate
            {
                pawn.rotationTracker.FaceTarget(Target);
            };
            // endConditions are evaluated before initAction, while installCell is still invalid.
            install.AddFailCondition(() => installCell.IsValid && (pawn.Position != installCell || !pawn.Position.AdjacentTo8WayOrInside(Target)));
            install.handlingFacing = true;
            install.defaultCompleteMode = ToilCompleteMode.Delay;
            install.defaultDuration = Extension?.installTicks ?? 300;
            install.WithProgressBarToilDelay(TargetIndex.A);
            yield return install;

            Toil finish = ToilMaker.MakeToil("PlantC4Finish");
            finish.initAction = delegate
            {
                AbilityExtension_PlantC4 ext = Extension;
                if (PlantedC4Utility.TryPlant(pawn, Target, ext) && job.ability != null)
                {
                    job.ability.StartCooldown(job.ability.def.cooldownTicksRange.RandomInRange);
                }
            };
            finish.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return finish;
        }
    }

    public static class PlantedC4Utility
    {
        public static bool TryPlant(Pawn planter, Thing target, AbilityExtension_PlantC4 ext)
        {
            if (planter?.Map == null || target == null || !target.Spawned || ext?.chargeDef == null)
            {
                return false;
            }

            Thing_PlantedC4 charge = (Thing_PlantedC4)ThingMaker.MakeThing(ext.chargeDef);
            charge.Init(target, ext);
            GenSpawn.Spawn(charge, target.Position, target.Map);
            SoundDef sound = ext.plantedSound ?? SoundDefOf.MetalHitImportant;
            sound?.PlayOneShot(new TargetInfo(target.Position, target.Map));
            return true;
        }

        public static Thing_PlantedC4 ChargeOn(Thing target)
        {
            if (target?.Map == null)
            {
                return null;
            }

            List<Thing> things = target.Position.GetThingList(target.Map);
            foreach (Thing thing in things)
            {
                if (thing is Thing_PlantedC4 charge && charge.Target == target)
                {
                    return charge;
                }
            }

            return null;
        }
    }

    // Draws over the target, ticks the fuse and wipes the target with Destroy() on detonation.
    // The blast is purely cosmetic: nothing else on the map takes damage.
    public class Thing_PlantedC4 : Thing
    {
        private Thing target;
        private int detonateTick = -1;
        private SoundDef explosionSound;
        private float explosionFlashScale = 5f;
        private Sustainer wickSustainer;

        public Thing Target => target;

        public int TicksLeft => Mathf.Max(0, detonateTick - Find.TickManager.TicksGame);

        public override Vector3 DrawPos
        {
            get
            {
                Vector3 pos = target != null && target.Spawned ? target.TrueCenter() : base.DrawPos;
                pos.y = def.Altitude;
                return pos;
            }
        }

        public void Init(Thing target, AbilityExtension_PlantC4 ext)
        {
            this.target = target;
            detonateTick = Find.TickManager.TicksGame + ext.fuseTicks;
            explosionSound = ext.explosionSound;
            explosionFlashScale = ext.explosionFlashScale;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref target, "target");
            Scribe_Values.Look(ref detonateTick, "detonateTick", -1);
            Scribe_Defs.Look(ref explosionSound, "explosionSound");
            Scribe_Values.Look(ref explosionFlashScale, "explosionFlashScale", 5f);
        }

        protected override void Tick()
        {
            base.Tick();
            if (target == null || target.Destroyed || !target.Spawned || target.Map != Map)
            {
                Destroy();
                return;
            }

            if (wickSustainer == null || wickSustainer.Ended)
            {
                wickSustainer = SoundDefOf.HissSmall.TrySpawnSustainer(SoundInfo.InMap(this, MaintenanceType.PerTick));
            }

            wickSustainer?.Maintain();

            if (Find.TickManager.TicksGame >= detonateTick)
            {
                Detonate();
            }
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            if (wickSustainer != null && !wickSustainer.Ended)
            {
                wickSustainer.End();
            }

            wickSustainer = null;
            base.DeSpawn(mode);
        }

        private void Detonate()
        {
            Map map = Map;
            Vector3 center = target.TrueCenter();
            IntVec3 cell = target.Position;

            FleckMaker.Static(center, map, FleckDefOf.ExplosionFlash, explosionFlashScale);
            for (int i = 0; i < 4; i++)
            {
                FleckMaker.ThrowSmoke(center + Gen.RandomHorizontalVector(0.6f), map, Rand.Range(1.2f, 2f));
                FleckMaker.ThrowMicroSparks(center + Gen.RandomHorizontalVector(0.4f), map);
            }

            FleckMaker.ThrowDustPuffThick(center, map, 2.5f, new Color(0.6f, 0.6f, 0.6f));
            SoundDef sound = explosionSound ?? DamageDefOf.Bomb.soundExplosion;
            sound?.PlayOneShot(new TargetInfo(cell, map));

            if (!target.Destroyed)
            {
                target.Destroy();
            }

            if (!Destroyed)
            {
                Destroy();
            }
        }
    }
}
