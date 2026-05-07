using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace NyarsModPackTwo
{
    // Token: 0x0200000C RID: 12
    public class CompProperties_ShootRandomBullet : CompProperties_AbilityEffect
    {
        // Token: 0x06000022 RID: 34 RVA: 0x00002736 File Offset: 0x00000936
        public CompProperties_ShootRandomBullet()
        {
            this.compClass = typeof(CompAbilityEffect_ShootRandomBullet);
        }

        // Token: 0x04000009 RID: 9
        public List<ThingDef> bullets;

        // Token: 0x0400000A RID: 10
        public IntRange castCount = new IntRange(1, 1);
    }
}

namespace NyarsModPackTwo
{
    // Token: 0x0200000B RID: 11
    public class CompAbilityEffect_ShootRandomBullet : CompAbilityEffect
    {
        // Token: 0x17000007 RID: 7
        // (get) Token: 0x0600001D RID: 29 RVA: 0x000025C7 File Offset: 0x000007C7
        public new CompProperties_ShootRandomBullet Props
        {
            get
            {
                return (CompProperties_ShootRandomBullet)this.props;
            }
        }

        // Token: 0x0600001E RID: 30 RVA: 0x000025D4 File Offset: 0x000007D4
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn pawn = this.parent.pawn;
            bool flag = CompAbilityEffect_ShootRandomBullet.bulletCache.Count == 0;
            if (flag)
            {
                CompAbilityEffect_ShootRandomBullet.bulletCache.AddRange(this.Props.bullets);
            }
            for (int i = 0; i < this.Props.castCount.RandomInRange; i++)
            {
                bool flag2 = CompAbilityEffect_ShootRandomBullet.bulletCache.Count == 0;
                if (flag2)
                {
                    CompAbilityEffect_ShootRandomBullet.bulletCache.AddRange(this.Props.bullets);
                }
                int index = Rand.Range(0, CompAbilityEffect_ShootRandomBullet.bulletCache.Count);
                ThingDef bulletDef = CompAbilityEffect_ShootRandomBullet.bulletCache[index];
                CompAbilityEffect_ShootRandomBullet.bulletCache.RemoveAt(index);
                this.LaunchBullet(pawn, bulletDef);
            }
        }

        // Token: 0x0600001F RID: 31 RVA: 0x000026A4 File Offset: 0x000008A4
        private void LaunchBullet(Pawn caster, ThingDef bulletDef)
        {
            Map map = caster.Map;
            Projectile projectile = (Projectile)GenSpawn.Spawn(bulletDef, caster.Position, map, WipeMode.Vanish);
            Bullet_TracingEnemies bullet_TracingEnemies = projectile as Bullet_TracingEnemies;
            bool flag = bullet_TracingEnemies != null;
            if (flag)
            {
                bullet_TracingEnemies.flyingAngle = Rand.Value * 360f;
                bullet_TracingEnemies.trackingPosNow = caster.TrueCenter();
            }
            projectile.Launch(caster, caster.TrueCenter(), default(LocalTargetInfo), default(LocalTargetInfo), ProjectileHitFlags.IntendedTarget | ProjectileHitFlags.NonTargetWorld, false, null, null);
        }

        // Token: 0x04000008 RID: 8
        private static List<ThingDef> bulletCache = new List<ThingDef>();
    }
}
namespace NyarsModPackTwo
{
    public class Bullet_TracingEnemies : Bullet
{
        private static readonly Dictionary<int, Bullet_TracingEnemies> LockedProjectiles =
            new Dictionary<int, Bullet_TracingEnemies>();
        private enum TargetType
    {
        None,       // 无目标
        Projectile, // 敌方投射物
        Pawn        // 敌人单位
    }

    public override Vector3 ExactPosition =>
        trackingPosNow + Vector3.up * def.Altitude;

    public override Quaternion ExactRotation =>
        Quaternion.AngleAxis(flyingAngle, Vector3.up);

    private float TargetAngle =>
        (trackingCell.ToVector3() - trackingPosNow).AngleFlat();

    private ModExtension_BulletProperties Props =>
        _props ??= def.GetModExtension<ModExtension_BulletProperties>();

