using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace NCL
{
    public class Thing_GunshipCrashFaller : ThingWithComps, IThingHolder
    {
        private const int RotCycleTicks = 10;
        private const float SwayAmplitude = 0.42f;

        private ThingOwner innerContainer;
        private ThingDef wreckageDef;
        private int ticksLeft = 150;
        private int ticksTotal = 150;
        private float startOffsetZ = 1.8f;
        private float startAltitudeY = 0.08f;
        private string bodyTexPath = "Races/MechGunship/MechGunship";
        private Vector2 bodyDrawSize = new Vector2(4.6f, 4.6f);
        private Rot4 drawRot = Rot4.South;
        private int rotCycleTick;
        private Graphic cachedGraphic;
        private bool impactDone;
        private float explosionRadius = 8.9f;
        private int explosionDamage = 55;
        private DamageDef explosionDamageDef;
        private IntVec3 deathCell = IntVec3.Invalid;
        private IntVec3 impactCell = IntVec3.Invalid;
        private Rot4 wreckageRot = Rot4.South;
        private int passengerDamage;

        public Thing_GunshipCrashFaller()
        {
            // Not oneStackOnly: the bay rides down alongside the corpse.
            innerContainer = new ThingOwner<Thing>(this, oneStackOnly: false, LookMode.Deep);
        }

        public void Configure(
            Corpse corpse,
            ThingDef wreckage,
            int crashTicks,
            float hoverOffsetZ,
            float hoverAltitudeY,
            string texPath,
            Vector2 drawSize,
            IntVec3 crashDeathCell,
            IntVec3 crashImpactCell,
            float crashExplosionRadius = 8.9f,
            int crashExplosionDamage = 55,
            DamageDef crashExplosionDamageDef = null,
            Rot4 wreckageRotation = default,
            int crashPassengerDamage = 0)
        {
            wreckageDef = wreckage;
            wreckageRot = wreckageRotation.IsValid ? wreckageRotation : Rot4.South;
            passengerDamage = Mathf.Max(0, crashPassengerDamage);
            ticksTotal = Mathf.Max(1, crashTicks);
            ticksLeft = ticksTotal;
            startOffsetZ = hoverOffsetZ;
            startAltitudeY = hoverAltitudeY;
            deathCell = crashDeathCell;
            impactCell = crashImpactCell.IsValid ? crashImpactCell : crashDeathCell;
            explosionRadius = Mathf.Max(0.1f, crashExplosionRadius);
            explosionDamage = Mathf.Max(1, crashExplosionDamage);
            explosionDamageDef = crashExplosionDamageDef ?? DamageDefOf.Bomb;
            if (!texPath.NullOrEmpty())
            {
                bodyTexPath = texPath;
            }

            if (drawSize.x > 0.01f && drawSize.y > 0.01f)
            {
                bodyDrawSize = drawSize;
            }

            cachedGraphic = null;
            if (corpse != null)
            {
                AcceptCorpse(corpse);
            }
        }

        public void AcceptCorpse(Corpse corpse)
        {
            if (corpse == null || corpse.Destroyed)
            {
                return;
            }

            if (corpse.Spawned)
            {
                corpse.DeSpawn(DestroyMode.Vanish);
            }

            innerContainer.TryAdd(corpse);
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            cachedGraphic = null;
        }

        protected override void Tick()
        {
            base.Tick();
            if (!Spawned || impactDone)
            {
                return;
            }

            rotCycleTick++;
            if (rotCycleTick >= RotCycleTicks)
            {
                rotCycleTick = 0;
                AdvanceDrawRotation();
            }

            ticksLeft--;
            if (ticksLeft <= 0)
            {
                Impact();
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            Graphic bodyGraphic = BodyGraphic;
            if (bodyGraphic == null)
            {
                return;
            }

            bodyGraphic.Draw(drawLoc, drawRot, this);
        }

        public override Vector3 DrawPos
        {
            get
            {
                IntVec3 from = deathCell.IsValid ? deathCell : Position;
                IntVec3 to = impactCell.IsValid ? impactCell : Position;
                float progress = 1f - ((float)ticksLeft / ticksTotal);
                progress = Mathf.Clamp01(progress);
                // Ease toward the impact cell so the wreck does not appear to fall in place.
                float travelT = progress * progress;
                Vector3 pos = Vector3.Lerp(from.ToVector3Shifted(), to.ToVector3Shifted(), travelT);
                float height = Mathf.Lerp(startOffsetZ, 0f, progress);
                float altitude = Mathf.Lerp(startAltitudeY, 0f, progress);
                float sway = Mathf.Sin(Find.TickManager.TicksGame * 0.32f) * SwayAmplitude * (1f - progress * 0.55f);
                pos.x += sway;
                pos.y = AltitudeLayer.MoteOverhead.AltitudeFor() + altitude;
                pos.z += height;
                return pos;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref innerContainer, "gunshipCrashInner", this);
            Scribe_Defs.Look(ref wreckageDef, "gunshipCrashWreckageDef");
            Scribe_Values.Look(ref ticksLeft, "gunshipCrashTicksLeft", 150);
            Scribe_Values.Look(ref ticksTotal, "gunshipCrashTicksTotal", 150);
            Scribe_Values.Look(ref startOffsetZ, "gunshipCrashStartOffsetZ", 1.8f);
            Scribe_Values.Look(ref startAltitudeY, "gunshipCrashStartAltitudeY", 0.08f);
            Scribe_Values.Look(ref bodyTexPath, "gunshipCrashTexPath", "Races/MechGunship/MechGunship");
            Scribe_Values.Look(ref bodyDrawSize, "gunshipCrashDrawSize", new Vector2(4.6f, 4.6f));
            Scribe_Values.Look(ref drawRot, "gunshipCrashDrawRot", Rot4.South);
            Scribe_Values.Look(ref rotCycleTick, "gunshipCrashRotCycleTick", 0);
            Scribe_Values.Look(ref impactDone, "gunshipCrashImpactDone", false);
            Scribe_Values.Look(ref explosionRadius, "gunshipCrashExplosionRadius", 8.9f);
            Scribe_Values.Look(ref explosionDamage, "gunshipCrashExplosionDamage", 55);
            Scribe_Defs.Look(ref explosionDamageDef, "gunshipCrashExplosionDamageDef");
            Scribe_Values.Look(ref deathCell, "gunshipCrashDeathCell", IntVec3.Invalid);
            Scribe_Values.Look(ref impactCell, "gunshipCrashImpactCell", IntVec3.Invalid);
            Scribe_Values.Look(ref wreckageRot, "gunshipCrashWreckageRot", Rot4.South);
            Scribe_Values.Look(ref passengerDamage, "gunshipCrashPassengerDamage", 0);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (innerContainer == null)
                {
                    innerContainer = new ThingOwner<Thing>(this, oneStackOnly: false, LookMode.Deep);
                }

                if (explosionDamageDef == null)
                {
                    explosionDamageDef = DamageDefOf.Bomb;
                }

                if (!impactCell.IsValid && Spawned)
                {
                    impactCell = Position;
                }

                if (!deathCell.IsValid)
                {
                    deathCell = impactCell;
                }

                cachedGraphic = null;
            }
        }

        public ThingOwner GetDirectlyHeldThings()
        {
            return innerContainer;
        }

        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
        }

        private Graphic BodyGraphic
        {
            get
            {
                if (cachedGraphic == null && !bodyTexPath.NullOrEmpty())
                {
                    cachedGraphic = GraphicDatabase.Get<Graphic_Multi>(
                        bodyTexPath,
                        ShaderDatabase.Cutout,
                        bodyDrawSize,
                        Color.white);
                }

                return cachedGraphic;
            }
        }

        private void AdvanceDrawRotation()
        {
            if (drawRot == Rot4.South)
            {
                drawRot = Rot4.East;
            }
            else if (drawRot == Rot4.East)
            {
                drawRot = Rot4.North;
            }
            else if (drawRot == Rot4.North)
            {
                drawRot = Rot4.West;
            }
            else
            {
                drawRot = Rot4.South;
            }
        }

        private void Impact()
        {
            // Only once: wreckage spawns after the fall animation finishes.
            if (impactDone)
            {
                return;
            }

            impactDone = true;

            Map map = Map;
            IntVec3 cell = impactCell.IsValid ? impactCell : Position;
            if (map == null)
            {
                DestroyFaller();
                return;
            }

            Corpse corpse = null;
            for (int i = innerContainer.Count - 1; i >= 0; i--)
            {
                if (innerContainer[i] is Corpse found)
                {
                    corpse = found;
                    break;
                }
            }

            // Everything else in the bay is a passenger (possibly another loaded gunship carrying its
            // own bay). Take them out of the faller before it is destroyed so they can be placed
            // beside the wreck once the explosion is over.
            List<Pawn> passengers = new List<Pawn>();
            for (int i = innerContainer.Count - 1; i >= 0; i--)
            {
                if (innerContainer[i] is Pawn passenger)
                {
                    innerContainer.Remove(passenger);
                    passengers.Add(passenger);
                }
            }

            ThingDef spawnDef = wreckageDef;
            if (spawnDef == null)
            {
                if (corpse != null)
                {
                    innerContainer.Remove(corpse);
                    GenPlace.TryPlaceThing(corpse, cell, map, ThingPlaceMode.Near);
                }

                DestroyFaller();
                ReleasePassengers(passengers, cell, map);
                return;
            }

            Building_GunshipWreckage wreck = (Building_GunshipWreckage)ThingMaker.MakeThing(spawnDef);
            wreck.Rotation = spawnDef.rotatable ? wreckageRot : Rot4.North;
            if (corpse != null)
            {
                innerContainer.Remove(corpse);
                wreck.AcceptCorpse(corpse);
            }

            Thing instigator = corpse?.InnerPawn;
            DestroyFaller();

            GenExplosion.DoExplosion(
                cell,
                map,
                explosionRadius,
                explosionDamageDef ?? DamageDefOf.Bomb,
                instigator,
                explosionDamage,
                -1f,
                null,
                null,
                null,
                null,
                null,
                0f,
                1,
                null,
                null,
                255,
                false,
                null,
                0f,
                1,
                0.15f,
                true,
                null,
                null,
                null,
                true,
                1f,
                0f,
                true,
                null,
                1.4f);

            if (!GenPlace.TryPlaceThing(wreck, cell, map, ThingPlaceMode.Near))
            {
                wreck.Destroy(DestroyMode.Vanish);
                if (corpse != null && !corpse.Destroyed && !corpse.Spawned)
                {
                    GenPlace.TryPlaceThing(corpse, cell, map, ThingPlaceMode.Near);
                }
            }
            else
            {
                FleckMaker.ThrowDustPuffThick(cell.ToVector3Shifted(), map, 2.2f, new Color(0.45f, 0.45f, 0.45f));
            }

            // After the blast so survivors are not caught in it.
            ReleasePassengers(passengers, cell, map);
        }

        private void ReleasePassengers(List<Pawn> passengers, IntVec3 cell, Map map)
        {
            if (passengers == null || passengers.Count == 0)
            {
                return;
            }

            for (int i = 0; i < passengers.Count; i++)
            {
                Pawn passenger = passengers[i];
                if (passenger == null || passenger.Destroyed)
                {
                    continue;
                }

                if (map == null || !GenPlace.TryPlaceThing(passenger, cell, map, ThingPlaceMode.Near))
                {
                    passenger.Destroy(DestroyMode.Vanish);
                    continue;
                }

                FleckMaker.ThrowDustPuff(passenger.Position.ToVector3Shifted(), map, 1.4f);
                if (passengerDamage > 0)
                {
                    passenger.TakeDamage(new DamageInfo(DamageDefOf.Blunt, passengerDamage));
                }
            }
        }

        private void DestroyFaller()
        {
            if (Destroyed)
            {
                return;
            }

            if (Spawned)
            {
                DeSpawn(DestroyMode.Vanish);
            }

            if (!Destroyed)
            {
                Destroy(DestroyMode.Vanish);
            }
        }
    }
}
