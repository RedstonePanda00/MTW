using RimWorld;
using UnityEngine;
using Verse;

namespace NCL
{
    // Map-plane slant variant for MTW_FatCatMobile reserve
    public class Projectile_FatCatMissile : Projectile_Explosive
    {
        private enum FlightPhase
        {
            Climb,
            Dive
        }

        private FlightPhase phase = FlightPhase.Climb;
        private Vector3 curExactPos;
        private Vector3 velocity;
        private Vector3 lastTargetPos;
        private int ticksAlive;
        private int trailCooldown;
        private float diveSpeed;
        private float flightAltitude;

        private CompProperties_FatCatMissile Config =>
            def.GetCompProperties<CompProperties_FatCatMissile>() ?? new CompProperties_FatCatMissile();

        public override Vector3 ExactPosition => curExactPos;

        public override Quaternion ExactRotation =>
            velocity.Yto0().sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(velocity.Yto0())
                : base.ExactRotation;

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
            base.Launch(launcher, origin, usedTarget, intendedTarget, hitFlags, preventFriendlyFire, equipment, targetCoverDef);
            InitializeFlight();
        }

        private void InitializeFlight()
        {
            phase = FlightPhase.Climb;
            ticksAlive = 0;
            trailCooldown = 0;
            diveSpeed = 0f;
            landed = false;
            ticksToImpact = 999999;
            curExactPos = GetLaunchPosition();
            flightAltitude = curExactPos.y;
            velocity = GetClimbVelocity(Config.climbSpeed);
            lastTargetPos = GetTargetDrawPos();

            if (Map != null)
            {
                Position = curExactPos.ToIntVec3();
            }
        }

