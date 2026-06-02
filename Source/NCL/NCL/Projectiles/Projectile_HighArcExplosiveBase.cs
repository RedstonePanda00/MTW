using RimWorld;
using UnityEngine;
using Verse;

namespace NCL.Projectiles
{
    /// <summary>
    /// High-arc flight (DrawPos, rotation, effects sync). Explosion is customized via
    /// <see cref="OnHighArcImpact"/>; call <see cref="ImpactExplosiveCore"/> for vanilla explosive impact —
    /// never call <c>base.Impact</c> from overrides (that re-enters this type's <see cref="Impact"/>).
    /// </summary>
    public abstract class Projectile_HighArcExplosiveBase : Projectile_Explosive
    {
        protected ModExtension_ProjectileEffects effectsExtension;
        protected ProjectileEffectTracker effects;

        protected float ArcProgress => DistanceCoveredFraction;

        /// <summary>Exposed for comps/debug; same as <see cref="DistanceCoveredFraction"/>.</summary>
        public float HighArcProgress => DistanceCoveredFraction;

        public override Quaternion ExactRotation
        {
            get
            {
                if (def.projectile.spinRate != 0f)
                {
                    return base.ExactRotation;
                }

                return HighArcTrajectory.GetVisualRotation(origin, destination, def, ArcProgress);
            }
        }

        public override Vector3 DrawPos
        {
            get
            {
                float t = ArcProgress;
                Vector3 ground = ExactPosition;
                float arcZ = HighArcTrajectory.GetArcOffsetZ(origin, destination, def, t);
                return ground + new Vector3(0f, 0f, arcZ);
            }
        }

        public override void PostMake()
        {
            base.PostMake();
            effects = new ProjectileEffectTracker(this);
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            effectsExtension = def.GetModExtension<ModExtension_ProjectileEffects>();
            effects.PostSpawnSetup(map, respawningAfterLoad);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref effects, "effectTracker", this);
            Scribe_Values.Look(ref impactHandled, "highArcImpactHandled", false);
        }

        protected override void TickInterval(int delta)
        {
            HighArcProjectileDebug.BeginScope(this, "TickInterval");
            try
            {
                effects.PreTick(origin, destination);
                base.TickInterval(delta);
                HighArcTrajectory.Apply(effects, this, origin, destination, ArcProgress);
                effects.Tick(Map, effectsExtension);
                effects.PostTick(delta);
                HighArcProjectileDebug.LogTick(this, delta, ArcProgress, StartingTicksToImpact, ticksToImpact);
            }
            finally
            {
                HighArcProjectileDebug.EndScope(this, "TickInterval");
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            float t = ArcProgress;
            HighArcProjectileDebug.BeginScope(this, "DrawAt");
            try
            {
                DrawAtCore(drawLoc, flip, t);
            }
            finally
            {
                HighArcProjectileDebug.LogDrawAt(this, drawLoc, t);
                HighArcProjectileDebug.EndScope(this, "DrawAt");
            }
        }

        private void DrawAtCore(Vector3 drawLoc, bool flip, float arcProgress)
        {
            float arcOnZ = HighArcTrajectory.GetArcOffsetZ(origin, destination, def, arcProgress);
            Vector3 groundPos = ExactPosition;

            if (def.projectile.shadowSize > 0f && arcOnZ > 0.001f)
            {
                DrawArcShadow(groundPos, arcOnZ);
            }

            Quaternion rotation = GetDrawRotation();

            if (def.projectile.useGraphicClass && Graphic != null)
            {
                Graphic.Draw(drawLoc, Rotation, this, rotation.eulerAngles.y);
            }
            else
            {
                Graphics.DrawMesh(
                    MeshPool.GridPlane(def.graphicData.drawSize),
                    drawLoc,
                    rotation,
                    DrawMat,
                    0);
            }

            Comps_PostDraw();
        }

        private Quaternion GetDrawRotation()
        {
            if (def.projectile.spinRate != 0f)
            {
                float spinPeriod = 60f / def.projectile.spinRate;
                return Quaternion.AngleAxis(
                    Find.TickManager.TicksGame % spinPeriod / spinPeriod * 360f,
                    Vector3.up);
            }

            return ExactRotation;
        }

        private bool impactHandled;

        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            if (impactHandled)
            {
                return;
            }

            impactHandled = true;
            HighArcProjectileDebug.LogImpact(this, hitThing, ArcProgress);
            SyncEffectTrackerForImpact();
            OnHighArcImpact(hitThing, blockedByShield);
        }

        /// <summary>Override to add explosion behaviour. Default runs vanilla <see cref="Projectile_Explosive"/> impact once.</summary>
        protected virtual void OnHighArcImpact(Thing hitThing, bool blockedByShield)
        {
            ImpactExplosiveCore(hitThing, blockedByShield);
        }

        /// <summary>Vanilla explosive impact. Safe from <see cref="OnHighArcImpact"/> overrides.</summary>
        protected void ImpactExplosiveCore(Thing hitThing, bool blockedByShield = false)
        {
            base.Impact(hitThing, blockedByShield);
        }

        protected void DrawArcShadow(Vector3 groundDrawLoc, float arcOnZ)
        {
            float z0 = origin.Yto0().z;
            float z1 = destination.Yto0().z;
            float maxLift = HighArcTrajectory.ResolveApexZ(z0, z1, def) - (z0 + z1) * 0.5f;
            float heightFactor = maxLift > 0.001f ? Mathf.Clamp01(arcOnZ / maxLift) : 0f;
            float size = def.projectile.shadowSize * Mathf.Lerp(1f, 0.6f, heightFactor);
            Vector3 shadowPos = groundDrawLoc + new Vector3(0f, -0.01f, 0f);
            Graphics.DrawMesh(
                MeshPool.plane10,
                Matrix4x4.TRS(shadowPos, Quaternion.identity, new Vector3(size, 1f, size)),
                FadedMaterialPool.FadedVersionOf(UIAssets.ProjectileShadowMaterial, Mathf.Lerp(1f, 0.3f, heightFactor)),
                0);
        }

        protected void SyncEffectTrackerForImpact()
        {
            effects.currentExactPosition = ExactPosition;
            effects.currentVisualPosition = DrawPos;
            effects.currentVisualHeight = effects.currentVisualPosition.z - effects.currentExactPosition.z;
            effects.currentVisualRotation = ExactRotation;
        }

        public override void Launch(
            Thing launcher,
            Vector3 origin,
            LocalTargetInfo usedTarget,
            LocalTargetInfo intendedTarget,
            ProjectileHitFlags hitFlags,
            bool preventFriendlyFire = false,
            Thing equipment = null,
            ThingDef targetCoverDef = null)
        {
            effects.PreLaunch(equipment, ref origin, usedTarget.Cell.ToVector3Shifted());
            base.Launch(launcher, origin, usedTarget, intendedTarget, hitFlags, preventFriendlyFire, equipment, targetCoverDef);
            effects.parentDuration = ticksToImpact;
            effects.PostLaunch(this.origin, this.destination, false);
            HighArcProjectileDebug.LogLaunch(this, StartingTicksToImpact, ticksToImpact, origin, destination);
        }
    }

    public class Projectile_HighArcExplosive : Projectile_HighArcExplosiveBase
    {
    }
}