    private bool IsHostileProjectile(Thing projectile)
    {
        // 检查是否为投射物
        if (!(projectile is Projectile proj))
            return false;

        // 检查发射者是否存在且敌对
        Thing launcher = proj.Launcher;
        return launcher != null && launcher.Faction != null &&
               this.launcher != null && this.launcher.Faction != null &&
               launcher.Faction.HostileTo(this.launcher.Faction);
    }

        protected override void Tick()
        {
            // 计算当前飞行速度（包含加速阶段）
            float currentFlyingStep;
            if (_flyingTime < Props.ticksBeforeTracing)
            {
                // 初始阶段：使用初始速度
                currentFlyingStep = Props.initialFlyingStep;
            }
            else if (_flyingTime < Props.ticksBeforeTracing + Props.accelerationDuration)
            {
                // 加速阶段：线性插值过渡
                float progress = (_flyingTime - Props.ticksBeforeTracing) / (float)Props.accelerationDuration;
                currentFlyingStep = Props.initialFlyingStep + progress * (Props.flyingStep - Props.initialFlyingStep);
            }
            else
            {
                // 稳定阶段：使用最终速度
                currentFlyingStep = Props.flyingStep;
            }

            bool landed = this.landed;
            bool flag = !landed;
            if (flag)
            {
                bool flag2 = this._flyingTime >= this.Props.maxFlyingTime;
                bool flag3 = flag2;
                if (flag3)
                {
                    this.Impact(null, false);
                }
                else
                {
                    bool flag4 = this._flyingTime >= this.Props.ticksBeforeTracing && (this._flyingTime - this.Props.ticksBeforeTracing) % this.Props.ticksBetweenFindTarget == 0;
                    bool flag5 = flag4;
                    if (flag5)
                    {
                        this.UpdateTarget();
                    }
                    this.UpdateTargetCell();
                    this._flyingTime++;
                    Vector3 vector = this.trackingCell.ToVector3();
                    float num = vector.x - this.trackingPosNow.x;
                    float num2 = vector.z - this.trackingPosNow.z;
                    float num3 = num * num + num2 * num2;

                    // 使用当前速度计算碰撞检测阈值
                    bool flag6 = num3 <= currentFlyingStep * currentFlyingStep * 3f;
                    bool flag7 = flag6;
                    if (flag7)
                    {
                        this.ticksToImpact = 0;
                        base.Position = this.trackingCell;
                        bool flag8 = this.trackingTargetThing != null && this.trackingTargetThing.Spawned;
                        bool flag9 = flag8;
                        if (flag9)
                        {
                            // 拦截投射物而不是造成伤害
                            if (currentTargetType == TargetType.Projectile)
                            {
                                InterceptProjectile(this.trackingTargetThing as Projectile);
                            }
                            else
                            {
                                this.Impact(this.trackingTargetThing, false);
                            }
                        }
                        else
                        {
                            this.Impact(null, false);
                        }
                    }
                    else
                    {
                        Vector3 exactPosition = this.ExactPosition;

                        // 使用当前速度移动子弹位置
                        this.trackingPosNow += new Vector3(
                            (float)Math.Sin((double)(this.flyingAngle / 180f * 3.14159f)),
                            0f,
                            (float)Math.Cos((double)(this.flyingAngle / 180f * 3.14159f))
                        ) * currentFlyingStep;

                        bool flag10 = !this.trackingPosNow.InBounds(base.Map);
                        bool flag11 = flag10;
                        if (flag11)
                        {
                            this.ticksToImpact = 0;
                            this.Destroy(DestroyMode.Vanish);
                        }
                        else
                        {
                            Vector3 exactPosition2 = this.ExactPosition;
                            bool flag12 = this.Props.trailFleck != null;
                            bool flag13 = flag12;
                            if (flag13)
                            {
                                FleckMaker.ConnectingLine(exactPosition, exactPosition2, this.Props.trailFleck, base.Map, 0.1f);
                            }
                            bool flag14 = (bool)Bullet_TracingEnemies._interceptCheck.Invoke(this, this._interceptParams);
                            bool flag15 = !flag14;
                            if (flag15)
                            {
                                base.Position = this.trackingPosNow.ToIntVec3();
                                this.Rotate();
                            }
                        }
                    }
                }
            }
        }


