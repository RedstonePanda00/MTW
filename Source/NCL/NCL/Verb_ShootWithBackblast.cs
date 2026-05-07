using RimWorld;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Verse;

namespace NCL
{
    public class Verb_ShootWithDustBlast : Verb_Shoot
    {
        // 存储每个 DustPuff 粒子的移动数据
        private class MovingDustPuff
        {
            public Vector3 position;
            public Vector3 direction;
            public float distanceTraveled;
            public float size;
            public float rotation;

            // 使用静态常量而不是常量字段
            public static readonly float MaxDistance = 8f; // 移动5格距离后消失
        }

        private List<MovingDustPuff> movingPuffs = new List<MovingDustPuff>();
        private const float PuffSpeed = 0.4f; // 粒子移动速度

        // 每帧更新移动的粒子


        protected override bool TryCastShot()
        {
            bool shotFired = base.TryCastShot();

            if (shotFired && Caster != null && Caster.Map != null)
            {
                // 计算射击方向
                Vector3 shotDirection = GetShotDirection().normalized;

                // 向后90度随机扩散（±45度）
                Vector3 backblastDirection = -shotDirection;

                // 创建多个移动的 DustPuff 粒子（5-8个）
                int puffCount = Rand.Range(0, 2);
                for (int i = 0; i < puffCount; i++)
                {
                    // 随机角度偏移（90度范围内）
                    Vector3 spreadDirection = backblastDirection.RotatedBy(Rand.Range(-45f, 45f)).normalized;

                    // 在射击者后方生成（距离0.5-1.0格）
                    Vector3 spawnPosition = Caster.DrawPos + (spreadDirection * Rand.Range(2f, 3f));

                    // 直接创建带速度的尘埃粒子
                    CreateMovingDustPuff(
                        spawnPosition,
                        Rand.Range(2.4f, 3.6f),
                        spreadDirection,
                        Rand.Range(0f, 360f)
                    );
                }

                // 生成向前飞的火光效果
                CreateForwardFlares(shotDirection);
            }

            return shotFired;
        }

        // 创建向前飞的火光效果
        private void CreateForwardFlares(Vector3 shotDirection)
        {
            if (Caster.Map == null) return;

            Vector3 flareStartPos = Caster.DrawPos + (shotDirection * 2f);
            flareStartPos.y = AltitudeLayer.MoteOverhead.AltitudeFor();

            // 生成3-5个火光粒子
            int flareCount = Rand.Range(3, 6);
            for (int i = 0; i < flareCount; i++)
            {
                // 带轻微随机偏移的射击方向
                Vector3 flareDirection = shotDirection.RotatedBy(Rand.Range(-5f, 5f));

                // 创建火光粒子
                FleckCreationData flareData = FleckMaker.GetDataStatic(
                    flareStartPos,
                    Caster.Map,
                    FleckDefOf.ShotFlash, // 使用更亮的闪光效果
                    Rand.Range(1.2f, 1.8f) // 增大尺寸
                );

                // 设置速度和方向（向前飞）
                flareData.velocity = flareDirection * Rand.Range(3.0f, 8.5f);
                flareData.rotationRate = Rand.Range(-180f, 180f);
                flareData.solidTimeOverride = 0.5f; // 持续时间更长

                Caster.Map.flecks.CreateFleck(flareData);
            }

            // 额外火花效果
            CreateForwardSparks(flareStartPos, shotDirection);
        }

        // 创建向前飞的火花效果
        private void CreateForwardSparks(Vector3 position, Vector3 direction)
        {
            if (Caster.Map == null) return;

            position.y = AltitudeLayer.MoteOverhead.AltitudeFor();

            int sparkCount = Rand.Range(4, 8);
            for (int i = 0; i < sparkCount; i++)
            {
                FleckCreationData sparkData = FleckMaker.GetDataStatic(
                    position,
                    Caster.Map,
                    FleckDefOf.MicroSparks,
                    Rand.Range(0.6f, 0.9f) // 增大尺寸
                );

                // 带随机偏移的射击方向
                Vector3 sparkDirection = direction.RotatedBy(Rand.Range(-10f, 10f));

                // 设置速度和方向（向前飞）
                sparkData.velocity = sparkDirection * Rand.Range(2.5f, 4.0f);
                sparkData.rotationRate = Rand.Range(-120f, 120f);

                Caster.Map.flecks.CreateFleck(sparkData);
            }
        }

        // 获取射击方向
        private Vector3 GetShotDirection()
        {
            if (currentTarget.IsValid)
            {
                return (currentTarget.CenterVector3 - Caster.DrawPos).normalized;
            }
            return Caster.Rotation.FacingCell.ToVector3();
        }

        // 创建尘埃粒子（确保高度正确）
        private void CreateMovingDustPuff(Vector3 position, float size, Vector3 direction, float rotation)
        {
            Vector3 puffPos = position;
            puffPos.y = AltitudeLayer.MoteLow.AltitudeFor();

            FleckCreationData data = FleckMaker.GetDataStatic(
                puffPos,
                Caster.Map,
                FleckDefOf.DustPuff,
                size * Rand.Range(0.9f, 1.1f)
            );

            // 关键修改：设置粒子移动速度和方向
            data.velocity = direction * Rand.Range(0.8f, 3f); // 速度值可根据需要调整

            // 设置旋转和随机旋转速度
            data.rotation = rotation;
            data.rotationRate = Rand.Range(-90f, 90f);

            // 设置生命周期（与速度匹配）
            data.solidTimeOverride = Mathf.Clamp(size * 0.3f, 0.2f, 0.6f);

            if (Caster.Map != null)
            {
                Caster.Map.flecks.CreateFleck(data);
            }
        }
    }
}




namespace RimWorld
{
    // Token: 0x02000002 RID: 2
    public class Verb_ShootFromAbove : Verb_Shoot
    {
        // Token: 0x06000001 RID: 1 RVA: 0x00002050 File Offset: 0x00000250
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

        // Token: 0x06000002 RID: 2 RVA: 0x000020B4 File Offset: 0x000002B4
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
                // 使用正确的三个参数：精度值、是否可击中非目标单位、地图
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

        // Token: 0x06000003 RID: 3 RVA: 0x0000250C File Offset: 0x0000070C
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

        // Token: 0x04000001 RID: 1
        private const float HeightOffset = -2f;
    }
}