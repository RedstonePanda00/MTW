using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace NCL
{
    public enum GunshipFlightState : byte
    {
        Grounded = 0,
        TakingOff = 1,
        Airborne = 2,
        Landing = 3
    }

    public class CompGunshipFlight : ThingComp
    {
        private static readonly SimpleCurve TakeoffCurve = new SimpleCurve
        {
            new CurvePoint(0f, 0f),
            new CurvePoint(0.5f, 0.6f),
            new CurvePoint(1f, 1f)
        };

        private static readonly SimpleCurve LandingCurve = new SimpleCurve
        {
            new CurvePoint(0f, 1f),
            new CurvePoint(0.5f, 0.4f),
            new CurvePoint(1f, 0f)
        };

        private GunshipFlightState flightState = GunshipFlightState.Grounded;
        private int lerpTick;
        private int thrusterCooldown;
        private readonly Dictionary<int, Vector3> smoothedEnginePos = new Dictionary<int, Vector3>();
        private bool engineKillTriggered;
        private bool crashHandled;
        private Effecter takeoffEffecter;
        private Effecter landingEffecter;

        public CompProperties_GunshipFlight Props => (CompProperties_GunshipFlight)props;

        public Pawn Pawn => parent as Pawn;

        public GunshipFlightState FlightState => flightState;

        public bool IsAirborneVisual =>
            flightState == GunshipFlightState.Airborne
            || flightState == GunshipFlightState.TakingOff
            || flightState == GunshipFlightState.Landing;

        public bool UsesFlyingPathGrid => IsAirborneVisual;

        public bool TurretsAllowed => flightState == GunshipFlightState.Airborne;

        public float PositionOffsetFactor
        {
            get
            {
                switch (flightState)
                {
                    case GunshipFlightState.Airborne:
                        return 1f;
                    case GunshipFlightState.TakingOff:
                        return TakeoffCurve.Evaluate((float)lerpTick / Mathf.Max(1, Props.takeoffTicks));
                    case GunshipFlightState.Landing:
                        return LandingCurve.Evaluate((float)lerpTick / Mathf.Max(1, Props.landingTicks));
                    default:
                        return 0f;
                }
            }
        }

        public Vector3 DrawOffset
        {
            get
            {
                float factor = PositionOffsetFactor;
                if (factor <= 0f)
                {
                    return Vector3.zero;
                }

                // Match vanilla flight draw: Z lifts the sprite on isometric view, Y adjusts altitude layer.
                return new Vector3(0f, Props.hoverAltitudeY * factor, Props.hoverOffsetZ * factor);
            }
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (!respawningAfterLoad && ShouldSpawnAirborne())
            {
                flightState = GunshipFlightState.Airborne;
                lerpTick = 0;
            }

            SyncLandedHediff();
            smoothedEnginePos.Clear();
            EnsurePathCache();
        }

        private bool ShouldSpawnAirborne()
        {
            if (Props.defaultAirborneOnSpawn)
            {
                return true;
            }

            // AI factions get no take-off gizmo, so a grounded spawn would lock their turrets forever.
            return Props.autoAirborneForNonPlayer && parent.Faction != Faction.OfPlayer;
        }

        public override void CompTick()
        {
            base.CompTick();
            Pawn pawn = Pawn;
            if (pawn == null || !pawn.Spawned || pawn.Dead)
            {
                CleanupEffecters();
                return;
            }

            TickFlightState(pawn);
            TickTransitionEffects(pawn);
            TickAutoTakeOff(pawn);
            TickThrusters(pawn);
            SyncLandedHediff();
        }

        public override void PostPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.PostPostApplyDamage(dinfo, totalDamageDealt);
            // Body part scans are far too expensive to run every tick; losing an engine can only
            // happen as a result of damage.
            CheckEngineLethality(Pawn);
        }

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            CleanupEffecters();
            base.PostDeSpawn(map, mode);
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            CleanupEffecters();
            base.PostDestroy(mode, previousMap);
        }

        public override void Notify_Killed(Map prevMap, DamageInfo? dinfo = null)
        {
            base.Notify_Killed(prevMap, dinfo);
            TryBeginAirCrash(prevMap);
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }

            Pawn pawn = Pawn;
            if (pawn == null || !pawn.Spawned || pawn.Dead || pawn.Faction != Faction.OfPlayer)
            {
                yield break;
            }

            if (flightState == GunshipFlightState.Grounded)
            {
                yield return new Command_Action
                {
                    defaultLabel = "MTW.Gunship.TakeOff".Translate(),
                    defaultDesc = "MTW.Gunship.TakeOffDesc".Translate(),
                    action = TryTakeOff,
                    icon = TexCommand.Attack
                };
            }
            else if (flightState == GunshipFlightState.Airborne)
            {
                yield return new Command_Action
                {
                    defaultLabel = "MTW.Gunship.Land".Translate(),
                    defaultDesc = "MTW.Gunship.LandDesc".Translate(),
                    action = TryLand,
                    icon = TexCommand.CannotShoot
                };
            }
        }

        public override string CompInspectStringExtra()
        {
            switch (flightState)
            {
                case GunshipFlightState.Airborne:
                    return "MTW.Gunship.Status.Flying".Translate();
                case GunshipFlightState.TakingOff:
                    return "MTW.Gunship.Status.TakingOff".Translate();
                case GunshipFlightState.Landing:
                    return "MTW.Gunship.Status.Landing".Translate();
                default:
                    return "MTW.Gunship.Status.Landed".Translate();
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref flightState, "gunshipFlightState", GunshipFlightState.Grounded);
            Scribe_Values.Look(ref lerpTick, "gunshipLerpTick", 0);
            Scribe_Values.Look(ref engineKillTriggered, "gunshipEngineKillTriggered", false);
            Scribe_Values.Look(ref crashHandled, "gunshipCrashHandled", false);
        }

        public void TryTakeOff()
        {
            if (flightState != GunshipFlightState.Grounded)
            {
                return;
            }

            flightState = GunshipFlightState.TakingOff;
            lerpTick = 0;
            Pawn?.pather?.StopDead();
            EnsurePathCache();
            BeginTakeoffEffects();
        }

        public void TryLand()
        {
            if (flightState != GunshipFlightState.Airborne && flightState != GunshipFlightState.TakingOff)
            {
                return;
            }

            bool wasTakingOff = flightState == GunshipFlightState.TakingOff;
            flightState = GunshipFlightState.Landing;
            lerpTick = wasTakingOff ? Props.landingTicks / 2 : 0;
            Pawn?.pather?.StopDead();
            CleanupTakeoffEffecter();
            BeginLandingEffects();
        }

        private void TickFlightState(Pawn pawn)
        {
            switch (flightState)
            {
                case GunshipFlightState.TakingOff:
                    lerpTick++;
                    if (lerpTick >= Props.takeoffTicks)
                    {
                        flightState = GunshipFlightState.Airborne;
                        lerpTick = 0;
                        CleanupTakeoffEffecter();
                        pawn.pather?.StopDead();
                    }
                    break;
                case GunshipFlightState.Landing:
                    lerpTick++;
                    if (lerpTick >= Props.landingTicks)
                    {
                        flightState = GunshipFlightState.Grounded;
                        lerpTick = 0;
                        CleanupLandingEffecter();
                        PlayLandingImpact(pawn);
                        pawn.pather?.StopDead();
                        ValidateLandedCell(pawn);
                    }
                    break;
            }
        }

        private void TickTransitionEffects(Pawn pawn)
        {
            TargetInfo target = pawn;
            if (takeoffEffecter != null)
            {
                takeoffEffecter.EffectTick(target, TargetInfo.Invalid);
            }

            if (landingEffecter != null)
            {
                landingEffecter.EffectTick(target, TargetInfo.Invalid);
            }
        }

        private void BeginTakeoffEffects()
        {
            Pawn pawn = Pawn;
            if (pawn?.Map == null)
            {
                return;
            }

            EffecterDef warmup = DefDatabase<EffecterDef>.GetNamedSilentFail("JumpMechWarmupEffect")
                ?? DefDatabase<EffecterDef>.GetNamedSilentFail("JumpWarmupEffect");
            warmup?.SpawnMaintained(pawn, pawn.Map);

            EffecterDef flightFx = DefDatabase<EffecterDef>.GetNamedSilentFail("JumpMechFlightEffect")
                ?? DefDatabase<EffecterDef>.GetNamedSilentFail("JumpFlightEffect");
            if (flightFx != null)
            {
                CleanupTakeoffEffecter();
                takeoffEffecter = flightFx.Spawn();
                takeoffEffecter.Trigger(pawn, TargetInfo.Invalid);
            }

            SoundDef launch = DefDatabase<SoundDef>.GetNamedSilentFail("JumpMechLaunch")
                ?? DefDatabase<SoundDef>.GetNamedSilentFail("JumpPackLaunch");
            launch?.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
        }

        private void BeginLandingEffects()
        {
            Pawn pawn = Pawn;
            if (pawn?.Map == null)
            {
                return;
            }

            EffecterDef flightFx = DefDatabase<EffecterDef>.GetNamedSilentFail("JumpMechFlightEffect")
                ?? DefDatabase<EffecterDef>.GetNamedSilentFail("JumpFlightEffect");
            if (flightFx != null)
            {
                CleanupLandingEffecter();
                landingEffecter = flightFx.Spawn();
                landingEffecter.Trigger(pawn, TargetInfo.Invalid);
            }
        }

        private void PlayLandingImpact(Pawn pawn)
        {
            if (pawn?.Map == null)
            {
                return;
            }

            SoundDef land = DefDatabase<SoundDef>.GetNamedSilentFail("JumpPackLand");
            land?.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));

            FleckDef smoke = DefDatabase<FleckDef>.GetNamedSilentFail("JumpWarmupSmoke")
                ?? DefDatabase<FleckDef>.GetNamedSilentFail("JumpSmoke");
            if (smoke != null)
            {
                for (int i = 0; i < 8; i++)
                {
                    FleckCreationData data = FleckMaker.GetDataStatic(
                        pawn.DrawPos,
                        pawn.Map,
                        smoke,
                        Rand.Range(0.7f, 1.2f));
                    data.velocityAngle = Rand.Range(0f, 360f);
                    data.velocitySpeed = Rand.Range(0.6f, 1.4f);
                    pawn.Map.flecks.CreateFleck(data);
                }
            }
        }

        private void ValidateLandedCell(Pawn pawn)
        {
            if (pawn?.Map == null)
            {
                return;
            }

            if (pawn.Position.WalkableByNormal(pawn.Map))
            {
                return;
            }

            // Widen the search progressively: a gunship that drifted over rock should limp back out
            // rather than evaporate.
            int[] radii = { 8, 16, 30, 50 };
            for (int i = 0; i < radii.Length; i++)
            {
                if (CellFinder.TryRandomClosewalkCellNear(
                        pawn.Position,
                        pawn.Map,
                        radii[i],
                        out IntVec3 safe,
                        c => c.WalkableByNormal(pawn.Map)))
                {
                    pawn.Position = safe;
                    pawn.Notify_Teleported(endCurrentJob: false, resetTweenedPos: true);
                    return;
                }
            }

            // Nowhere to put it down: restore the airborne state so the death runs the full crash
            // sequence instead of making the gunship vanish.
            flightState = GunshipFlightState.Airborne;
            lerpTick = 0;
            pawn.Kill(null, null);
        }

        private void EnsurePathCache()
        {
            Map map = Pawn?.Map;
            if (map == null)
            {
                return;
            }

            if (map.GetComponent<MapComponent_GunshipPath>() == null)
            {
                map.components.Add(new MapComponent_GunshipPath(map));
            }
        }

        private void CleanupEffecters()
        {
            CleanupTakeoffEffecter();
            CleanupLandingEffecter();
            smoothedEnginePos.Clear();
        }

        private void CleanupTakeoffEffecter()
        {
            takeoffEffecter?.Cleanup();
            takeoffEffecter = null;
        }

        private void CleanupLandingEffecter()
        {
            landingEffecter?.Cleanup();
            landingEffecter = null;
        }

        private void TickAutoTakeOff(Pawn pawn)
        {
            if (!Props.autoAirborneForNonPlayer
                || flightState != GunshipFlightState.Grounded
                || pawn.Faction == Faction.OfPlayer
                || !pawn.IsHashIntervalTick(60))
            {
                return;
            }

            TryTakeOff();
        }

        private void CheckEngineLethality(Pawn pawn)
        {
            if (pawn == null || engineKillTriggered || Props.lethalEngineParts == null || pawn.RaceProps?.body == null)
            {
                return;
            }

            List<BodyPartRecord> allParts = pawn.RaceProps.body.AllParts;
            for (int i = 0; i < allParts.Count; i++)
            {
                BodyPartRecord part = allParts[i];
                if (part?.def == null || !Props.lethalEngineParts.Contains(part.def))
                {
                    continue;
                }

                if (pawn.health.hediffSet.PartIsMissing(part))
                {
                    engineKillTriggered = true;
                    pawn.Kill(null, null);
                    return;
                }
            }
        }

        private void TickThrusters(Pawn pawn)
        {
            if (flightState != GunshipFlightState.Airborne
                && flightState != GunshipFlightState.TakingOff
                && flightState != GunshipFlightState.Landing)
            {
                smoothedEnginePos.Clear();
                return;
            }

            if (thrusterCooldown > 0)
            {
                thrusterCooldown--;
                return;
            }

            thrusterCooldown = Props.thrusterFleckInterval;

            // Absolute -Z (south). Matches vanilla JumpFlightEffect absoluteAngle ~180.
            const float thrusterAngleNegZ = 180f;
            SpawnThrusterFlecks(pawn, thrusterAngleNegZ);
        }

        private void SpawnThrusterFlecks(Pawn pawn, float tailAngle)
        {
            Map map = pawn.Map;
            if (map == null)
            {
                return;
            }

            FleckDef flame = DefDatabase<FleckDef>.GetNamedSilentFail("JumpFlameMech")
                ?? DefDatabase<FleckDef>.GetNamedSilentFail("JumpFlame");
            FleckDef glow = DefDatabase<FleckDef>.GetNamedSilentFail("JumpFlameGlowMech")
                ?? DefDatabase<FleckDef>.GetNamedSilentFail("JumpFlameGlow");

            List<IntVec3> cells = Props.engineCellsNorth;
            if (cells == null || cells.Count == 0)
            {
                return;
            }

            CompMultiCellPawn multi = pawn.TryGetComp<CompMultiCellPawn>();
            const float followLerp = 0.45f;
            bool useSideOffsets = (pawn.Rotation == Rot4.East || pawn.Rotation == Rot4.West)
                && Props.engineOffsetsEast != null
                && Props.engineOffsetsEast.Count > 0;
            int engineCount = useSideOffsets ? Props.engineOffsetsEast.Count : cells.Count;

            for (int i = 0; i < engineCount; i++)
            {
                Vector3 targetPos;
                if (useSideOffsets)
                {
                    Vector3 offset = Props.engineOffsetsEast[i];
                    if (pawn.Rotation == Rot4.West)
                    {
                        offset.x = -offset.x;
                    }

                    targetPos = pawn.DrawPos + offset;
                }
                else
                {
                    IntVec3 localNorth = cells[i];
                    targetPos = multi != null
                        ? multi.GetSmoothDrawPosForLocalCell(localNorth)
                        : (pawn.Position + localNorth.RotatedBy(pawn.Rotation)).ToVector3Shifted() + DrawOffset;
                }

                if (!smoothedEnginePos.TryGetValue(i, out Vector3 smoothed))
                {
                    smoothed = targetPos;
                }
                else
                {
                    smoothed = Vector3.Lerp(smoothed, targetPos, followLerp);
                }

                smoothedEnginePos[i] = smoothed;
                Vector3 pos = smoothed;
                pos.y = AltitudeLayer.MoteOverhead.AltitudeFor();

                if (flame != null)
                {
                    FleckCreationData flameData = FleckMaker.GetDataStatic(pos, map, flame, Rand.Range(0.65f, 0.95f));
                    flameData.velocityAngle = tailAngle + Rand.Range(-6f, 6f);
                    flameData.velocitySpeed = Rand.Range(8f, 11f);
                    map.flecks.CreateFleck(flameData);
                }

                if (glow != null)
                {
                    FleckCreationData glowData = FleckMaker.GetDataStatic(pos, map, glow, Rand.Range(0.9f, 1.25f));
                    glowData.velocityAngle = tailAngle + Rand.Range(-10f, 10f);
                    glowData.velocitySpeed = Rand.Range(7.5f, 10.5f);
                    map.flecks.CreateFleck(glowData);
                }
            }
        }

        private void SyncLandedHediff()
        {
            Pawn pawn = Pawn;
            if (pawn?.health?.hediffSet == null || Props.landedHediff == null)
            {
                return;
            }

            bool wantLanded = flightState == GunshipFlightState.Grounded;
            Hediff existing = pawn.health.hediffSet.GetFirstHediffOfDef(Props.landedHediff);
            if (wantLanded)
            {
                if (existing == null)
                {
                    pawn.health.AddHediff(Props.landedHediff);
                }
            }
            else if (existing != null)
            {
                pawn.health.RemoveHediff(existing);
            }
        }

        private void TryBeginAirCrash(Map map)
        {
            if (crashHandled || !IsAirborneVisual || map == null)
            {
                return;
            }

            if (Props.crashFallerDef == null || Props.wreckageDef == null)
            {
                return;
            }

            Pawn pawn = Pawn;
            Corpse corpse = pawn?.Corpse;
            if (corpse == null || corpse.Destroyed || !corpse.Spawned)
            {
                return;
            }

            crashHandled = true;

            IntVec3 deathCell = corpse.Position;
            // Despawn corpse first so it does not block landing-cell validation.
            if (corpse.Spawned)
            {
                corpse.DeSpawn(DestroyMode.Vanish);
            }

            IntVec3 impactCell = FindCrashLandingCell(deathCell, map, Props.crashLandingSearchRadius);
            string texPath = ResolveBodyTexPath(pawn);
            Vector2 drawSize = ResolveBodyDrawSize(pawn);
            ThingDef selectedWreckageDef = Props.wreckageDef;
            if (Props.ancientWreckageDef != null && texPath.Contains("MechGunshipAncient"))
            {
                selectedWreckageDef = Props.ancientWreckageDef;
            }

            float heightFactor = PositionOffsetFactor;
            float hoverZ = Props.hoverOffsetZ * heightFactor;
            float hoverY = Props.hoverAltitudeY * heightFactor;

            Thing_GunshipCrashFaller faller = (Thing_GunshipCrashFaller)ThingMaker.MakeThing(Props.crashFallerDef);
            faller.Configure(
                null,
                selectedWreckageDef,
                Props.crashTicks,
                hoverZ,
                hoverY,
                texPath,
                drawSize,
                deathCell,
                impactCell,
                Props.crashExplosionRadius,
                Props.crashExplosionDamage,
                Props.crashExplosionDamageDef ?? DamageDefOf.Bomb,
                pawn?.Rotation ?? Rot4.South,
                Props.crashPassengerDamage);

            CompGunshipCargo cargo = GunshipDefCache.GetCargo(pawn);

            // Keep logical Position at death cell; DrawPos lerps toward impactCell.
            if (!GenPlace.TryPlaceThing(faller, deathCell, map, ThingPlaceMode.Direct)
                && !GenPlace.TryPlaceThing(faller, deathCell, map, ThingPlaceMode.Near))
            {
                GenPlace.TryPlaceThing(corpse, deathCell, map, ThingPlaceMode.Near);
                if (!faller.Destroyed)
                {
                    faller.Destroy(DestroyMode.Vanish);
                }

                cargo?.DropAllPassengers(deathCell, map);
                return;
            }

            faller.AcceptCorpse(corpse);
            // Passengers ride the wreck down and are ejected once the animation finishes.
            cargo?.TransferAllTo(faller.GetDirectlyHeldThings());
        }

        private static IntVec3 FindCrashLandingCell(IntVec3 deathCell, Map map, float radius)
        {
            List<IntVec3> candidates = new List<IntVec3>();
            float searchRadius = Mathf.Max(1f, radius);

            // Exclude the death cell itself: pick a valid cell within the radius ring.
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(deathCell, searchRadius, useCenter: false))
            {
                if (!cell.InBounds(map) || !IsValidCrashLandingCell(cell, map))
                {
                    continue;
                }

                candidates.Add(cell);
            }

            if (candidates.Count > 0)
            {
                return candidates.RandomElement();
            }

            if (CellFinder.TryRandomClosewalkCellNear(
                    deathCell,
                    map,
                    Mathf.CeilToInt(searchRadius),
                    out IntVec3 fallback,
                    c => c != deathCell && IsValidCrashLandingCell(c, map)))
            {
                return fallback;
            }

            if (CellFinder.TryRandomClosewalkCellNear(
                    deathCell,
                    map,
                    Mathf.CeilToInt(searchRadius),
                    out IntVec3 anyWalkable))
            {
                return anyWalkable;
            }

            return deathCell;
        }

        private static bool IsValidCrashLandingCell(IntVec3 cell, Map map)
        {
            if (!cell.InBounds(map) || cell.Fogged(map))
            {
                return false;
            }

            // Single-cell check for the faller destination; wreckage uses GenPlace.Near on impact.
            if (!cell.Standable(map) && !cell.Walkable(map))
            {
                return false;
            }

            Building edifice = cell.GetEdifice(map);
            if (edifice != null && edifice.def.passability == Traversability.Impassable)
            {
                return false;
            }

            return true;
        }

        private static string ResolveBodyTexPath(Pawn pawn)
        {
            GraphicData data = pawn?.ageTracker?.CurKindLifeStage?.bodyGraphicData;
            if (data != null && !data.texPath.NullOrEmpty())
            {
                return data.texPath;
            }

            return "Races/MechGunship/MechGunship";
        }

        private static Vector2 ResolveBodyDrawSize(Pawn pawn)
        {
            GraphicData data = pawn?.ageTracker?.CurKindLifeStage?.bodyGraphicData;
            if (data != null)
            {
                return data.drawSize;
            }

            return new Vector2(4.6f, 4.6f);
        }
    }
}