        private void InterceptProjectile(Projectile projectile)
        {
            if (projectile == null || projectile.Destroyed) return;

            // 获取目标位置（修复缺失的变量定义）
            Vector3 targetPos = projectile.DrawPos;

            // === 1. 检测是否为特殊子弹 ===
            bool isSpecialBullet = projectile.def.defName == "Bullet_HellsphereCannonGun";

            // === 2. 创建特效 ===
            // 主特效 - 随机从列表中选取一个
            if (Props.randomImpactFlecks != null && Props.randomImpactFlecks.Count > 0 && !isSpecialBullet)
            {
                string chosenFleckName = Props.randomImpactFlecks.RandomElement();
                FleckDef impactFleckDef = DefDatabase<FleckDef>.GetNamedSilentFail(chosenFleckName);
                if (impactFleckDef != null)
                {
                    FleckMaker.Static(
                        targetPos,
                        base.Map,
                        impactFleckDef,
                        Props.impactScale
                    );
                }
            }

            // 次要特效（带随机角度偏移）
            if (!string.IsNullOrEmpty(Props.secondaryImpactFleck) && !isSpecialBullet)
            {
                FleckDef secondaryFleckDef = DefDatabase<FleckDef>.GetNamedSilentFail(Props.secondaryImpactFleck);
                if (secondaryFleckDef != null)
                {
                    // 计算随机偏移位置
                    float offsetDistance = Props.secondaryImpactScale * 0.8f;
                    Vector3 offsetPos = GetOffsetPosition(targetPos, offsetDistance);

                    FleckMaker.Static(
                        offsetPos,
                        base.Map,
                        secondaryFleckDef,
                        Props.secondaryImpactScale
                    );
                }
            }

            // 第三特效（带随机角度偏移）
            if (!string.IsNullOrEmpty(Props.tertiaryImpactFleck) && !isSpecialBullet)
            {
                FleckDef tertiaryFleckDef = DefDatabase<FleckDef>.GetNamedSilentFail(Props.tertiaryImpactFleck);
                if (tertiaryFleckDef != null)
                {
                    // 计算随机偏移位置
                    float offsetDistance = Props.tertiaryImpactScale * 0.8f;
                    Vector3 offsetPos = GetOffsetPosition(targetPos, offsetDistance);

                    FleckMaker.Static(
                        offsetPos,
                        base.Map,
                        tertiaryFleckDef,
                        Props.tertiaryImpactScale
                    );
                }
            }

            // 烟雾效果（带随机角度偏移）
            if (Props.enableSmokeEffect && !isSpecialBullet)
            {
                float offsetDistance = Props.smokeSize * 0.5f;
                Vector3 offsetPos = GetOffsetPosition(targetPos, offsetDistance);

                FleckMaker.ThrowSmoke(
                    offsetPos,
                    base.Map,
                    Props.smokeSize
                );
            }

            // 火光效果（带随机角度偏移）
            if (Props.enableFireGlowEffect && !isSpecialBullet)
            {
                float offsetDistance = Props.fireGlowSize * 0.5f;
                Vector3 offsetPos = GetOffsetPosition(targetPos, offsetDistance);

                FleckMaker.ThrowFireGlow(
                    offsetPos,
                    base.Map,
                    Props.fireGlowSize
                );
            }

            // === 3. 播放音效 ===
            if (Props.interceptSound != null)
            {
                Props.interceptSound.PlayOneShot(new TargetInfo(projectile.Position, base.Map));
            }

            // === 4. 处理特殊子弹 ===
            if (isSpecialBullet)
            {
                // 特殊处理：直接触发子弹的爆炸效果
                TriggerBulletExplosion(projectile);
            }
            else
            {
                // 普通投射物直接销毁
                projectile.Destroy(DestroyMode.Vanish);
            }

            // === 5. 销毁自身 ===
            this.Destroy(DestroyMode.Vanish);
        }

        // 辅助方法：获取带随机偏移的位置
        private Vector3 GetOffsetPosition(Vector3 center, float maxOffset)
        {
            // 生成随机角度（0-360度）
            float angle = Rand.Range(0f, 360f);

            // 生成随机偏移距离（0到maxOffset之间）
            float distance = Rand.Range(0f, maxOffset);

            // 计算偏移向量
            float xOffset = Mathf.Sin(angle * Mathf.Deg2Rad) * distance;
            float zOffset = Mathf.Cos(angle * Mathf.Deg2Rad) * distance;

            // 返回偏移后的位置
            return new Vector3(
                center.x + xOffset,
                center.y,
                center.z + zOffset
            );
        }


