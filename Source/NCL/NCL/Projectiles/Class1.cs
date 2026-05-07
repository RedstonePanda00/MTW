using RimWorld;
using RimWorld.Planet;
using System;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine;
using Verse;
using Verse;
using Verse.AI;
using Verse.Sound;


namespace NCL.Projectiles
{
    // Token: 0x02000054 RID: 84
    public class Projectile_ExplosiveStagedWithEffects : Projectile_ExplosiveWithEffects
    {
        // Token: 0x1700002A RID: 42
        // (get) Token: 0x060001D2 RID: 466 RVA: 0x0000CD2F File Offset: 0x0000AF2F
        protected int TicksSinceLaunch
        {
            get
            {
                return this.calculatedRuntimeTicks - this.ticksToImpact;
            }
        }

        // Token: 0x1700002B RID: 43
        // (get) Token: 0x060001D3 RID: 467 RVA: 0x0000CD40 File Offset: 0x0000AF40
        public override string Label
        {
            get
            {
                ProjectileStagingTracker projectileStagingTracker = this.staging;
                if (((projectileStagingTracker != null) ? projectileStagingTracker.stageConfig : null) != null)
                {
                    return this.LabelNoCount + " (" + this.staging.stageConfig.label + ")";
                }
                return base.Label;
            }
        }

        // Token: 0x060001D4 RID: 468 RVA: 0x0000CD8D File Offset: 0x0000AF8D
        public override void PostMake()
        {
            base.PostMake();
            this.staging = new ProjectileStagingTracker(this);
        }

        // Token: 0x060001D5 RID: 469 RVA: 0x0000CDA1 File Offset: 0x0000AFA1
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            this.staging.PostSpawnSetup(map, respawningAfterLoad, this.effectsExtension, this.TicksSinceLaunch);
        }

        // Token: 0x060001D6 RID: 470 RVA: 0x0000CDC4 File Offset: 0x0000AFC4
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look<ProjectileStagingTracker>(ref this.staging, "stagingTracker", new object[]
            {
                this
            });
            Scribe_Values.Look<int>(ref this.calculatedRuntimeTicks, "calculatedRuntimeTicks", 0, false);
        }

        // Token: 0x060001D7 RID: 471 RVA: 0x0000CDF8 File Offset: 0x0000AFF8
        protected override void TickInterval(int delta)
        {
            this.staging.PreTick(this.effectsExtension, this.effects, this.TicksSinceLaunch, this.activeTracking);
            Map map = base.Map;
            base.TickInterval(delta);
            this.staging.Tick(map, this.effectsExtension);
        }

        // Token: 0x060001D8 RID: 472 RVA: 0x0000CE48 File Offset: 0x0000B048
        protected override void CalculateExactPosition()
        {
            ProjectileUtility.CalculateExactPosition(this, this.effects, this.staging, this.TicksSinceLaunch);
            this.cachedPositionTick = this.ticksToImpact;
        }

        // Token: 0x060001D9 RID: 473 RVA: 0x0000CE6E File Offset: 0x0000B06E
        protected override void CalculateExactRotation()
        {
            ProjectileUtility.CalculateExactRotation(this, this.effects, this.staging, this.effectsExtension, this.TicksSinceLaunch);
        }

        // Token: 0x060001DA RID: 474 RVA: 0x0000CE90 File Offset: 0x0000B090
        public override void Launch(Thing launcher, Vector3 origin, LocalTargetInfo usedTarget, LocalTargetInfo intendedTarget, ProjectileHitFlags hitFlags, bool preventFriendlyFire = false, Thing equipment = null, ThingDef targetCoverDef = null)
        {
            this.effects.PreLaunch(equipment, ref origin, usedTarget.Cell.ToVector3Shifted());
            base.BaseLaunch(launcher, origin, usedTarget, intendedTarget, hitFlags, preventFriendlyFire, equipment, targetCoverDef);
            if (this.effectsExtension != null && this.effectsExtension.activeTracking)
            {
                Thing thing = intendedTarget.Thing;
                if (thing != null && thing.Spawned)
                {
                    this.activeTracking = true;
                }
            }
            this.calculatedRuntimeTicks = (this.ticksToImpact = this.staging.PostLaunch(this.effectsExtension, this.origin.Yto0(), this.destination.Yto0()));
            this.effects.parentDuration = this.ticksToImpact;
            this.effects.PostLaunch(this.origin, this.destination, false);
        }

        // Token: 0x04000240 RID: 576
        protected ProjectileStagingTracker staging;

        // Token: 0x04000241 RID: 577
        protected int calculatedRuntimeTicks = -1;
    }
}

namespace NCL.Projectiles
{
    // Token: 0x02000056 RID: 86
    public class Projectile_ExplosiveWithEffects : Projectile_Explosive
    {
        // Token: 0x1700002C RID: 44
        // (get) Token: 0x060001ED RID: 493 RVA: 0x0000D780 File Offset: 0x0000B980
        protected virtual Material ShadowMaterial
        {
            get
            {
                return UIAssets.ProjectileShadowMaterial;
            }
        }

        // Token: 0x1700002D RID: 45
        // (get) Token: 0x060001EE RID: 494 RVA: 0x0000D787 File Offset: 0x0000B987
        protected override int MaxTickIntervalRate
        {
            get
            {
                return 1;
            }
        }

        // Token: 0x060001EF RID: 495 RVA: 0x0000D78A File Offset: 0x0000B98A
        public override void PostMake()
        {
            base.PostMake();
            this.effects = new ProjectileEffectTracker(this);
        }

        // Token: 0x060001F0 RID: 496 RVA: 0x0000D79E File Offset: 0x0000B99E
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            this.effectsExtension = this.def.GetModExtension<ModExtension_ProjectileEffects>();
            this.effects.PostSpawnSetup(map, respawningAfterLoad);
        }

        // Token: 0x060001F1 RID: 497 RVA: 0x0000D7C6 File Offset: 0x0000B9C6
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look<ProjectileEffectTracker>(ref this.effects, "effectTracker", new object[]
            {
                this
            });
            Scribe_Values.Look<bool>(ref this.activeTracking, "activeTracking", false, false);
        }

        // Token: 0x060001F2 RID: 498 RVA: 0x0000D7FA File Offset: 0x0000B9FA
        public virtual void BaseTickInterval(int delta)
        {
            base.TickInterval(delta);
        }

        // Token: 0x060001F3 RID: 499 RVA: 0x0000D804 File Offset: 0x0000BA04
        protected override void TickInterval(int delta)
        {
            if (this.activeTracking)
            {
                Thing thing = this.intendedTarget.Thing;
                if (thing != null && thing.Spawned)
                {
                    this.destination = thing.DrawPos;
                }
            }
            this.effects.PreTick(this.origin, this.destination);
            this.CalculateExactPosition();
            Map map = base.Map;
            base.TickInterval(delta);
            this.CalculateExactRotation();
            this.effects.Tick(map, this.effectsExtension);
            this.effects.PostTick(delta);
        }

        // Token: 0x060001F4 RID: 500 RVA: 0x0000D88C File Offset: 0x0000BA8C
        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (this.def.projectile.shadowSize > 0f && this.def.projectile.arcHeightFactor > 0f)
            {
                ProjectileUtility.DrawShadow(this, this.effects, this.ShadowMaterial);
            }
            this.DrawMainMesh();
            base.Comps_PostDraw();
        }

        // Token: 0x060001F5 RID: 501 RVA: 0x0000D8E8 File Offset: 0x0000BAE8
        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            Map map = base.Map;
            this.CalculateExactRotation();
            this.CalculateExactPosition();
            base.Impact(hitThing, blockedByShield);
            this.effects.Impact(map, hitThing, blockedByShield);
        }

        // Token: 0x060001F6 RID: 502 RVA: 0x0000D91E File Offset: 0x0000BB1E
        protected virtual void DrawMainMesh()
        {
            ProjectileUtility.DrawProjectileMesh(this, this.effects);
        }

        // Token: 0x060001F7 RID: 503 RVA: 0x0000D92C File Offset: 0x0000BB2C
        protected virtual void CalculateExactPosition()
        {
            ProjectileUtility.CalculateExactPosition(this, this.effects, this.origin, this.destination, base.DistanceCoveredFraction);
            this.cachedPositionTick = this.ticksToImpact;
        }

        // Token: 0x060001F8 RID: 504 RVA: 0x0000D958 File Offset: 0x0000BB58
        protected virtual void CalculateExactRotation()
        {
            ProjectileUtility.CalculateExactRotation(this, this.effects, this.effectsExtension, this.origin, this.destination, base.DistanceCoveredFraction);
        }

        // Token: 0x1700002E RID: 46
        // (get) Token: 0x060001F9 RID: 505 RVA: 0x0000D97E File Offset: 0x0000BB7E
        public override Vector3 DrawPos
        {
            get
            {
                return this.effects.currentVisualPosition;
            }
        }

        // Token: 0x1700002F RID: 47
        // (get) Token: 0x060001FA RID: 506 RVA: 0x0000D98B File Offset: 0x0000BB8B
        public override Vector3 ExactPosition
        {
            get
            {
                if (this.cachedPositionTick != this.ticksToImpact)
                {
                    this.CalculateExactPosition();
                }
                return this.effects.currentExactPosition;
            }
        }

        // Token: 0x17000030 RID: 48
        // (get) Token: 0x060001FB RID: 507 RVA: 0x0000D9AC File Offset: 0x0000BBAC
        public override Quaternion ExactRotation
        {
            get
            {
                return this.effects.currentVisualRotation;
            }
        }

        // Token: 0x17000031 RID: 49
        // (get) Token: 0x060001FC RID: 508 RVA: 0x0000D9B9 File Offset: 0x0000BBB9
        protected virtual float ArcHeightFactor
        {
            get
            {
                return this.def.projectile.arcHeightFactor;
            }
        }

        // Token: 0x060001FD RID: 509 RVA: 0x0000D9CC File Offset: 0x0000BBCC
        public void BaseLaunch(Thing launcher, Vector3 origin, LocalTargetInfo usedTarget, LocalTargetInfo intendedTarget, ProjectileHitFlags hitFlags, bool preventFriendlyFire = false, Thing equipment = null, ThingDef targetCoverDef = null)
        {
            base.Launch(launcher, origin, usedTarget, intendedTarget, hitFlags, preventFriendlyFire, equipment, targetCoverDef);
        }

        // Token: 0x060001FE RID: 510 RVA: 0x0000D9EC File Offset: 0x0000BBEC
        public override void Launch(Thing launcher, Vector3 origin, LocalTargetInfo usedTarget, LocalTargetInfo intendedTarget, ProjectileHitFlags hitFlags, bool preventFriendlyFire = false, Thing equipment = null, ThingDef targetCoverDef = null)
        {
            this.effects.PreLaunch(equipment, ref origin, usedTarget.Cell.ToVector3Shifted());
            base.Launch(launcher, origin, usedTarget, intendedTarget, hitFlags, preventFriendlyFire, equipment, targetCoverDef);
            if (this.effectsExtension != null && this.effectsExtension.activeTracking)
            {
                Thing thing = intendedTarget.Thing;
                if (thing != null && thing.Spawned)
                {
                    this.activeTracking = true;
                }
            }
            this.effects.parentDuration = this.ticksToImpact;
            this.effects.PostLaunch(this.origin, this.destination, true);
        }

        // Token: 0x04000243 RID: 579
        protected ModExtension_ProjectileEffects effectsExtension;

        // Token: 0x04000244 RID: 580
        protected ProjectileEffectTracker effects;

        // Token: 0x04000245 RID: 581
        protected int cachedPositionTick = -1;

        // Token: 0x04000246 RID: 582
        protected bool impacted;

        // Token: 0x04000247 RID: 583
        protected bool activeTracking;
    }
}

namespace NCL.Projectiles
{
    // Token: 0x02000052 RID: 82
    public class ProjectileStagingTracker : IExposable
    {
        // Token: 0x060001C9 RID: 457 RVA: 0x0000C89B File Offset: 0x0000AA9B
        public ProjectileStagingTracker(Thing parent)
        {
            this.parent = parent;
        }

        // Token: 0x060001CA RID: 458 RVA: 0x0000C8AC File Offset: 0x0000AAAC
        public void PostSpawnSetup(Map map, bool respawningAfterLoad, ModExtension_ProjectileEffects extension, int ticksSinceLaunch)
        {
            if (this.stages != null)
            {
                int stageIndex = this.GetStageIndex(ticksSinceLaunch);
                if (stageIndex > -1)
                {
                    this.stageConfig = extension.stages[stageIndex];
                    this.stage = this.stages[stageIndex];
                }
            }
        }

        // Token: 0x060001CB RID: 459 RVA: 0x0000C8F2 File Offset: 0x0000AAF2
        public int PostLaunch(ModExtension_ProjectileEffects extension, Vector3 origin, Vector3 destination)
        {
            this.InitializeStages(extension, origin, destination);
            return this.totalFlightDuration;
        }

        // Token: 0x060001CC RID: 460 RVA: 0x0000C903 File Offset: 0x0000AB03
        public void ExposeData()
        {
            Scribe_Values.Look<int>(ref this.totalFlightDuration, "totalFlightDuration", 0, false);
            Scribe_Collections.Look<ProjectileFlightStage>(ref this.stages, "stages", LookMode.Undefined, Array.Empty<object>());
        }

        // Token: 0x060001CD RID: 461 RVA: 0x0000C930 File Offset: 0x0000AB30
        public int GetStageIndex(int tick = 0)
        {
            if (this.stages != null)
            {
                for (int i = 0; i < this.stages.Count; i++)
                {
                    if (this.stages[i].duration < 0)
                    {
                        return i;
                    }
                    if (tick < this.stages[i].duration)
                    {
                        return i;
                    }
                    tick -= this.stages[i].duration;
                }
            }
            return -1;
        }

        // Token: 0x060001CE RID: 462 RVA: 0x0000C9A0 File Offset: 0x0000ABA0
        public void PreTick(ModExtension_ProjectileEffects extension, ProjectileEffectTracker effectTracker, int ticksSinceLaunch, bool activeTracking = false)
        {
            int ticksSinceStageStart = ticksSinceLaunch - this.stage.startingTick;
            if (this.stage.duration > -1 && ticksSinceStageStart > this.stage.duration)
            {
                int stageIndex = this.GetStageIndex(ticksSinceLaunch);
                if (stageIndex > -1)
                {
                    Vector3 previousDestination = this.stage.destination;
                    this.stageConfig = extension.stages[stageIndex];
                    this.stage = this.stages[stageIndex];
                    SoundDef startSound = this.stageConfig.startSound;
                    if (startSound != null)
                    {
                        startSound.PlayOneShot(new TargetInfo(this.parent.Position, this.parent.Map, false));
                    }
                    if (activeTracking && previousDestination != default(Vector3))
                    {
                        this.stage.origin = previousDestination;
                    }
                }
            }
            if (activeTracking && this.stageConfig != null && (this.stageConfig.type == ProjectileStageType.Cruise || this.stageConfig.type == ProjectileStageType.Terminal))
            {
                this.stage.destination = effectTracker.destination + ProjectileUtility.CalculateStagePositionOffset(this.stageConfig, this.stage.origin, effectTracker.destination);
            }
        }

        // Token: 0x060001CF RID: 463 RVA: 0x0000CACE File Offset: 0x0000ACCE
        public void Tick(Map map, ModExtension_ProjectileEffects extension)
        {
        }

        // Token: 0x060001D0 RID: 464 RVA: 0x0000CAD0 File Offset: 0x0000ACD0
        private void InitializeStages(ModExtension_ProjectileEffects extension, Vector3 origin, Vector3 destination)
        {
            Vector3 start = origin;
            float startHeight = 0f;
            this.stages = new List<ProjectileFlightStage>(extension.stages.Count);
            int i = 0;
            while (i < extension.stages.Count)
            {
                ProjectileStageConfiguration stageConfig = extension.stages[i];
                Vector3 end;
                float endHeight;
                switch (stageConfig.type)
                {
                    case ProjectileStageType.Launch:
                        end = start;
                        end += ProjectileUtility.CalculateStagePositionOffset(stageConfig, start, destination);
                        startHeight = stageConfig.initialHeight;
                        endHeight = stageConfig.heightOffset;
                        break;
                    case ProjectileStageType.Cruise:
                        end = destination + ProjectileUtility.CalculateStagePositionOffset(stageConfig, start, destination);
                        endHeight = stageConfig.heightOffset;
                        break;
                    case ProjectileStageType.Terminal:
                        goto IL_8E;
                    default:
                        goto IL_8E;
                }
            IL_97:
                int stageDuration;
                if (stageConfig.duration > -1)
                {
                    stageDuration = stageConfig.duration;
                }
                else
                {
                    stageDuration = Mathf.CeilToInt((end - start).MagnitudeHorizontal() / stageConfig.TilesPerTick);
                }
                this.stages.Add(new ProjectileFlightStage
                {
                    origin = start,
                    destination = end,
                    startingTick = this.totalFlightDuration,
                    startingHeight = startHeight,
                    endingHeight = endHeight,
                    distance = (end - start).MagnitudeHorizontal(),
                    duration = stageDuration
                });
                this.totalFlightDuration += stageDuration;
                start = end;
                startHeight = endHeight;
                i++;
                continue;
            IL_8E:
                end = destination;
                endHeight = 0f;
                goto IL_97;
            }
            this.stageConfig = extension.stages[0];
            this.stage = this.stages[0];
            SoundDef startSound = this.stageConfig.startSound;
            if (startSound == null)
            {
                return;
            }
            startSound.PlayOneShot(new TargetInfo(this.parent.Position, this.parent.Map, false));
        }

        // Token: 0x04000233 RID: 563
        private readonly Thing parent;

        // Token: 0x04000234 RID: 564
        public List<ProjectileFlightStage> stages;

        // Token: 0x04000235 RID: 565
        public ProjectileFlightStage stage;

        // Token: 0x04000236 RID: 566
        public ProjectileStageConfiguration stageConfig;

        // Token: 0x04000237 RID: 567
        public int totalFlightDuration;

        // Token: 0x04000238 RID: 568
        public bool impacted;
    }
}

namespace NCL.Projectiles
{
    // Token: 0x02000055 RID: 85
    public static class ProjectileUtility
    {
        // Token: 0x060001DC RID: 476 RVA: 0x0000CF6C File Offset: 0x0000B16C
        public static int GetEffectRadiusCellCount(float radius, int baseSize)
        {
            List<IntVec3> list;
            if (ProjectileUtility.CachedEffectRadiusCells.TryGetValue(new ValueTuple<float, int>(radius, baseSize), out list))
            {
                return list.Count;
            }
            return ProjectileUtility.GetEffectRadiusCells(radius, baseSize).Count;
        }

        // Token: 0x060001DD RID: 477 RVA: 0x0000CFA4 File Offset: 0x0000B1A4
        public static List<IntVec3> GetEffectRadiusCells(float radius, int baseSize)
        {
            if (!ProjectileUtility.CachedEffectRadiusCells.ContainsKey(new ValueTuple<float, int>(radius, baseSize)))
            {
                int intRadius = Mathf.CeilToInt(radius);
                List<IntVec3> cells = new List<IntVec3>();
                if (baseSize % 2 == 0)
                {
                    int lowerBound = -intRadius;
                    int upperBound = intRadius + 1;
                    float radiusSquared = Mathf.Pow(radius, 2f);
                    for (int x = lowerBound; x <= upperBound; x++)
                    {
                        for (int z = lowerBound; z <= upperBound; z++)
                        {
                            if (Mathf.Pow(-0.5f + (float)x, 2f) + Mathf.Pow(-0.5f + (float)z, 2f) <= radiusSquared)
                            {
                                cells.Add(new IntVec3(x, 0, z));
                            }
                        }
                    }
                }
                else
                {
                    cells.AddRange(GenRadial.RadialCellsAround(IntVec3.Zero, radius, true));
                }
                ProjectileUtility.CachedEffectRadiusCells[new ValueTuple<float, int>(radius, baseSize)] = cells;
                return cells;
            }
            return ProjectileUtility.CachedEffectRadiusCells[new ValueTuple<float, int>(radius, baseSize)];
        }

        // Token: 0x060001DE RID: 478 RVA: 0x0000D081 File Offset: 0x0000B281
        public static List<IntVec3> GetEffectRadiusCellsAround(Thing thing, float radius)
        {
            return ProjectileUtility.GetEffectRadiusCellsAround(thing.Position, radius, thing.def.size.x);
        }

        // Token: 0x060001DF RID: 479 RVA: 0x0000D0A0 File Offset: 0x0000B2A0
        public static List<IntVec3> GetEffectRadiusCellsAround(IntVec3 origin, float radius, int baseSize)
        {
            List<IntVec3> list = ProjectileUtility.GetEffectRadiusCells(radius, baseSize);
            List<IntVec3> modifiedList = new List<IntVec3>(list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                modifiedList.Add(origin + list[i]);
            }
            return modifiedList;
        }

        // Token: 0x060001E0 RID: 480 RVA: 0x0000D0E8 File Offset: 0x0000B2E8
        public static Vector3 CalculateStagePositionOffset(ProjectileStageConfiguration stage, Vector3 origin, Vector3 destination)
        {
            Vector3 position = Vector3.zero;
            if (stage.positionOffset != Vector3.zero & stage.alignPositionWithDestination)
            {
                position += ((destination == origin) ? Quaternion.identity : Quaternion.LookRotation(destination - origin)) * stage.positionOffset;
            }
            else
            {
                position += stage.positionOffset;
            }
            return position;
        }

        // Token: 0x060001E1 RID: 481 RVA: 0x0000D154 File Offset: 0x0000B354
        public static void CalculateExactPosition(Thing projectile, ProjectileEffectTracker effects, Vector3 origin, Vector3 destination, float progress)
        {
            float effectiveProgress = (effects.progress == null) ? progress : effects.progress(progress);
            effects.currentExactPosition = Vector3.Lerp(origin, destination, effectiveProgress);
            if (effects.lateralOffsetMagnitude != 0f && effects.lateralOffset != null)
            {
                effects.currentExactPosition += effects.destinationRotation * new Vector3(effects.lateralOffsetMagnitude * effects.lateralOffset(progress), 0f);
            }
            effects.currentVisualHeight = ((effects.arcFactor > 0f && effects.height != null) ? (effects.arcFactor * effects.height(progress)) : 0f);
            effects.currentVisualPosition = effects.currentExactPosition;
            effects.currentVisualPosition.y = projectile.def.Altitude;
            effects.currentVisualPosition.z = effects.currentVisualPosition.z + effects.currentVisualHeight;
        }

        // Token: 0x060001E2 RID: 482 RVA: 0x0000D248 File Offset: 0x0000B448
        public static void CalculateExactPosition(Thing projectile, ProjectileEffectTracker effects, ProjectileStagingTracker staging, int ticksSinceLaunch)
        {
            float stageProgress = (float)(ticksSinceLaunch - staging.stage.startingTick) / (float)staging.stage.duration;
            if (staging.stageConfig.progress != null)
            {
                stageProgress = staging.stageConfig.progress(stageProgress);
            }
            effects.currentExactPosition = Vector3.Lerp(staging.stage.origin, staging.stage.destination, (staging.stageConfig.position == null) ? stageProgress : staging.stageConfig.position(stageProgress));
            effects.currentVisualHeight = Mathf.Lerp(staging.stage.startingHeight, staging.stage.endingHeight, staging.stageConfig.height(stageProgress));
            if (staging.stageConfig.arcFactor != 0f && staging.stageConfig.arc != null)
            {
                effects.currentVisualHeight += staging.stageConfig.arcFactor * staging.stageConfig.arc(stageProgress);
            }
            effects.currentVisualPosition = effects.currentExactPosition;
            effects.currentVisualPosition.y = projectile.def.Altitude;
            effects.currentVisualPosition.z = effects.currentVisualPosition.z + effects.currentVisualHeight;
        }

        // Token: 0x060001E3 RID: 483 RVA: 0x0000D384 File Offset: 0x0000B584
        private static void ExtrapolateRotation(ProjectileEffectTracker effects)
        {
            if (effects.previousVisualPosition != effects.currentVisualPosition)
            {
                effects.currentVisualRotation = Quaternion.LookRotation((effects.currentVisualPosition - effects.previousVisualPosition).Yto0());
                effects.currentVisualAngle = effects.currentVisualRotation.eulerAngles.y;
            }
        }

        // Token: 0x060001E4 RID: 484 RVA: 0x0000D3DC File Offset: 0x0000B5DC
        public static void CalculateExactRotation(Thing projectile, ProjectileEffectTracker effects, ModExtension_ProjectileEffects effectsExtension, Vector3 origin, Vector3 destination, float distance)
        {
            if (effectsExtension != null)
            {
                if (effectsExtension.fixedRotation)
                {
                    effects.currentVisualAngle = 0f;
                    effects.currentVisualRotation = Quaternion.identity;
                    return;
                }
                if (effectsExtension.rotationRate != 0f)
                {
                    effects.currentVisualAngle = effectsExtension.rotationRate * (float)effects.ticksSinceLaunch / 60f;
                    effects.currentVisualRotation = Quaternion.AngleAxis(effects.currentVisualAngle, Vector3.up);
                    return;
                }
                if (effectsExtension.useVariableHeightFactor)
                {
                    ProjectileUtility.ExtrapolateRotation(effects);
                    return;
                }
            }
            if (effects.arcFactor != 0f)
            {
                ProjectileUtility.ExtrapolateRotation(effects);
                return;
            }
            effects.currentVisualRotation = effects.destinationRotation;
            effects.currentVisualAngle = effects.currentVisualRotation.eulerAngles.y;
        }

        // Token: 0x060001E5 RID: 485 RVA: 0x0000D490 File Offset: 0x0000B690
        public static void CalculateExactRotation(Thing projectile, ProjectileEffectTracker effects, ProjectileStagingTracker staging, ModExtension_ProjectileEffects effectsExtension, int ticksSinceLaunch)
        {
            if (effectsExtension != null)
            {
                if (effectsExtension.fixedRotation)
                {
                    effects.currentVisualAngle = 0f;
                    effects.currentVisualRotation = Quaternion.identity;
                    return;
                }
                if (effectsExtension.rotationRate != 0f)
                {
                    effects.currentVisualAngle = effectsExtension.rotationRate * (float)effects.ticksSinceLaunch / 60f;
                    effects.currentVisualRotation = Quaternion.AngleAxis(effects.currentVisualAngle, Vector3.up);
                    return;
                }
            }
            if (effects.previousVisualPosition != effects.currentVisualPosition)
            {
                effects.currentVisualRotation = Quaternion.LookRotation(effects.currentVisualPosition - effects.previousVisualPosition);
                effects.currentVisualAngle = effects.currentVisualRotation.eulerAngles.y;
                return;
            }
            if (staging.stageConfig.overrideInitialAngle)
            {
                effects.currentVisualAngle = staging.stageConfig.angle;
                effects.currentVisualRotation = Quaternion.Euler(0f, effects.currentVisualAngle, 0f);
                return;
            }
        }

        // Token: 0x060001E6 RID: 486 RVA: 0x0000D57D File Offset: 0x0000B77D
        public static void DrawProjectileMesh(Projectile projectile, ProjectileEffectTracker effects)
        {
            Graphics.DrawMesh(MeshPool.GridPlane(projectile.def.graphicData.drawSize), effects.currentVisualPosition, effects.currentVisualRotation, projectile.def.DrawMatSingle, 0);
        }

        // Token: 0x060001E7 RID: 487 RVA: 0x0000D5B4 File Offset: 0x0000B7B4
        public static void DrawShadow(Projectile projectile, ProjectileEffectTracker effects, Material shadowMaterial)
        {
            float heightFactor = effects.currentVisualHeight / projectile.def.projectile.arcHeightFactor;
            float num = projectile.def.projectile.shadowSize * 2f * heightFactor;
            Vector3 s = new Vector3(num, 1f, num);
            Vector3 vector = new Vector3(0f, -0.01f, 0f);
            Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(effects.currentExactPosition + vector, Quaternion.identity, s), FadedMaterialPool.FadedVersionOf(shadowMaterial, Mathf.Lerp(1f, 0.3f, heightFactor)), 0);
        }

        // Token: 0x060001E8 RID: 488 RVA: 0x0000D64E File Offset: 0x0000B84E
        public static InterceptorMapComponent GetInterceptorMapComponent(this Map map)
        {
            if (map == null)
            {
                return null;
            }
            return map.GetComponent<InterceptorMapComponent>();
        }

        // Token: 0x060001E9 RID: 489 RVA: 0x0000D65B File Offset: 0x0000B85B
        public static Vector2 ToRimWorldVector2(this Vector3 v)
        {
            return new Vector2(v.x, v.z);
        }

        // Token: 0x060001EA RID: 490 RVA: 0x0000D66E File Offset: 0x0000B86E
        public static Vector3 ToRimWorldVector3(this Vector2 v2)
        {
            return new Vector3(v2.x, 1f, v2.y);
        }

