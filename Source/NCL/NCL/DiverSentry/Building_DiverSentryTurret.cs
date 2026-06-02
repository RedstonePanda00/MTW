using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace NCL
{
    public class Building_DiverSentryTurret : Building_TurretGun
    {
        // Vanilla skips TurretTopTick while VerbState.Bursting; gatling stays bursting almost continuously.
        private const float TurnDegreesPerTick = 9f;

        private static readonly FieldInfo CurrentTargetIntField =
            AccessTools.Field(typeof(Building_TurretGun), "currentTargetInt");

        protected override void Tick()
        {
            base.Tick();
            TickTurretTopAiming();
        }

        public bool TryRefreshBurstTarget(out LocalTargetInfo target)
        {
            target = LocalTargetInfo.Invalid;
            if (!Spawned || CurrentTargetIntField == null)
            {
                return false;
            }

            LocalTargetInfo previous = CurrentTarget;
            LocalTargetInfo resolved = previous;

            if (previous.IsValid && !IsBurstTargetDead(previous))
            {
                if (AttackVerb != null && !AttackVerb.CanHitTarget(previous))
                {
                    return false;
                }

                target = previous;
                TickTurretTopAiming();
                return true;
            }

            LocalTargetInfo next = ForcedTarget.IsValid ? ForcedTarget : TryFindNewTarget();
            if (!next.IsValid)
            {
                return false;
            }

            if (AttackVerb != null && !AttackVerb.CanHitTarget(next))
            {
                return false;
            }

            CurrentTargetIntField.SetValue(this, next);
            target = next;

            if (!previous.Equals(target))
            {
                TickTurretTopAiming();
            }

            return true;
        }

        private void TickTurretTopAiming()
        {
            if (!Spawned || IsStunned || Top == null)
            {
                return;
            }

            LocalTargetInfo target = CurrentTarget;
            if (!target.IsValid)
            {
                return;
            }

            float desiredAngle = GetAimAngleForTarget(target);
            RotateTopToward(desiredAngle, TurnDegreesPerTick);
        }

        private float GetAimAngleForTarget(LocalTargetInfo target)
        {
            Vector3 aimPos = target.HasThing && target.Thing.Spawned
                ? target.Thing.DrawPos
                : target.Cell.ToVector3Shifted();
            return (aimPos - DrawPos).AngleFlat();
        }

        private void RotateTopToward(float desiredAngle, float maxDegreesPerTick)
        {
            float current = Top.CurRotation;
            float delta = Mathf.DeltaAngle(current, desiredAngle);
            if (Mathf.Abs(delta) <= maxDegreesPerTick)
            {
                Top.CurRotation = desiredAngle;
            }
            else
            {
                Top.CurRotation = current + Mathf.Sign(delta) * maxDegreesPerTick;
            }
        }

        private static bool IsBurstTargetDead(LocalTargetInfo target)
        {
            if (!target.IsValid)
            {
                return true;
            }

            if (!target.HasThing)
            {
                return false;
            }

            Thing thing = target.Thing;
            if (thing.Destroyed)
            {
                return true;
            }

            if (thing is Pawn pawn)
            {
                return pawn.Dead;
            }

            return false;
        }
    }
}