        private void TriggerBulletExplosion(Projectile projectile)
        {
            try
            {
                if (projectile == null || projectile.Destroyed || !projectile.Spawned)
                    return;

                // 使用反射触发子弹的爆炸效果
                MethodInfo impactMethod = typeof(Projectile).GetMethod("Impact", BindingFlags.Instance | BindingFlags.NonPublic);
                if (impactMethod != null)
                {
                    impactMethod.Invoke(projectile, new object[] { null, false });
                }
                else
                {
                    // 如果反射失败，使用替代方法
                    projectile.Destroy(DestroyMode.Vanish);
                }

                // 如果子弹没有被摧毁（可能由于某些原因），强制销毁
                if (!projectile.Destroyed)
                {
                    projectile.Destroy(DestroyMode.Vanish);
                }
            }
            catch (Exception ex)
            {
                // 错误处理
                Log.Error($"处理特殊子弹爆炸时出错: {ex}");
            }
        }

        private void CleanupReferences()
        {
            // 释放当前锁定的投射物（如果有）
            if (trackingTargetThing != null && currentTargetType == TargetType.Projectile)
            {
                ReleaseProjectileLock(trackingTargetThing.thingIDNumber);
            }

            this.trackingTargetThing = null;
            this.trackingCell = IntVec3.Invalid;
            currentTargetType = TargetType.None;
        }

        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            // 清理引用
            this.CleanupReferences();

            Vector3 hitPos = this.ExactPosition;

            // 生成配置的特效
            if (this.Props.impactFleck != null)
            {
                FleckCreationData fleckData = new FleckCreationData
                {
                    def = this.Props.impactFleck,
                    spawnPosition = hitPos,
                    scale = 1f,
                    rotation = (float)Rand.Range(0, 360),
                    ageTicksOverride = -1
                };
                base.Map.flecks.CreateFleck(fleckData);
            }

            // 仅在命中敌方单位时生成爆炸（根据配置决定）
            if (hitThing != null && !blockedByShield && hitThing is Pawn hitPawn)
            {
                // 检查是否敌对
                if (this.launcher != null && this.launcher.Faction != null &&
                    hitPawn.Faction != null && hitPawn.Faction.HostileTo(this.launcher.Faction))
                {
                    // 检查是否启用了爆炸效果
                    if (Props.enableExplosionOnHit)
                    {
                        // 计算爆炸位置 - 在敌人正下方
                        Vector3 explosionPos = hitPawn.DrawPos;

                        // 创建爆炸
                        GenExplosion.DoExplosion(
                            explosionPos.ToIntVec3(), // 爆炸位置（敌人正下方）
                            base.Map,                 // 当前地图
                            4.2f,                     // 爆炸半径
                            DamageDefOf.Bomb,         // 伤害类型
                            this.launcher,            // 发起者
                            damAmount: 35,            // 基础伤害
                            armorPenetration: 1f,     // 护甲穿透
                            weapon: null,
                            projectile: null,
                            intendedTarget: null
                        );
                    }
                }
            }

            // 调用基类Impact方法
            base.Impact(hitThing, blockedByShield);
        }



        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
    {
        this.CleanupReferences();
        base.Destroy(mode);
    }