        private Vector3 GetLaunchPosition()
        {
            Vector3 launchPos = origin;
            if (equipment?.DrawPosHeld is Vector3 heldPos)
            {
                launchPos = heldPos;
            }
            else if (launcher is Pawn pawn)
            {
                launchPos = pawn.DrawPos;
            }

            return launchPos.Yto0() + Vector3.up * def.Altitude;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref phase, "fatCatPhase", FlightPhase.Climb);
            Scribe_Values.Look(ref curExactPos, "fatCatCurExactPos");
            Scribe_Values.Look(ref velocity, "fatCatVelocity");
            Scribe_Values.Look(ref lastTargetPos, "fatCatLastTargetPos");
            Scribe_Values.Look(ref ticksAlive, "fatCatTicksAlive", 0);
            Scribe_Values.Look(ref trailCooldown, "fatCatTrailCooldown", 0);
            Scribe_Values.Look(ref diveSpeed, "fatCatDiveSpeed", 0f);
            Scribe_Values.Look(ref flightAltitude, "fatCatFlightAltitude", 0f);
        }

        protected override void TickInterval(int delta)
        {
            if (landed || Map == null)
            {
                return;
            }

            lifetime -= delta;
            for (int i = 0; i < delta; i++)
            {
                AdvanceFlight();
                if (!curExactPos.InBounds(Map))
                {
                    Destroy();
                    return;
                }

                Position = curExactPos.ToIntVec3();

                if (TryImpactOnTarget() || TryImpactOnTimeout())
                {
                    return;
                }
            }
        }

        private void AdvanceFlight()
        {
            ticksAlive++;
            CompProperties_FatCatMissile cfg = Config;

            if (phase == FlightPhase.Climb)
            {
                ApplyMapPlaneMotion(velocity);
                if (ticksAlive >= cfg.climbTicks)
                {
                    phase = FlightPhase.Dive;
                    diveSpeed = cfg.climbSpeed;
                }

                return;
            }

            Vector3 predictedTarget = GetPredictedTargetPos(cfg.predictTicks, forDive: true);
            Vector3 toTarget = (predictedTarget - curExactPos).Yto0();
            float distToActualTarget = HorizontalDistanceTo(GetTargetDrawPos());

            if (toTarget.sqrMagnitude > 0.0001f)
            {
                diveSpeed = Mathf.Min(diveSpeed + cfg.diveAcceleration, cfg.diveMaxSpeed);
                velocity = toTarget.normalized * diveSpeed;
            }

            ApplyMapPlaneMotion(velocity);
            SnapToTargetIfClose(cfg, distToActualTarget);
            EmitDiveTrail(cfg);
        }

        private void SnapToTargetIfClose(CompProperties_FatCatMissile cfg, float distBeforeMove)
        {
            Vector3 targetPos = GetTargetDrawPos();
            float dist = HorizontalDistanceTo(targetPos);
            if (dist <= cfg.hitRadius)
            {
                curExactPos = targetPos.Yto0() + Vector3.up * flightAltitude;
                return;
            }

            // Overshoot guard: passed the target while still within intercept range.
            if (distBeforeMove <= cfg.hitRadius + diveSpeed && dist >= distBeforeMove)
            {
                curExactPos = targetPos.Yto0() + Vector3.up * flightAltitude;
            }
        }

        private float HorizontalDistanceTo(Vector3 targetPos) =>
            (targetPos - curExactPos).MagnitudeHorizontal();

        private float GetEffectiveHitRadius(CompProperties_FatCatMissile cfg, float horizontalDistance) =>
            cfg.hitRadius + (horizontalDistance <= 3f ? 1f : 0f);

        private void ApplyMapPlaneMotion(Vector3 delta)
        {
            curExactPos += delta.Yto0();
            curExactPos.y = flightAltitude;
        }

        private Vector3 GetClimbVelocity(float speed)
        {
            float angle = GetLauncherAngle();
            Vector3 facing = (Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward).Yto0();
            if (facing.sqrMagnitude < 0.0001f)
            {
                facing = Vector3.forward;
            }
            else
            {
                facing.Normalize();
            }

            // Map-plane slant: climbAngleDegrees off launcher forward toward lateral (no Y change).
            // East/South slant left; North/West slant right.
            Vector3 right = Vector3.Cross(Vector3.up, facing).normalized;
            float lateralSign = GetClimbSlantLateralSign();
            float climbRad = Config.climbAngleDegrees * Mathf.Deg2Rad;
            Vector3 direction = facing * Mathf.Cos(climbRad) + right * (Mathf.Sin(climbRad) * lateralSign);
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = facing;
            }
            else
            {
                direction = direction.Yto0().normalized;
            }

            return direction * speed;
        }

        private float GetClimbSlantLateralSign()
        {
            Rot4 rotation = launcher is Pawn pawn
                ? pawn.Rotation
                : Rot4.FromAngleFlat(GetLauncherAngle());

            return rotation.AsInt switch
            {
                1 => -1f, // East
                2 => -1f, // South
                _ => 1f,   // North, West
            };
        }

        private float GetLauncherAngle()
        {
            if (launcher is Pawn pawn)
            {
                return pawn.Rotation.AsAngle;
            }

            return (destination - origin).AngleFlat();
        }

        private Vector3 GetTargetDrawPos()
        {
            if (intendedTarget.HasThing && intendedTarget.Thing.Spawned)
            {
                return intendedTarget.Thing.DrawPos;
            }

            return destination;
        }

        private Vector3 GetPredictedTargetPos(int predictTicksAhead, bool forDive = false)
        {
            Vector3 currentTargetPos = GetTargetDrawPos();
            Vector3 targetVelocity = currentTargetPos - lastTargetPos;
            lastTargetPos = currentTargetPos;

            if (intendedTarget.Thing is Pawn targetPawn
                && targetPawn.Spawned
                && !targetPawn.Dead
                && !targetPawn.Downed
                && targetPawn.pather?.Moving == true
                && targetVelocity.sqrMagnitude > 0.00001f)
            {
                int leadTicks = predictTicksAhead;
                if (forDive)
                {
                    float dist = HorizontalDistanceTo(currentTargetPos);
                    int maxLead = Mathf.Max(5, Mathf.CeilToInt(dist / Mathf.Max(diveSpeed, 0.05f)));
                    leadTicks = Mathf.Min(predictTicksAhead, maxLead);
                }

                return currentTargetPos + targetVelocity * leadTicks;
            }

            return currentTargetPos;
        }

        private bool TryImpactOnTarget()
        {
            CompProperties_FatCatMissile cfg = Config;
            if (phase != FlightPhase.Dive)
            {
                return false;
            }

            Vector3 targetPos = GetTargetDrawPos();
            float horizontalDistance = HorizontalDistanceTo(targetPos);
            if (horizontalDistance <= GetEffectiveHitRadius(cfg, horizontalDistance))
            {
                if (intendedTarget.HasThing && intendedTarget.Thing.Spawned)
                {
                    Position = intendedTarget.Thing.Position;
                }

                ImpactSomething();
                return true;
            }

            return false;
        }

        private bool TryImpactOnTimeout()
        {
            CompProperties_FatCatMissile cfg = Config;
            if (phase != FlightPhase.Dive || ticksAlive <= cfg.climbTicks + 300)
            {
                return false;
            }

            Impact(null);
            return true;
        }

        private void EmitDiveTrail(CompProperties_FatCatMissile cfg)
        {
            if (phase != FlightPhase.Dive || Map == null)
            {
                return;
            }

            trailCooldown--;
            if (trailCooldown > 0)
            {
                return;
            }

            trailCooldown = cfg.trailIntervalTicks;
            if (velocity.sqrMagnitude < 0.0001f)
            {
                return;
            }

            FleckDef jumpFlame = DefDatabase<FleckDef>.GetNamedSilentFail("JumpFlame");
            FleckDef jumpGlow = DefDatabase<FleckDef>.GetNamedSilentFail("JumpFlameGlow");
            Vector3 pos = ExactPosition;
            float tailAngle = velocity.AngleFlat() + 180f;

            if (jumpFlame != null)
            {
                FleckCreationData flameData = FleckMaker.GetDataStatic(pos, Map, jumpFlame, Rand.Range(0.5f, 0.6f));
                flameData.velocityAngle = tailAngle;
                flameData.velocitySpeed = Rand.Range(4f, 5f);
                Map.flecks.CreateFleck(flameData);
            }

            if (jumpGlow != null)
            {
                FleckCreationData glowData = FleckMaker.GetDataStatic(pos, Map, jumpGlow, Rand.Range(0.9f, 0.7f));
                glowData.velocityAngle = tailAngle + Rand.Range(-10f, 10f);
                glowData.velocitySpeed = Rand.Range(4f, 5f);
                Map.flecks.CreateFleck(glowData);
            }
        }
    }
}
