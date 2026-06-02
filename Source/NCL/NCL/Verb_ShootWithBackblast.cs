using RimWorld;
using UnityEngine;
using Verse;

namespace RimWorld
{
    public class Verb_ShootFromAbove : Verb_Shoot
    {
        private Vector3 GetSourcePosition()
        {
            Thing caster = this.caster;
            bool flag = caster != null;
            Vector3 vector;
            if (flag)
            {
                vector = caster.DrawPos;
            }
            else
            {
                vector = this.caster.Position.ToVector3Shifted();
            }
            return new Vector3(vector.x, vector.y, vector.z + -2f);
        }

        protected override bool TryCastShot()
        {
            bool flag = this.currentTarget.HasThing && this.currentTarget.Thing.Map != this.caster.Map;
            bool result;
            if (flag)
            {
                result = false;
            }
            else
            {
                ThingDef projectile = this.Projectile;
                bool flag2 = projectile == null;
                if (flag2)
                {
                    result = false;
                }
                else
                {
                    ShootLine shootLine;
                    bool flag3 = base.TryFindShootLineFromTo(this.caster.Position, this.currentTarget, out shootLine, false);
                    bool flag4 = this.verbProps.stopBurstWithoutLos && !flag3;
                    if (flag4)
                    {
                        result = false;
                    }
                    else
                    {
                        bool flag5 = base.EquipmentSource != null;
                        if (flag5)
                        {
                            CompChangeableProjectile comp = base.EquipmentSource.GetComp<CompChangeableProjectile>();
                            if (comp != null)
                            {
                                comp.Notify_ProjectileLaunched();
                            }
                        }
                        this.lastShotTick = Find.TickManager.TicksGame;
                        Thing thing = this.caster;
                        Thing equipment = base.EquipmentSource;
                        CompMannable compMannable = this.caster.TryGetComp<CompMannable>();
                        bool flag6 = ((compMannable != null) ? compMannable.ManningPawn : null) != null;
                        if (flag6)
                        {
                            thing = compMannable.ManningPawn;
                            equipment = this.caster;
                        }
                        Vector3 sourcePosition = this.GetSourcePosition();
                        Projectile projectile2 = (Projectile)GenSpawn.Spawn(projectile, shootLine.Source, this.caster.Map, WipeMode.Vanish);
                        bool flag7 = this.verbProps.ForcedMissRadius > 0.5f;
                        if (flag7)
                        {
                            float num = this.verbProps.ForcedMissRadius;
                            Pawn pawn = thing as Pawn;
                            bool flag8 = pawn != null;
                            if (flag8)
                            {
                                num *= this.verbProps.GetForceMissFactorFor(equipment, pawn);
                            }
                            float num2 = VerbUtility.CalculateAdjustedForcedMiss(num, this.currentTarget.Cell - this.caster.Position);
                            bool flag9 = num2 > 0.5f;
                            if (flag9)
                            {
                                IntVec3 forcedMissTarget = base.GetForcedMissTarget(num2);
                                bool flag10 = forcedMissTarget != this.currentTarget.Cell;
                                if (flag10)
                                {
                                    ProjectileHitFlags projectileHitFlags = ProjectileHitFlags.NonTargetWorld;
                                    bool flag11 = Rand.Chance(0.5f);
                                    if (flag11)
                                    {
                                        projectileHitFlags = ProjectileHitFlags.All;
                                    }
                                    bool flag12 = !this.canHitNonTargetPawnsNow;
                                    if (flag12)
                                    {
                                        projectileHitFlags &= ~ProjectileHitFlags.NonTargetPawns;
                                    }
                                    projectile2.Launch(thing, sourcePosition, forcedMissTarget, this.currentTarget, projectileHitFlags, this.preventFriendlyFire, equipment, null);
                                    return true;
                                }
                            }
                        }
                        ShotReport report = ShotReport.HitReportFor(caster, this, currentTarget);
                        Thing cover = report.GetRandomCoverToMissInto();
                        ThingDef coverDef = cover?.def;

                        ShotReport shotReport = ShotReport.HitReportFor(this.caster, this, this.currentTarget);
                        Thing randomCoverToMissInto = shotReport.GetRandomCoverToMissInto();
                        ThingDef targetCoverDef = (randomCoverToMissInto != null) ? randomCoverToMissInto.def : null;
                        bool flag13 = this.verbProps.canGoWild && !Rand.Chance(shotReport.AimOnTargetChance_IgnoringPosture);
                        if (flag13)
                        {
                            shootLine.ChangeDestToMissWild(
                                report.AimOnTargetChance_StandardTarget,
                                this.canHitNonTargetPawnsNow,
                                caster.Map
                            );
                            ProjectileHitFlags projectileHitFlags2 = ProjectileHitFlags.NonTargetWorld;
                            bool flag14 = !this.canHitNonTargetPawnsNow;
                            if (flag14)
                            {
                                projectileHitFlags2 &= ~ProjectileHitFlags.NonTargetPawns;
                            }
                            projectile2.Launch(thing, sourcePosition, shootLine.Dest, this.currentTarget, projectileHitFlags2, this.preventFriendlyFire, equipment, targetCoverDef);
                            result = true;
                        }
                        else
                        {
                            bool flag15 = this.currentTarget.Thing != null && this.currentTarget.Thing.def.category == ThingCategory.Pawn && !Rand.Chance(shotReport.AimOnTargetChance_IgnoringPosture) && !Rand.Chance(shotReport.PassCoverChance);
                            if (flag15)
                            {
                                ProjectileHitFlags projectileHitFlags3 = ProjectileHitFlags.NonTargetWorld;
                                bool canHitNonTargetPawnsNow = this.canHitNonTargetPawnsNow;
                                if (canHitNonTargetPawnsNow)
                                {
                                    projectileHitFlags3 |= ProjectileHitFlags.NonTargetPawns;
                                }
                                projectile2.Launch(thing, sourcePosition, randomCoverToMissInto, this.currentTarget, projectileHitFlags3, this.preventFriendlyFire, equipment, targetCoverDef);
                                result = true;
                            }
                            else
                            {
                                ProjectileHitFlags projectileHitFlags4 = ProjectileHitFlags.IntendedTarget;
                                bool canHitNonTargetPawnsNow2 = this.canHitNonTargetPawnsNow;
                                if (canHitNonTargetPawnsNow2)
                                {
                                    projectileHitFlags4 |= ProjectileHitFlags.NonTargetPawns;
                                }
                                bool flag16 = !this.currentTarget.HasThing || this.currentTarget.Thing.def.Fillage == FillCategory.Full;
                                if (flag16)
                                {
                                    projectileHitFlags4 |= ProjectileHitFlags.NonTargetWorld;
                                }
                                bool flag17 = this.currentTarget.Thing != null;
                                if (flag17)
                                {
                                    projectile2.Launch(thing, sourcePosition, this.currentTarget, this.currentTarget, projectileHitFlags4, this.preventFriendlyFire, equipment, targetCoverDef);
                                }
                                else
                                {
                                    projectile2.Launch(thing, sourcePosition, shootLine.Dest, this.currentTarget, projectileHitFlags4, this.preventFriendlyFire, equipment, targetCoverDef);
                                }
                                result = true;
                            }
                        }
                    }
                }
            }
            return result;
        }

        public override void DrawHighlight(LocalTargetInfo target)
        {
            base.DrawHighlight(target);
            bool isValid = target.IsValid;
            if (isValid)
            {
                Vector3 sourcePosition = this.GetSourcePosition();
                GenDraw.DrawLineBetween(sourcePosition, target.CenterVector3, SimpleColor.Red, 0.2f);
            }
        }

        private const float HeightOffset = -2f;
    }
}