    private bool IsEnemy(Pawn pawn)
    {
        return launcher != null && launcher.HostileTo(pawn) && !pawn.Downed;
    }
        private void UpdateTarget()
        {
            // 重置目标
            trackingTargetThing = null;
            currentTargetType = TargetType.None;

            // 释放当前锁定的投射物（如果有）
            if (currentTargetType == TargetType.Projectile && trackingTargetThing != null)
            {
                ReleaseProjectileLock(trackingTargetThing.thingIDNumber);
            }

            // 1. 首先检查是否开启追踪投射物
            if (Props.trackEnemyProjectiles)
            {
                List<Thing> projectiles = Map.listerThings.ThingsInGroup(ThingRequestGroup.Projectile);
                List<Thing> availableProjectiles = new List<Thing>();

                foreach (var proj in projectiles)
                {
                    if (proj != null && proj.Spawned && proj.Map == Map && IsHostileProjectile(proj))
                    {
                        // 应用投射物类型过滤
                        bool isAirProjectile = proj.def.projectile.flyOverhead;
                        if ((isAirProjectile && Props.ignoreAirProjectiles) ||
                            (!isAirProjectile && Props.ignoreGroundProjectiles))
                        {
                            continue;
                        }

                        // 检查投射物是否已被其他导弹锁定
                        int projectileId = proj.thingIDNumber;
                        if (LockedProjectiles.ContainsKey(projectileId) &&
                            LockedProjectiles[projectileId] != this)
                        {
                            continue;
                        }

                        availableProjectiles.Add(proj);
                    }
                }

                if (availableProjectiles.Count > 0)
                {
                    // 找到最近的可用敌方投射物
                    Thing closestProjectile = availableProjectiles
                        .OrderBy(p => (p.Position - Position).LengthHorizontalSquared)
                        .FirstOrDefault();

                    if (closestProjectile != null)
                    {
                        trackingTargetThing = closestProjectile;
                        currentTargetType = TargetType.Projectile;

                        // 锁定这个投射物
                        LockProjectile(closestProjectile.thingIDNumber);
                        return;
                    }
                }
            }

            // 2. 如果没有找到可用敌方投射物或未开启追踪，搜索敌人单位
            _localTargetCache.Clear();

            foreach (Pawn pawn in Map.mapPawns.AllPawnsSpawned)
            {
                if (pawn != null && pawn.Spawned && pawn.Map == Map && IsEnemy(pawn))
                {
                    _localTargetCache.Add(pawn);
                }
            }

            if (_localTargetCache.Count > 0)
            {
                // 按距离排序
                _localTargetCache.Sort((a, b) =>
                    (a.Position - Position).LengthHorizontalSquared.CompareTo(
                    (b.Position - Position).LengthHorizontalSquared));

                trackingTargetThing = _localTargetCache[0];
                currentTargetType = TargetType.Pawn;
            }
        }

        private void LockProjectile(int projectileId)
        {
            if (LockedProjectiles.ContainsKey(projectileId))
            {
                // 如果之前被其他导弹锁定，现在改为被当前导弹锁定
                LockedProjectiles[projectileId] = this;
            }
            else
            {
                // 添加新锁定
                LockedProjectiles.Add(projectileId, this);
            }
        }

        // 释放投射物锁定
        private void ReleaseProjectileLock(int projectileId)
        {
            // 检查是否是当前导弹锁定的投射物
            if (LockedProjectiles.ContainsKey(projectileId) &&
                LockedProjectiles[projectileId] == this)
            {
                LockedProjectiles.Remove(projectileId);
            }
        }


        private void UpdateTargetCell()
        {
            // 增强空引用检查
            if (this.trackingTargetThing == null ||
                this.trackingTargetThing.Destroyed ||
                !this.trackingTargetThing.Spawned ||
                this.trackingTargetThing.Map != this.Map)
            {
                this.trackingCell = IntVec3.Invalid;
                return;
            }

            // 根据目标类型调整追踪精度
            float precisionFactor = currentTargetType == TargetType.Projectile ?
                0.8f :  // 投射物追踪更精确
                0.6f;   // 单位追踪稍宽松

            // 获取目标位置 - 添加空检查
            Vector3 predictedPos = this.trackingTargetThing.DrawPos;

            // 处理Pawn目标
            if (this.trackingTargetThing is Pawn pawn)
            {
                // 确保Pawn有寻路组件且正在移动
                if (pawn.pather != null && pawn.pather.Moving)
                {
                    Vector3 moveDir = (pawn.pather.Destination.Cell.ToVector3() - pawn.DrawPos).normalized;
                    predictedPos += moveDir * Props.flyingStep * precisionFactor;
                }
            }
            // 处理投射物目标
            else if (this.trackingTargetThing is Projectile projectile)
            {
                // 确保投射物有定义和速度组件
                if (projectile.def != null && projectile.def.projectile != null)
                {
                    Vector3 projDir = projectile.ExactRotation * Vector3.forward;
                    predictedPos += projDir * projectile.def.projectile.SpeedTilesPerTick * 2f;
                }
            }

            this.trackingCell = predictedPos.ToIntVec3();
        }