        // Token: 0x060001EB RID: 491 RVA: 0x0000D688 File Offset: 0x0000B888
        public static void ModifyOriginVector(ref Vector3 origin, Vector3 destination, Vector3 originOffset, bool alignOffset, float originDistance, float pawnScaleFactor = 1f)
        {
            originOffset.x *= pawnScaleFactor;
            originOffset.x *= pawnScaleFactor;
            if (alignOffset && origin != destination)
            {
                Quaternion rotation = Quaternion.LookRotation((destination - origin).Yto0());
                origin += rotation * originOffset;
                if (originDistance != 0f)
                {
                    origin += rotation * new Vector3(0f, 0f, originDistance * pawnScaleFactor);
                    return;
                }
            }
            else
            {
                origin += originOffset;
                if (originDistance != 0f)
                {
                    Vector3 delta = (destination - origin).Yto0();
                    delta.Normalize();
                    origin += pawnScaleFactor * originDistance * delta;
                }
            }
        }

        // Token: 0x04000242 RID: 578
        private static Dictionary<ValueTuple<float, int>, List<IntVec3>> CachedEffectRadiusCells = new Dictionary<ValueTuple<float, int>, List<IntVec3>>();
    }
}

namespace NCL.Projectiles
{
    // Token: 0x02000058 RID: 88
    [StaticConstructorOnStartup]
    public class UIAssets
    {
        // Token: 0x0400024C RID: 588
        public static readonly Material ProjectileShadowMaterial = MaterialPool.MatFrom("Things/Skyfaller/SkyfallerShadowCircle", ShaderDatabase.Transparent);
    }
}

namespace NCL.Projectiles
{
    // Token: 0x02000039 RID: 57
    public class ProjectileEffectTracker : IExposable
    {
        // Token: 0x1700001D RID: 29
        // (get) Token: 0x0600011C RID: 284 RVA: 0x000082AF File Offset: 0x000064AF
        public Projectile Projectile
        {
            get
            {
                return this.parent as Projectile;
            }
        }

        // Token: 0x0600011D RID: 285 RVA: 0x000082BC File Offset: 0x000064BC
        public ProjectileEffectTracker(Thing parent)
        {
            this.parent = parent;
        }

        // Token: 0x0600011E RID: 286 RVA: 0x0000833C File Offset: 0x0000653C
        public void PostSpawnSetup(Map map, bool respawningAfterLoad)
        {
            ProjectileProperties projectile = this.parent.def.projectile;
            this.arcFactor = ((projectile != null) ? projectile.arcHeightFactor : 0f);
            this.effectExtension = this.parent.def.GetModExtension<ModExtension_ProjectileEffects>();
            if (this.effectExtension == null)
            {
                if (this.arcFactor > 0f)
                {
                    this.height = AnimationUtility.Sine;
                    return;
                }
            }
            else
            {
                this.progress = this.effectExtension.progress;
                this.height = this.effectExtension.height;
                this.lateralOffset = this.effectExtension.lateralOffset;
                if (!respawningAfterLoad)
                {
                    this.lateralOffsetMagnitude = this.effectExtension.lateralOffsetMagnitude.RandomInRange;
                    if (this.lateralOffsetMagnitude != 0f && Rand.Chance(this.effectExtension.lateralOffsetMirrorChance))
                    {
                        this.lateralOffsetMagnitude *= -1f;
                    }
                }
            }
        }

        // Token: 0x0600011F RID: 287 RVA: 0x00008428 File Offset: 0x00006628
        public void PreLaunch(Thing equipment, ref Vector3 origin, Vector3 destination)
        {
            if (origin == destination)
            {
                return;
            }
            WeaponWithAttachments weapon = equipment as WeaponWithAttachments;
            if (weapon != null)
            {
                ModExtension_WeaponAttachments attachmentExtension = weapon.AttachmentExtension;
                if (attachmentExtension != null)
                {
                    if (this.effectExtension.originAttachment != null)
                    {
                        origin = weapon.GetAttachmentPosition(this.effectExtension.originAttachment, this.effectExtension.originAttachmentIndex, this.effectExtension.randomizeOriginAttachment);
                    }
                    ProjectileUtility.ModifyOriginVector(ref origin, destination, attachmentExtension.GetOriginOffsetFor(equipment), attachmentExtension.alignOriginOffsetWithDirection, attachmentExtension.originDistance, weapon.pawnScaleFactor);
                }
            }
            if (this.effectExtension != null)
            {
                if (this.effectExtension.alignOriginWithDrawPos)
                {
                    Vector3? drawPosHeld = equipment.DrawPosHeld;
                    if (drawPosHeld != null)
                    {
                        Vector3 drawPos = drawPosHeld.GetValueOrDefault();
                        origin = drawPos;
                    }
                }
                ProjectileUtility.ModifyOriginVector(ref origin, destination, this.effectExtension.GetOriginOffsetFor(equipment), this.effectExtension.alignOriginOffsetWithDirection, this.effectExtension.originDistance, 1f);
            }
        }

        // Token: 0x06000120 RID: 288 RVA: 0x00008514 File Offset: 0x00006714
        public void PostLaunch(Vector3 origin, Vector3 destination, bool calculatePositionImmediately = true)
        {
            this.origin = origin;
            this.destination = destination;
            this.fullVector = (destination - origin).Yto0();
            this.destinationRotation = ((this.fullVector == Vector3.zero) ? Quaternion.identity : Quaternion.LookRotation(this.fullVector));
            if (calculatePositionImmediately)
            {
                ProjectileUtility.CalculateExactPosition(this.parent, this, origin, destination, 0f);
                ProjectileUtility.CalculateExactRotation(this.parent, this, this.effectExtension, origin, destination, 0f);
            }
            if (this.effectExtension != null)
            {
                if (this.effectExtension.useVariableHeightFactor)
                {
                    this.arcFactor = (origin - destination).Yto0().MagnitudeHorizontal() * this.effectExtension.heightFactorMagnitude.RandomInRange;
                }
                Map map = this.parent.Map;
                if (map != null && !this.effectExtension.launchEffects.NullOrEmpty<EffectDef>())
                {
                    EffectMapComponent component = map.EccentricProjectilesEffectComp();
                    if (component != null)
                    {
                        foreach (EffectDef effectDef in this.effectExtension.launchEffects)
                        {
                            component.CreateEffect(new EffectContext(map, effectDef)
                            {
                                anchor = this.parent,
                                position = origin,
                                origin = origin,
                                destination = destination,
                                rotation = this.destinationRotation,
                                angle = this.destinationRotation.eulerAngles.y,
                                parentDuration = this.parentDuration
                            });
                        }
                    }
                }
            }
            if (this.arcFactor > 0f && this.height == null)
            {
                this.height = AnimationUtility.Sine;
            }
        }

        // Token: 0x06000121 RID: 289 RVA: 0x000086DC File Offset: 0x000068DC
        public void ExposeData()
        {
            Scribe_Values.Look<int>(ref this.ticksSinceLaunch, "ticksSinceLaunch", 0, false);
            Scribe_Values.Look<float>(ref this.arcFactor, "arcFactor", 0f, false);
            Scribe_Values.Look<float>(ref this.lateralOffsetMagnitude, "lateralOffsetMagnitude", 0f, false);
            Scribe_Values.Look<Vector3>(ref this.previousExactPosition, "previousExactPosition", default(Vector3), false);
            Scribe_Values.Look<Vector3>(ref this.previousVisualPosition, "previousVisualPosition", default(Vector3), false);
            Scribe_Values.Look<float>(ref this.previousVisualHeight, "previousVisualHeight", 0f, false);
            Scribe_Values.Look<Vector3>(ref this.currentExactPosition, "currentExactPosition", default(Vector3), false);
            Scribe_Values.Look<Vector3>(ref this.currentVisualPosition, "currentVisualPosition", default(Vector3), false);
            Scribe_Values.Look<float>(ref this.currentVisualHeight, "currentVisualHeight", 0f, false);
        }

        // Token: 0x06000122 RID: 290 RVA: 0x000087BC File Offset: 0x000069BC
        public void PreTick(Vector3 origin, Vector3 destination)
        {
            this.origin = origin;
            this.destination = destination;
            this.fullVector = (destination - origin).Yto0();
            this.destinationRotation = ((this.fullVector == Vector3.zero) ? Quaternion.identity : Quaternion.LookRotation(this.fullVector));
            this.previousExactPosition = this.currentExactPosition;
            this.previousVisualPosition = this.currentVisualPosition;
            this.previousVisualHeight = this.currentVisualHeight;
        }

        // Token: 0x06000123 RID: 291 RVA: 0x00008838 File Offset: 0x00006A38
        public void Tick(Map map, ModExtension_ProjectileEffects extension)
        {
            if (map == null || extension == null || extension.effects.NullOrEmpty<EffectDef>())
            {
                return;
            }
            EffectMapComponent component = map.EccentricProjectilesEffectComp();
            if (component == null)
            {
                return;
            }
            foreach (EffectDef effectDef in extension.effects)
            {
                if (effectDef.ShouldBeActive(this.ticksSinceLaunch))
                {
                    component.CreateEffect(new EffectContext(map, effectDef)
                    {
                        anchor = this.parent,
                        destinationAnchor = null,
                        position = this.previousVisualPosition,
                        origin = this.origin,
                        destination = this.currentVisualPosition,
                        rotation = this.currentVisualRotation,
                        angle = this.currentVisualAngle,
                        parentDuration = this.parentDuration,
                        parentTicksElapsed = this.ticksSinceLaunch
                    });
                }
            }
        }

        // Token: 0x06000124 RID: 292 RVA: 0x00008938 File Offset: 0x00006B38
        public void PostTick(int interval = 1)
        {
            this.ticksSinceLaunch += interval;
        }

        // Token: 0x06000125 RID: 293 RVA: 0x00008948 File Offset: 0x00006B48
        public void Impact(Map map, Thing hitThing, bool blockedByShield = false)
        {
            ModExtension_ProjectileEffects modExtension_ProjectileEffects = this.effectExtension;
            if (modExtension_ProjectileEffects == null || !modExtension_ProjectileEffects.hasImpactEffects)
            {
                return;
            }
            EffectMapComponent mapComponent = (map != null) ? map.EccentricProjectilesEffectComp() : null;
            if (mapComponent == null)
            {
                return;
            }
            if (!this.effectExtension.impactEffects.NullOrEmpty<EffectDef>())
            {
                float angle = this.currentVisualAngle;
                Quaternion rotation = this.currentVisualRotation;
                Vector3 finalVector = this.currentExactPosition - this.previousVisualPosition;
                if (finalVector != Vector3.zero)
                {
                    rotation = Quaternion.LookRotation(finalVector);
                    angle = rotation.eulerAngles.y;
                }
                foreach (EffectDef effectDef in this.effectExtension.impactEffects)
                {
                    if (effectDef != null && (!blockedByShield || effectDef.drawIfIntercepted))
                    {
                        mapComponent.CreateEffect(new EffectContext(map, effectDef)
                        {
                            anchor = null,
                            destinationAnchor = hitThing,
                            position = this.currentVisualPosition,
                            origin = this.destination,
                            destination = this.destination,
                            rotation = rotation,
                            angle = angle
                        });
                    }
                }
            }
            Projectile projectile = this.Projectile;
            if (projectile != null)
            {
                ThingDef def = projectile.def;
                if (def != null)
                {
                    ProjectileProperties projectile2 = def.projectile;
                    if (projectile2 != null)
                    {
                        SoundDef soundImpact = projectile2.soundImpact;
                        if (soundImpact != null)
                        {
                            soundImpact.PlayOneShot(new TargetInfo((hitThing != null) ? hitThing.Position : this.destination.ToIntVec3(), map, false));
                        }
                    }
                }
            }
            if (!this.effectExtension.returnEffects.NullOrEmpty<EffectDef>())
            {
                Quaternion returnRotation = Quaternion.LookRotation(this.origin - this.destination);
                float returnAngle = returnRotation.eulerAngles.y;
                foreach (EffectDef effectDef2 in this.effectExtension.returnEffects)
                {
                    if (effectDef2 != null && (!blockedByShield || effectDef2.drawIfIntercepted))
                    {
                        mapComponent.CreateEffect(new EffectContext(map, effectDef2)
                        {
                            anchor = null,
                            position = this.currentVisualPosition,
                            origin = this.currentVisualPosition,
                            destination = this.origin,
                            rotation = returnRotation,
                            angle = returnAngle
                        });
                    }
                }
            }
        }

        // Token: 0x04000186 RID: 390
        public Thing parent;

        // Token: 0x04000187 RID: 391
        public int ticksSinceLaunch;

        // Token: 0x04000188 RID: 392
        public int parentDuration;

        // Token: 0x04000189 RID: 393
        public Vector3 origin = Vector3.zero;

        // Token: 0x0400018A RID: 394
        public Vector3 destination = Vector3.zero;

        // Token: 0x0400018B RID: 395
        public Vector3 fullVector = Vector3.zero;

        // Token: 0x0400018C RID: 396
        public Quaternion destinationRotation = Quaternion.identity;

        // Token: 0x0400018D RID: 397
        public Vector3 previousExactPosition = Vector3.zero;

        // Token: 0x0400018E RID: 398
        public Vector3 previousVisualPosition = Vector3.zero;

        // Token: 0x0400018F RID: 399
        public float previousVisualHeight;

        // Token: 0x04000190 RID: 400
        public Vector3 currentExactPosition = Vector3.zero;

        // Token: 0x04000191 RID: 401
        public Vector3 currentVisualPosition = Vector3.zero;

        // Token: 0x04000192 RID: 402
        public float currentVisualHeight;

        // Token: 0x04000193 RID: 403
        public float currentVisualAngle;

        // Token: 0x04000194 RID: 404
        public Quaternion currentVisualRotation = Quaternion.identity;

        // Token: 0x04000195 RID: 405
        public ModExtension_ProjectileEffects effectExtension;

        // Token: 0x04000196 RID: 406
        public Func<float, float> progress;

        // Token: 0x04000197 RID: 407
        public Func<float, float> height;

        // Token: 0x04000198 RID: 408
        public float arcFactor;

        // Token: 0x04000199 RID: 409
        public Func<float, float> lateralOffset;

        // Token: 0x0400019A RID: 410
        public float lateralOffsetMagnitude;
    }
}

namespace NCL.Projectiles
{
    // Token: 0x0200004C RID: 76
    public class ModExtension_ProjectileEffects : DefModExtension
    {
        // Token: 0x060001A9 RID: 425 RVA: 0x0000C134 File Offset: 0x0000A334
        public ModExtension_ProjectileEffects()
        {
            LongEventHandler.ExecuteWhenFinished(delegate
            {
                if (!this.progressFunction.NullOrEmpty())
                {
                    this.progress = AnimationUtility.GetFunctionByName(this.progressFunction, null);
                }
                if (!this.heightFunction.NullOrEmpty())
                {
                    this.height = AnimationUtility.GetFunctionByName(this.heightFunction, AnimationUtility.Sine);
                }
                if (!this.lateralOffsetFunction.NullOrEmpty())
                {
                    this.lateralOffset = AnimationUtility.GetFunctionByName(this.lateralOffsetFunction, null);
                }
                if (this.stages != null)
                {
                    foreach (ProjectileStageConfiguration projectileStageConfiguration in this.stages)
                    {
                        projectileStageConfiguration.Initialize();
                    }
                }
                this.hasImpactEffects = (!this.impactEffects.NullOrEmpty<EffectDef>() || !this.returnEffects.NullOrEmpty<EffectDef>());
            });
        }

        // Token: 0x060001AA RID: 426 RVA: 0x0000C18C File Offset: 0x0000A38C
        public Vector3 GetOriginOffsetFor(Thing thing)
        {
            if (thing == null || this.originOffsets.NullOrEmpty<Vector3>())
            {
                return this.originOffset;
            }
            int incrementer = 0;
            int i;
            if (ModExtension_ProjectileEffects.originIncrementers.TryGetValue(thing.thingIDNumber, out i))
            {
                incrementer = i;
            }
            if (incrementer >= this.originOffsets.Count)
            {
                incrementer = 0;
            }
            Vector3 result = this.originOffsets[incrementer++];
            ModExtension_ProjectileEffects.originIncrementers[thing.thingIDNumber] = incrementer;
            return result;
        }

        // Token: 0x060001AB RID: 427 RVA: 0x0000C1FC File Offset: 0x0000A3FC
        public ProjectileStageConfiguration GetStageAt(int tick = 0)
        {
            if (this.stages != null)
            {
                foreach (ProjectileStageConfiguration stage in this.stages)
                {
                    if (stage.duration < 0)
                    {
                        return stage;
                    }
                    if (tick < stage.duration)
                    {
                        return stage;
                    }
                    tick -= stage.duration;
                }
            }
            return null;
        }

        // Token: 0x060001AC RID: 428 RVA: 0x0000C278 File Offset: 0x0000A478
        public int GetFlightTimeOffset()
        {
            int offset = 0;
            if (this.stages != null)
            {
                foreach (ProjectileStageConfiguration stage in this.stages)
                {
                    if (stage.duration > -1)
                    {
                        offset += stage.duration;
                    }
                }
            }
            return offset;
        }

        // Token: 0x040001EE RID: 494
        private static readonly Dictionary<int, int> originIncrementers = new Dictionary<int, int>();

        // Token: 0x040001EF RID: 495
        public bool alignOriginWithDrawPos;

        // Token: 0x040001F0 RID: 496
        public Vector3 originOffset = Vector3.zero;

        // Token: 0x040001F1 RID: 497
        public List<Vector3> originOffsets;

        // Token: 0x040001F2 RID: 498
        public float originDistance;

        // Token: 0x040001F3 RID: 499
        public bool alignOriginOffsetWithDirection;

        // Token: 0x040001F4 RID: 500
        public string originAttachment;

        // Token: 0x040001F5 RID: 501
        public int originAttachmentIndex = -1;

        // Token: 0x040001F6 RID: 502
        public bool randomizeOriginAttachment;

        // Token: 0x040001F7 RID: 503
        public float rotationRate;

        // Token: 0x040001F8 RID: 504
        public bool fixedRotation;

        // Token: 0x040001F9 RID: 505
        public bool activeTracking;

        // Token: 0x040001FA RID: 506
        public string progressFunction;

        // Token: 0x040001FB RID: 507
        [Unsaved(false)]
        public Func<float, float> progress;

        // Token: 0x040001FC RID: 508
        public string heightFunction;

        // Token: 0x040001FD RID: 509
        [Unsaved(false)]
        public Func<float, float> height;

        // Token: 0x040001FE RID: 510
        public FloatRange heightFactorMagnitude = FloatRange.ZeroToOne;

        // Token: 0x040001FF RID: 511
        public bool useVariableHeightFactor;

        // Token: 0x04000200 RID: 512
        public string lateralOffsetFunction;

        // Token: 0x04000201 RID: 513
        [Unsaved(false)]
        public Func<float, float> lateralOffset;

        // Token: 0x04000202 RID: 514
        public FloatRange lateralOffsetMagnitude = FloatRange.ZeroToOne;

        // Token: 0x04000203 RID: 515
        public float lateralOffsetMirrorChance = 0.5f;

        // Token: 0x04000204 RID: 516
        public List<EffectDef> launchEffects;

        // Token: 0x04000205 RID: 517
        public List<EffectDef> effects;

        // Token: 0x04000206 RID: 518
        public List<EffectDef> impactEffects;

        // Token: 0x04000207 RID: 519
        public List<EffectDef> returnEffects;

        // Token: 0x04000208 RID: 520
        public List<ProjectileStageConfiguration> stages;

        // Token: 0x04000209 RID: 521
        [Unsaved(false)]
        public bool hasImpactEffects;
    }
}

namespace NCL.Projectiles
{
    // Token: 0x02000053 RID: 83
    public struct ProjectileFlightStage : IExposable
    {
        // Token: 0x060001D1 RID: 465 RVA: 0x0000CC88 File Offset: 0x0000AE88
        public void ExposeData()
        {
            Scribe_Values.Look<Vector3>(ref this.origin, "origin", default(Vector3), false);
            Scribe_Values.Look<Vector3>(ref this.destination, "destination", default(Vector3), false);
            Scribe_Values.Look<int>(ref this.startingTick, "startingTick", 0, false);
            Scribe_Values.Look<float>(ref this.startingHeight, "startingHeight", 0f, false);
            Scribe_Values.Look<float>(ref this.endingHeight, "endingHeight", 0f, false);
            Scribe_Values.Look<float>(ref this.distance, "distance", 0f, false);
            Scribe_Values.Look<int>(ref this.duration, "duration", 0, false);
        }

        // Token: 0x04000239 RID: 569
        public Vector3 origin;

        // Token: 0x0400023A RID: 570
        public Vector3 destination;

        // Token: 0x0400023B RID: 571
        public int startingTick;

        // Token: 0x0400023C RID: 572
        public float startingHeight;

        // Token: 0x0400023D RID: 573
        public float endingHeight;

        // Token: 0x0400023E RID: 574
        public float distance;

        // Token: 0x0400023F RID: 575
        public int duration;
    }
}

namespace NCL.Projectiles
{
    // Token: 0x0200004D RID: 77
    public enum ProjectileStageType
    {
        // Token: 0x0400020B RID: 523
        Launch,
        // Token: 0x0400020C RID: 524
        Cruise,
        // Token: 0x0400020D RID: 525
        Terminal
    }
}

namespace NCL.Projectiles
{
    // Token: 0x0200004E RID: 78
    public class ProjectileStageConfiguration
    {
        // Token: 0x17000023 RID: 35
        // (get) Token: 0x060001AF RID: 431 RVA: 0x0000C3D0 File Offset: 0x0000A5D0
        public float TilesPerTick
        {
            get
            {
                return this.speed / 100f;
            }
        }

        // Token: 0x060001B0 RID: 432 RVA: 0x0000C3E0 File Offset: 0x0000A5E0
        public void Initialize()
        {
            this.progress = AnimationUtility.GetFunctionByName(this.progressFunction, AnimationUtility.Linear);
            this.position = AnimationUtility.GetFunctionByName(this.positionFunction, AnimationUtility.Linear);
            this.height = AnimationUtility.GetFunctionByName(this.heightFunction, AnimationUtility.Linear);
            this.arc = AnimationUtility.GetFunctionByName(this.arcFunction, AnimationUtility.Sine);
        }

        // Token: 0x0400020E RID: 526
        public string label;

        // Token: 0x0400020F RID: 527
        public ProjectileStageType type = ProjectileStageType.Terminal;

        // Token: 0x04000210 RID: 528
        public int duration = -1;

        // Token: 0x04000211 RID: 529
        public float speed = 5f;

        // Token: 0x04000212 RID: 530
        public bool overrideInitialAngle;

        // Token: 0x04000213 RID: 531
        public float angle;

        // Token: 0x04000214 RID: 532
        public bool activeTracking;

        // Token: 0x04000215 RID: 533
        public string progressFunction;

        // Token: 0x04000216 RID: 534
        public Func<float, float> progress;

        // Token: 0x04000217 RID: 535
        public Vector3 positionOffset = Vector3.zero;

        // Token: 0x04000218 RID: 536
        public string positionFunction;

        // Token: 0x04000219 RID: 537
        public Func<float, float> position;

        // Token: 0x0400021A RID: 538
        public bool alignPositionWithDestination = true;

        // Token: 0x0400021B RID: 539
        public float initialHeight;

        // Token: 0x0400021C RID: 540
        public float heightOffset;

        // Token: 0x0400021D RID: 541
        public string heightFunction;

        // Token: 0x0400021E RID: 542
        public Func<float, float> height;

        // Token: 0x0400021F RID: 543
        public float arcFactor;

        // Token: 0x04000220 RID: 544
        public string arcFunction;

        // Token: 0x04000221 RID: 545
        public Func<float, float> arc;

        // Token: 0x04000222 RID: 546
        public List<EffectDef> startEffects;

        // Token: 0x04000223 RID: 547
        public List<EffectDef> endEffects;

        // Token: 0x04000224 RID: 548
        public SoundDef startSound;
    }
}

namespace NCL.Projectiles
{
    // Token: 0x02000049 RID: 73
    public class InterceptorMapComponent : MapComponent
    {
        // Token: 0x06000191 RID: 401 RVA: 0x0000BA5C File Offset: 0x00009C5C
        public InterceptorMapComponent(Map map) : base(map)
        {
            this.mapWidth = map.Size.x;
            this.mapHeight = map.Size.z;
            this.mapTotalCells = this.mapWidth * this.mapHeight;
            this.cellGrid = new List<InterceptorGrid>[this.mapTotalCells];
        }

        // Token: 0x06000192 RID: 402 RVA: 0x0000BAC1 File Offset: 0x00009CC1
        private int GetCellIndex(Vector3 cell)
        {
            return (int)cell.z * this.mapWidth + (int)cell.x;
        }

        // Token: 0x06000193 RID: 403 RVA: 0x0000BAD9 File Offset: 0x00009CD9
        public int GetCellIndex(IntVec3 cell)
        {
            return cell.z * this.mapWidth + cell.x;
        }

        // Token: 0x06000194 RID: 404 RVA: 0x0000BAEF File Offset: 0x00009CEF
        public IntVec3 GetCell(int index)
        {
            return new IntVec3(index % this.mapWidth, 0, index / this.mapWidth);
        }

        // Token: 0x06000195 RID: 405 RVA: 0x0000BB07 File Offset: 0x00009D07
        public IEnumerable<IntVec3> GetCoveredCells(CellRect rect)
        {
            if (rect.Area < 1)
            {
                yield break;
            }
            int num;
            for (int x = rect.minX; x <= rect.maxX; x = num + 1)
            {
                for (int z = rect.minZ; z <= rect.maxZ; z = num + 1)
                {
                    int index = x + z * this.mapWidth;
                    if (index > -1 && index < this.mapTotalCells && this.cellGrid[index] != null && this.cellGrid[index].Count > 0)
                    {
                        yield return new IntVec3(x, 0, z);
                    }
                    num = z;
                }
                num = x;
            }
            yield break;
        }

        // Token: 0x06000196 RID: 406 RVA: 0x0000BB20 File Offset: 0x00009D20
        private void PaintGrid(InterceptorGrid grid, IInterceptorSource source)
        {
            if (grid == null || source == null)
            {
                return;
            }
            foreach (IntVec3 cell in ProjectileUtility.GetEffectRadiusCellsAround(source.GetSourceCell(), source.GetGridRadius(), source.GetBaseWidth()))
            {
                if (cell.InBounds(this.map))
                {
                    int index = this.GetCellIndex(cell);
                    this.PaintCell(index, grid);
                    grid.PaintCell(index, source);
                }
            }
            grid.SortCellSources();
        }

        // Token: 0x06000197 RID: 407 RVA: 0x0000BBB0 File Offset: 0x00009DB0
        private void PaintCell(int index, InterceptorGrid grid)
        {
            if (index < 0 || index >= this.cellGrid.Length)
            {
                return;
            }
            if (this.cellGrid[index] == null)
            {
                this.cellGrid[index] = new List<InterceptorGrid>(1)
                {
                    grid
                };
                return;
            }
            if (!this.cellGrid[index].Contains(grid))
            {
                this.cellGrid[index].Add(grid);
            }
        }

        // Token: 0x06000198 RID: 408 RVA: 0x0000BC0C File Offset: 0x00009E0C
        private void UnpaintGrid(InterceptorGrid grid)
        {
            foreach (int index in grid.CellIndices)
            {
                this.UnpaintCell(index, grid);
            }
            grid.ClearIndices();
        }

        // Token: 0x06000199 RID: 409 RVA: 0x0000BC60 File Offset: 0x00009E60
        private void UnpaintCell(int index, InterceptorGrid grid)
        {
            if (this.cellGrid[index] != null)
            {
                this.cellGrid[index].Remove(grid);
            }
        }

        // Token: 0x0600019A RID: 410 RVA: 0x0000BC7C File Offset: 0x00009E7C
        public void RepaintGrid(InterceptorGrid grid)
        {
            this.UnpaintGrid(grid);
            foreach (IInterceptorSource source in grid.sources)
            {
                this.PaintGrid(grid, source);
            }
            grid.dirty = false;
        }

        // Token: 0x0600019B RID: 411 RVA: 0x0000BCE0 File Offset: 0x00009EE0
        public InterceptorGrid RegisterSource(IInterceptorSource source, InterceptorGrid grid = null)
        {
            if (source == null)
            {
                return null;
            }
            if (grid == null)
            {
                return this.AddNewGrid(source);
            }
            grid.AddSource(source);
            this.PaintGrid(grid, source);
            return grid;
        }

        // Token: 0x0600019C RID: 412 RVA: 0x0000BD04 File Offset: 0x00009F04
        private InterceptorGrid AddNewGrid(IInterceptorSource source)
        {
            int num = this.nextGridId;
            this.nextGridId = num + 1;
            InterceptorGrid grid = new InterceptorGrid(this, num, source);
            this.grids.Add(grid);
            this.PaintGrid(grid, source);
            return grid;
        }