        private void Rotate()
    {
        // 如果目标是投射物，使用更快的旋转速度
        float rotationMultiplier = currentTargetType == TargetType.Projectile ? 1.5f : 1.0f;

        bool flag = this.trackingCell == IntVec3.Invalid;
        bool flag2 = !flag;
        if (flag2)
        {
            float targetAngle = this.TargetAngle;
            float num = this.flyingAngle - targetAngle;
            float num2 = this.Props.rotatingStep * rotationMultiplier * ((this._flyingTime < 60 + this.Props.ticksBeforeTracing) ? 1f : ((float)(this._flyingTime - 60 - this.Props.ticksBeforeTracing) / 15f + 1f));
            bool flag3 = num > 180f;
            bool flag4 = flag3;
            if (flag4)
            {
                num -= 360f;
            }
            bool flag5 = num < -180f;
            bool flag6 = flag5;
            if (flag6)
            {
                num += 360f;
            }
            bool flag7 = num > num2;
            bool flag8 = flag7;
            if (flag8)
            {
                this.flyingAngle -= num2;
            }
            else
            {
                bool flag9 = num < -num2;
                bool flag10 = flag9;
                if (flag10)
                {
                    this.flyingAngle += num2;
                }
                else
                {
                    this.flyingAngle = targetAngle;
                }
            }
            this.flyingAngle %= 360f;
        }
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look<int>(ref this._flyingTime, "_flyingTime", 0, false);
        Scribe_References.Look<Thing>(ref this.trackingTargetThing, "trackingTargetThing", false);
        Scribe_Values.Look<IntVec3>(ref this.trackingCell, "trackingCell", default(IntVec3), false);
        Scribe_Values.Look<float>(ref this.flyingAngle, "flyingAngle", 0f, false);
        Scribe_Values.Look<Vector3>(ref this.trackingPosNow, "trackingPosNow", default(Vector3), false);
        Scribe_Values.Look<TargetType>(ref currentTargetType, "currentTargetType", TargetType.None);
    }

    private static readonly MethodInfo _interceptCheck = typeof(Projectile).GetMethod("CheckForFreeInterceptBetween", BindingFlags.Instance | BindingFlags.NonPublic);
    private readonly object[] _interceptParams = new object[2];
    private readonly List<Thing> _localTargetCache = new List<Thing>();
    private ModExtension_BulletProperties _props;
    private int _flyingTime;
    public Thing trackingTargetThing;
    public IntVec3 trackingCell = IntVec3.Invalid;
    public float flyingAngle;
    public Vector3 trackingPosNow;
    private TargetType currentTargetType = TargetType.None; // 当前追踪的目标类型
}

    public class ModExtension_BulletProperties : DefModExtension
    {
        // 目标追踪配置
        public bool trackEnemyProjectiles = false;    // 是否追踪敌方投射物
        public bool ignoreGroundProjectiles = true; // 是否忽略地面投射物
        public bool ignoreAirProjectiles = false;    // 是否忽略空中投射物
        public bool enableExplosionOnHit = false; // 是否在击中敌人时产生爆炸效果（默认关闭）
        public float explosionRadius = 4.2f;      // 爆炸半径
        public int explosionDamage = 35;
        // 视觉效果
        public FleckDef trailFleck;
        public FleckDef impactFleck;

        // 追踪参数
        public int accelerationDuration = 120; // 默认加速时间为15tick
        public float initialFlyingStep = 0.4f;  // 初始阶段飞行速度（默认值）
        public int ticksBeforeTracing = 30;
        public int maxFlyingTime = 900;
        public float rotatingStep = 6f;
        public float flyingStep = 0.4f;
        public int ticksBetweenFindTarget = 30;

        public List<string> randomImpactFlecks = new List<string>();
        public float impactScale = 1f;
        public string secondaryImpactFleck = "NCL_Fleck_BurnerUsedEmber";
        public float secondaryImpactScale = 1f;
        public string tertiaryImpactFleck = "NCL_Fleck_BurnerUsedEmber";
        public float tertiaryImpactScale = 1f;
        public bool enableSmokeEffect = true;
        public float smokeSize = 1f;
        public bool enableFireGlowEffect = true;
        public float fireGlowSize = 1f;
        public SoundDef interceptSound;

        // 投射物追踪专用参数
        public float projectileTrackingMultiplier = 1f; // 追踪投射物时的旋转速度倍率
    }
}