        // Token: 0x0600019D RID: 413 RVA: 0x0000BD3F File Offset: 0x00009F3F
        private void RemoveGrid(InterceptorGrid grid)
        {
            this.UnpaintGrid(grid);
            grid.ClearIndices();
            grid.mapComponent = null;
            this.grids.Remove(grid);
        }

        // Token: 0x0600019E RID: 414 RVA: 0x0000BD64 File Offset: 0x00009F64
        public void DeregisterSource(InterceptorGrid grid, IInterceptorSource source = null)
        {
            if (grid == null)
            {
                return;
            }
            if (source != null)
            {
                if (grid.sources.Contains(source))
                {
                    grid.sources.Remove(source);
                    if (grid.sources.Count < 1)
                    {
                        this.RemoveGrid(grid);
                        return;
                    }
                    grid.dirty = true;
                    return;
                }
            }
            else
            {
                this.RemoveGrid(grid);
            }
        }

        // Token: 0x0600019F RID: 415 RVA: 0x0000BDB8 File Offset: 0x00009FB8
        public bool CheckIntercept(Thing thing, Vector3 origin, Vector3 destination)
        {
            this.destinationIndex = this.GetCellIndex(destination);
            if (this.destinationIndex < 0 || this.destinationIndex >= this.mapTotalCells)
            {
                return false;
            }
            List<InterceptorGrid> grid = this.cellGrid[this.destinationIndex];
            if (grid != null)
            {
                using (List<InterceptorGrid>.Enumerator enumerator = grid.GetEnumerator())
                {
                    while (enumerator.MoveNext())
                    {
                        if (enumerator.Current.TryIntercept(thing, ref origin, ref destination))
                        {
                            return true;
                        }
                    }
                }
                return false;
            }
            return false;
        }

        // Token: 0x060001A0 RID: 416 RVA: 0x0000BE48 File Offset: 0x0000A048
        public bool CheckBombardmentIntercept(float damage, IntVec3 cell)
        {
            this.destinationIndex = this.GetCellIndex(cell);
            List<InterceptorGrid> grid = this.cellGrid[this.destinationIndex];
            if (grid != null)
            {
                using (List<InterceptorGrid>.Enumerator enumerator = grid.GetEnumerator())
                {
                    while (enumerator.MoveNext())
                    {
                        if (enumerator.Current.TryBombardmentIntercept(damage, cell))
                        {
                            return true;
                        }
                    }
                }
                return false;
            }
            return false;
        }

        // Token: 0x060001A1 RID: 417 RVA: 0x0000BEBC File Offset: 0x0000A0BC
        public override void MapComponentUpdate()
        {
            if (WorldRendererUtility.DrawingMap && Find.CurrentMap == this.map)
            {
                this.Draw();
            }
        }

        // Token: 0x060001A2 RID: 418 RVA: 0x0000BED8 File Offset: 0x0000A0D8
        private void Draw()
        {
            try
            {
                CellRect cameraRect = Find.CameraDriver.CurrentViewRect;
                cameraRect.ClipInsideMap(this.map);
                cameraRect = cameraRect.ExpandedBy(1);
                foreach (InterceptorGrid grid in this.grids)
                {
                    if (grid.dirty)
                    {
                        this.RepaintGrid(grid);
                    }
                    grid.Draw(ref cameraRect);
                }
            }
            catch (Exception e)
            {
                Log.Error(string.Format("(NCL Defense Grid) Error trying to draw in DefenseGridMapComponent on map {0}: {1}", this.map.ToStringSafe<Map>(), e));
            }
        }

        // Token: 0x060001A3 RID: 419 RVA: 0x0000BF8C File Offset: 0x0000A18C
        public override void MapComponentTick()
        {
            foreach (InterceptorGrid grid in this.grids)
            {
                if (grid.dirty)
                {
                    this.RepaintGrid(grid);
                }
            }
        }

        // Token: 0x060001A4 RID: 420 RVA: 0x0000BFE8 File Offset: 0x0000A1E8
        internal string DebugOutput()
        {
            StringBuilder str = new StringBuilder(string.Format("(NCL Projectiles) Debug output for InterceptorMapComponent with {0} active grids:\n", this.grids.Count));
            foreach (InterceptorGrid grid in this.grids)
            {
                str.AppendLine(string.Format("#{0} with {1} sources covering {2} cells", grid.id, grid.SourceCount, grid.CellCount));
            }
            return str.ToString();
        }

        // Token: 0x040001DC RID: 476
        private int nextGridId;

        // Token: 0x040001DD RID: 477
        private readonly List<InterceptorGrid> grids = new List<InterceptorGrid>();

        // Token: 0x040001DE RID: 478
        private readonly List<InterceptorGrid>[] cellGrid;

        // Token: 0x040001DF RID: 479
        private readonly int mapWidth;

        // Token: 0x040001E0 RID: 480
        private readonly int mapHeight;

        // Token: 0x040001E1 RID: 481
        private readonly int mapTotalCells;

        // Token: 0x040001E2 RID: 482
        private int destinationIndex;
    }
}

namespace NCL.Projectiles
{
    // Token: 0x0200001B RID: 27
    [StaticConstructorOnStartup]
    public static class AnimationUtility
    {
        // Token: 0x06000089 RID: 137 RVA: 0x00004E14 File Offset: 0x00003014
        public static void RegisterFunction(string name, Func<float, float> function)
        {
            if (name.NullOrEmpty())
            {
                Log.Error("(NCL Projectiles) Error: Received an attempt to register an animation function with a null or empty name");
                return;
            }
            if (AnimationUtility.functionsByName.ContainsKey(name))
            {
                Log.Warning("(NCL Projectiles) Warning: Ignoring an attempt to override an animation function with key " + name + ".");
            }
            AnimationUtility.functionsByName[name] = function;
        }

        // Token: 0x0600008A RID: 138 RVA: 0x00004E64 File Offset: 0x00003064
        public static Func<float, float> GetFunctionByName(string name, Func<float, float> defaultValue = null)
        {
            if (name.NullOrEmpty())
            {
                return defaultValue;
            }
            if (name == "None")
            {
                return null;
            }
            Func<float, float> function;
            if (AnimationUtility.functionsByName.TryGetValue(name, out function))
            {
                return function;
            }
            return defaultValue;
        }

        // Token: 0x0600008B RID: 139 RVA: 0x00004E9C File Offset: 0x0000309C
        static AnimationUtility()
        {
            AnimationUtility.RegisterFunction("Linear", AnimationUtility.Linear);
            AnimationUtility.RegisterFunction("Floor", AnimationUtility.Floor);
            AnimationUtility.RegisterFunction("Ceil", AnimationUtility.Ceil);
            AnimationUtility.RegisterFunction("Round", AnimationUtility.Round);
            AnimationUtility.RegisterFunction("FadeOutLinear", AnimationUtility.FadeOutLinear);
            AnimationUtility.RegisterFunction("FadeOutQuad", AnimationUtility.FadeOutQuad);
            AnimationUtility.RegisterFunction("FadeOutCubic", AnimationUtility.FadeOutCubic);
            AnimationUtility.RegisterFunction("Sine", AnimationUtility.Sine);
            AnimationUtility.RegisterFunction("Cosine", AnimationUtility.Cosine);
            AnimationUtility.RegisterFunction("Tangent", AnimationUtility.Tangent);
            AnimationUtility.RegisterFunction("InverseSine", AnimationUtility.InverseSine);
            AnimationUtility.RegisterFunction("UnsignedSine", AnimationUtility.UnsignedSine);
            AnimationUtility.RegisterFunction("EaseInSine", AnimationUtility.EaseInSine);
            AnimationUtility.RegisterFunction("EaseOutSine", AnimationUtility.EaseOutSine);
            AnimationUtility.RegisterFunction("EaseInOutSine", AnimationUtility.EaseInOutSine);
            AnimationUtility.RegisterFunction("EaseInQuad", AnimationUtility.EaseInQuad);
            AnimationUtility.RegisterFunction("EaseOutQuad", AnimationUtility.EaseOutQuad);
            AnimationUtility.RegisterFunction("EaseInOutQuad", AnimationUtility.EaseInOutQuad);
            AnimationUtility.RegisterFunction("EaseOutInQuad", AnimationUtility.EaseOutInQuad);
            AnimationUtility.RegisterFunction("EaseInCubic", AnimationUtility.EaseInCubic);
            AnimationUtility.RegisterFunction("EaseOutCubic", AnimationUtility.EaseOutCubic);
            AnimationUtility.RegisterFunction("EaseInOutCubic", AnimationUtility.EaseInOutCubic);
            AnimationUtility.RegisterFunction("EaseOutInCubic", AnimationUtility.EaseOutInCubic);
            AnimationUtility.RegisterFunction("Burst", AnimationUtility.Burst);
            AnimationUtility.RegisterFunction("InverseBurst", AnimationUtility.InverseBurst);
            AnimationUtility.RegisterFunction("ReverseBurst", AnimationUtility.ReverseBurst);
        }

        // Token: 0x0400007C RID: 124
        public static readonly Func<float, float> Linear = (float x) => x;

        // Token: 0x0400007D RID: 125
        public static readonly Func<float, float> Floor = (float x) => 0f;

        // Token: 0x0400007E RID: 126
        public static readonly Func<float, float> Ceil = (float x) => 1f;

        // Token: 0x0400007F RID: 127
        public static readonly Func<float, float> Round = delegate (float x)
        {
            if (x >= 0.5f)
            {
                return 1f;
            }
            return 0f;
        };

        // Token: 0x04000080 RID: 128
        public static readonly Func<float, float> FadeOutLinear = (float x) => 1f - x;

        // Token: 0x04000081 RID: 129
        public static readonly Func<float, float> FadeOutQuad = (float x) => 1f - x * x;

        // Token: 0x04000082 RID: 130
        public static readonly Func<float, float> FadeOutCubic = (float x) => 1f - x * x * x;

        // Token: 0x04000083 RID: 131
        public static readonly Func<float, float> Sine = (float x) => Mathf.Sin(x * 3.1415927f);

        // Token: 0x04000084 RID: 132
        public static readonly Func<float, float> Cosine = (float x) => Mathf.Cos(x * 3.1415927f);

        // Token: 0x04000085 RID: 133
        public static readonly Func<float, float> Tangent = (float x) => Mathf.Tan(x * 3.1415927f);

        // Token: 0x04000086 RID: 134
        public static readonly Func<float, float> InverseSine = (float x) => 1f - AnimationUtility.Sine(x);

        // Token: 0x04000087 RID: 135
        public static readonly Func<float, float> UnsignedSine = (float x) => Mathf.Sin(2f * x * 3.1415927f);

        // Token: 0x04000088 RID: 136
        public static readonly Func<float, float> EaseInSine = (float x) => 1f - Mathf.Cos(x * 3.1415927f / 2f);

        // Token: 0x04000089 RID: 137
        public static readonly Func<float, float> EaseOutSine = (float x) => Mathf.Sin(x * 3.1415927f / 2f);

        // Token: 0x0400008A RID: 138
        public static readonly Func<float, float> EaseInOutSine = (float x) => -(Mathf.Cos(3.1415927f * x) - 1f) / 2f;

        // Token: 0x0400008B RID: 139
        public static readonly Func<float, float> EaseInQuad = (float x) => x * x;

        // Token: 0x0400008C RID: 140
        public static readonly Func<float, float> EaseOutQuad = (float x) => 1f - (1f - x) * (1f - x);

        // Token: 0x0400008D RID: 141
        public static readonly Func<float, float> EaseInOutQuad = delegate (float x)
        {
            if ((double)x >= 0.5)
            {
                return 1f - Mathf.Pow(-2f * x + 2f, 2f) / 2f;
            }
            return 2f * x * x;
        };

        // Token: 0x0400008E RID: 142
        public static readonly Func<float, float> EaseOutInQuad = delegate (float x)
        {
            if ((double)x >= 0.5)
            {
                return 0.5f + 0.5f * AnimationUtility.EaseInQuad(2f * (x - 0.5f));
            }
            return 0.5f * AnimationUtility.EaseOutQuad(2f * x);
        };

        // Token: 0x0400008F RID: 143
        public static readonly Func<float, float> EaseInCubic = (float x) => x * x * x;

        // Token: 0x04000090 RID: 144
        public static readonly Func<float, float> EaseOutCubic = (float x) => 1f - Mathf.Pow(1f - x, 3f);

        // Token: 0x04000091 RID: 145
        public static readonly Func<float, float> EaseInOutCubic = delegate (float x)
        {
            if ((double)x >= 0.5)
            {
                return 1f - Mathf.Pow(-2f * x + 2f, 3f) / 2f;
            }
            return 4f * x * x * x;
        };

        // Token: 0x04000092 RID: 146
        public static readonly Func<float, float> EaseOutInCubic = delegate (float x)
        {
            if ((double)x >= 0.5)
            {
                return 0.5f + 0.5f * AnimationUtility.EaseInCubic(2f * (x - 0.5f));
            }
            return 0.5f * AnimationUtility.EaseOutCubic(2f * x);
        };

        // Token: 0x04000093 RID: 147
        public static readonly Func<float, float> Burst = (float x) => AnimationUtility.Sine(AnimationUtility.EaseOutCubic(x));

        // Token: 0x04000094 RID: 148
        public static readonly Func<float, float> InverseBurst = (float x) => 1f - AnimationUtility.Burst(x);

        // Token: 0x04000095 RID: 149
        public static readonly Func<float, float> ReverseBurst = (float x) => AnimationUtility.Sine(AnimationUtility.EaseInCubic(x));

        // Token: 0x04000096 RID: 150
        private static Dictionary<string, Func<float, float>> functionsByName = new Dictionary<string, Func<float, float>>();
    }
}

namespace NCL.Projectiles
{
    // Token: 0x0200006D RID: 109
    public class WeaponWithAttachments : ThingWithComps
    {
        // Token: 0x17000044 RID: 68
        // (get) Token: 0x0600026E RID: 622 RVA: 0x00010084 File Offset: 0x0000E284
        public ModExtension_WeaponAttachments AttachmentExtension
        {
            get
            {
                ModExtension_WeaponAttachments result;
                if ((result = this.cachedWeaponExtension) == null)
                {
                    result = (this.cachedWeaponExtension = this.def.GetModExtension<ModExtension_WeaponAttachments>());
                }
                return result;
            }
        }

        // Token: 0x17000045 RID: 69
        // (get) Token: 0x0600026F RID: 623 RVA: 0x000100AF File Offset: 0x0000E2AF
        public bool ShouldDrawNormally
        {
            get
            {
                return this.AttachmentExtension.drawWeaponNormally;
            }
        }

        // Token: 0x17000046 RID: 70
        // (get) Token: 0x06000270 RID: 624 RVA: 0x000100BC File Offset: 0x0000E2BC
        public bool TickWeaponWhileEquipped
        {
            get
            {
                return this.AttachmentExtension.tickWeaponWhileEquipped;
            }
        }

        // Token: 0x17000047 RID: 71
        // (get) Token: 0x06000271 RID: 625 RVA: 0x000100C9 File Offset: 0x0000E2C9
        public bool DrawNorthIdleMirrored
        {
            get
            {
                return this.AttachmentExtension.drawNorthIdleMirrored;
            }
        }

        // Token: 0x17000048 RID: 72
        // (get) Token: 0x06000272 RID: 626 RVA: 0x000100D6 File Offset: 0x0000E2D6
        public Pawn Wielder
        {
            get
            {
                Pawn_EquipmentTracker pawn_EquipmentTracker = base.ParentHolder as Pawn_EquipmentTracker;
                if (pawn_EquipmentTracker == null)
                {
                    return null;
                }
                return pawn_EquipmentTracker.pawn;
            }
        }

        // Token: 0x17000049 RID: 73
        // (get) Token: 0x06000273 RID: 627 RVA: 0x000100F0 File Offset: 0x0000E2F0
        public bool IsAiming
        {
            get
            {
                Pawn wielder = this.Wielder;
                object obj;
                if (wielder == null)
                {
                    obj = null;
                }
                else
                {
                    Pawn_StanceTracker stances = wielder.stances;
                    obj = ((stances != null) ? stances.curStance : null);
                }
                Stance_Busy busy = obj as Stance_Busy;
                return busy != null && !busy.neverAimWeapon;
            }
        }

        // Token: 0x1700004A RID: 74
        // (get) Token: 0x06000274 RID: 628 RVA: 0x0001012F File Offset: 0x0000E32F
        public Vector3 LastRenderedPosition
        {
            get
            {
                return this.lastRenderedPosition;
            }
        }

        // Token: 0x06000275 RID: 629 RVA: 0x00010137 File Offset: 0x0000E337
        public override void PostMake()
        {
            base.PostMake();
            this.InitializeAttachments();
        }

        // Token: 0x06000276 RID: 630 RVA: 0x00010148 File Offset: 0x0000E348
        private void InitializeAttachments()
        {
            ModExtension_WeaponAttachments extension = this.AttachmentExtension;
            if (extension != null && !extension.attachments.NullOrEmpty<WeaponAttachmentConfiguration>())
            {
                this.weaponAttachments = new List<WeaponAttachment>();
                for (int i = 0; i < extension.attachments.Count; i++)
                {
                    WeaponAttachment attachment = extension.attachments[i].CreateInstance(this);
                    this.weaponAttachments.Add(attachment);
                    if (extension.attachments[i].scaleWithParentSize)
                    {
                        this.scaleWithPawnSize = true;
                    }
                }
                for (int j = 0; j < this.weaponAttachments.Count; j++)
                {
                    this.weaponAttachments[j].PostInitialize();
                }
            }
        }

        // Token: 0x06000277 RID: 631 RVA: 0x000101F4 File Offset: 0x0000E3F4
        protected void CheckPawnScale(Thing parent)
        {
            if (this.scaleWithPawnSize)
            {
                Pawn pawn = parent as Pawn;
                if (pawn != null)
                {
                    if (this.lastEquippedPawn != pawn)
                    {
                        GraphicMeshSet humanlikeBodySetForPawn = HumanlikeMeshPoolUtility.GetHumanlikeBodySetForPawn(pawn, 1f, 1f);
                        Vector3? vector;
                        if (humanlikeBodySetForPawn == null)
                        {
                            vector = null;
                        }
                        else
                        {
                            Mesh mesh = humanlikeBodySetForPawn.MeshAt(pawn.Rotation);
                            vector = ((mesh != null) ? new Vector3?(mesh.bounds.size) : null);
                        }
                        Vector3? size = vector;
                        this.pawnScaleFactor = ((size != null) ? (size.Value.z / 1.5f) : 1f);
                        this.lastEquippedPawn = pawn;
                        return;
                    }
                }
                else
                {
                    this.pawnScaleFactor = 1f;
                }
            }
        }

        // Token: 0x06000278 RID: 632 RVA: 0x000102AC File Offset: 0x0000E4AC
        public virtual void CalculateRenderingPosition(Thing parent, Vector3 drawPosition, float aimAngle)
        {
            this.lastRenderedPosition = drawPosition;
            this.CheckPawnScale(parent);
            if (!this.ShouldDrawNormally)
            {
                this.orientationData.initialized = false;
            }
            else
            {
                ValueTuple<Mesh, Vector3, float> valueTuple = WeaponUtility.CalculateEquipmentAiming(parent, this, drawPosition, aimAngle, this.def.equippedAngleOffset, true, null);
                Mesh mesh = valueTuple.Item1;
                Vector3 position = valueTuple.Item2;
                float drawAngle = valueTuple.Item3;
                this.orientationData.mesh = mesh;
                this.orientationData.aimAngle = aimAngle;
                this.orientationData.drawAngle = drawAngle;
                this.orientationData.position = position;
                this.orientationData.rotation = Quaternion.Euler(0f, drawAngle, 0f);
                this.orientationData.initialized = true;
            }
            if (this.weaponAttachments != null)
            {
                for (int i = 0; i < this.weaponAttachments.Count; i++)
                {
                    this.weaponAttachments[i].CalculateRenderingPosition(parent, drawPosition, aimAngle);
                }
            }
        }

        // Token: 0x06000279 RID: 633 RVA: 0x00010394 File Offset: 0x0000E594
        public virtual bool DrawAttachments(Thing parent, Vector3 drawPosition, float aimAngle, bool openlyCarrying = true)
        {
            this.lastRenderedPosition = drawPosition;
            this.lastRenderedTick = Find.TickManager.TicksGame;
            if (this.weaponAttachments != null)
            {
                this.CheckPawnScale(parent);
                this.orientationData.initialized = false;
                bool drawNormally = this.ShouldDrawNormally;
                for (int i = 0; i < this.weaponAttachments.Count; i++)
                {
                    WeaponAttachment attachment = this.weaponAttachments[i];
                    if ((!openlyCarrying || attachment.config.drawWhileWielded) && (openlyCarrying || attachment.config.drawWhileNotWielded))
                    {
                        drawNormally &= attachment.Draw(parent, drawPosition, aimAngle);
                    }
                }
                return drawNormally;
            }
            return true;
        }

        // Token: 0x0600027A RID: 634 RVA: 0x00010430 File Offset: 0x0000E630
        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                this.InitializeAttachments();
            }
            if (this.weaponAttachments != null && Scribe.EnterNode("weaponAttachments"))
            {
                try
                {
                    for (int i = 0; i < this.weaponAttachments.Count; i++)
                    {
                        this.weaponAttachments[i].ExposeData();
                    }
                }
                finally
                {
                    Scribe.ExitNode();
                }
            }
        }

        // Token: 0x0600027B RID: 635 RVA: 0x000104A4 File Offset: 0x0000E6A4
        public virtual void EquippedTick()
        {
            if (this.weaponAttachments != null)
            {
                for (int i = 0; i < this.weaponAttachments.Count; i++)
                {
                    this.weaponAttachments[i].EquippedTick();
                }
            }
        }

        // Token: 0x0600027C RID: 636 RVA: 0x000104E0 File Offset: 0x0000E6E0
        public override void Notify_Equipped(Pawn pawn)
        {
            base.Notify_Equipped(pawn);
            if (pawn.Spawned && this.TickWeaponWhileEquipped)
            {
                Map map = pawn.Map;
                if (map != null)
                {
                    map.EccentricProjectilesEffectComp().RegisterTickingWeapon(this);
                }
            }
            this.SendWeaponSignal("Equipped", pawn);
        }

        // Token: 0x0600027D RID: 637 RVA: 0x0001051C File Offset: 0x0000E71C
        public override void Notify_Unequipped(Pawn pawn)
        {
            base.Notify_Unequipped(pawn);
            if (this.TickWeaponWhileEquipped)
            {
                Map map = pawn.Map;
                if (map != null)
                {
                    map.EccentricProjectilesEffectComp().DeregisterTickingWeapon(this);
                }
            }
            this.SendWeaponSignal("Unequipped", pawn);
        }

        // Token: 0x0600027E RID: 638 RVA: 0x00010550 File Offset: 0x0000E750
        public virtual void SendWeaponSignal(string signal, object value)
        {
            if (this.weaponAttachments != null)
            {
                for (int i = 0; i < this.weaponAttachments.Count; i++)
                {
                    this.weaponAttachments[i].SendWeaponSignal(signal, value);
                }
            }
        }

        // Token: 0x0600027F RID: 639 RVA: 0x00010590 File Offset: 0x0000E790
        protected virtual void CheckRenderingPositions()
        {
            int currentTick = Find.TickManager.TicksGame;
            if (this.lastRenderedTick < currentTick)
            {
                Pawn pawn = this.Wielder;
                if (pawn != null)
                {
                    ValueTuple<Vector3, float> valueTuple = WeaponUtility.CalculateEquipmentOrientation(this, pawn);
                    Vector3 position = valueTuple.Item1;
                    float aimAngle = valueTuple.Item2;
                    this.CalculateRenderingPosition(pawn, position, aimAngle);
                }
                this.lastRenderedTick = currentTick;
            }
        }

        // Token: 0x06000280 RID: 640 RVA: 0x000105E0 File Offset: 0x0000E7E0
        public virtual Vector3 GetAttachmentPosition(string label, int index = -1, bool random = false)
        {
            this.CheckRenderingPositions();

            // ??????
            if (this.weaponAttachments == null || label == null)
            {
                return this.LastRenderedPosition;
            }

            // ??????????????
            if (this.cachedAttachmentPositionGetters.TryGetValue(label, out Func<int, bool, Vector3> getter))
            {
                return getter(index, random); // ??:??????????
            }

            // ???????????
            List<WeaponAttachment> attachments = new List<WeaponAttachment>();
            foreach (WeaponAttachment attachment in this.weaponAttachments)
            {
                if (attachment.config.label == label)
                {
                    attachments.Add(attachment);
                }
            }

            // ????????????????
            if (attachments.Count == 1)
            {
                WeaponAttachment singleAttachment = attachments[0];
                this.cachedAttachmentPositionGetters[label] = (_, __) => singleAttachment.LastRenderedPosition;
            }
            else if (attachments.Count > 1)
            {
                // ??????????(??????)
                int counter = -1;
                object lockObj = new object(); // ???????????

                this.cachedAttachmentPositionGetters[label] = (idx, rand) =>
                {
                    if (rand)
                    {
                        return attachments.RandomElement().LastRenderedPosition;
                    }

                    if (idx > -1)
                    {
                        return attachments[idx % attachments.Count].LastRenderedPosition;
                    }

                    // ??????????
                    lock (lockObj)
                    {
                        counter = (counter + 1) % attachments.Count;
                        return attachments[counter].LastRenderedPosition;
                    }
                };
            }
            else // ?????
            {
                this.cachedAttachmentPositionGetters[label] = (_, __) => this.LastRenderedPosition;
            }

            // ?????????
            return this.cachedAttachmentPositionGetters[label](index, random); // ??:??????
        }


        // Token: 0x040002C8 RID: 712
        public static bool isAiming;

        // Token: 0x040002C9 RID: 713
        public const string SignalEquipped = "Equipped";

        // Token: 0x040002CA RID: 714
        public const string SignalUnequipped = "Unequipped";

        // Token: 0x040002CB RID: 715
        public const string SignalDrafted = "Drafted";

        // Token: 0x040002CC RID: 716
        public const string SignalMeleeAttack = "MeleeAttack";

        // Token: 0x040002CD RID: 717
        public const string SignalWarmupStarted = "WarmupStarted";

        // Token: 0x040002CE RID: 718
        public const string SignalWarmupCompleted = "WarmupFinished";

        // Token: 0x040002CF RID: 719
        public const string SignalShotCast = "ShotCast";

        // Token: 0x040002D0 RID: 720
        public const string SignalCooldownStarted = "CooldownStarted";

        // Token: 0x040002D1 RID: 721
        public const string SignalAmmoChanged = "AmmoChanged";

        // Token: 0x040002D2 RID: 722
        public const string SignalAbilityWarmupStarted = "AbilityWarmupStarted";

        // Token: 0x040002D3 RID: 723
        public const string SignalAbilityCast = "AbilityCast";

        // Token: 0x040002D4 RID: 724
        private const float DEFAULT_HUMAN_PAWN_SIZE = 1.5f;

        // Token: 0x040002D5 RID: 725
        public List<WeaponAttachment> weaponAttachments;

        // Token: 0x040002D6 RID: 726
        private ModExtension_WeaponAttachments cachedWeaponExtension;

        // Token: 0x040002D7 RID: 727
        public readonly WeaponOrientationData orientationData = new WeaponOrientationData();

        // Token: 0x040002D8 RID: 728
        public float pawnScaleFactor = 1f;

        // Token: 0x040002D9 RID: 729
        protected bool scaleWithPawnSize;

        // Token: 0x040002DA RID: 730
        protected Pawn lastEquippedPawn;

        // Token: 0x040002DB RID: 731
        protected Vector3 lastRenderedPosition;

        // Token: 0x040002DC RID: 732
        protected int lastRenderedTick;

        // Token: 0x040002DD RID: 733
        protected readonly Dictionary<string, Func<int, bool, Vector3>> cachedAttachmentPositionGetters = new Dictionary<string, Func<int, bool, Vector3>>();
    }
}

namespace NCL.Projectiles
{
    // Token: 0x0200004F RID: 79
    public class ModExtension_WeaponAttachments : DefModExtension
    {
        // Token: 0x060001B2 RID: 434 RVA: 0x0000C478 File Offset: 0x0000A678
        public ModExtension_WeaponAttachments()
        {
            LongEventHandler.ExecuteWhenFinished(delegate
            {
                foreach (WeaponAttachmentConfiguration weaponAttachmentConfiguration in this.attachments)
                {
                    weaponAttachmentConfiguration.Initialize(this);
                }
            });
        }

        // Token: 0x060001B3 RID: 435 RVA: 0x0000C4A4 File Offset: 0x0000A6A4
        public Vector3 GetOriginOffsetFor(Thing thing)
        {
            if (thing == null || this.originOffsets.NullOrEmpty<Vector3>())
            {
                return this.originOffset;
            }
            int incrementer = 0;
            int i;
            if (ModExtension_WeaponAttachments.originIncrementers.TryGetValue(thing.thingIDNumber, out i))
            {
                incrementer = i;
            }
            if (incrementer >= this.originOffsets.Count)
            {
                incrementer = 0;
            }
            Vector3 result = this.originOffsets[incrementer++];
            ModExtension_WeaponAttachments.originIncrementers[thing.thingIDNumber] = incrementer;
            return result;
        }

        // Token: 0x04000225 RID: 549
        private static readonly Dictionary<int, int> originIncrementers = new Dictionary<int, int>();

        // Token: 0x04000226 RID: 550
        public bool tickWeaponWhileEquipped;

        // Token: 0x04000227 RID: 551
        public bool drawWeaponNormally = true;

        // Token: 0x04000228 RID: 552
        public bool drawWhileNotWielded;

        // Token: 0x04000229 RID: 553
        public bool drawNorthIdleMirrored;

        // Token: 0x0400022A RID: 554
        public Vector3 originOffset = Vector3.zero;

        // Token: 0x0400022B RID: 555
        public List<Vector3> originOffsets;

        // Token: 0x0400022C RID: 556
        public float originDistance;

        // Token: 0x0400022D RID: 557
        public bool alignOriginOffsetWithDirection;

        // Token: 0x0400022E RID: 558
        public List<WeaponAttachmentConfiguration> attachments;
    }
}

namespace NCL.Projectiles
{
    // Token: 0x02000062 RID: 98
    public class WeaponAttachmentConfiguration
    {
        // Token: 0x06000247 RID: 583 RVA: 0x0000EBA8 File Offset: 0x0000CDA8
        public virtual void Initialize(ModExtension_WeaponAttachments parentExtension)
        {
            ShaderTypeDef shaderTypeDef = this.shaderType;
            Shader shader = ((shaderTypeDef != null) ? shaderTypeDef.Shader : null) ?? ShaderDatabase.Cutout;
            if (!this.materialPath.NullOrEmpty())
            {
                this.material = MaterialPool.MatFrom(ContentFinder<Texture2D>.Get(this.materialPath, true), shader, this.color);
                this.hasTexture = true;
            }
            if (this.directionalMaterials != null)
            {
                if (!this.directionalMaterials.north.NullOrEmpty())
                {
                    this.directionalMaterials.northMaterial = MaterialPool.MatFrom(ContentFinder<Texture2D>.Get(this.directionalMaterials.north, true), shader, this.color);
                    this.hasTexture = true;
                }
                if (!this.directionalMaterials.east.NullOrEmpty())
                {
                    this.directionalMaterials.eastMaterial = MaterialPool.MatFrom(ContentFinder<Texture2D>.Get(this.directionalMaterials.east, true), shader, this.color);
                    this.hasTexture = true;
                }
                if (!this.directionalMaterials.west.NullOrEmpty())
                {
                    this.directionalMaterials.westMaterial = MaterialPool.MatFrom(ContentFinder<Texture2D>.Get(this.directionalMaterials.west, true), shader, this.color);
                    this.hasTexture = true;
                }
                if (!this.directionalMaterials.south.NullOrEmpty())
                {
                    this.directionalMaterials.southMaterial = MaterialPool.MatFrom(ContentFinder<Texture2D>.Get(this.directionalMaterials.south, true), shader, this.color);
                    this.hasTexture = true;
                }
            }
            if (this.useParentGraphic || this.graphicData != null || this.directionalGraphicData != null)
            {
                this.hasTexture = true;
            }
        }

        // Token: 0x06000248 RID: 584 RVA: 0x0000ED34 File Offset: 0x0000CF34
        public Material GetMaterial(Rot4 rotation, Thing equipment)
        {
            if (this.directionalGraphicData != null)
            {
                if (rotation == Rot4.North && this.directionalGraphicData.north != null)
                {
                    return this.directionalGraphicData.north.Graphic.GetColoredVersion(this.directionalGraphicData.north.Graphic.Shader, equipment.DrawColor, equipment.DrawColorTwo).MatSingleFor(equipment);
                }
                if (rotation == Rot4.East && this.directionalGraphicData.east != null)
                {
                    return this.directionalGraphicData.east.Graphic.GetColoredVersion(this.directionalGraphicData.east.Graphic.Shader, equipment.DrawColor, equipment.DrawColorTwo).MatSingleFor(equipment);
                }
                if (rotation == Rot4.West && this.directionalGraphicData.west != null)
                {
                    return this.directionalGraphicData.west.Graphic.GetColoredVersion(this.directionalGraphicData.west.Graphic.Shader, equipment.DrawColor, equipment.DrawColorTwo).MatSingleFor(equipment);
                }
                if (rotation == Rot4.South && this.directionalGraphicData.south != null)
                {
                    return this.directionalGraphicData.south.Graphic.GetColoredVersion(this.directionalGraphicData.south.Graphic.Shader, equipment.DrawColor, equipment.DrawColorTwo).MatSingleFor(equipment);
                }
            }
            if (this.graphicData != null)
            {
                return this.graphicData.Graphic.GetColoredVersion(this.graphicData.Graphic.Shader, equipment.DrawColor, equipment.DrawColorTwo).MatSingleFor(equipment);
            }
            if (this.directionalMaterials == null)
            {
                return this.material;
            }
            switch (rotation.AsInt)
            {
                case 0:
                    return this.directionalMaterials.northMaterial ?? this.material;
                case 1:
                    return this.directionalMaterials.eastMaterial ?? this.material;
                case 3:
                    return this.directionalMaterials.westMaterial ?? this.material;
            }
            return this.directionalMaterials.southMaterial ?? this.material;
        }

        // Token: 0x06000249 RID: 585 RVA: 0x0000EF74 File Offset: 0x0000D174
        public Vector3 GetDrawSize(Rot4 rotation)
        {
            GraphicData graphicData = this.graphicData;
            Vector3 offset = (graphicData != null) ? graphicData.drawSize.ToRimWorldVector3() : Vector3.one;
            offset = offset.MultipliedBy(this.drawSize);
            if (this.directionalOffsets != null)
            {
                offset = offset.MultipliedBy(this.directionalOffsets.GetSize(rotation));
            }
            if (WeaponWithAttachments.isAiming)
            {
                if (this.aimingOffsets != null)
                {
                    offset = offset.MultipliedBy(this.aimingOffsets.GetSize(rotation));
                }
            }
            else if (this.idleOffsets != null)
            {
                offset = offset.MultipliedBy(this.idleOffsets.GetSize(rotation));
            }
            return offset;
        }

        // Token: 0x0600024A RID: 586 RVA: 0x0000F008 File Offset: 0x0000D208
        public Vector3 GetDrawOffset(Rot4 rotation)
        {
            Vector3 offset = this.drawOffset;
            if (this.directionalOffsets != null)
            {
                offset += this.directionalOffsets.GetOffset(rotation);
            }
            if (WeaponWithAttachments.isAiming)
            {
                if (this.aimingOffsets != null)
                {
                    offset += this.aimingOffsets.GetOffset(rotation);
                }
            }
            else if (this.idleOffsets != null)
            {
                offset += this.idleOffsets.GetOffset(rotation);
            }
            return offset;
        }

        // Token: 0x0600024B RID: 587 RVA: 0x0000F078 File Offset: 0x0000D278
        public float GetDrawAngle(Rot4 rotation, float aimAngle, bool isAiming = false)
        {
            float drawAngle = this.angleOffset.GetValueOrDefault();
            if (this.alignWithAimAngle)
            {
                drawAngle += aimAngle;
            }
            if (this.directionalOffsets != null)
            {
                drawAngle += this.directionalOffsets.GetAngle(rotation);
            }
            if (isAiming)
            {
                if (this.aimingOffsets != null)
                {
                    drawAngle += this.aimingOffsets.GetAngle(rotation);
                }
            }
            else if (this.idleOffsets != null)
            {
                drawAngle += this.idleOffsets.GetAngle(rotation);
            }
            return drawAngle;
        }

        // Token: 0x0600024C RID: 588 RVA: 0x0000F0E8 File Offset: 0x0000D2E8
        public virtual WeaponAttachment CreateInstance(ThingWithComps weapon)
        {
            return (WeaponAttachment)Activator.CreateInstance(this.attachmentClass, new object[]
            {
                weapon,
                this
            });
        }

        // Token: 0x0400026E RID: 622
        public string label;

        // Token: 0x0400026F RID: 623
        public string parent;

        // Token: 0x04000270 RID: 624
        public Type attachmentClass = typeof(WeaponAttachment);

        // Token: 0x04000271 RID: 625
        public bool drawWhileWielded = true;

        // Token: 0x04000272 RID: 626
        public bool drawWhileNotWielded;

        // Token: 0x04000273 RID: 627
        public bool forceRecalculateOrientation;

        // Token: 0x04000274 RID: 628
        public Vector3 drawOffset = Vector3.zero;

        // Token: 0x04000275 RID: 629
        public WeaponDirectionalOffsets directionalOffsets;

        // Token: 0x04000276 RID: 630
        public WeaponDirectionalOffsets aimingOffsets;

        // Token: 0x04000277 RID: 631
        public WeaponDirectionalOffsets idleOffsets;

        // Token: 0x04000278 RID: 632
        public WeaponDirectionalIdleConfiguration idle;

        // Token: 0x04000279 RID: 633
        public bool alignWithParentPosition;

        // Token: 0x0400027A RID: 634
        public bool alignOffsetWithWeaponAngle;

        // Token: 0x0400027B RID: 635
        public Vector3 drawSize = Vector3.one;

        // Token: 0x0400027C RID: 636
        public bool scaleWithParentSize = true;

        // Token: 0x0400027D RID: 637
        public float? angleOffset;

        // Token: 0x0400027E RID: 638
        public bool alignWithAimAngle;

        // Token: 0x0400027F RID: 639
        public bool useRecoil;

        // Token: 0x04000280 RID: 640
        [Unsaved(false)]
        public bool hasTexture;

        // Token: 0x04000281 RID: 641
        public bool useParentGraphic;

        // Token: 0x04000282 RID: 642
        public GraphicData graphicData;

        // Token: 0x04000283 RID: 643
        public WeaponDirectionalGraphics directionalGraphicData;

        // Token: 0x04000284 RID: 644
        public ShaderTypeDef shaderType;

        // Token: 0x04000285 RID: 645
        public Color color = Color.white;

        // Token: 0x04000286 RID: 646
        public Material material;

        // Token: 0x04000287 RID: 647
        public string materialPath;

        // Token: 0x04000288 RID: 648
        public WeaponDirectionalMaterials directionalMaterials;
    }
}

namespace NCL.Projectiles
{
    // Token: 0x02000066 RID: 102
    public class WeaponDirectionalMaterials
    {
        // Token: 0x0400029B RID: 667
        public string north;

        // Token: 0x0400029C RID: 668
        public string east;

        // Token: 0x0400029D RID: 669
        public string west;

        // Token: 0x0400029E RID: 670
        public string south;

        // Token: 0x0400029F RID: 671
        public Material northMaterial;

        // Token: 0x040002A0 RID: 672
        public Material eastMaterial;

        // Token: 0x040002A1 RID: 673
        public Material westMaterial;

        // Token: 0x040002A2 RID: 674
        public Material southMaterial;
    }
}

namespace NCL.Projectiles
{
    // Token: 0x02000032 RID: 50
    public class EffectMapComponent : MapComponent
    {
        // Token: 0x060000DE RID: 222 RVA: 0x00006B38 File Offset: 0x00004D38
        public EffectMapComponent(Map map) : base(map)
        {
            EffectMapComponent.CachedInstances[map.uniqueID] = this;
            EffectMapComponent.cachedInstance = null;
        }

        // Token: 0x060000DF RID: 223 RVA: 0x00006B9E File Offset: 0x00004D9E
        public override void MapRemoved()
        {
            base.MapRemoved();
            EffectMapComponent.CachedInstances.Remove(this.map.uniqueID);
        }

        // Token: 0x060000E0 RID: 224 RVA: 0x00006BBC File Offset: 0x00004DBC
        public override void FinalizeInit()
        {
            base.FinalizeInit();
            this.FindTickingWeaponsOnMap();
        }

        // Token: 0x060000E1 RID: 225 RVA: 0x00006BCA File Offset: 0x00004DCA
        public override void MapComponentUpdate()
        {
            if (WorldRendererUtility.DrawingMap && Find.CurrentMap == this.map && !WorldComponent_GravshipController.CutsceneInProgress)
            {
                this.Draw();
            }
        }

        // Token: 0x060000E2 RID: 226 RVA: 0x00006BED File Offset: 0x00004DED
        public override void MapComponentTick()
        {
            this.EffectTick();
            this.WeaponTick();
        }

        // Token: 0x060000E3 RID: 227 RVA: 0x00006BFC File Offset: 0x00004DFC
        private void EffectTick()
        {
            this.ResetEffectIncrementer();
            int i = 0;
            while (i < this.Effects.Count)
            {
                VisualEffect effect = this.Effects[i];
                if (effect.Tick())
                {
                    i++;
                    this.effectCountIncrementer[(int)effect.def.priority]++;
                }
                else
                {
                    this.Effects.RemoveAt(i);
                    SoundDef endSound = effect.def.endSound;
                    if (endSound != null)
                    {
                        endSound.PlayOneShot(SoundInfo.InMap(new TargetInfo(effect.IntPosition, this.map, false), MaintenanceType.None));
                    }
                }
            }
            this.UpdateEffectCounts();
        }

        // Token: 0x060000E4 RID: 228 RVA: 0x00006C98 File Offset: 0x00004E98
        private void ResetEffectIncrementer()
        {
            for (int i = 0; i < EffectMapComponent.EffectPriorityCount; i++)
            {
                this.effectCountIncrementer[i] = 0;
            }
        }

        // Token: 0x060000E5 RID: 229 RVA: 0x00006CC0 File Offset: 0x00004EC0
        private void UpdateEffectCounts()
        {
            for (int i = 0; i < EffectMapComponent.EffectPriorityCount; i++)
            {
                this.EffectCount[i] = this.effectCountIncrementer[i];
            }
        }