namespace NyarsModPackTwo
{
    // Token: 0x0200000F RID: 15
    public class ModExtension_BulletsDefs : DefModExtension
    {
        // Token: 0x0400001B RID: 27
        public List<ThingDef> bullets = new List<ThingDef>();

        // Token: 0x0400001C RID: 28
        public IntRange castCount;
    }
}
namespace NyarsModPackTwo
{
    // Token: 0x02000010 RID: 16
    public class PlaceWorker_ShowTurretRadius : PlaceWorker
    {
        // Token: 0x06000037 RID: 55 RVA: 0x00002FE4 File Offset: 0x000011E4
        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            VerbProperties verbProperties = ((ThingDef)checkingDef).building.turretGunDef.Verbs[0];
            bool flag = verbProperties.range > 0f;
            bool flag2 = flag;
            if (flag2)
            {
                GenDraw.DrawRadiusRing(loc, verbProperties.range);
            }
            bool flag3 = verbProperties.minRange > 0f;
            bool flag4 = flag3;
            if (flag4)
            {
                GenDraw.DrawRadiusRing(loc, verbProperties.minRange);
            }
            return true;
        }
    }
}
namespace NyarsModPackTwo
{
    // Token: 0x02000006 RID: 6
    public class Verb_ShootRandomBullet : Verb_Shoot
    {
        // Token: 0x17000005 RID: 5
        // (get) Token: 0x06000017 RID: 23 RVA: 0x00002889 File Offset: 0x00000A89
        public List<ThingDef> BulletDefs
        {
            get
            {
                return base.EquipmentSource.def.GetModExtension<ModExtension_BulletsDefs>().bullets;
            }
        }

        // Token: 0x17000006 RID: 6
        // (get) Token: 0x06000018 RID: 24 RVA: 0x000028A0 File Offset: 0x00000AA0
        public int MaxCastCount
        {
            get
            {
                return base.EquipmentSource.def.GetModExtension<ModExtension_BulletsDefs>().castCount.RandomInRange;
            }
        }

        // Token: 0x06000019 RID: 25 RVA: 0x000028BC File Offset: 0x00000ABC
        protected override bool TryCastShot()
        {
            this.lastShotTick = Find.TickManager.TicksGame;
            int i = 0;
            while (i <= this.MaxCastCount)
            {
                bool flag = Verb_ShootRandomBullet.BulletCache.Count == 0;
                if (flag)
                {
                    Verb_ShootRandomBullet.BulletCache.AddRange(this.BulletDefs);
                }
                int index = Rand.Range(0, Verb_ShootRandomBullet.BulletCache.Count);
                ThingDef bulletDef = Verb_ShootRandomBullet.BulletCache[index];
                Verb_ShootRandomBullet.BulletCache.RemoveAt(index);
                i++;
                this.LaunchBullet(bulletDef);
            }
            return true;
        }

        // Token: 0x0600001A RID: 26 RVA: 0x00002954 File Offset: 0x00000B54
        private void LaunchBullet(ThingDef bulletDef)
        {
            Bullet_TracingEnemies bullet_TracingEnemies = (Bullet_TracingEnemies)GenSpawn.Spawn(bulletDef, this.caster.Position, this.caster.Map, WipeMode.Vanish);
            bullet_TracingEnemies.flyingAngle = Rand.Value * 360f;
            bullet_TracingEnemies.trackingPosNow = this.Caster.TrueCenter();
            bullet_TracingEnemies.Launch(this.caster, this.Caster.TrueCenter(), default(LocalTargetInfo), default(LocalTargetInfo), ProjectileHitFlags.IntendedTarget, this.preventFriendlyFire, base.EquipmentSource, null);
        }

        // Token: 0x04000013 RID: 19
        private static readonly List<ThingDef> BulletCache = new List<ThingDef>();
    }
}