        // Token: 0x060000E6 RID: 230 RVA: 0x00006CF0 File Offset: 0x00004EF0
        private void Draw()
        {
            try
            {
                FogGrid fogGrid = this.map.fogGrid;
                CellRect currentViewRect = Find.CameraDriver.CurrentViewRect;
                currentViewRect.ClipInsideMap(this.map);
                currentViewRect = currentViewRect.ExpandedBy(1);
                CellIndices cellIndices = this.map.cellIndices;
                for (int i = 0; i < this.Effects.Count; i++)
                {
                    VisualEffect effect = this.Effects[i];
                    if (effect.IsInViewOf(ref currentViewRect, fogGrid, cellIndices))
                    {
                        try
                        {
                            effect.Draw();
                        }
                        catch (Exception e)
                        {
                            Log.Error(string.Format("(NCL Projectiles) Error trying to draw visual effect at {0}: {1}", effect.Position, e));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(string.Format("(NCL Projectiles) Error trying to draw visual effects: {0}", ex));
            }
        }

        // Token: 0x060000E7 RID: 231 RVA: 0x00006DC8 File Offset: 0x00004FC8


        // Token: 0x060000E8 RID: 232 RVA: 0x00006DFD File Offset: 0x00004FFD
        public void AddEffect(VisualEffect effect)
        {
            this.Effects.Add(effect);
        }

        // Token: 0x060000E9 RID: 233 RVA: 0x00006E0C File Offset: 0x0000500C
        public void CreateEffect(EffectContext context)
        {

            if (context.def.subtractParentElapsed && context.parentDuration >= context.def.duration.max)
            {
                return;
            }
            switch (context.def.type)
            {
                case EffectType.Muzzle:
                    this.CreateFlash(ref context);
                    return;
                case EffectType.Trail:
                    this.CreateTrail(ref context);
                    return;
                case EffectType.TrailOrbiter:
                    this.CreateTrailOrbiter(ref context);
                    return;
                case EffectType.LinePulse:
                    this.CreateLinePulse(ref context);
                    return;
                case EffectType.Particle:
                    this.CreateParticle(ref context);
                    return;
                case EffectType.Deformer:
                    this.CreateDeformer(ref context);
                    return;
                case EffectType.Animated:
                    this.CreateAnimated(ref context);
                    return;
                case EffectType.Flicker:
                    this.CreateFlicker(ref context);
                    return;
                case EffectType.Drifter:
                    this.CreateDrifter(ref context);
                    return;
                case EffectType.Pather:
                    this.CreatePather(ref context);
                    return;
                case EffectType.Orbiter:
                    this.CreateOrbiter(ref context);
                    return;
                case EffectType.Scatterer:
                    this.CreateScatterer(ref context);
                    return;
                case EffectType.Flipper:
                    this.CreateFlipper(ref context);
                    return;
                case EffectType.Composite:
                    this.CreateComposite(ref context);
                    return;
                case EffectType.Spawner:
                    this.CreateSpawner(ref context);
                    return;
                case EffectType.Sequencer:
                    this.CreateSequencer(ref context);
                    return;
            }
            if (EffectMapComponent.InvalidEffectDefs.Add(context.def.defName))
            {
                Log.Warning(string.Format("(NCL Projectiles) Could not create effect {0} with type {1}, skipping future attempts", context.def.LabelCap, context.def.type));
            }
        }

        // Token: 0x060000EA RID: 234 RVA: 0x00006F80 File Offset: 0x00005180
        private void CreateComposite(ref EffectContext context)
        {
            EffectDef effectDef = context.def;
            if (effectDef != null && !effectDef.subeffects.NullOrEmpty<EffectDef>())
            {
                int delayOffset = 0;
                for (int i = 0; i < effectDef.count; i++)
                {
                    Vector3 driftOffset = Vector3.zero;
                    if (effectDef.applyDriftToPosition)
                    {
                        float driftDistance = effectDef.drawDriftDistance.RandomInRange;
                        if (driftDistance != 0f)
                        {
                            driftOffset = EffectUtility.CalculateDriftOffset(driftDistance);
                        }
                    }
                    foreach (EffectDef subEffectDef in effectDef.subeffects)
                    {
                        EffectContext childContext = context.CreateSubEffectContext(subEffectDef);
                        childContext.delayOffset = context.delayOffset + delayOffset;
                        if (effectDef.applyDriftToPosition)
                        {
                            childContext.position += driftOffset;
                        }
                        if (effectDef.applyDriftToOrigin)
                        {
                            childContext.origin += driftOffset;
                        }
                        if (effectDef.applyDriftToDestination)
                        {
                            childContext.destination += driftOffset;
                        }
                        this.CreateEffect(childContext);
                    }
                    int num = delayOffset;
                    EffectDef effectDef2 = effectDef;
                    delayOffset = num + ((effectDef2.delayStep != null) ? effectDef2.delayStep.GetValueOrDefault().RandomInRange : 0);
                }
            }
        }

        // Token: 0x060000EB RID: 235 RVA: 0x000070DC File Offset: 0x000052DC
        private void CreateFlash(ref EffectContext context)
        {
            EffectDef def = context.def;
            if (def != null && def.HasMaterial && context.def.CheckInterval(context))
            {
                this.AddEffect(new VisualEffect_Flash(this, context));
            }
        }

        // Token: 0x060000EC RID: 236 RVA: 0x00007120 File Offset: 0x00005320
        private void CreateTrail(ref EffectContext context)
        {
            if (context.def != null && context.def.HasMaterial && context.destination != Vector3.zero && context.origin != context.destination && context.def.CheckInterval(context))
            {
                this.AddEffect(new VisualEffect_Line(this, context));
            }
        }

        // Token: 0x060000ED RID: 237 RVA: 0x0000718C File Offset: 0x0000538C
        private void CreateTrailOrbiter(ref EffectContext context)
        {
            if (context.def != null && context.def.count > 0 && !context.def.subeffects.NullOrEmpty<EffectDef>() && context.def.CheckInterval(context))
            {
                this.AddEffect(new VisualEffect_TrailerOrbiter(this, context));
            }
        }

        // Token: 0x060000EE RID: 238 RVA: 0x000071E8 File Offset: 0x000053E8
        private void CreateLinePulse(ref EffectContext context)
        {
            if (context.def != null && context.def.HasMaterial && context.destination != Vector3.zero && context.origin != context.destination && context.def.CheckInterval(context))
            {
                this.AddEffect(new VisualEffect_LinePulse(this, context));
            }
        }

        // Token: 0x060000EF RID: 239 RVA: 0x00007254 File Offset: 0x00005454
        private void CreateParticle(ref EffectContext context)
        {
            if (context.def != null && context.def.HasMaterial && context.def.CheckInterval(context))
            {
                for (int i = 0; i < context.def.count; i++)
                {
                    this.AddEffect(new VisualEffect_Particle(this, context));
                }
            }
        }

        // Token: 0x060000F0 RID: 240 RVA: 0x000072B1 File Offset: 0x000054B1
        private void CreateDeformer(ref EffectContext context)
        {
            if (context.def != null && context.def.HasMaterial && context.def.CheckInterval(context))
            {
                this.AddEffect(new VisualEffect_ParticleDeformer(this, context));
            }
        }

        // Token: 0x060000F1 RID: 241 RVA: 0x000072ED File Offset: 0x000054ED
        private void CreateAnimated(ref EffectContext context)
        {
            if (context.def != null && context.def.HasMaterial && context.def.CheckInterval(context))
            {
                this.AddEffect(new VisualEffect_ParticleAnimated(this, context));
            }
        }

        // Token: 0x060000F2 RID: 242 RVA: 0x00007329 File Offset: 0x00005529
        private void CreateFlicker(ref EffectContext context)
        {
            if (context.def != null && context.def.HasMaterial && context.def.CheckInterval(context))
            {
                this.AddEffect(new VisualEffect_ParticleFlicker(this, context));
            }
        }

        // Token: 0x060000F3 RID: 243 RVA: 0x00007368 File Offset: 0x00005568
        private void CreateDrifter(ref EffectContext context)
        {
            if (context.def != null && context.def.HasMaterial && context.def.CheckInterval(context))
            {
                if (context.def.count > 1)
                {
                    float angle = context.def.inheritRotation ? context.angle : 0f;
                    for (int i = 0; i < context.def.count; i++)
                    {
                        float driftAngle;
                        if (context.def.useEvenDriftSpread)
                        {
                            driftAngle = angle + Mathf.Lerp(context.def.driftOffset.min, context.def.driftOffset.max, (float)i / (float)context.def.count);
                        }
                        else
                        {
                            driftAngle = angle + context.def.driftOffset.RandomInRange;
                        }
                        Quaternion driftRotation = Quaternion.Euler(0f, driftAngle, 0f);
                        float effectiveAngle = angle + context.def.rotationOffset.RandomInRange;
                        Quaternion rotation = Quaternion.Euler(0f, effectiveAngle, 0f);
                        float distance = context.def.distance.RandomInRange;
                        if (context.def.scaleDistanceWithParent)
                        {
                            distance *= context.parentScale;
                        }
                        Vector3 destination = context.position + driftRotation * new Vector3(0f, 0f, distance);
                        EffectContext subContext = context.CreateSubEffectContext(context.def);
                        subContext.destination = destination;
                        if (context.def.startingDistance != FloatRange.Zero)
                        {
                            distance = context.def.startingDistance.RandomInRange;
                            if (context.def.scaleDistanceWithParent)
                            {
                                distance *= context.parentScale;
                            }
                            subContext.position = context.position + driftRotation * new Vector3(0f, 0f, distance);
                        }
                        if (context.def.inheritRotationFromPath)
                        {
                            subContext.rotation = rotation;
                            subContext.angle = effectiveAngle;
                        }
                        else
                        {
                            subContext.rotation = Quaternion.Euler(0f, angle, 0f);
                            subContext.angle = angle;
                        }
                        this.AddEffect(new VisualEffect_ParticleDrifter(this, subContext));
                    }
                    return;
                }
                float angle2 = (context.def.inheritRotation ? context.angle : 0f) + context.def.rotationOffset.RandomInRange;
                float distance2 = context.def.distance.RandomInRange;
                Quaternion rotation2 = Quaternion.AngleAxis(angle2, Vector3.up);
                if (context.def.scaleDistanceWithParent)
                {
                    distance2 *= context.parentScale;
                }
                Vector3 destination2 = context.position + rotation2 * (Vector3.forward * distance2);
                EffectContext subContext2 = context.CreateSubEffectContext(context.def);
                subContext2.destination = destination2;
                if (context.def.startingDistance != FloatRange.Zero)
                {
                    distance2 = context.def.startingDistance.RandomInRange;
                    if (context.def.scaleDistanceWithParent)
                    {
                        distance2 *= context.parentScale;
                    }
                    subContext2.position = context.position + rotation2 * (Vector3.forward * distance2);
                }
                this.AddEffect(new VisualEffect_ParticleDrifter(this, subContext2));
            }
        }

        // Token: 0x060000F4 RID: 244 RVA: 0x000076A8 File Offset: 0x000058A8
        private void CreatePather(ref EffectContext context)
        {
            if (context.def != null && context.def.CheckInterval(context))
            {
                float angle = context.def.inheritRotation ? context.angle : 0f;
                if (context.def.destinationDrawOffset != Vector3.zero)
                {
                    EffectContext subContext = context.CreateSubEffectContext(context.def);
                    Vector3 delta = (subContext.destination - subContext.origin).Yto0();
                    subContext.destination += Quaternion.Euler(0f, angle, 0f) * context.def.destinationDrawOffset;
                    subContext.rotation = ((delta == Vector3.zero) ? Quaternion.identity : Quaternion.LookRotation(delta));
                    subContext.angle = subContext.rotation.eulerAngles.y;
                    this.AddEffect(new VisualEffect_ParticlePather(this, subContext));
                    return;
                }
                this.AddEffect(new VisualEffect_ParticlePather(this, context));
            }
        }

        // Token: 0x060000F5 RID: 245 RVA: 0x000077C0 File Offset: 0x000059C0
        private void CreateOrbiter(ref EffectContext context)
        {
            if (context.def != null && context.def.HasMaterial && context.def.CheckInterval(context))
            {
                float angle = (context.def.inheritRotation ? context.angle : 0f) + context.def.orbitOffset.RandomInRange;
                float angleIncrement = (context.def.count > 1) ? (360f / (float)context.def.count) : 0f;
                for (int i = 0; i < context.def.count; i++)
                {
                    float effectiveAngle = (angle + angleIncrement * (float)i) % 360f;
                    EffectContext subContext = context.CreateSubEffectContext(context.def);
                    subContext.orbitAngle = effectiveAngle;
                    this.AddEffect(new VisualEffect_ParticleOrbiter(this, subContext));
                }
            }
        }

        // Token: 0x060000F6 RID: 246 RVA: 0x0000789C File Offset: 0x00005A9C
        private void CreateScatterer(ref EffectContext context)
        {
            if (context.def != null && !context.def.subeffects.NullOrEmpty<EffectDef>())
            {
                this.AddEffect(new VisualEffect_ParticleScatterer(this, context));
            }
        }

        // Token: 0x060000F7 RID: 247 RVA: 0x000078CA File Offset: 0x00005ACA
        private void CreateFlipper(ref EffectContext context)
        {
            if (context.def != null)
            {
                this.AddEffect(new VisualEffect_ParticleFlipper(this, context));
            }
        }

        // Token: 0x060000F8 RID: 248 RVA: 0x000078E6 File Offset: 0x00005AE6
        private void CreateSpawner(ref EffectContext context)
        {
            if (context.def != null && context.def.CheckInterval(context))
            {
                this.AddEffect(new VisualEffect_Spawner(this, context));
            }
        }

        // Token: 0x060000F9 RID: 249 RVA: 0x00007915 File Offset: 0x00005B15
        private void CreateSequencer(ref EffectContext context)
        {
            if (context.def != null && context.def.CheckInterval(context))
            {
                this.AddEffect(new VisualEffect_Sequencer(this, context));
            }
        }

        // Token: 0x060000FA RID: 250 RVA: 0x00007944 File Offset: 0x00005B44
        private void FindTickingWeaponsOnMap()
        {
            this.tickingWeapons.Clear();
            foreach (Pawn pawn in this.map.mapPawns.AllPawnsSpawned)
            {
                Pawn_EquipmentTracker equipment = pawn.equipment;
                WeaponWithAttachments weapon = ((equipment != null) ? equipment.Primary : null) as WeaponWithAttachments;
                if (weapon != null && weapon.TickWeaponWhileEquipped)
                {
                    this.tickingWeapons.Add(weapon);
                }
            }
        }

        // Token: 0x060000FB RID: 251 RVA: 0x000079CC File Offset: 0x00005BCC
        private void WeaponTick()
        {
            for (int i = 0; i < this.tickingWeapons.Count; i++)
            {
                this.tickingWeapons[i].EquippedTick();
            }
        }

        // Token: 0x060000FC RID: 252 RVA: 0x00007A00 File Offset: 0x00005C00
        public void RegisterTickingWeapon(WeaponWithAttachments weapon)
        {
            if (!this.tickingWeapons.Contains(weapon))
            {
                this.tickingWeapons.Add(weapon);
            }
        }

        // Token: 0x060000FD RID: 253 RVA: 0x00007A1C File Offset: 0x00005C1C
        public void DeregisterTickingWeapon(WeaponWithAttachments weapon)
        {
            this.tickingWeapons.Remove(weapon);
        }

        // Token: 0x0400014C RID: 332
        public static EffectMapComponent cachedInstance;

        // Token: 0x0400014D RID: 333
        public static readonly Dictionary<int, EffectMapComponent> CachedInstances = new Dictionary<int, EffectMapComponent>();

        // Token: 0x0400014E RID: 334
        private static readonly HashSet<string> InvalidEffectDefs = new HashSet<string>();

        // Token: 0x0400014F RID: 335
        private static readonly int EffectPriorityCount = Enum.GetValues(typeof(EffectPriority)).Length;

        // Token: 0x04000150 RID: 336
        public readonly List<VisualEffect> Effects = new List<VisualEffect>(1000);

        // Token: 0x04000151 RID: 337
        public readonly int[] EffectCount = new int[EffectMapComponent.EffectPriorityCount];

        // Token: 0x04000152 RID: 338
        private readonly int[] effectCountIncrementer = new int[EffectMapComponent.EffectPriorityCount];

        // Token: 0x04000153 RID: 339
        private readonly List<WeaponWithAttachments> tickingWeapons = new List<WeaponWithAttachments>();
    }
}

namespace NCL.Projectiles
{
    // Token: 0x02000031 RID: 49
    public class InterceptorGrid : IEquatable<InterceptorGrid>
    {
        // Token: 0x1700001A RID: 26
        // (get) Token: 0x060000D0 RID: 208 RVA: 0x000066D0 File Offset: 0x000048D0
        public IEnumerable<int> CellIndices
        {
            get
            {
                return this.cellIndices;
            }
        }

        // Token: 0x1700001B RID: 27
        // (get) Token: 0x060000D1 RID: 209 RVA: 0x000066D8 File Offset: 0x000048D8
        public int CellCount
        {
            get
            {
                return this.cellIndices.Count;
            }
        }

        // Token: 0x1700001C RID: 28
        // (get) Token: 0x060000D2 RID: 210 RVA: 0x000066E5 File Offset: 0x000048E5
        public int SourceCount
        {
            get
            {
                return this.sources.Count;
            }
        }

        // Token: 0x060000D3 RID: 211 RVA: 0x000066F4 File Offset: 0x000048F4
        public InterceptorGrid(InterceptorMapComponent mapComponent, int id, IInterceptorSource source)
        {
            this.mapComponent = mapComponent;
            this.id = id;
            if (source == null)
            {
                this.cellIndices = new List<int>();
                return;
            }
            this.AddSource(source);
            this.cellIndices = new List<int>(ProjectileUtility.GetEffectRadiusCellCount(source.GetRadius(), source.GetBaseWidth()));
        }

        // Token: 0x060000D4 RID: 212 RVA: 0x00006760 File Offset: 0x00004960
        public void ClearIndices()
        {
            this.cellIndices.Clear();
            foreach (List<IInterceptorSource> list in this.sourcesByCellIndex.Values)
            {
                list.Clear();
            }
        }

        // Token: 0x060000D5 RID: 213 RVA: 0x000067C0 File Offset: 0x000049C0
        public void PaintCell(int cellIndex, IInterceptorSource source)
        {
            this.cellIndices.Add(cellIndex);
            if (!this.sourcesByCellIndex.ContainsKey(cellIndex))
            {
                this.sourcesByCellIndex[cellIndex] = new List<IInterceptorSource>();
            }
            if (!this.sourcesByCellIndex[cellIndex].Contains(source))
            {
                this.sourcesByCellIndex[cellIndex].Add(source);
            }
        }

        // Token: 0x060000D6 RID: 214 RVA: 0x00006820 File Offset: 0x00004A20
        public void SortCellSources()
        {
            foreach (int index in this.cellIndices)
            {
                if (this.sourcesByCellIndex[index].Count > 1)
                {
                    IntVec3 cell = this.mapComponent.GetCell(index);
                    this.sourcesByCellIndex[index].SortBy((IInterceptorSource source) => source.GetSourceCell().DistanceTo(cell));
                }
            }
        }

        // Token: 0x060000D7 RID: 215 RVA: 0x000068B8 File Offset: 0x00004AB8
        public bool Equals(InterceptorGrid other)
        {
            return other != null && this.mapComponent == other.mapComponent && this.id == other.id;
        }

        // Token: 0x060000D8 RID: 216 RVA: 0x000068DB File Offset: 0x00004ADB
        public void AddSource(IInterceptorSource source)
        {
            this.sources.Add(source);
        }

        // Token: 0x060000D9 RID: 217 RVA: 0x000068E9 File Offset: 0x00004AE9
        public void RemoveSource(IInterceptorSource source)
        {
            this.sources.Remove(source);
        }

        // Token: 0x060000DA RID: 218 RVA: 0x000068F8 File Offset: 0x00004AF8
        public bool TryIntercept(Thing thing, ref Vector3 origin, ref Vector3 destination)
        {
            IntVec3 destinationCell = destination.ToIntVec3();
            List<IInterceptorSource> list;
            if (this.sourcesByCellIndex.TryGetValue(this.mapComponent.GetCellIndex(destinationCell), out list))
            {
                if (list.Count > 1)
                {
                    using (List<IInterceptorSource>.Enumerator enumerator = list.GetEnumerator())
                    {
                        while (enumerator.MoveNext())
                        {
                            if (enumerator.Current.RejectInterception(thing, origin))
                            {
                                return false;
                            }
                        }
                    }
                    using (List<IInterceptorSource>.Enumerator enumerator = list.GetEnumerator())
                    {
                        while (enumerator.MoveNext())
                        {
                            IInterceptorSource source = enumerator.Current;
                            if (source.CanIntercept(thing, origin, destination))
                            {
                                source.NotifyIntercept(thing);
                                return true;
                            }
                        }
                        return false;
                    }
                }
                if (list.Count > 0)
                {
                    IInterceptorSource source2 = Enumerable.First<IInterceptorSource>(list);
                    if (source2.CanIntercept(thing, origin, destination))
                    {
                        source2.NotifyIntercept(thing);
                        return true;
                    }
                }
            }
            return false;
        }

        // Token: 0x060000DB RID: 219 RVA: 0x00006A18 File Offset: 0x00004C18
        public bool TryBombardmentIntercept(float damage, IntVec3 cell)
        {
            List<IInterceptorSource> list;
            if (this.sourcesByCellIndex.TryGetValue(this.mapComponent.GetCellIndex(cell), out list))
            {
                foreach (IInterceptorSource source in list)
                {
                    if (source.CanInterceptBombardment(this.mapComponent.map, damage, cell))
                    {
                        source.NotifyInterceptBombardment(this.mapComponent.map, damage, cell);
                        return true;
                    }
                }
                return false;
            }
            return false;
        }

        // Token: 0x060000DC RID: 220 RVA: 0x00006AAC File Offset: 0x00004CAC
        public void Draw(ref CellRect cameraRect)
        {
            foreach (IInterceptorSource source in this.sources)
            {
                if (source.ShouldDrawField(ref cameraRect))
                {
                    source.DrawField();
                }
            }
        }

        // Token: 0x04000146 RID: 326
        public readonly int id;

        // Token: 0x04000147 RID: 327
        public InterceptorMapComponent mapComponent;

        // Token: 0x04000148 RID: 328
        public readonly List<IInterceptorSource> sources = new List<IInterceptorSource>();

        // Token: 0x04000149 RID: 329
        private readonly List<int> cellIndices;

        // Token: 0x0400014A RID: 330
        private readonly Dictionary<int, List<IInterceptorSource>> sourcesByCellIndex = new Dictionary<int, List<IInterceptorSource>>();

        // Token: 0x0400014B RID: 331
        public bool dirty;
    }
}

namespace NCL.Projectiles
{
    // Token: 0x02000029 RID: 41
    public class EffectDef : Def
    {
        // Token: 0x17000017 RID: 23
        // (get) Token: 0x060000AB RID: 171 RVA: 0x00005D90 File Offset: 0x00003F90
        public float Size
        {
            get
            {
                if (this.sizeRange.max > 0f)
                {
                    return this.size * this.sizeRange.RandomInRange;
                }
                return this.size;
            }
        }

        // Token: 0x17000018 RID: 24
        // (get) Token: 0x060000AC RID: 172 RVA: 0x00005DBD File Offset: 0x00003FBD
        public bool HasMaterial
        {
            get
            {
                return this.material != null || !this.materials.NullOrEmpty<Material>();
            }
        }

        // Token: 0x17000019 RID: 25
        // (get) Token: 0x060000AD RID: 173 RVA: 0x00005DDD File Offset: 0x00003FDD
        public Material Material
        {
            get
            {
                if (!this.materials.NullOrEmpty<Material>())
                {
                    return this.materials.RandomElement<Material>();
                }
                return this.material;
            }
        }

        // Token: 0x060000AE RID: 174 RVA: 0x00005E00 File Offset: 0x00004000
        public Material MaterialForProgress(float progress)
        {
            if (this.materials.NullOrEmpty<Material>())
            {
                return this.material;
            }
            progress = Mathf.InverseLerp(this.animationDuration.min, this.animationDuration.max, progress);
            return this.materials[Mathf.RoundToInt(progress * (float)(this.materials.Count - 1))];
        }

        // Token: 0x060000AF RID: 175 RVA: 0x00005E60 File Offset: 0x00004060
        public Material MaterialForRotation(float angle)
        {
            angle %= 360f;
            if (angle > 340f || angle < 20f)
            {
                return this.materials[0];
            }
            if (angle > 200f)
            {
                return this.materials[3];
            }
            if (angle > 160f)
            {
                return this.materials[2];
            }
            return this.materials[1];
        }

        // Token: 0x060000B0 RID: 176 RVA: 0x00005EC9 File Offset: 0x000040C9
        public bool ShouldBeActive(int ticksElapsed)
        {
            if (this.triggerAt > -1)
            {
                return ticksElapsed == this.triggerAt;
            }
            return (this.startAfter <= -1 || ticksElapsed > this.startAfter) && (this.endBefore <= -1 || ticksElapsed < this.endBefore);
        }

        // Token: 0x060000B1 RID: 177 RVA: 0x00005F07 File Offset: 0x00004107
        public bool CheckInterval(EffectContext context)
        {
            return this.CheckInterval(context.parentTicksElapsed);
        }

        // Token: 0x060000B2 RID: 178 RVA: 0x00005F15 File Offset: 0x00004115
        public bool CheckInterval(int ticksElapsed)
        {
            if (this.startAfter > 0)
            {
                ticksElapsed -= this.startAfter;
            }
            return this.interval < 2 || ticksElapsed % this.interval == 0;
        }

        // Token: 0x060000B3 RID: 179 RVA: 0x00005F40 File Offset: 0x00004140
        public override IEnumerable<string> ConfigErrors()
        {
            if (this.directionalMaterial && this.materialPaths.Count != 4)
            {
                yield return "(NCL Projectiles) EffectDef with directionalMaterial set to true but does not have exactly 4 materialPaths: " + this.defName;
            }
            yield break;
        }

        // Token: 0x060000B4 RID: 180 RVA: 0x00005F50 File Offset: 0x00004150
        public override void PostLoad()
        {
            LongEventHandler.ExecuteWhenFinished(delegate
            {
                ShaderTypeDef shaderTypeDef = this.shaderType;
                Shader shader = ((shaderTypeDef != null) ? shaderTypeDef.Shader : null) ?? ShaderDatabase.TransparentPostLight;
                if (!this.materialPath.NullOrEmpty())
                {
                    MaterialRequest req;
                    if (this.type == EffectType.Animated && this.materialPaths.NullOrEmpty<string>())
                    {
                        IEnumerable<Texture2D> enumerable = Enumerable.OrderBy<Texture2D, string>(Enumerable.Where<Texture2D>(ContentFinder<Texture2D>.GetAllInFolder(this.materialPath), (Texture2D x) => !x.name.EndsWith(Graphic_Single.MaskSuffix)), (Texture2D x) => x.name);
                        this.materials = new List<Material>();
                        using (IEnumerator<Texture2D> enumerator = enumerable.GetEnumerator())
                        {
                            while (enumerator.MoveNext())
                            {
                                Texture2D texture = enumerator.Current;
                                req = new MaterialRequest(texture, shader)
                                {
                                    shaderParameters = this.shaderParameters
                                };
                                Material mat = MaterialPool.MatFrom(req);
                                if (this.renderQueueOverride > -1)
                                {
                                    mat.renderQueue = this.renderQueueOverride;
                                }
                                this.materials.Add(mat);
                            }
                            goto IL_150;
                        }
                    }
                    req = new MaterialRequest(ContentFinder<Texture2D>.Get(this.materialPath, true), shader)
                    {
                        shaderParameters = this.shaderParameters
                    };
                    this.material = MaterialPool.MatFrom(req);
                    if (this.renderQueueOverride > -1)
                    {
                        this.material.renderQueue = this.renderQueueOverride;
                    }
                }
            IL_150:
                if (!this.materialPaths.NullOrEmpty<string>())
                {
                    this.materials = new List<Material>(this.materialPaths.Count);
                    foreach (string path in this.materialPaths)
                    {
                        MaterialRequest req = new MaterialRequest(ContentFinder<Texture2D>.Get(path, true), shader)
                        {
                            shaderParameters = this.shaderParameters
                        };
                        Material mat2 = MaterialPool.MatFrom(req);
                        if (this.renderQueueOverride > -1)
                        {
                            mat2.renderQueue = this.renderQueueOverride;
                        }
                        this.materials.Add(mat2);
                    }
                }
                this.isLargeParticle = (this.drawSize.x > 1f || this.drawSize.z > 1f);
            });
        }

        // Token: 0x040000DA RID: 218
        public static readonly int TypeCount = Enum.GetValues(typeof(EffectType)).Length;

        // Token: 0x040000DB RID: 219
        public static readonly int PriorityCount = Enum.GetValues(typeof(EffectPriority)).Length;

        // Token: 0x040000DC RID: 220
        public EffectType type;

        // Token: 0x040000DD RID: 221
        public EffectPriority priority;

        // Token: 0x040000DE RID: 222
        public List<EffectDef> subeffects;

        // Token: 0x040000DF RID: 223
        [Unsaved(false)]
        public Material material;

        // Token: 0x040000E0 RID: 224
        [Unsaved(false)]
        public List<Material> materials;

        // Token: 0x040000E1 RID: 225
        public string materialPath;

        // Token: 0x040000E2 RID: 226
        public List<string> materialPaths;

        // Token: 0x040000E3 RID: 227
        public int renderQueueOverride = -1;

        // Token: 0x040000E4 RID: 228
        public int materialInterval = 1;

        // Token: 0x040000E5 RID: 229
        public bool randomizeMaterial;

        // Token: 0x040000E6 RID: 230
        public bool randomizeAngle;

        // Token: 0x040000E7 RID: 231
        public bool directionalMaterial;

        // Token: 0x040000E8 RID: 232
        public AltitudeLayer altitude = AltitudeLayer.Projectile;

        // Token: 0x040000E9 RID: 233
        public int altitudeAdjustment;

        // Token: 0x040000EA RID: 234
        public int altitudeDrift;

        // Token: 0x040000EB RID: 235
        public bool syncAltitudeDrift;

        // Token: 0x040000EC RID: 236
        public ShaderTypeDef shaderType;

        // Token: 0x040000ED RID: 237
        public List<ShaderParameter> shaderParameters;

        // Token: 0x040000EE RID: 238
        public ColorCurve colorCurve;

        // Token: 0x040000EF RID: 239
        public Color? color;

        // Token: 0x040000F0 RID: 240
        public int count = 1;

        // Token: 0x040000F1 RID: 241
        public float size = 1f;

        // Token: 0x040000F2 RID: 242
        public float minSize;

        // Token: 0x040000F3 RID: 243
        public FloatRange sizeRange = FloatRange.Zero;

        // Token: 0x040000F4 RID: 244
        public float length = 1f;

        // Token: 0x040000F5 RID: 245
        public float minWidth;

        // Token: 0x040000F6 RID: 246
        public float minLength;

        // Token: 0x040000F7 RID: 247
        public float opacity = 1f;

        // Token: 0x040000F8 RID: 248
        public float minOpacity;

        // Token: 0x040000F9 RID: 249
        public Vector3 drawSize = Vector3.one;

        // Token: 0x040000FA RID: 250
        public Vector3 drawOffset = Vector3.zero;

        // Token: 0x040000FB RID: 251
        public Vector3 destinationDrawOffset = Vector3.zero;

        // Token: 0x040000FC RID: 252
        public FloatRange drawDriftDistance = FloatRange.Zero;

        // Token: 0x040000FD RID: 253
        public bool applyDriftToPosition = true;

        // Token: 0x040000FE RID: 254
        public bool applyDriftToDestination;

        // Token: 0x040000FF RID: 255
        public bool applyDriftToOrigin;

        // Token: 0x04000100 RID: 256
        public FloatRange driftOffset = FloatRange.Zero;

        // Token: 0x04000101 RID: 257
        public FloatRange height = FloatRange.Zero;

        // Token: 0x04000102 RID: 258
        public FloatRange startingDistance = FloatRange.Zero;

        // Token: 0x04000103 RID: 259
        public FloatRange distance = FloatRange.Zero;

        // Token: 0x04000104 RID: 260
        public FloatRange rotationOffset = FloatRange.Zero;

        // Token: 0x04000105 RID: 261
        public FloatRange rotationRate = FloatRange.Zero;

        // Token: 0x04000106 RID: 262
        public float minRadius;

        // Token: 0x04000107 RID: 263
        public float radius;

        // Token: 0x04000108 RID: 264
        public FloatRange orbitRate = FloatRange.Zero;

        // Token: 0x04000109 RID: 265
        public FloatRange orbitOffset = FloatRange.Zero;

        // Token: 0x0400010A RID: 266
        public FloatRange flipRate = FloatRange.Zero;

        // Token: 0x0400010B RID: 267
        public FloatRange flipOffset = FloatRange.Zero;

        // Token: 0x0400010C RID: 268
        public bool useEvenDriftSpread;

        // Token: 0x0400010D RID: 269
        public bool drawIfIntercepted = true;

        // Token: 0x0400010E RID: 270
        public bool scaleSizeWithParent;

        // Token: 0x0400010F RID: 271
        public bool scaleDistanceWithParent;

        // Token: 0x04000110 RID: 272
        public bool attachToOrigin;

        // Token: 0x04000111 RID: 273
        public bool attachToParent;

        // Token: 0x04000112 RID: 274
        public bool attachToTarget;

        // Token: 0x04000113 RID: 275
        public bool attachPersistently = true;

        // Token: 0x04000114 RID: 276
        public bool inheritRotation = true;

        // Token: 0x04000115 RID: 277
        public bool inheritRotationFromPath;

        // Token: 0x04000116 RID: 278
        public bool inheritRotationFromOrbit;

        // Token: 0x04000117 RID: 279
        public bool applyRotationToDrawOffset = true;

        // Token: 0x04000118 RID: 280
        public bool applyRotationToDestinationDrawOffset = true;

        // Token: 0x04000119 RID: 281
        public bool applyRotationToOrbit = true;

        // Token: 0x0400011A RID: 282
        public bool mirrorWestRotations;

        // Token: 0x0400011B RID: 283
        public bool neverDrawRotated;

        // Token: 0x0400011C RID: 284
        public bool useColorOverride = true;

        // Token: 0x0400011D RID: 285
        public AdditionalMotionProperties additionalMotion;

        // Token: 0x0400011E RID: 286
        public SoundDef startSound;

        // Token: 0x0400011F RID: 287
        public SoundDef endSound;

        // Token: 0x04000120 RID: 288
        public IntRange duration = new IntRange(60, 60);

        // Token: 0x04000121 RID: 289
        public FloatRange animationDuration = FloatRange.ZeroToOne;

        // Token: 0x04000122 RID: 290
        public IntRange? delay;

        // Token: 0x04000123 RID: 291
        public IntRange? delayStep;

        // Token: 0x04000124 RID: 292
        public int interval;

        // Token: 0x04000125 RID: 293
        public int triggerAt = -1;

        // Token: 0x04000126 RID: 294
        public int startAfter = -1;

        // Token: 0x04000127 RID: 295
        public int endBefore = -1;

        // Token: 0x04000128 RID: 296
        public int flipStopsAt = -1;

        // Token: 0x04000129 RID: 297
        public bool inheritDuration;

        // Token: 0x0400012A RID: 298
        public bool subtractParentElapsed;

        // Token: 0x0400012B RID: 299
        public bool randomize;

        // Token: 0x0400012C RID: 300
        public float chance = 1f;

        // Token: 0x0400012D RID: 301
        public string progressFunction;

        // Token: 0x0400012E RID: 302
        public string sizeFunction;

        // Token: 0x0400012F RID: 303
        public string widthFunction;

        // Token: 0x04000130 RID: 304
        public string lengthFunction;

        // Token: 0x04000131 RID: 305
        public string heightFunction;

        // Token: 0x04000132 RID: 306
        public string pathingFunction;

        // Token: 0x04000133 RID: 307
        public string opacityFunction;

        // Token: 0x04000134 RID: 308
        public string rotationFunction;

        // Token: 0x04000135 RID: 309
        public string radiusFunction;

        // Token: 0x04000136 RID: 310
        public string flipFunction;

        // Token: 0x04000137 RID: 311
        public string colorFunction;

        // Token: 0x04000138 RID: 312
        [Unsaved(false)]
        public bool isLargeParticle;
    }
}

namespace NCL.Projectiles
{
    // Token: 0x02000061 RID: 97
    [StaticConstructorOnStartup]
    public class WeaponAttachment : IExposable
    {
        // Token: 0x17000040 RID: 64
        // (get) Token: 0x06000234 RID: 564 RVA: 0x0000E750 File Offset: 0x0000C950
        public WeaponOrientationData OrientationData
        {
            get
            {
                return this.orientationData;
            }
        }

        // Token: 0x17000041 RID: 65
        // (get) Token: 0x06000235 RID: 565 RVA: 0x0000E758 File Offset: 0x0000C958
        public Vector3 LastRenderedPosition
        {
            get
            {
                return this.lastRenderedPosition;
            }
        }

        // Token: 0x17000042 RID: 66
        // (get) Token: 0x06000236 RID: 566 RVA: 0x0000E760 File Offset: 0x0000C960
        public virtual float EquippedAngleOffset
        {
            get
            {
                float? angleOffset = this.config.angleOffset;
                if (angleOffset == null)
                {
                    return this.weapon.def.equippedAngleOffset;
                }
                return angleOffset.GetValueOrDefault();
            }
        }

        // Token: 0x06000237 RID: 567 RVA: 0x0000E79A File Offset: 0x0000C99A
        public WeaponAttachment(WeaponWithAttachments weapon, WeaponAttachmentConfiguration config)
        {
            this.weapon = weapon;
            this.config = config;
        }

        // Token: 0x06000238 RID: 568 RVA: 0x0000E7B0 File Offset: 0x0000C9B0
        public virtual void PostInitialize()
        {
        }

        // Token: 0x06000239 RID: 569 RVA: 0x0000E7B4 File Offset: 0x0000C9B4
        public virtual WeaponOrientationData InitializeOrientationData(Thing parent, Vector3 location, float aimAngle, bool applyOffsets = true)
        {
            WeaponOrientationData data = this.weapon.orientationData.Clone();
            if (!data.initialized || this.config.forceRecalculateOrientation)
            {
                ValueTuple<Mesh, Vector3, float> valueTuple = WeaponUtility.CalculateEquipmentAiming(parent, this.weapon, location, aimAngle, this.EquippedAngleOffset, this.config.useRecoil, this.config.idle);
                Mesh mesh = valueTuple.Item1;
                Vector3 effectivePosition = valueTuple.Item2;
                float drawAngle = valueTuple.Item3;
                data.mesh = mesh;
                data.aimAngle = aimAngle;
                data.drawAngle = drawAngle;
                if (applyOffsets)
                {
                    data.rotation = this.CalculateDrawRotation(parent, effectivePosition, drawAngle);
                    data.position = this.CalculateDrawPosition(parent, effectivePosition, drawAngle);
                }
                else
                {
                    data.rotation = Quaternion.Euler(0f, drawAngle, 0f);
                    data.position = effectivePosition;
                }
                data.initialized = true;
            }
            return data;
        }

        // Token: 0x0600023A RID: 570 RVA: 0x0000E886 File Offset: 0x0000CA86
        public virtual void CalculateRenderingPosition(Thing parent, Vector3 location, float aimAngle)
        {
            this.lastRenderedPosition = this.CalculateDrawPosition(parent, location, aimAngle);
        }

        // Token: 0x0600023B RID: 571 RVA: 0x0000E898 File Offset: 0x0000CA98
        public virtual bool Draw(Thing parent, Vector3 location, float aimAngle)
        {
            this.CalculateRenderingPosition(parent, location, aimAngle);
            if (this.config.hasTexture)
            {
                this.DrawInternal(MeshPool.plane10, this.GetMaterial(parent, location, aimAngle), this.lastRenderedPosition, this.CalculateDrawRotation(parent, location, aimAngle), this.CalculateDrawSize(parent, location, aimAngle));
            }
            return true;
        }

        // Token: 0x0600023C RID: 572 RVA: 0x0000E8E8 File Offset: 0x0000CAE8
        protected Material GetCachedMaterial(Rot4 rotation, ThingWithComps weapon)
        {
            // ?????????????
            switch (rotation.AsInt)
            {
                case 0: // North
                    return GetOrCreateMaterial(ref this.materials.north, rotation, weapon);
                case 1: // East
                    return GetOrCreateMaterial(ref this.materials.east, rotation, weapon);
                case 3: // West
                    return GetOrCreateMaterial(ref this.materials.west, rotation, weapon);
                default: // South (??case 2)
                    return GetOrCreateMaterial(ref this.materials.south, rotation, weapon);
            }
        }

        // ????:???????
        private Material GetOrCreateMaterial(ref Material materialField, Rot4 rotation, ThingWithComps weapon)
        {
            // ?????????????
            if (materialField == null)
            {
                materialField = this.config.GetMaterial(rotation, weapon);
            }
            return materialField;
        }


        // Token: 0x0600023D RID: 573 RVA: 0x0000E9C3 File Offset: 0x0000CBC3
        protected virtual Material GetMaterial(Thing parent, Vector3 location, float aimAngle)
        {
            if (this.config.useParentGraphic)
            {
                return this.weapon.Graphic.MatSingleFor(this.weapon);
            }
            return this.GetCachedMaterial(parent.Rotation, this.weapon);
        }

        // Token: 0x0600023E RID: 574 RVA: 0x0000E9FC File Offset: 0x0000CBFC
        protected virtual Vector3 CalculateDrawPosition(Thing parent, Vector3 location, float drawAngle)
        {
            Vector3 offset = this.config.GetDrawOffset(parent.Rotation);
            if (this.config.scaleWithParentSize)
            {
                offset *= this.weapon.pawnScaleFactor;
            }
            if (this.config.alignOffsetWithWeaponAngle)
            {
                offset = Quaternion.Euler(0f, drawAngle, 0f) * offset;
            }
            return (this.config.alignWithParentPosition ? parent.DrawPosHeld.GetValueOrDefault(location) : location) + offset;
        }

        // Token: 0x0600023F RID: 575 RVA: 0x0000EA84 File Offset: 0x0000CC84
        protected virtual Quaternion CalculateDrawRotation(Thing parent, Vector3 location, float aimAngle)
        {
            float drawAngle = this.config.GetDrawAngle(parent.Rotation, aimAngle, WeaponWithAttachments.isAiming);
            return Quaternion.Euler(0f, drawAngle, 0f);
        }

        // Token: 0x06000240 RID: 576 RVA: 0x0000EABC File Offset: 0x0000CCBC
        protected virtual Vector3 CalculateDrawSize(Thing parent, Vector3 location, float aimAngle)
        {
            Vector3 drawSize;
            if (this.config.useParentGraphic)
            {
                drawSize = this.weapon.DrawSize.ToRimWorldVector3();
            }
            else
            {
                drawSize = this.config.GetDrawSize(parent.Rotation);
            }
            if (this.config.scaleWithParentSize)
            {
                drawSize *= this.weapon.pawnScaleFactor;
            }
            return drawSize;
        }

        // Token: 0x06000241 RID: 577 RVA: 0x0000EB1C File Offset: 0x0000CD1C
        protected virtual void DrawInternal(Mesh mesh, Material material, Vector3 position, Quaternion quaternion, Vector3 size)
        {
            if (material != null)
            {
                Matrix4x4 matrix = Matrix4x4.TRS(position, quaternion, size);
                Graphics.DrawMesh(mesh, matrix, material, 0);
            }
        }

        // Token: 0x06000242 RID: 578 RVA: 0x0000EB48 File Offset: 0x0000CD48
        protected virtual void DrawInternal(Mesh mesh, Material material, Vector3 position, Quaternion quaternion, Vector3 size, Color color)
        {
            if (material != null)
            {
                WeaponAttachment.materialPropertyBlock.Clear();
                WeaponAttachment.materialPropertyBlock.SetColor("_Color", color);
                Matrix4x4 matrix = Matrix4x4.TRS(position, quaternion, size);
                Graphics.DrawMesh(mesh, matrix, material, 0, null, 0, WeaponAttachment.materialPropertyBlock);
            }
        }

        // Token: 0x06000243 RID: 579 RVA: 0x0000EB94 File Offset: 0x0000CD94
        public virtual void EquippedTick()
        {
        }

        // Token: 0x06000244 RID: 580 RVA: 0x0000EB96 File Offset: 0x0000CD96
        public virtual void ExposeData()
        {
        }

        // Token: 0x06000245 RID: 581 RVA: 0x0000EB98 File Offset: 0x0000CD98
        public virtual void SendWeaponSignal(string signal, object value)
        {
        }

        // Token: 0x04000268 RID: 616
        protected static readonly MaterialPropertyBlock materialPropertyBlock = new MaterialPropertyBlock();

        // Token: 0x04000269 RID: 617
        public WeaponWithAttachments weapon;

        // Token: 0x0400026A RID: 618
        public WeaponAttachmentConfiguration config;

        // Token: 0x0400026B RID: 619
        protected WeaponOrientationData orientationData;

        // Token: 0x0400026C RID: 620
        protected WeaponAttachment.CachedDirectionalMaterials materials;

        // Token: 0x0400026D RID: 621
        protected Vector3 lastRenderedPosition;

        // Token: 0x02000082 RID: 130
        protected struct CachedDirectionalMaterials
        {
            // Token: 0x04000338 RID: 824
            public Material north;

            // Token: 0x04000339 RID: 825
            public Material east;

            // Token: 0x0400033A RID: 826
            public Material west;

            // Token: 0x0400033B RID: 827
            public Material south;
        }
    }
}

namespace NCL.Projectiles
{
    // Token: 0x0200006E RID: 110
    public class WeaponOrientationData
    {
        // Token: 0x1700004B RID: 75
        // (get) Token: 0x06000282 RID: 642 RVA: 0x0001076D File Offset: 0x0000E96D
        public string DebugString
        {
            get
            {
                return string.Format("(position={0},aim={1},draw={2})", this.position, this.aimAngle, this.drawAngle);
            }
        }

        // Token: 0x06000283 RID: 643 RVA: 0x0001079C File Offset: 0x0000E99C
        public void CopyFrom(WeaponOrientationData other)
        {
            this.initialized = other.initialized;
            this.mesh = other.mesh;
            this.position = other.position;
            this.aimAngle = other.aimAngle;
            this.drawAngle = other.drawAngle;
            this.rotation = other.rotation;
        }

        // Token: 0x06000284 RID: 644 RVA: 0x000107F1 File Offset: 0x0000E9F1
        public void CopyFromIfNotInitialized(WeaponOrientationData other)
        {
            if (!this.initialized)
            {
                this.CopyFrom(other);
            }
        }

        // Token: 0x06000285 RID: 645 RVA: 0x00010802 File Offset: 0x0000EA02
        public WeaponOrientationData Clone()
        {
            WeaponOrientationData weaponOrientationData = new WeaponOrientationData();
            weaponOrientationData.CopyFrom(this);
            return weaponOrientationData;
        }

        // Token: 0x040002DE RID: 734
        public bool initialized;

        // Token: 0x040002DF RID: 735
        public Mesh mesh;

        // Token: 0x040002E0 RID: 736
        public Vector3 position;

        // Token: 0x040002E1 RID: 737
        public float aimAngle;

        // Token: 0x040002E2 RID: 738
        public float drawAngle;

        // Token: 0x040002E3 RID: 739
        public Quaternion rotation;
    }
}

namespace NCL.Projectiles
{
    // Token: 0x02000065 RID: 101
    public class WeaponDirectionalOffsets
    {
        // Token: 0x06000252 RID: 594 RVA: 0x0000F238 File Offset: 0x0000D438
        public Vector3 GetSize(Rot4 rotation)
        {
            switch (rotation.AsInt)
            {
                case 0:
                    return this.northSize;
                case 1:
                    return this.eastSize;
                case 3:
                    return this.westSize;
            }
            return this.southSize;
        }

        // Token: 0x06000253 RID: 595 RVA: 0x0000F288 File Offset: 0x0000D488
        public Vector3 GetOffset(Rot4 rotation)
        {
            switch (rotation.AsInt)
            {
                case 0:
                    return this.north;
                case 1:
                    return this.east;
                case 3:
                    return this.west;
            }
            return this.south;
        }

        // Token: 0x06000254 RID: 596 RVA: 0x0000F2D8 File Offset: 0x0000D4D8
        public float GetAngle(Rot4 rotation)
        {
            switch (rotation.AsInt)
            {
                case 0:
                    return this.northAngle;
                case 1:
                    return this.eastAngle;
                case 3:
                    return this.westAngle;
            }
            return this.southAngle;
        }

        // Token: 0x0400028F RID: 655
        public Vector3 northSize = Vector3.one;

        // Token: 0x04000290 RID: 656
        public Vector3 eastSize = Vector3.one;

        // Token: 0x04000291 RID: 657
        public Vector3 westSize = Vector3.one;

        // Token: 0x04000292 RID: 658
        public Vector3 southSize = Vector3.one;

        // Token: 0x04000293 RID: 659
        public Vector3 north = Vector3.zero;

        // Token: 0x04000294 RID: 660
        public Vector3 east = Vector3.zero;

        // Token: 0x04000295 RID: 661
        public Vector3 west = Vector3.zero;

        // Token: 0x04000296 RID: 662
        public Vector3 south = Vector3.zero;

        // Token: 0x04000297 RID: 663
        public float northAngle;

        // Token: 0x04000298 RID: 664
        public float eastAngle;

        // Token: 0x04000299 RID: 665
        public float westAngle;

        // Token: 0x0400029A RID: 666
        public float southAngle;
    }
}

namespace NCL.Projectiles
{
    // Token: 0x02000063 RID: 99
    public class WeaponDirectionalIdleConfiguration
    {
        // Token: 0x0600024E RID: 590 RVA: 0x0000F15C File Offset: 0x0000D35C
        public WeaponIdleConfiguration GetConfigurationForRotation(Rot4 rotation)
        {
            switch (rotation.AsInt)
            {
                case 0:
                    return this.north;
                case 1:
                    return this.east;
                case 3:
                    return this.west;
            }
            return this.south;
        }

        // Token: 0x0600024F RID: 591 RVA: 0x0000F1AC File Offset: 0x0000D3AC
        public void ApplyConfiguration(ref float angle, ref bool flipped, Rot4 rotation)
        {
            WeaponIdleConfiguration config = this.GetConfigurationForRotation(rotation);
            if (config != null)
            {
                angle = config.angle;
                flipped = config.flipped;
            }
        }

        // Token: 0x04000289 RID: 649
        public WeaponIdleConfiguration north = new WeaponIdleConfiguration();

        // Token: 0x0400028A RID: 650
        public WeaponIdleConfiguration east = new WeaponIdleConfiguration();

        // Token: 0x0400028B RID: 651
        public WeaponIdleConfiguration west = new WeaponIdleConfiguration
        {
            angle = 217f,
            flipped = true
        };

        // Token: 0x0400028C RID: 652
        public WeaponIdleConfiguration south = new WeaponIdleConfiguration();
    }
}

namespace NCL.Projectiles
{
    // Token: 0x02000064 RID: 100
    public class WeaponIdleConfiguration
    {
        // Token: 0x0400028D RID: 653
        public float angle = 143f;

        // Token: 0x0400028E RID: 654
        public bool flipped;
    }
}

namespace NCL.Projectiles
{
    // Token: 0x02000059 RID: 89
    public abstract class VisualEffect
    {
        // Token: 0x17000038 RID: 56
        // (get) Token: 0x06000213 RID: 531 RVA: 0x0000DD25 File Offset: 0x0000BF25
        protected float DurationFactor
        {
            get
            {
                return (float)this.duration / (float)this.def.duration.max;
            }
        }

        // Token: 0x06000214 RID: 532 RVA: 0x0000DD40 File Offset: 0x0000BF40
        public VisualEffect(EffectMapComponent parentComponent, EffectContext context)
        {
            this.parentComponent = parentComponent;
            this.def = context.def;
            this.PreInitialize(context);
            this.Initialize(context);
        }

        // Token: 0x06000215 RID: 533 RVA: 0x0000DD80 File Offset: 0x0000BF80
        protected virtual void PreInitialize(EffectContext context)
        {
            if (this.def.attachToParent)
            {
                this.anchor = context.anchor;
                if (this.anchor != null)
                {
                    this.SetPosition(this.anchor.DrawPosHeld ?? Vector3.zero, true);
                }
            }
            else if (this.def.attachToTarget)
            {
                if (this.def.attachPersistently)
                {
                    this.anchor = context.destinationAnchor;
                    if (this.anchor != null)
                    {
                        this.SetPosition(this.anchor.DrawPosHeld ?? Vector3.zero, true);
                    }
                }
                else
                {
                    this.SetPosition(context.destination, true);
                }
            }
            else if (this.def.attachToOrigin)
            {
                this.SetPosition(context.origin, true);
            }
            else
            {
                this.SetPosition(context.position, true);
            }
            this.minSize = this.def.minSize;
            this.baseSize = this.def.Size;
            if (this.def.scaleSizeWithParent)
            {
                this.minSize *= context.parentScale;
                this.baseSize *= context.parentScale;
            }
            this.sizeFactor = this.baseSize;
            this.sizeFunction = AnimationUtility.GetFunctionByName(this.def.sizeFunction, null);
            EffectDef effectDef = this.def;
            this.delay = ((effectDef.delay != null) ? effectDef.delay.GetValueOrDefault().RandomInRange : 0) + context.delayOffset;
            this.duration = ((this.def.inheritDuration && context.parentDuration > 0) ? context.parentDuration : this.def.duration.RandomInRange);
            if (this.def.subtractParentElapsed)
            {
                this.duration -= context.parentTicksElapsed;
            }
            this.progressFunction = AnimationUtility.GetFunctionByName(this.def.progressFunction, null);
        }

        // Token: 0x06000216 RID: 534
        protected abstract void Initialize(EffectContext context);

        // Token: 0x06000217 RID: 535 RVA: 0x0000DF88 File Offset: 0x0000C188
        protected virtual void SetPosition(Vector3 pos, bool normalize = true)
        {
            if (normalize)
            {
                pos.y = this.def.altitude.AltitudeFor((float)this.def.altitudeAdjustment);
            }
            this.position = pos;
            this.intPosition = this.position.ToIntVec3();
            this.intPositionIndex = this.parentComponent.map.cellIndices.CellToIndex(this.intPosition);
            this.inBounds = this.intPosition.InBounds(this.parentComponent.map);
        }

        // Token: 0x17000039 RID: 57
        // (get) Token: 0x06000218 RID: 536 RVA: 0x0000E010 File Offset: 0x0000C210
        public Vector3 Position
        {
            get
            {
                return this.position;
            }
        }

        // Token: 0x1700003A RID: 58
        // (get) Token: 0x06000219 RID: 537 RVA: 0x0000E018 File Offset: 0x0000C218
        public IntVec3 IntPosition
        {
            get
            {
                return this.intPosition;
            }
        }

        // Token: 0x1700003B RID: 59
        // (get) Token: 0x0600021A RID: 538 RVA: 0x0000E020 File Offset: 0x0000C220
        protected float RawProgress
        {
            get
            {
                return (float)this.progressTicks / (float)this.duration;
            }
        }

        // Token: 0x1700003C RID: 60
        // (get) Token: 0x0600021B RID: 539 RVA: 0x0000E031 File Offset: 0x0000C231
        protected float Progress
        {
            get
            {
                if (this.progressFunction != null)
                {
                    return this.progressFunction(this.RawProgress);
                }
                return this.RawProgress;
            }
        }

        // Token: 0x1700003D RID: 61
        // (get) Token: 0x0600021C RID: 540 RVA: 0x0000E053 File Offset: 0x0000C253
        public virtual bool IsDone
        {
            get
            {
                return this.progressTicks > this.duration;
            }
        }

        // Token: 0x1700003E RID: 62
        // (get) Token: 0x0600021D RID: 541 RVA: 0x0000E063 File Offset: 0x0000C263
        public bool IsActive
        {
            get
            {
                return this.delay < 1;
            }
        }

        // Token: 0x0600021E RID: 542 RVA: 0x0000E070 File Offset: 0x0000C270
        public virtual bool IsInViewOf(ref CellRect viewRect, FogGrid fogGrid, CellIndices cellIndices)
        {
            if (this.def.isLargeParticle)
            {
                return this.inBounds && viewRect.ShouldBeVisibleFrom(this.intPosition, this.def.drawSize) && !fogGrid.IsFogged(this.intPositionIndex);
            }
            return this.inBounds && viewRect.Contains(this.intPosition) && !fogGrid.IsFogged(this.intPositionIndex);
        }

        // Token: 0x0600021F RID: 543 RVA: 0x0000E0EC File Offset: 0x0000C2EC
        public virtual bool Tick()
        {
            if (this.delay > 0)
            {
                this.delay--;
            }
            else
            {
                this.progressTicks++;
                this.progress = this.Progress;
                if (this.def.startSound != null && !this.startSoundPlayed)
                {
                    this.def.startSound.PlayOneShot(SoundInfo.InMap(new TargetInfo(this.intPosition, this.parentComponent.map, false), MaintenanceType.None));
                    this.startSoundPlayed = true;
                }
            }
            return !this.IsDone && this.CalculateSize();
        }

        // Token: 0x06000220 RID: 544 RVA: 0x0000E185 File Offset: 0x0000C385
        protected float LerpSizeFactor(float value)
        {
            if (this.minSize <= 0f)
            {
                return this.baseSize * value;
            }
            return Mathf.Lerp(this.minSize, this.baseSize, value);
        }

        // Token: 0x06000221 RID: 545 RVA: 0x0000E1AF File Offset: 0x0000C3AF
        protected virtual bool CalculateSize()
        {
            if (this.sizeFunction != null)
            {
                this.sizeFactor = this.LerpSizeFactor(this.sizeFunction(this.progress));
            }
            return true;
        }

        // Token: 0x06000222 RID: 546 RVA: 0x0000E1D7 File Offset: 0x0000C3D7
        public virtual void Draw()
        {
            if (this.IsActive && !this.IsDone)
            {
                this.DrawInternal();
            }
        }

        // Token: 0x06000223 RID: 547 RVA: 0x0000E1EF File Offset: 0x0000C3EF
        public virtual void End()
        {
            this.progressTicks = this.duration + 1;
        }

        // Token: 0x06000224 RID: 548
        protected abstract void DrawInternal();

        // Token: 0x0400024D RID: 589
        public EffectMapComponent parentComponent;

        // Token: 0x0400024E RID: 590
        public EffectDef def;

        // Token: 0x0400024F RID: 591
        public Thing anchor;

        // Token: 0x04000250 RID: 592
        protected Vector3 position;

        // Token: 0x04000251 RID: 593
        protected IntVec3 intPosition;

        // Token: 0x04000252 RID: 594
        protected int intPositionIndex;

        // Token: 0x04000253 RID: 595
        protected bool inBounds;

        // Token: 0x04000254 RID: 596
        protected int delay;

        // Token: 0x04000255 RID: 597
        protected int duration;

        // Token: 0x04000256 RID: 598
        protected int progressTicks;

        // Token: 0x04000257 RID: 599
        protected float progress;

        // Token: 0x04000258 RID: 600
        protected Func<float, float> progressFunction;

        // Token: 0x04000259 RID: 601
        protected float minSize;

        // Token: 0x0400025A RID: 602
        protected float baseSize = 1f;

        // Token: 0x0400025B RID: 603
        protected float sizeFactor = 1f;

        // Token: 0x0400025C RID: 604
        protected Func<float, float> sizeFunction;

        // Token: 0x0400025D RID: 605
        protected bool startSoundPlayed;
    }
}


namespace NCL.Projectiles
{
    // Token: 0x0200006C RID: 108
    public static class WeaponUtility
    {
        // Token: 0x0600026B RID: 619 RVA: 0x0000FD14 File Offset: 0x0000DF14
        public static ValueTuple<Vector3, float> CalculateEquipmentOrientation(Thing equipment, Pawn wielder)
        {
            if (equipment == null || wielder == null)
            {
                return new ValueTuple<Vector3, float>(Vector3.zero, 0f);
            }
            float distanceFactor = wielder.ageTracker.CurLifeStage.equipmentDrawDistanceFactor;
            Vector3 drawPos = wielder.DrawPosHeld ?? Vector3.zero;
            float angle = 0f;
            Job curJob = wielder.CurJob;
            if (curJob != null)
            {
                JobDef def = curJob.def;
                if (def != null && !def.neverShowWeapon)
                {
                    Pawn_StanceTracker stances = wielder.stances;
                    Stance_Busy stance = ((stances != null) ? stances.curStance : null) as Stance_Busy;
                    if (stance != null && stance.focusTarg.IsValid)
                    {
                        Vector3 destination = stance.focusTarg.CenterVector3;
                        if ((destination - drawPos).MagnitudeHorizontalSquared() > 0.001f)
                        {
                            angle = (destination - drawPos).Yto0().AngleFlat();
                        }
                        Verb verb = wielder.CurrentEffectiveVerb;
                        if (verb != null && verb.AimAngleOverride != null)
                        {
                            angle = verb.AimAngleOverride.Value;
                        }
                        drawPos += (WeaponUtility.BaseEquippedDistanceOffset + new Vector3(0f, 0f, equipment.def.equippedDistanceOffset)).RotatedBy(angle) * distanceFactor;
                        WeaponWithAttachments.isAiming = true;
                        return new ValueTuple<Vector3, float>(drawPos, angle);
                    }
                }
            }
            WeaponWithAttachments.isAiming = false;
            angle = 143f;
            switch (wielder.Rotation.AsInt)
            {
                case 0:
                    {
                        drawPos += WeaponUtility.EquipOffsetNorth * distanceFactor;
                        WeaponWithAttachments weaponWithAttachments = equipment as WeaponWithAttachments;
                        if (weaponWithAttachments != null && weaponWithAttachments.DrawNorthIdleMirrored)
                        {
                            angle = 217f;
                        }
                        break;
                    }
                case 1:
                    drawPos += WeaponUtility.EquipOffsetEast * distanceFactor;
                    break;
                case 2:
                    drawPos += WeaponUtility.EquipOffsetSouth * distanceFactor;
                    break;
                case 3:
                    drawPos += WeaponUtility.EquipOffsetWest * distanceFactor;
                    angle = 217f;
                    break;
            }
            return new ValueTuple<Vector3, float>(drawPos, angle);
        }

        // Token: 0x0600026C RID: 620 RVA: 0x0000FF1C File Offset: 0x0000E11C
        public static ValueTuple<Mesh, Vector3, float> CalculateEquipmentAiming(Thing parent, Thing equipment, Vector3 location, float aimAngle, float equippedAngleOffset, bool useRecoil, WeaponDirectionalIdleConfiguration idleConfig = null)
        {
            Mesh mesh = MeshPool.plane10;
            float drawAngle = aimAngle - 90f;
            if (idleConfig != null && parent != null && equipment != null && !WeaponWithAttachments.isAiming)
            {
                bool flipped = false;
                idleConfig.ApplyConfiguration(ref aimAngle, ref flipped, parent.Rotation);
                drawAngle = aimAngle - 90f;
                if (flipped)
                {
                    mesh = MeshPool.plane10Flip;
                    drawAngle -= 180f;
                    drawAngle -= equippedAngleOffset;
                }
                else
                {
                    drawAngle += equippedAngleOffset;
                }
                useRecoil = false;
            }
            else if (aimAngle > 200f && aimAngle < 340f)
            {
                mesh = MeshPool.plane10Flip;
                drawAngle -= 180f;
                drawAngle -= equippedAngleOffset;
            }
            else
            {
                drawAngle += equippedAngleOffset;
            }
            drawAngle %= 360f;
            if (useRecoil)
            {
                CompEquippable equippable = equipment.TryGetComp<CompEquippable>();
                if (equippable != null)
                {
                    Vector3 drawOffset;
                    float angleOffset;
                    EquipmentUtility.Recoil(equipment.def, EquipmentUtility.GetRecoilVerb(equippable.AllVerbs), out drawOffset, out angleOffset, aimAngle);
                    location += drawOffset;
                    drawAngle += angleOffset;
                }
            }
            return new ValueTuple<Mesh, Vector3, float>(mesh, location, drawAngle);
        }

        // Token: 0x040002C1 RID: 705
        public const float DefaultIdleAngle = 143f;

        // Token: 0x040002C2 RID: 706
        public const float ReverseIdleAngle = 217f;

        // Token: 0x040002C3 RID: 707
        public static readonly Vector3 BaseEquippedDistanceOffset = new Vector3(0f, 0f, 0.4f);

        // Token: 0x040002C4 RID: 708
        public static readonly Vector3 EquipOffsetNorth = new Vector3(0f, 0f, -0.11f);

        // Token: 0x040002C5 RID: 709
        public static readonly Vector3 EquipOffsetEast = new Vector3(0.22f, 0f, -0.22f);

        // Token: 0x040002C6 RID: 710
        public static readonly Vector3 EquipOffsetSouth = new Vector3(0f, 0f, -0.22f);

        // Token: 0x040002C7 RID: 711
        public static readonly Vector3 EquipOffsetWest = new Vector3(-0.22f, 0f, -0.22f);
    }
}

namespace NCL.Projectiles
{
    // Token: 0x02000027 RID: 39
    public enum EffectType
    {
        // Token: 0x040000C4 RID: 196
        Undefined,
        // Token: 0x040000C5 RID: 197
        Muzzle,
        // Token: 0x040000C6 RID: 198
        Trail,
        // Token: 0x040000C7 RID: 199
        TrailOrbiter,
        // Token: 0x040000C8 RID: 200
        LinePulse,
        // Token: 0x040000C9 RID: 201
        Particle,
        // Token: 0x040000CA RID: 202
        Deformer,
        // Token: 0x040000CB RID: 203
        Animated,
        // Token: 0x040000CC RID: 204
        Flicker,
        // Token: 0x040000CD RID: 205
        Drifter,
        // Token: 0x040000CE RID: 206
        Pather,
        // Token: 0x040000CF RID: 207
        Orbiter,
        // Token: 0x040000D0 RID: 208
        Scatterer,
        // Token: 0x040000D1 RID: 209
        Flipper,
        // Token: 0x040000D2 RID: 210
        Composite,
        // Token: 0x040000D3 RID: 211
        Spawner,
        // Token: 0x040000D4 RID: 212
        Sequencer
    }
}

namespace NCL.Projectiles
{
    // Token: 0x02000026 RID: 38
    public struct EffectContext
    {
        // Token: 0x060000A8 RID: 168 RVA: 0x00005B6C File Offset: 0x00003D6C
        public EffectContext(Map map, EffectDef def)
        {
            this.anchor = null;
            this.destinationAnchor = null;
            this.position = default(Vector3);
            this.origin = default(Vector3);
            this.destination = default(Vector3);
            this.rotation = default(Quaternion);
            this.angle = 0f;
            this.orbitAngle = 0f;
            this.parentDuration = 0;
            this.parentTicksElapsed = 0;
            this.delayOffset = 0;
            this.color = null;
            this.parentScale = 1f;
            this.map = map;
            this.def = def;
        }

        // Token: 0x060000A9 RID: 169 RVA: 0x00005C08 File Offset: 0x00003E08
        public EffectContext CreateSubEffectContext(EffectDef subEffectDef)
        {
            return new EffectContext(this.map, subEffectDef)
            {
                anchor = this.anchor,
                destinationAnchor = this.destinationAnchor,
                position = this.position,
                origin = this.origin,
                destination = this.destination,
                rotation = this.rotation,
                angle = this.angle,
                orbitAngle = this.orbitAngle,
                parentScale = this.parentScale,
                parentDuration = this.parentDuration,
                parentTicksElapsed = this.parentTicksElapsed,
                delayOffset = this.delayOffset,
                color = this.color
            };
        }

        // Token: 0x060000AA RID: 170 RVA: 0x00005CD0 File Offset: 0x00003ED0
        public EffectContext CreateSubEffectContext(EffectDef subEffectDef, int ticksElapsed)
        {
            return new EffectContext(this.map, subEffectDef)
            {
                anchor = this.anchor,
                destinationAnchor = this.destinationAnchor,
                position = this.position,
                origin = this.origin,
                destination = this.destination,
                rotation = this.rotation,
                angle = this.angle,
                orbitAngle = this.orbitAngle,
                parentScale = this.parentScale,
                parentDuration = this.parentDuration,
                parentTicksElapsed = ticksElapsed,
                delayOffset = this.delayOffset,
                color = this.color
            };
        }

        // Token: 0x040000B4 RID: 180
        public Map map;

        // Token: 0x040000B5 RID: 181
        public EffectDef def;

        // Token: 0x040000B6 RID: 182
        public Thing anchor;

        // Token: 0x040000B7 RID: 183
        public Thing destinationAnchor;

        // Token: 0x040000B8 RID: 184
        public Vector3 position;

        // Token: 0x040000B9 RID: 185
        public Vector3 origin;

        // Token: 0x040000BA RID: 186
        public Vector3 destination;

        // Token: 0x040000BB RID: 187
        public Quaternion rotation;

        // Token: 0x040000BC RID: 188
        public float angle;

        // Token: 0x040000BD RID: 189
        public float orbitAngle;

        // Token: 0x040000BE RID: 190
        public float parentScale;

        // Token: 0x040000BF RID: 191
        public int parentDuration;

        // Token: 0x040000C0 RID: 192
        public int parentTicksElapsed;

        // Token: 0x040000C1 RID: 193
        public int delayOffset;

        // Token: 0x040000C2 RID: 194
        public Color? color;
    }
}
namespace NCL.Projectiles
{
    // Token: 0x0200003B RID: 59
    public class VisualEffect_Flash : VisualEffect_Particle
    {
        // Token: 0x0600012F RID: 303 RVA: 0x00008E3C File Offset: 0x0000703C
        public VisualEffect_Flash(EffectMapComponent parentComponent, EffectContext context) : base(parentComponent, context)
        {
        }
    }
}
namespace NCL.Projectiles
{
    // Token: 0x0200003A RID: 58
    public class VisualEffect_Line : VisualEffect_Particle
    {
        // Token: 0x06000126 RID: 294 RVA: 0x00008BB8 File Offset: 0x00006DB8
        public VisualEffect_Line(EffectMapComponent parentComponent, EffectContext context) : base(parentComponent, context)
        {
        }

        // Token: 0x06000127 RID: 295 RVA: 0x00008BC4 File Offset: 0x00006DC4
        protected override void PreInitialize(EffectContext context)
        {
            base.PreInitialize(context);
            Vector3 destination = context.destination;
            if (this.def.applyRotationToDestinationDrawOffset)
            {
                destination += context.rotation * this.def.destinationDrawOffset;
            }
            else
            {
                destination += this.def.destinationDrawOffset;
            }
            this.Destination = destination;
        }

        // Token: 0x06000128 RID: 296 RVA: 0x00008C24 File Offset: 0x00006E24
        protected override void Initialize(EffectContext context)
        {
            base.Initialize(context);
            if (this.def.attachToTarget && this.destinationAnchor != null)
            {
                this.destinationAnchor = context.destinationAnchor;
                this.destinationOffset = this.def.destinationDrawOffset;
                this.CalculateDestinationPosition();
            }
        }

        // Token: 0x1700001E RID: 30
        // (get) Token: 0x06000129 RID: 297 RVA: 0x00008C71 File Offset: 0x00006E71
        // (set) Token: 0x0600012A RID: 298 RVA: 0x00008C7C File Offset: 0x00006E7C
        public Vector3 Destination
        {
            get
            {
                return this.destination;
            }
            set
            {
                this.destination = value;
                this.destination.y = this.def.altitude.AltitudeFor((float)this.def.altitudeAdjustment);
                this.intDestination = this.destination.ToIntVec3();
                this.intDestinationIndex = this.parentComponent.map.cellIndices.CellToIndex(this.intDestination);
                this.inBoundsDestination = this.intDestination.InBounds(this.parentComponent.map);
            }
        }

        // Token: 0x0600012B RID: 299 RVA: 0x00008D05 File Offset: 0x00006F05
        public override bool IsInViewOf(ref CellRect viewRect, FogGrid fogGrid, CellIndices cellIndices)
        {
            return base.IsInViewOf(ref viewRect, fogGrid, cellIndices) || (this.inBoundsDestination && viewRect.Contains(this.intDestination) && !fogGrid.IsFogged(this.intDestinationIndex));
        }

        // Token: 0x0600012C RID: 300 RVA: 0x00008D3B File Offset: 0x00006F3B
        protected override bool CalculatePosition()
        {
            return base.CalculatePosition() && this.CalculateDestinationPosition();
        }

        // Token: 0x0600012D RID: 301 RVA: 0x00008D50 File Offset: 0x00006F50
        protected virtual bool CalculateDestinationPosition()
        {
            if (this.destinationAnchor != null && base.IsActive)
            {
                if (!this.destinationAnchor.SpawnedOrAnyParentSpawned)
                {
                    return false;
                }
                this.Destination = (this.destinationAnchor.DrawPosHeld ?? (Vector3.zero + this.destinationOffset));
            }
            return true;
        }

        // Token: 0x0600012E RID: 302 RVA: 0x00008DB4 File Offset: 0x00006FB4
        protected override void DrawInternal()
        {
            if (base.Position != this.Destination)
            {
                Vector3 look = this.Destination - base.Position;
                float length = look.MagnitudeHorizontal() * this.def.length;
                if (length > 0.01f)
                {
                    Matrix4x4 matrix = Matrix4x4.TRS(base.Position + look / 2f, Quaternion.LookRotation(look), new Vector3(this.sizeFactor, 1f, length));
                    this.DrawInternal(ref matrix);
                }
            }
        }

        // Token: 0x0400019B RID: 411
        protected Vector3 destination;

        // Token: 0x0400019C RID: 412
        protected IntVec3 intDestination;

        // Token: 0x0400019D RID: 413
        protected int intDestinationIndex;

        // Token: 0x0400019E RID: 414
        protected bool inBoundsDestination;

        // Token: 0x0400019F RID: 415
        public Vector3 destinationOffset;

        // Token: 0x040001A0 RID: 416
        public Thing destinationAnchor;
    }
}
namespace NCL.Projectiles
{
    // Token: 0x0200003C RID: 60
    public class VisualEffect_LinePulse : VisualEffect_Line
    {
        // Token: 0x06000130 RID: 304 RVA: 0x00008E46 File Offset: 0x00007046
        public VisualEffect_LinePulse(EffectMapComponent parentComponent, EffectContext context) : base(parentComponent, context)
        {
        }

        // Token: 0x06000131 RID: 305 RVA: 0x00008E50 File Offset: 0x00007050
        protected override void PreInitialize(EffectContext context)
        {
            base.PreInitialize(context);
            this.delta = (this.destination - this.position).Yto0();
            this.pulseFunction = AnimationUtility.GetFunctionByName(this.def.pathingFunction, AnimationUtility.Linear);
        }

        // Token: 0x06000132 RID: 306 RVA: 0x00008E90 File Offset: 0x00007090
        protected override void Initialize(EffectContext context)
        {
            base.Initialize(context);
            this.CalculatePulse();
        }

        // Token: 0x06000133 RID: 307 RVA: 0x00008EA0 File Offset: 0x000070A0
        public override bool Tick()
        {
            return base.Tick() && this.CalculatePulse();
        }

        // Token: 0x06000134 RID: 308 RVA: 0x00008EB4 File Offset: 0x000070B4
        protected virtual bool CalculatePulse()
        {
            if (this.anchor != null || this.destinationAnchor != null)
            {
                this.delta = (this.destination - this.position).Yto0();
                this.rotation = ((this.delta == Vector3.zero) ? Quaternion.identity : Quaternion.LookRotation(this.delta));
                this.angle = this.rotation.eulerAngles.y;
            }
            this.pulseProgress = this.pulseFunction(base.Progress);
            this.pulseLength = 2f * ((this.pulseProgress < 0.5f) ? this.pulseProgress : (1f - this.pulseProgress));
            this.pulsePosition = this.position + this.pulseProgress * this.delta;
            this.pulsePosition.y = this.def.altitude.AltitudeFor((float)this.def.altitudeAdjustment);
            return true;
        }

        // Token: 0x06000135 RID: 309 RVA: 0x00008FBC File Offset: 0x000071BC
        protected override void DrawInternal()
        {
            if (this.pulseLength > 0f)
            {
                Vector3 lineVector = new Vector3(this.sizeFactor, 1f, this.pulseLength * this.delta.MagnitudeHorizontal());
                Matrix4x4 matrix = default(Matrix4x4);
                matrix.SetTRS(this.pulsePosition, this.rotation, lineVector);
                Graphics.DrawMesh(MeshPool.plane10, matrix, this.material, 0);
            }
        }

        // Token: 0x040001A1 RID: 417
        protected Vector3 delta;

        // Token: 0x040001A2 RID: 418
        protected Vector3 pulsePosition;

        // Token: 0x040001A3 RID: 419
        protected float pulseLength;

        // Token: 0x040001A4 RID: 420
        protected float pulseProgress;

        // Token: 0x040001A5 RID: 421
        protected Func<float, float> pulseFunction;
    }
}
namespace NCL.Projectiles
{
    // Token: 0x0200003D RID: 61
    [StaticConstructorOnStartup]
    public class VisualEffect_Particle : VisualEffect
    {
        // Token: 0x06000136 RID: 310 RVA: 0x00009028 File Offset: 0x00007228
        public VisualEffect_Particle(EffectMapComponent parentComponent, EffectContext context) : base(parentComponent, context)
        {
            if (this.def.colorCurve != null)
            {
                this.colorFunction = AnimationUtility.GetFunctionByName(this.def.colorFunction, AnimationUtility.Linear);
            }
            if (this.def.useColorOverride)
            {
                Color? color = context.color;
                this.color = ((color != null) ? color : this.def.color);
            }
        }

        // Token: 0x06000137 RID: 311 RVA: 0x000090AC File Offset: 0x000072AC
        protected override void PreInitialize(EffectContext context)
        {
            base.PreInitialize(context);
            this.drawSize = this.def.drawSize;
            if (this.def.inheritRotationFromPath)
            {
                this.rotation = ((context.destination == context.origin) ? Quaternion.identity : Quaternion.LookRotation(context.destination - context.origin));
                this.angle = (this.originalAngle = this.rotation.eulerAngles.y);
            }
            else
            {
                this.angle = (this.originalAngle = (this.def.inheritRotation ? context.angle : 0f));
            }
            if (this.def.mirrorWestRotations)
            {
                this.angle += this.def.rotationOffset.RandomInRange;
                this.angle %= 360f;
                if (this.angle < 0f)
                {
                    this.angle += 360f;
                }
                if (this.angle > 90f && this.angle <= 270f)
                {
                    this.angle += 180f;
                    this.angle %= 360f;
                    this.mesh = MeshPool.plane10Flip;
                }
            }
            else
            {
                this.angle += this.def.rotationOffset.RandomInRange;
            }
            this.rotationFunction = AnimationUtility.GetFunctionByName(this.def.rotationFunction, null);
            this.rotationRate = this.def.rotationRate.RandomInRange;
            this.rotation = Quaternion.Euler(0f, this.angle, 0f);
            this.rotationDelta = Quaternion.Euler(0f, this.rotationRate, 0f);
            this.opacityFunction = AnimationUtility.GetFunctionByName(this.def.opacityFunction, AnimationUtility.FadeOutLinear);
            this.opacity = this.def.opacity;
            this.positionOffset = (this.def.applyRotationToDrawOffset ? (Quaternion.Euler(0f, this.originalAngle, 0f) * this.def.drawOffset) : this.def.drawOffset);
            if (this.def.applyDriftToPosition)
            {
                float driftDistance = this.def.drawDriftDistance.RandomInRange;
                if (driftDistance != 0f)
                {
                    this.positionOffset += EffectUtility.CalculateDriftOffset(driftDistance);
                }
            }
            if (this.def.directionalMaterial)
            {
                this.material = this.def.MaterialForRotation(this.angle);
                this.angle = 0f;
                this.rotation = Quaternion.identity;
                return;
            }
            this.material = this.def.Material;
        }

        // Token: 0x06000138 RID: 312 RVA: 0x00009381 File Offset: 0x00007581
        protected override void Initialize(EffectContext context)
        {
            this.CalculateOpacity();
            if (this.anchor != null)
            {
                this.CalculatePosition();
                return;
            }
            this.SetPosition(this.position + this.positionOffset, true);
        }

        // Token: 0x06000139 RID: 313 RVA: 0x000093B2 File Offset: 0x000075B2
        public override bool Tick()
        {
            return base.Tick() && this.CalculateRotation() && this.CalculatePosition() && this.CalculateOpacity();
        }

        // Token: 0x0600013A RID: 314 RVA: 0x000093D4 File Offset: 0x000075D4
        protected virtual bool CalculateRotation()
        {
            if (base.IsActive && this.rotationRate != 0f)
            {
                if (this.rotationFunction != null)
                {
                    this.rotationDelta = Quaternion.Euler(0f, this.rotationRate * this.rotationFunction(base.Progress), 0f);
                }
                this.rotation *= this.rotationDelta;
            }
            return true;
        }

        // Token: 0x0600013B RID: 315 RVA: 0x00009444 File Offset: 0x00007644
        protected virtual bool CalculatePosition()
        {
            if (this.anchor != null && base.IsActive)
            {
                Vector3 pos = VisualEffect_Particle.GetAnchorPosition(this.anchor);
                if (pos == Vector3.zero)
                {
                    return false;
                }
                this.SetPosition(pos + this.positionOffset, true);
            }
            return true;
        }

        // Token: 0x0600013C RID: 316 RVA: 0x00009490 File Offset: 0x00007690
        protected float LerpOpacity(float value)
        {
            if (this.def.minOpacity <= 0f)
            {
                return this.def.opacity * value;
            }
            return Mathf.Lerp(this.def.minOpacity, this.def.opacity, value);
        }

        // Token: 0x0600013D RID: 317 RVA: 0x000094CE File Offset: 0x000076CE
        protected virtual bool CalculateOpacity()
        {
            if (this.opacityFunction != null && base.IsActive)
            {
                this.opacity = this.LerpOpacity(this.opacityFunction(base.Progress));
            }
            return true;
        }

        // Token: 0x0600013E RID: 318 RVA: 0x00009500 File Offset: 0x00007700
        protected static Vector3 GetAnchorPosition(Thing anchor)
        {
            Vector3? vector = (anchor != null) ? anchor.DrawPosHeld : null;
            if (vector == null)
            {
                return Vector3.zero;
            }
            return vector.GetValueOrDefault();
        }

        // Token: 0x0600013F RID: 319 RVA: 0x00009538 File Offset: 0x00007738
        protected override void DrawInternal()
        {
            if (this.material == null)
            {
                return;
            }
            Matrix4x4 matrix = Matrix4x4.TRS(base.Position, this.rotation, this.drawSize * this.sizeFactor);
            this.DrawInternal(ref matrix);
        }

        // Token: 0x06000140 RID: 320 RVA: 0x00009580 File Offset: 0x00007780
        protected virtual void DrawInternal(ref Matrix4x4 matrix)
        {
            if (this.color != null)
            {
                Color c = this.color.Value;
                c.a *= this.opacity;
                VisualEffect_Particle.materialPropertyBlock.Clear();
                VisualEffect_Particle.materialPropertyBlock.SetColor(ShaderPropertyIDs.Color, c);
                Graphics.DrawMesh(this.mesh, matrix, this.material, 0, null, 0, VisualEffect_Particle.materialPropertyBlock);
                return;
            }
            if (this.colorFunction != null && this.def.colorCurve != null)
            {
                Color c2 = this.def.colorCurve.Evaluate(this.colorFunction(this.progress));
                c2.a *= this.opacity;
                VisualEffect_Particle.materialPropertyBlock.Clear();
                VisualEffect_Particle.materialPropertyBlock.SetColor(ShaderPropertyIDs.Color, c2);
                Graphics.DrawMesh(this.mesh, matrix, this.material, 0, null, 0, VisualEffect_Particle.materialPropertyBlock);
                return;
            }
            Graphics.DrawMesh(this.mesh, matrix, FadedMaterialPool.FadedVersionOf(this.material, this.opacity), 0);
        }

        // Token: 0x040001A6 RID: 422
        protected Material material;

        // Token: 0x040001A7 RID: 423
        protected Mesh mesh = MeshPool.plane10;

        // Token: 0x040001A8 RID: 424
        protected Vector3 drawSize;

        // Token: 0x040001A9 RID: 425
        protected Vector3 positionOffset;

        // Token: 0x040001AA RID: 426
        protected float angle;

        // Token: 0x040001AB RID: 427
        protected float originalAngle;

        // Token: 0x040001AC RID: 428
        protected Quaternion rotation;

        // Token: 0x040001AD RID: 429
        protected float rotationRate;

        // Token: 0x040001AE RID: 430
        protected Quaternion rotationDelta;

        // Token: 0x040001AF RID: 431
        protected Func<float, float> rotationFunction;

        // Token: 0x040001B0 RID: 432
        protected float opacity = 1f;

        // Token: 0x040001B1 RID: 433
        protected Func<float, float> opacityFunction;

        // Token: 0x040001B2 RID: 434
        protected Color? color;

        // Token: 0x040001B3 RID: 435
        protected Func<float, float> colorFunction;

        // Token: 0x040001B4 RID: 436
        protected static readonly MaterialPropertyBlock materialPropertyBlock = new MaterialPropertyBlock();
    }
}
namespace NCL.Projectiles
{
    // Token: 0x0200003E RID: 62
    public class VisualEffect_ParticleAnimated : VisualEffect_Particle
    {
        // Token: 0x06000142 RID: 322 RVA: 0x000096A2 File Offset: 0x000078A2
        public VisualEffect_ParticleAnimated(EffectMapComponent parentComponent, EffectContext context) : base(parentComponent, context)
        {
        }

        // Token: 0x06000143 RID: 323 RVA: 0x000096AC File Offset: 0x000078AC
        protected override void PreInitialize(EffectContext context)
        {
            base.PreInitialize(context);
            if (this.def.opacityFunction.NullOrEmpty())
            {
                this.opacityFunction = null;
            }
        }

        // Token: 0x06000144 RID: 324 RVA: 0x000096CE File Offset: 0x000078CE
        protected override void Initialize(EffectContext context)
        {
            base.Initialize(context);
            this.CalculateMaterial();
        }

        // Token: 0x06000145 RID: 325 RVA: 0x000096DE File Offset: 0x000078DE
        public override bool Tick()
        {
            return base.Tick() && this.CalculateMaterial();
        }

        // Token: 0x06000146 RID: 326 RVA: 0x000096F0 File Offset: 0x000078F0
        protected virtual bool CalculateMaterial()
        {
            if (this.def.randomizeMaterial && this.progressTicks % this.def.materialInterval == 0)
            {
                this.material = this.def.Material;
                if (this.def.randomizeAngle)
                {
                    this.angle = this.originalAngle + this.def.rotationOffset.RandomInRange;
                    this.rotation = Quaternion.Euler(0f, this.angle, 0f);
                }
            }
            else
            {
                this.material = this.def.MaterialForProgress(this.progress);
            }
            return this.material != null;
        }
    }
}
namespace NCL.Projectiles
{
    // Token: 0x0200003F RID: 63
    public class VisualEffect_ParticleDeformer : VisualEffect_Particle
    {
        // Token: 0x06000147 RID: 327 RVA: 0x00009799 File Offset: 0x00007999
        public VisualEffect_ParticleDeformer(EffectMapComponent parentComponent, EffectContext context) : base(parentComponent, context)
        {
        }

        // Token: 0x06000148 RID: 328 RVA: 0x000097A3 File Offset: 0x000079A3
        protected override void PreInitialize(EffectContext context)
        {
            base.PreInitialize(context);
            this.widthFunction = AnimationUtility.GetFunctionByName(this.def.widthFunction, null);
            this.lengthFunction = AnimationUtility.GetFunctionByName(this.def.lengthFunction, null);
        }

        // Token: 0x06000149 RID: 329 RVA: 0x000097DA File Offset: 0x000079DA
        protected override void Initialize(EffectContext context)
        {
            base.Initialize(context);
        }

        // Token: 0x0600014A RID: 330 RVA: 0x000097E3 File Offset: 0x000079E3
        protected float GetWidth(float value)
        {
            if (this.def.minWidth <= 0f)
            {
                return value;
            }
            return Mathf.Lerp(this.def.minWidth, 1f, value);
        }

        // Token: 0x0600014B RID: 331 RVA: 0x0000980F File Offset: 0x00007A0F
        protected float GetLength(float value)
        {
            if (this.def.minLength <= 0f)
            {
                return value;
            }
            return Mathf.Lerp(this.def.minLength, 1f, value);
        }

        // Token: 0x0600014C RID: 332 RVA: 0x0000983C File Offset: 0x00007A3C
        protected override void DrawInternal()
        {
            Vector3 size = this.drawSize * this.sizeFactor;
            if (this.widthFunction != null)
            {
                size.x *= this.GetWidth(this.widthFunction(this.progress));
            }
            if (this.lengthFunction != null)
            {
                size.z *= this.GetLength(this.lengthFunction(this.progress));
            }
            Matrix4x4 matrix = Matrix4x4.TRS(base.Position, this.rotation, size);
            this.DrawInternal(ref matrix);
        }

        // Token: 0x040001B5 RID: 437
        protected Func<float, float> widthFunction;

        // Token: 0x040001B6 RID: 438
        protected Func<float, float> lengthFunction;
    }
}
namespace NCL.Projectiles
{
    // Token: 0x02000040 RID: 64
    public class VisualEffect_ParticleDrifter : VisualEffect_Particle
    {
        // Token: 0x0600014D RID: 333 RVA: 0x000098CC File Offset: 0x00007ACC
        public VisualEffect_ParticleDrifter(EffectMapComponent parentComponent, EffectContext context) : base(parentComponent, context)
        {
            this.origin = context.position;
            this.origin.y = this.def.altitude.AltitudeFor((float)this.def.altitudeAdjustment);
            this.destination = context.destination + this.def.destinationDrawOffset;
            this.destination.y = this.def.altitude.AltitudeFor((float)this.def.altitudeAdjustment);
            this.movementVector = (this.destination - this.origin).Yto0();
            this.movementVector.y = this.movementVector.y + 0.03658537f * (float)this.def.altitudeDrift * (this.def.syncAltitudeDrift ? base.DurationFactor : 1f);
            this.pathingFunction = AnimationUtility.GetFunctionByName(this.def.pathingFunction, AnimationUtility.Linear);
            this.height = this.def.height.RandomInRange;
            this.heightFunction = AnimationUtility.GetFunctionByName(this.def.heightFunction, AnimationUtility.Sine);
            AdditionalMotionProperties additionalMotionProperties = this.def.additionalMotion;
            this.additionalMotion = ((additionalMotionProperties != null) ? additionalMotionProperties.CreateInstance() : null);
            if (this.def.inheritRotationFromOrbit)
            {
                this.rotation = ((this.movementVector == Vector3.zero) ? Quaternion.identity : Quaternion.LookRotation(this.movementVector));
                this.angle = this.rotation.eulerAngles.y;
            }
        }

        // Token: 0x1700001F RID: 31
        // (get) Token: 0x0600014E RID: 334 RVA: 0x00009A65 File Offset: 0x00007C65
        protected Vector3 MotionOffset
        {
            get
            {
                if (this.pathingFunction != null)
                {
                    return this.movementVector * this.pathingFunction(this.progress);
                }
                return Vector3.zero;
            }
        }

        // Token: 0x17000020 RID: 32
        // (get) Token: 0x0600014F RID: 335 RVA: 0x00009A91 File Offset: 0x00007C91
        protected Vector3 HeightOffset
        {
            get
            {
                if (this.height == 0f)
                {
                    return Vector3.zero;
                }
                return this.heightFunction(this.progress) * this.height * Vector3.forward;
            }
        }

        // Token: 0x17000021 RID: 33
        // (get) Token: 0x06000150 RID: 336 RVA: 0x00009AC8 File Offset: 0x00007CC8
        protected Vector3 AdditionalOffset
        {
            get
            {
                AdditionalMotion additionalMotion = this.additionalMotion;
                if (additionalMotion == null)
                {
                    return Vector3.zero;
                }
                return additionalMotion.Resolve(this.progressTicks);
            }
        }

        // Token: 0x06000151 RID: 337 RVA: 0x00009AE8 File Offset: 0x00007CE8
        protected override bool CalculatePosition()
        {
            if (base.IsActive)
            {
                if (this.anchor != null)
                {
                    Vector3 pos = VisualEffect_Particle.GetAnchorPosition(this.anchor);
                    if (pos == Vector3.zero)
                    {
                        return false;
                    }
                    this.SetPosition(pos + this.positionOffset + this.MotionOffset + this.HeightOffset + this.AdditionalOffset, false);
                }
                else
                {
                    this.SetPosition(this.origin + this.positionOffset + this.MotionOffset + this.HeightOffset + this.AdditionalOffset, false);
                }
            }
            return true;
        }

        // Token: 0x040001B7 RID: 439
        protected Vector3 origin;

        // Token: 0x040001B8 RID: 440
        protected Vector3 destination;

        // Token: 0x040001B9 RID: 441
        protected Vector3 movementVector;

        // Token: 0x040001BA RID: 442
        protected float height;

        // Token: 0x040001BB RID: 443
        protected Func<float, float> pathingFunction;

        // Token: 0x040001BC RID: 444
        protected Func<float, float> heightFunction;

        // Token: 0x040001BD RID: 445
        protected AdditionalMotion additionalMotion;
    }
}
namespace NCL.Projectiles
{
    // Token: 0x02000041 RID: 65
    public class VisualEffect_ParticleFlicker : VisualEffect_Particle
    {
        // Token: 0x06000152 RID: 338 RVA: 0x00009B94 File Offset: 0x00007D94
        public VisualEffect_ParticleFlicker(EffectMapComponent parentComponent, EffectContext context) : base(parentComponent, context)
        {
            this.sizeFactor = (this.baseSize = this.def.size);
        }

        // Token: 0x06000153 RID: 339 RVA: 0x00009BC3 File Offset: 0x00007DC3
        public override bool Tick()
        {
            if (base.Tick())
            {
                this.material = this.def.Material;
                return true;
            }
            return false;
        }

        // Token: 0x06000154 RID: 340 RVA: 0x00009BE1 File Offset: 0x00007DE1
        protected override bool CalculateSize()
        {
            if (base.CalculateSize())
            {
                this.sizeFactor *= this.def.sizeRange.RandomInRange;
                return true;
            }
            return false;
        }

        // Token: 0x06000155 RID: 341 RVA: 0x00009C0C File Offset: 0x00007E0C
        protected override bool CalculateRotation()
        {
            if (base.IsActive)
            {
                float offset = this.def.rotationOffset.RandomInRange;
                if (offset != 0f)
                {
                    this.angle = this.originalAngle + offset;
                    this.rotation = Quaternion.Euler(0f, this.angle, 0f);
                }
                else
                {
                    base.CalculateRotation();
                }
            }
            return true;
        }
    }
}
namespace NCL.Projectiles
{
    // Token: 0x0200002D RID: 45
    public class VisualEffect_ParticleFlipper : VisualEffect_Particle
    {
        // Token: 0x060000BD RID: 189 RVA: 0x00006398 File Offset: 0x00004598
        public VisualEffect_ParticleFlipper(EffectMapComponent parentComponent, EffectContext context) : base(parentComponent, context)
        {
            this.frontMaterial = this.material;
            this.backMaterial = (this.def.material ?? this.material);
            this.flipRate = this.def.flipRate.RandomInRange;
            this.flipAngle = this.def.flipOffset.RandomInRange % 360f;
            this.flipFunction = AnimationUtility.GetFunctionByName(this.def.flipFunction, AnimationUtility.Sine);
        }

        // Token: 0x060000BE RID: 190 RVA: 0x00006424 File Offset: 0x00004624
        protected override bool CalculateSize()
        {
            if (base.CalculateSize())
            {
                if (this.def.flipStopsAt < 0 || this.progressTicks <= this.def.flipStopsAt)
                {
                    this.flipAngle += this.flipRate;
                    this.flipAngle %= 360f;
                }
                return true;
            }
            return false;
        }

        // Token: 0x060000BF RID: 191 RVA: 0x00006484 File Offset: 0x00004684
        protected override void DrawInternal()
        {
            this.material = ((this.flipAngle < 180f) ? this.frontMaterial : this.backMaterial);
            if (this.flipFunction != null)
            {
                float widthFactor = this.flipFunction(this.flipAngle % 180f / 180f);
                if (widthFactor > 0f)
                {
                    Vector3 size = this.drawSize * this.sizeFactor;
                    size.x *= widthFactor;
                    Matrix4x4 matrix = Matrix4x4.TRS(base.Position, this.rotation, size);
                    this.DrawInternal(ref matrix);
                    return;
                }
            }
            else
            {
                base.DrawInternal();
            }
        }

        // Token: 0x0400013D RID: 317
        protected Material frontMaterial;

        // Token: 0x0400013E RID: 318
        protected Material backMaterial;

        // Token: 0x0400013F RID: 319
        protected float flipRate;

        // Token: 0x04000140 RID: 320
        protected float flipAngle;

        // Token: 0x04000141 RID: 321
        protected Func<float, float> flipFunction;
    }
}

namespace NCL.Projectiles
{
    // Token: 0x02000042 RID: 66
    public class VisualEffect_ParticleOrbiter : VisualEffect_Particle
    {
        // Token: 0x06000156 RID: 342 RVA: 0x00009C6D File Offset: 0x00007E6D
        public VisualEffect_ParticleOrbiter(EffectMapComponent parentComponent, EffectContext context) : base(parentComponent, context)
        {
        }

        // Token: 0x06000157 RID: 343 RVA: 0x00009C78 File Offset: 0x00007E78
        protected override void PreInitialize(EffectContext context)
        {
            base.PreInitialize(context);
            this.centerpoint = context.position;
            this.orbitRadius = this.def.radius;
            this.orbitAngle = context.orbitAngle;
            this.orbitRate = this.def.orbitRate.RandomInRange;
            this.radiusFunction = AnimationUtility.GetFunctionByName(this.def.radiusFunction, null);
        }

        // Token: 0x06000158 RID: 344 RVA: 0x00009CE4 File Offset: 0x00007EE4
        protected override bool CalculatePosition()
        {
            if (!base.IsActive)
            {
                return true;
            }
            if (this.orbitRate != 0f)
            {
                this.orbitAngle = (this.orbitAngle + this.orbitRate) % 360f;
            }
            if (!this.CalculateRadius())
            {
                return false;
            }
            Vector3 orbitalOffset = this.CalculateOrbitalOffset();
            if (this.anchor == null)
            {
                this.SetPosition(this.centerpoint + orbitalOffset, true);
            }
            else
            {
                Vector3 anchorPosition = VisualEffect_Particle.GetAnchorPosition(this.anchor);
                if (anchorPosition == Vector3.zero)
                {
                    return false;
                }
                this.SetPosition(anchorPosition + this.positionOffset + orbitalOffset, true);
            }
            return true;
        }

        // Token: 0x06000159 RID: 345 RVA: 0x00009D84 File Offset: 0x00007F84
        protected virtual bool CalculateRadius()
        {
            if (this.radiusFunction != null)
            {
                this.orbitRadius = this.radiusFunction(this.progress);
            }
            return true;
        }

        // Token: 0x0600015A RID: 346 RVA: 0x00009DA8 File Offset: 0x00007FA8
        protected virtual Vector3 CalculateOrbitalOffset()
        {
            float radians = 3.1415927f * (90f - this.orbitAngle) / 180f;
            return new Vector3(this.orbitRadius * Mathf.Cos(radians), 0f, this.orbitRadius * Mathf.Sin(radians));
        }

        // Token: 0x040001BE RID: 446
        protected Vector3 centerpoint;

        // Token: 0x040001BF RID: 447
        protected float orbitRadius;

        // Token: 0x040001C0 RID: 448
        protected float orbitAngle;

        // Token: 0x040001C1 RID: 449
        protected float orbitRate;

        // Token: 0x040001C2 RID: 450
        protected Func<float, float> radiusFunction;
    }
}
namespace NCL.Projectiles
{
    // Token: 0x02000043 RID: 67
    public class VisualEffect_ParticlePather : VisualEffect_Particle
    {
        // Token: 0x0600015B RID: 347 RVA: 0x00009DF2 File Offset: 0x00007FF2
        public VisualEffect_ParticlePather(EffectMapComponent parentComponent, EffectContext context) : base(parentComponent, context)
        {
        }

        // Token: 0x0600015C RID: 348 RVA: 0x00009DFC File Offset: 0x00007FFC
        protected override void PreInitialize(EffectContext context)
        {
            base.PreInitialize(context);
            this.origin = context.origin;
            this.origin.y = this.def.altitude.AltitudeFor((float)this.def.altitudeAdjustment);
            this.destination = context.destination;
            this.destination.y = this.def.altitude.AltitudeFor((float)this.def.altitudeAdjustment);
            this.movementVector = (this.destination - this.origin).Yto0();
            this.movementVector.y = this.movementVector.y + 0.03658537f * (float)this.def.altitudeDrift * (this.def.syncAltitudeDrift ? base.DurationFactor : 1f);
            this.height = context.def.height.RandomInRange;
            this.heightFunction = AnimationUtility.GetFunctionByName(context.def.heightFunction, null);
            if (this.parentComponent != null && !this.def.subeffects.NullOrEmpty<EffectDef>())
            {
                this.hasSubEffects = true;
                this.tracker = new PositionTracker
                {
                    previousVisualPosition = this.origin,
                    currentVisualPosition = this.origin,
                    currentVisualRotation = context.rotation,
                    currentVisualAngle = context.angle
                };
            }
        }

        // Token: 0x0600015D RID: 349 RVA: 0x00009F57 File Offset: 0x00008157
        public override bool Tick()
        {
            if (base.Tick())
            {
                if (this.hasSubEffects && this.delay < 1)
                {
                    this.tracker.Tick(base.Position);
                    this.GenerateSubEffects();
                }
                return true;
            }
            return false;
        }

        // Token: 0x0600015E RID: 350 RVA: 0x00009F8C File Offset: 0x0000818C
        protected override bool CalculatePosition()
        {
            Vector3 pos = this.origin + this.progress * this.movementVector;
            if (this.height != 0f)
            {
                pos.z += ((this.heightFunction == null) ? this.height : (this.heightFunction(this.progress) * this.height));
            }
            this.SetPosition(pos, false);
            return true;
        }

        // Token: 0x0600015F RID: 351 RVA: 0x0000A000 File Offset: 0x00008200
        protected virtual void GenerateSubEffects()
        {
            for (int i = 0; i < this.def.subeffects.Count; i++)
            {
                EffectDef effectDef = this.def.subeffects[i];
                if (effectDef.ShouldBeActive(this.progressTicks) && effectDef.CheckInterval(this.progressTicks))
                {
                    this.parentComponent.CreateEffect(new EffectContext(this.parentComponent.map, effectDef)
                    {
                        anchor = null,
                        destinationAnchor = null,
                        position = this.tracker.currentVisualPosition,
                        origin = this.tracker.currentVisualPosition,
                        destination = this.tracker.previousVisualPosition,
                        rotation = this.tracker.currentVisualRotation,
                        angle = this.tracker.currentVisualAngle,
                        parentTicksElapsed = this.progressTicks
                    });
                }
            }
        }

        // Token: 0x040001C3 RID: 451
        protected Vector3 origin;

        // Token: 0x040001C4 RID: 452
        protected Vector3 destination;

        // Token: 0x040001C5 RID: 453
        protected Vector3 movementVector;

        // Token: 0x040001C6 RID: 454
        protected float height;

        // Token: 0x040001C7 RID: 455
        protected bool hasSubEffects;

        // Token: 0x040001C8 RID: 456
        protected Func<float, float> heightFunction;

        // Token: 0x040001C9 RID: 457
        protected PositionTracker tracker;
    }
}
namespace NCL.Projectiles
{
    // Token: 0x02000044 RID: 68
    public class VisualEffect_ParticleScatterer : VisualEffect
    {
        // Token: 0x06000160 RID: 352 RVA: 0x0000A0F8 File Offset: 0x000082F8
        public VisualEffect_ParticleScatterer(EffectMapComponent parentComponent, EffectContext context) : base(parentComponent, context)
        {
        }

        // Token: 0x06000161 RID: 353 RVA: 0x0000A102 File Offset: 0x00008302
        protected override void PreInitialize(EffectContext context)
        {
            base.PreInitialize(context);
            this.cells = Enumerable.ToList<IntVec3>(GenRadial.RadialCellsAround(context.position.ToIntVec3(), this.def.radius, true));
            this.cells.Shuffle<IntVec3>();
        }

        // Token: 0x06000162 RID: 354 RVA: 0x0000A13D File Offset: 0x0000833D
        protected override void Initialize(EffectContext context)
        {
        }

        // Token: 0x06000163 RID: 355 RVA: 0x0000A140 File Offset: 0x00008340
        public override bool Tick()
        {
            if (!base.Tick())
            {
                return false;
            }
            if (this.delay > 0)
            {
                return true;
            }
            if (this.ticksUntilNextInterval < 1)
            {
                for (int i = 0; i < this.def.count; i++)
                {
                    if (this.cells.NullOrEmpty<IntVec3>())
                    {
                        return false;
                    }
                    IntVec3 cell = this.cells.Pop<IntVec3>();
                    if (cell.IsValid && cell.InBounds(this.parentComponent.map))
                    {
                        for (int j = 0; j < this.def.subeffects.Count; j++)
                        {
                            this.SpawnEffects(cell);
                        }
                    }
                }
                this.ticksUntilNextInterval = this.def.interval;
            }
            this.ticksUntilNextInterval--;
            return true;
        }

        // Token: 0x06000164 RID: 356 RVA: 0x0000A200 File Offset: 0x00008400
        protected virtual void SpawnEffects(IntVec3 cell)
        {
            Vector3 position = cell.ToVector3Shifted();
            for (int i = 0; i < this.def.subeffects.Count; i++)
            {
                if (this.def.distance.max > 0f)
                {
                    position += Quaternion.Euler(0f, this.def.rotationOffset.RandomInRange, 0f) * new Vector3(0f, 0f, this.def.distance.RandomInRange);
                }
                this.parentComponent.CreateEffect(new EffectContext(this.parentComponent.map, this.def.subeffects[i])
                {
                    anchor = null,
                    destinationAnchor = null,
                    position = position,
                    origin = base.Position,
                    destination = position,
                    parentTicksElapsed = this.progressTicks
                });
            }
        }

        // Token: 0x06000165 RID: 357 RVA: 0x0000A300 File Offset: 0x00008500
        protected override void DrawInternal()
        {
        }

        // Token: 0x040001CA RID: 458
        protected List<IntVec3> cells;

        // Token: 0x040001CB RID: 459
        protected int ticksUntilNextInterval;
    }
}
namespace NCL.Projectiles
{
    // Token: 0x0200002E RID: 46
    public class VisualEffect_Sequencer : VisualEffect_Particle
    {
        // Token: 0x060000C0 RID: 192 RVA: 0x00006520 File Offset: 0x00004720
        public VisualEffect_Sequencer(EffectMapComponent parentComponent, EffectContext context) : base(parentComponent, context)
        {
            this.originalContext = context;
            if (this.def.subeffects != null)
            {
                this.subeffects = new List<EffectDef>();
                this.subeffects.AddRange(this.def.subeffects);
                if (this.def.randomize)
                {
                    this.subeffects.Shuffle<EffectDef>();
                }
            }
        }

        // Token: 0x060000C1 RID: 193 RVA: 0x00006584 File Offset: 0x00004784
        public override bool Tick()
        {
            if (base.Tick())
            {
                if (this.delay < 1 && this.subeffects != null && this.def.CheckInterval(this.progressTicks))
                {
                    if (this.index >= this.subeffects.Count)
                    {
                        this.index = 0;
                    }
                    EffectMapComponent parentComponent = this.parentComponent;
                    if (parentComponent != null)
                    {
                        parentComponent.CreateEffect(this.originalContext.CreateSubEffectContext(this.subeffects[this.index], this.progressTicks));
                    }
                    this.index++;
                }
                return true;
            }
            return false;
        }

        // Token: 0x04000142 RID: 322
        protected EffectContext originalContext;

        // Token: 0x04000143 RID: 323
        protected List<EffectDef> subeffects;

        // Token: 0x04000144 RID: 324
        protected int index;
    }
}
namespace NCL.Projectiles
{
    // Token: 0x0200002F RID: 47
    public class VisualEffect_Spawner : VisualEffect_Particle
    {
        // Token: 0x060000C2 RID: 194 RVA: 0x0000661E File Offset: 0x0000481E
        public VisualEffect_Spawner(EffectMapComponent parentComponent, EffectContext context) : base(parentComponent, context)
        {
            this.originalContext = context;
        }

        // Token: 0x060000C3 RID: 195 RVA: 0x00006630 File Offset: 0x00004830
        public override bool Tick()
        {
            if (base.Tick())
            {
                if (this.delay < 1 && this.def.subeffects != null)
                {
                    foreach (EffectDef subDef in this.def.subeffects)
                    {
                        if (subDef.ShouldBeActive(this.progressTicks))
                        {
                            this.parentComponent.CreateEffect(this.originalContext.CreateSubEffectContext(subDef, this.progressTicks));
                        }
                    }
                }
                return true;
            }
            return false;
        }

        // Token: 0x04000145 RID: 325
        protected EffectContext originalContext;
    }
}
namespace NCL.Projectiles
{
    // Token: 0x02000045 RID: 69
    public class VisualEffect_TrailerOrbiter : VisualEffect_Particle
    {
        // Token: 0x06000166 RID: 358 RVA: 0x0000A302 File Offset: 0x00008502
        public VisualEffect_TrailerOrbiter(EffectMapComponent parentComponent, EffectContext context) : base(parentComponent, context)
        {
        }

        // Token: 0x06000167 RID: 359 RVA: 0x0000A30C File Offset: 0x0000850C
        protected override void PreInitialize(EffectContext context)
        {
            base.PreInitialize(context);
            this.orbitRadius = this.def.radius;
            this.orbitAngle = (this.def.applyRotationToOrbit ? this.angle : 0f) + this.def.orbitOffset.RandomInRange;
            this.orbitRate = this.def.orbitRate.RandomInRange;
            this.radiusFunction = AnimationUtility.GetFunctionByName(this.def.radiusFunction, null);
            this.height = this.def.height.RandomInRange;
            this.heightFunction = AnimationUtility.GetFunctionByName(this.def.heightFunction, null);
            this.trailCount = Mathf.Max(1, this.def.count);
            this.positionTrackers = new PositionTracker[this.trailCount];
            for (int i = 0; i < this.trailCount; i++)
            {
                this.positionTrackers[i] = new PositionTracker();
            }
        }

        // Token: 0x06000168 RID: 360 RVA: 0x0000A402 File Offset: 0x00008602
        public override bool Tick()
        {
            if (base.Tick() && this.CalculateRadius())
            {
                this.GenerateSubEffects();
                return true;
            }
            return false;
        }

        // Token: 0x06000169 RID: 361 RVA: 0x0000A420 File Offset: 0x00008620
        protected virtual void GenerateSubEffects()
        {
            if (this.orbitRate != 0f)
            {
                this.orbitAngle = (this.orbitAngle + this.orbitRate) % 360f;
            }
            if (this.parentComponent == null)
            {
                return;
            }
            Vector3 centerpoint = base.Position;
            if (this.heightFunction != null && this.height != 0f)
            {
                centerpoint.z += this.height * this.heightFunction(this.progress);
            }
            for (int i = 0; i < this.trailCount; i++)
            {
                PositionTracker tracker = this.positionTrackers[i];
                tracker.Tick(centerpoint + this.CalculateOrbitalOffset(i));
                foreach (EffectDef trailDef in this.def.subeffects)
                {
                    if (trailDef.ShouldBeActive(this.progressTicks))
                    {
                        this.parentComponent.CreateEffect(new EffectContext(this.parentComponent.map, trailDef)
                        {
                            anchor = null,
                            destinationAnchor = null,
                            position = tracker.currentVisualPosition,
                            origin = tracker.currentVisualPosition,
                            destination = tracker.previousVisualPosition,
                            rotation = tracker.currentVisualRotation,
                            angle = tracker.currentVisualAngle,
                            parentTicksElapsed = this.progressTicks
                        });
                    }
                }
            }
        }

        // Token: 0x0600016A RID: 362 RVA: 0x0000A5A8 File Offset: 0x000087A8
        protected virtual bool CalculateRadius()
        {
            if (this.radiusFunction != null)
            {
                float progress = this.radiusFunction(base.RawProgress);
                this.orbitRadius = ((this.def.minRadius > 0f) ? Mathf.Lerp(this.def.minRadius, this.def.radius, progress) : (this.def.radius * progress));
            }
            return true;
        }

        // Token: 0x0600016B RID: 363 RVA: 0x0000A614 File Offset: 0x00008814
        protected virtual Vector3 CalculateOrbitalOffset(int index)
        {
            float angle = this.orbitAngle + (float)index * (360f / (float)this.trailCount);
            float radians = 3.1415927f * (90f - angle) / 180f;
            return new Vector3(this.orbitRadius * Mathf.Cos(radians), 0f, this.orbitRadius * Mathf.Sin(radians));
        }

        // Token: 0x0600016C RID: 364 RVA: 0x0000A674 File Offset: 0x00008874
        protected override void DrawInternal()
        {
            if (this.material != null)
            {
                for (int i = 0; i < this.trailCount; i++)
                {
                    PositionTracker tracker = this.positionTrackers[i];
                    if (tracker != null && tracker.currentVisualPosition != Vector3.zero)
                    {
                        Vector3 newPosition = tracker.currentVisualPosition;
                        newPosition.y = this.def.altitude.AltitudeFor();
                        Matrix4x4 matrix = Matrix4x4.TRS(newPosition, this.def.neverDrawRotated ? Quaternion.identity : tracker.currentVisualRotation, this.drawSize * this.sizeFactor);
                        this.DrawInternal(ref matrix);
                    }
                }
            }
        }

        // Token: 0x040001CC RID: 460
        protected float orbitRadius;

        // Token: 0x040001CD RID: 461
        protected float orbitAngle;

        // Token: 0x040001CE RID: 462
        protected float orbitRate;

        // Token: 0x040001CF RID: 463
        protected Func<float, float> radiusFunction;

        // Token: 0x040001D0 RID: 464
        protected float height;

        // Token: 0x040001D1 RID: 465
        protected Func<float, float> heightFunction;

        // Token: 0x040001D2 RID: 466
        protected int trailCount;

        // Token: 0x040001D3 RID: 467
        protected PositionTracker[] positionTrackers;
    }
}
namespace NCL.Projectiles
{
    // Token: 0x02000030 RID: 48
    public interface IInterceptorSource
    {
        // Token: 0x060000C4 RID: 196
        IntVec3 GetSourceCell();

        // Token: 0x060000C5 RID: 197
        Vector3 GetSourcePosition();

        // Token: 0x060000C6 RID: 198
        int GetBaseWidth();

        // Token: 0x060000C7 RID: 199
        float GetRadius();

        // Token: 0x060000C8 RID: 200
        float GetGridRadius();

        // Token: 0x060000C9 RID: 201
        bool CanIntercept(Thing thing, Vector3 origin, Vector3 position);

        // Token: 0x060000CA RID: 202
        bool RejectInterception(Thing thing, Vector3 origin);

        // Token: 0x060000CB RID: 203
        bool CanInterceptBombardment(Map map, float damage, IntVec3 cell);

        // Token: 0x060000CC RID: 204
        void NotifyIntercept(Thing thing);

        // Token: 0x060000CD RID: 205
        void NotifyInterceptBombardment(Map map, float damage, IntVec3 cell);

        // Token: 0x060000CE RID: 206
        bool ShouldDrawField(ref CellRect cameraRect);

        // Token: 0x060000CF RID: 207
        void DrawField();
    }
}
namespace NCL.Projectiles
{
    // Token: 0x02000036 RID: 54
    public class PositionTracker : IExposable
    {
        // Token: 0x06000114 RID: 276 RVA: 0x00008126 File Offset: 0x00006326
        public void PostSpawnSetup(Vector3 initialPosition)
        {
            this.currentVisualPosition = initialPosition;
        }

        // Token: 0x06000115 RID: 277 RVA: 0x00008130 File Offset: 0x00006330
        public void ExposeData()
        {
            Scribe_Values.Look<Vector3>(ref this.previousVisualPosition, "previousVisualPosition", default(Vector3), false);
            Scribe_Values.Look<Vector3>(ref this.currentVisualPosition, "currentVisualPosition", default(Vector3), false);
        }

        // Token: 0x06000116 RID: 278 RVA: 0x00008171 File Offset: 0x00006371
        public void Tick(Vector3 newPosition)
        {
            this.previousVisualPosition = this.currentVisualPosition;
            this.currentVisualPosition = newPosition;
            this.CalculateRotation();
        }

        // Token: 0x06000117 RID: 279 RVA: 0x0000818C File Offset: 0x0000638C
        public void CalculateRotation()
        {
            if (this.currentVisualPosition != this.previousVisualPosition)
            {
                this.currentVisualRotation = Quaternion.LookRotation(this.currentVisualPosition - this.previousVisualPosition);
                this.currentVisualAngle = this.currentVisualRotation.eulerAngles.y;
            }
        }

        // Token: 0x04000162 RID: 354
        public Vector3 previousVisualPosition = Vector3.zero;

        // Token: 0x04000163 RID: 355
        public Vector3 currentVisualPosition = Vector3.zero;

        // Token: 0x04000164 RID: 356
        public Quaternion currentVisualRotation = Quaternion.identity;

        // Token: 0x04000165 RID: 357
        public float currentVisualAngle;
    }
}

namespace NCL.Projectiles
{
    // Token: 0x02000023 RID: 35
    public class AdditionalMotion
    {
        // Token: 0x0600009F RID: 159 RVA: 0x00005988 File Offset: 0x00003B88
        public Vector3 Resolve(int tick)
        {
            AdditionalMotionDirectional additionalMotionDirectional = this.horizontal;
            float x = (additionalMotionDirectional != null) ? additionalMotionDirectional.Resolve(tick) : 0f;
            float y = 0f;
            AdditionalMotionDirectional additionalMotionDirectional2 = this.vertical;
            return new Vector3(x, y, (additionalMotionDirectional2 != null) ? additionalMotionDirectional2.Resolve(tick) : 0f);
        }

        // Token: 0x040000AA RID: 170
        public AdditionalMotionDirectional horizontal;

        // Token: 0x040000AB RID: 171
        public AdditionalMotionDirectional vertical;
    }
}

namespace NCL.Projectiles
{
    // Token: 0x02000024 RID: 36
    public class AdditionalMotionDirectional
    {
        // Token: 0x060000A1 RID: 161 RVA: 0x000059CC File Offset: 0x00003BCC
        public float Resolve(int tick)
        {
            this.value = (float)(tick + this.periodOffset % this.period) / (float)this.period;
            return this.amplitude * ((this.function == null) ? this.value : this.function(this.value));
        }

        // Token: 0x040000AC RID: 172
        public float amplitude;

        // Token: 0x040000AD RID: 173
        public Func<float, float> function;

        // Token: 0x040000AE RID: 174
        public int period;

        // Token: 0x040000AF RID: 175
        public int periodOffset;

        // Token: 0x040000B0 RID: 176
        private float value;
    }
}

namespace NCL.Projectiles
{
    // Token: 0x02000022 RID: 34
    public class AdditionalMotionDirectionalProperties
    {
        // Token: 0x0600009D RID: 157 RVA: 0x00005904 File Offset: 0x00003B04
        public AdditionalMotionDirectional CreateInstance()
        {
            return new AdditionalMotionDirectional
            {
                amplitude = this.amplitude.RandomInRange,
                function = AnimationUtility.GetFunctionByName(this.function, null),
                period = this.period.RandomInRange,
                periodOffset = this.periodOffset.RandomInRange
            };
        }

        // Token: 0x040000A6 RID: 166
        public FloatRange amplitude = FloatRange.Zero;

        // Token: 0x040000A7 RID: 167
        public string function;

        // Token: 0x040000A8 RID: 168
        public IntRange period = new IntRange(60, 60);

        // Token: 0x040000A9 RID: 169
        public IntRange periodOffset = IntRange.Zero;
    }
}
namespace NCL.Projectiles
{
    // Token: 0x02000021 RID: 33
    public class AdditionalMotionProperties
    {
        // Token: 0x0600009B RID: 155 RVA: 0x000058A4 File Offset: 0x00003AA4
        public AdditionalMotion CreateInstance()
        {
            if (this.horizontal == null && this.vertical == null)
            {
                return null;
            }
            AdditionalMotion instance = new AdditionalMotion();
            if (this.horizontal != null)
            {
                instance.horizontal = this.horizontal.CreateInstance();
            }
            if (this.vertical != null)
            {
                instance.vertical = this.vertical.CreateInstance();
            }
            return instance;
        }

        // Token: 0x040000A4 RID: 164
        public AdditionalMotionDirectionalProperties horizontal;

        // Token: 0x040000A5 RID: 165
        public AdditionalMotionDirectionalProperties vertical;
    }
}
namespace NCL.Projectiles
{
    // Token: 0x0200001F RID: 31
    public enum WeaponAnimationLevel
    {
        // Token: 0x0400009F RID: 159
        Low,
        // Token: 0x040000A0 RID: 160
        Medium,
        // Token: 0x040000A1 RID: 161
        High
    }
}
namespace NCL.Projectiles
{
    // Token: 0x02000034 RID: 52
    public static class EffectUtility
    {
        // Token: 0x06000108 RID: 264 RVA: 0x00007D90 File Offset: 0x00005F90
        public static EffectMapComponent EccentricProjectilesEffectComp(this Map map)
        {
            if (EffectMapComponent.cachedInstance != null && EffectMapComponent.cachedInstance.map.uniqueID == map.uniqueID)
            {
                return EffectMapComponent.cachedInstance;
            }
            EffectMapComponent value;
            if (EffectMapComponent.CachedInstances.TryGetValue(map.uniqueID, out value))
            {
                EffectMapComponent.cachedInstance = value;
            }
            else
            {
                EffectMapComponent.cachedInstance = map.GetComponent<EffectMapComponent>();
                EffectMapComponent.CachedInstances[map.uniqueID] = EffectMapComponent.cachedInstance;
            }
            return EffectMapComponent.cachedInstance;
        }

        // Token: 0x06000109 RID: 265 RVA: 0x00007E02 File Offset: 0x00006002
        public static EffectContext CreateContext(EffectDef effectDef, Pawn caster, Thing target, bool positionAtTarget = false)
        {
            return EffectUtility.CreateContext(effectDef, caster.MapHeld, caster.DrawPos, target.DrawPos, caster, target, positionAtTarget);
        }

        // Token: 0x0600010A RID: 266 RVA: 0x00007E20 File Offset: 0x00006020
        public static EffectContext CreateContext(EffectDef effectDef, Map map, Vector3 casterPos, Vector3 targetPos, Thing anchor = null, Thing destinationAnchor = null, bool positionAtTarget = false)
        {
            Quaternion rotation = (targetPos == casterPos) ? Quaternion.identity : Quaternion.LookRotation((targetPos - casterPos).Yto0());
            float angle = rotation.eulerAngles.y;
            return new EffectContext(map, effectDef)
            {
                anchor = anchor,
                destinationAnchor = destinationAnchor,
                position = ((positionAtTarget || effectDef.attachToTarget) ? targetPos : casterPos),
                origin = casterPos,
                destination = targetPos,
                rotation = rotation,
                angle = angle
            };
        }

        // Token: 0x0600010B RID: 267 RVA: 0x00007EB0 File Offset: 0x000060B0
        public static Vector3 CalculateDriftOffset(float drawDriftDistance)
        {
            float distance = Rand.Value;
            distance = drawDriftDistance * (1f - distance * distance);
            if (distance > 0f)
            {
                float angle = 6.2831855f * Rand.Value;
                return new Vector3(distance * Mathf.Cos(angle), 0f, distance * Mathf.Sin(angle));
            }
            return Vector3.zero;
        }

        // Token: 0x0600010C RID: 268 RVA: 0x00007F04 File Offset: 0x00006104
        public static bool ShouldBeVisibleFrom(this CellRect cellRect, IntVec3 position, Vector3 drawSize)
        {
            int lateralOffset = Mathf.CeilToInt(drawSize.x / 2f);
            if (position.x < cellRect.minX - lateralOffset || position.x > cellRect.maxX + lateralOffset)
            {
                return false;
            }
            int verticalOffset = Mathf.CeilToInt(drawSize.z / 2f);
            return position.z >= cellRect.minZ - verticalOffset && position.z <= cellRect.maxZ + verticalOffset;
        }

        // Token: 0x0600010D RID: 269 RVA: 0x00007F7A File Offset: 0x0000617A
        public static void DrawLine(Vector3 from, Vector3 to, Material lineMaterial, float alpha)
        {
            GenDraw.DrawLineBetween(from, to, FadedMaterialPool.FadedVersionOf(lineMaterial, alpha), 0.2f);
        }

        // Token: 0x0600010E RID: 270 RVA: 0x00007F90 File Offset: 0x00006190
        public static void DrawPulsedLine(Vector3 from, Vector3 to, float progress, float width = 0.1f)
        {
            if (from == to)
            {
                return;
            }
            Vector3 delta = to - from;
            Quaternion quat = Quaternion.LookRotation(from - to);
            float easedProgress = AnimationUtility.EaseInOutCubic(progress);
            float alpha = (easedProgress > 0.5f) ? (1f - 2f * (easedProgress - 0.5f)) : (2f * easedProgress);
            Vector3 lineVector = new Vector3(width, 1f, alpha * (from - to).MagnitudeHorizontal());
            Matrix4x4 matrix = default(Matrix4x4);
            Vector3 centerPoint = from + delta * easedProgress;
            centerPoint.y = AltitudeLayer.MoteOverhead.AltitudeFor();
            matrix.SetTRS(centerPoint, quat, lineVector);
        }
    }
}
namespace NCL.Projectiles
{
    // Token: 0x02000028 RID: 40
    public enum EffectPriority
    {
        // Token: 0x040000D6 RID: 214
        Low,
        // Token: 0x040000D7 RID: 215
        Medium,
        // Token: 0x040000D8 RID: 216
        High,
        // Token: 0x040000D9 RID: 217
        Critical
    }
}
namespace NCL.Projectiles
{
    // Token: 0x02000067 RID: 103
    public class WeaponDirectionalGraphics
    {
        // Token: 0x040002A3 RID: 675
        public GraphicData north;

        // Token: 0x040002A4 RID: 676
        public GraphicData east;

        // Token: 0x040002A5 RID: 677
        public GraphicData west;

        // Token: 0x040002A6 RID: 678
        public GraphicData south;
    }
}

namespace NCL.Projectiles
{
    // Token: 0x02000025 RID: 37
    public class ColorCurve
    {
        // Token: 0x060000A3 RID: 163 RVA: 0x00005A28 File Offset: 0x00003C28
        private void Initialize()
        {
            if (this.points.NullOrEmpty<ColorCurve.Point>())
            {
                for (int i = 0; i < 61; i++)
                {
                    this.cachedValues[i] = Color.white;
                }
            }
            else
            {
                for (int j = 0; j < 61; j++)
                {
                    float key = (float)j / 60f;
                    this.cachedValues[j] = this.EvaluatePoint(key);
                }
            }
            this.initialized = true;
        }

        // Token: 0x060000A4 RID: 164 RVA: 0x00005A94 File Offset: 0x00003C94
        private Color EvaluatePoint(float key)
        {
            ColorCurve.Point previousPoint = null;
            int i = 0;
            while (i < this.points.Count)
            {
                ColorCurve.Point point = this.points[i];
                if (key <= point.key)
                {
                    if (previousPoint == null)
                    {
                        return point.value;
                    }
                    return Color.Lerp(previousPoint.value, point.value, (key - previousPoint.key) / (point.key - previousPoint.key));
                }
                else
                {
                    previousPoint = point;
                    i++;
                }
            }
            return previousPoint.value;
        }

        // Token: 0x060000A5 RID: 165 RVA: 0x00005B0C File Offset: 0x00003D0C
        public int GetIndex(float key)
        {
            int index = Mathf.FloorToInt(key * 60f);
            if (index > 60)
            {
                return 60;
            }
            if (index < 0)
            {
                return 0;
            }
            return index;
        }

        // Token: 0x060000A6 RID: 166 RVA: 0x00005B35 File Offset: 0x00003D35
        public Color Evaluate(float key)
        {
            if (!this.initialized)
            {
                this.Initialize();
            }
            return this.cachedValues[this.GetIndex(key)];
        }

        // Token: 0x040000B1 RID: 177
        private Color[] cachedValues = new Color[61];

        // Token: 0x040000B2 RID: 178
        public List<ColorCurve.Point> points;

        // Token: 0x040000B3 RID: 179
        private bool initialized;

        // Token: 0x02000078 RID: 120
        public class Point
        {
            // Token: 0x0400030C RID: 780
            public float key;

            // Token: 0x0400030D RID: 781
            public Color value;
        }
    }
}
