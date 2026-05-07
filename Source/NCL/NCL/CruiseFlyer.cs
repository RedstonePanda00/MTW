using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace NCL
{
    public class CruiseFlyer : Skyfaller
    {
        // 新增盘旋模式参数
        protected bool hoverMode = true; // 是否启用盘旋模式
        protected float hoverRadius = 100f; // 盘旋半径
        protected float hoverAngle = 0f; // 当前角度（弧度）
        protected float hoverSpeed = 0.005f; // 盘旋速度（每帧角度变化）
        protected float currentRotation = 0f; // 当前旋转角度（度）

        protected int bombDropInterval = 200; // 炸弹投放间隔（帧数）
        protected int bombDropCounter = 0;    // 炸弹投放计数器
        protected ThingDef bombDef = DefDatabase<ThingDef>.GetNamed("Bullet_FlyingCentipede"); // 炸弹类型
        private ThingDef CentipedeBomb => ThingDef.Named("Bullet_FlyingCentipede");
        private ThingDef LancerBomb => ThingDef.Named("Bullet_FlyingLancer");

        // 原有巡航参数
        protected float cruiseProgress;
        protected int cruiseDirection = -1;
        protected float cruiseSpeed = 0.001f;
        protected float cruiseAmplitude;
        protected float cruiseAltitude = 10f;
        protected float waveHeightX = 40f;
        protected float waveHeightZ = 40f;
        protected float waveFrequencyX = 2f;
        protected float waveFrequencyZ = 1.5f;
        protected float shadowSizeFactor = 4f;
        public FleckDef fleckToSpawn = DefDatabase<FleckDef>.GetNamed("Smoke");
        public float fleckSpawnInterval = 1f;
        protected int fleckSpawnCounter;
        public float fleckOffset = 0f;
        public bool autoReverse = true;
        public bool waveMotion = true;
        public bool drawShadow = true;

        // 新增机翼烟雾参数
        protected const float WingOffsetDistance = 4f; // 机翼距离飞机中心的距离
        protected const float WingHeightOffset = 0.5f; // 机翼烟雾的高度偏移

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            base.Position = map.Center;
            cruiseAmplitude = map.Size.x * 0.8f;

            // 自动设置盘旋半径为地图尺寸的40%
            if (hoverMode)
            {
                hoverRadius = Mathf.Min(map.Size.x, map.Size.z) * 0.4f;
            }
        }

        protected override void GetDrawPositionAndRotation(ref Vector3 drawLoc, out float extraRotation)
        {
            // 保持实际位置在地图中央
            drawLoc = base.Map.Center.ToVector3Shifted();

            if (hoverMode)
            {
                // 盘旋模式 - 圆周运动
                float offsetX = Mathf.Cos(hoverAngle) * hoverRadius;
                float offsetZ = Mathf.Sin(hoverAngle) * hoverRadius;

                drawLoc += new Vector3(offsetX, cruiseAltitude, offsetZ);

                // 使用在Tick中计算好的旋转角度
                extraRotation = currentRotation;
            }
            else
            {
                // 原有直线运动
                float horizontalOffset = Mathf.Lerp(cruiseAmplitude, -cruiseAmplitude, cruiseProgress);

                float waveOffsetX = 0f;
                float waveOffsetZ = 0f;

                if (waveMotion)
                {
                    waveOffsetX = Mathf.Sin(cruiseProgress * Mathf.PI * waveFrequencyX) * waveHeightX;
                    waveOffsetZ = Mathf.Cos(cruiseProgress * Mathf.PI * waveFrequencyZ) * waveHeightZ;
                }

                drawLoc += new Vector3(horizontalOffset + waveOffsetX, cruiseAltitude, waveOffsetZ);
                extraRotation = (cruiseDirection > 0) ? 0f : 180f;
            }
        }

        // 禁用实际移动逻辑
        protected override void HitRoof() { }
        protected override void Impact() { }
        protected override void LeaveMap() { }

        protected override void Tick()
        {
            base.Tick();

            if (hoverMode)
            {
                // 保存旧角度用于方向计算
                float oldAngle = hoverAngle;

                // 盘旋模式运动逻辑
                hoverAngle += hoverSpeed;

                // 角度归一化（0-2π）
                if (hoverAngle > Mathf.PI * 2)
                {
                    hoverAngle -= Mathf.PI * 2;
                }

                // 计算运动方向的变化量
                float angleDelta = hoverAngle - oldAngle;

                // 计算圆周运动的切线方向（运动方向）
                // 切线向量的X分量 = -sin(角度)
                // 切线向量的Z分量 = cos(角度)
                float tangentX = Mathf.Sin(hoverAngle);
                float tangentZ = Mathf.Cos(hoverAngle);

                // 计算运动方向的旋转角度（以度为单位）
                currentRotation = Mathf.Atan2(tangentZ, tangentX) * Mathf.Rad2Deg;

                // 添加平滑处理，避免角度突变
                float angleDifference = Mathf.DeltaAngle(currentRotation, currentRotation);
                if (Mathf.Abs(angleDifference) > 90f)
                {
                    currentRotation = Mathf.LerpAngle(currentRotation, currentRotation, 0.1f);
                }
            }
            else
            {
                // 原有直线运动
                cruiseProgress += cruiseSpeed * Mathf.Abs(cruiseDirection);

                if (cruiseProgress >= 1f || cruiseProgress <= 0f)
                {
                    if (autoReverse)
                    {
                        cruiseDirection *= -1;
                        cruiseProgress = Mathf.Clamp01(cruiseProgress);
                    }
                    else
                    {
                        cruiseProgress = (cruiseProgress >= 1f) ? 0f : 1f;
                    }
                }
            }

            if (bombDef != null && base.Map != null)
            {
                bombDropCounter++;
                if (bombDropCounter >= bombDropInterval)
                {
                    bombDropCounter = 0;
                    DropBomb();
                }
            }
            // 粒子效果
            if (fleckToSpawn != null && base.Map != null && fleckSpawnInterval > 0)
            {
                fleckSpawnCounter++;
                if (fleckSpawnCounter >= fleckSpawnInterval)
                {
                    fleckSpawnCounter = 0;
                    Vector3 drawPos = GetCurrentDrawPosition();

                    // 原始中心烟雾
                    FleckMaker.Static(drawPos, base.Map, fleckToSpawn);

                    // 新增：在左右机翼位置生成烟雾
                    GenerateWingSmoke(drawPos);
                }
            }
        }

        // 新增方法：投放炸弹
        private void DropBomb()
        {
            Vector3 bombPos = GetCurrentDrawPosition(); // 获取飞行器当前位置

            // 声明目标单元格变量
            IntVec3 targetCell = IntVec3.Invalid;

            // 检查正下方是否有屋顶
            IntVec3 cellBelow = bombPos.ToIntVec3();
            if (base.Map.roofGrid.Roofed(cellBelow))
            {
                // 如果有屋顶，尝试在附近寻找无屋顶位置
                targetCell = FindNearestOpenCell(cellBelow, base.Map);
                if (!targetCell.IsValid) return; // 没有合适位置，放弃投弹
            }
            else
            {
                // 当前位置无屋顶，直接使用当前位置
                targetCell = cellBelow;
            }

            // 随机选择炸弹类型
            ThingDef bombDef = Rand.Value < 0.5f ? CentipedeBomb : LancerBomb;

            // 创建炮弹实例
            Projectile bomb = (Projectile)ThingMaker.MakeThing(bombDef, null);
            bomb.def = bombDef;

            // 计算炸弹的初始位置（飞行器当前位置下方一点）
            Vector3 spawnPos = bombPos - new Vector3(0f, 1f, 0f);
            IntVec3 spawnCell = spawnPos.ToIntVec3();

            // 生成炸弹到地图
            GenSpawn.Spawn(bomb, spawnCell, base.Map);

            // 设置炸弹的垂直向下飞行轨迹
            bomb.Launch(
                launcher: this,
                origin: spawnPos,
                usedTarget: new LocalTargetInfo(targetCell),
                intendedTarget: new LocalTargetInfo(targetCell),
                hitFlags: ProjectileHitFlags.All
            );

            // 添加视觉效果
            FleckMaker.ThrowSmoke(spawnPos, base.Map, 1f);
            FleckMaker.ThrowLightningGlow(spawnPos, base.Map, 1f);
        }


        // 新增：寻找最近的无屋顶单元格
        private IntVec3 FindNearestOpenCell(IntVec3 center, Map map, int maxRadius = 15)
        {
            if (!map.roofGrid.Roofed(center)) return center;

            // 使用螺旋搜索模式寻找最近的开放单元格
            for (int radius = 1; radius <= maxRadius; radius++)
            {
                foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, radius, true))
                {
                    if (cell.InBounds(map) &&
                        !map.roofGrid.Roofed(cell) &&
                        cell.Standable(map))
                    {
                        return cell;
                    }
                }
            }

            return IntVec3.Invalid; // 未找到合适位置
        }

        // 生成机翼烟雾效果（优化版）
        protected virtual void GenerateWingSmoke(Vector3 centerPos)
        {
            if (fleckToSpawn == null) return;

            // 获取当前飞行方向（归一化向量）
            Vector3 forwardDirection = GetForwardDirection();

            // 计算垂直于飞行方向的左方向
            Vector3 leftDirection = new Vector3(-forwardDirection.z, 0f, forwardDirection.x).normalized;

            // 计算左右机翼位置
            Vector3 leftWingPos = centerPos + leftDirection * WingOffsetDistance;
            leftWingPos.y += WingHeightOffset;

            Vector3 rightWingPos = centerPos - leftDirection * WingOffsetDistance;
            rightWingPos.y += WingHeightOffset;

            // 生成机翼烟雾
            FleckMaker.Static(leftWingPos, base.Map, fleckToSpawn);
            FleckMaker.Static(rightWingPos, base.Map, fleckToSpawn);
        }

        // 获取当前飞行方向（归一化向量）
        protected virtual Vector3 GetForwardDirection()
        {
            if (hoverMode)
            {
                // 盘旋模式下的方向计算
                float forwardX = -Mathf.Sin(hoverAngle);
                float forwardZ = Mathf.Cos(hoverAngle);
                return new Vector3(forwardX, 0f, forwardZ).normalized;
            }
            else
            {
                // 直线飞行模式下的方向计算
                return new Vector3(cruiseDirection, 0f, 0f).normalized;
            }
        }

        // 获取当前视觉位置
        public Vector3 GetCurrentDrawPosition()
        {
            Vector3 drawLoc = base.Map.Center.ToVector3Shifted();
            float extraRotation;
            GetDrawPositionAndRotation(ref drawLoc, out extraRotation);
            return drawLoc;
        }

        // 自定义绘制方法
        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            // 计算实际绘制位置
            float extraRotation;
            GetDrawPositionAndRotation(ref drawLoc, out extraRotation);

            // 获取用于绘制的物体
            Thing thingForGraphic = GetThingForGraphic();

            // 绘制物体 - 应用旋转
            this.Graphic.Draw(drawLoc, thingForGraphic.Rotation, thingForGraphic, extraRotation);

            // 绘制阴影
            if (drawShadow && ShadowMaterial != null)
            {
                DrawCruiseShadow(drawLoc);
            }
        }

        // 获取绘制物体的方法
        protected Thing GetThingForGraphic()
        {
            if (this.def.graphicData != null || !this.innerContainer.Any)
            {
                return this;
            }
            return this.innerContainer[0];
        }

        // 自定义阴影绘制
        protected void DrawCruiseShadow(Vector3 center)
        {
            Material shadowMaterial = ShadowMaterial;
            if (shadowMaterial == null) return;

            Vector3 pos = center;
            pos.y = AltitudeLayer.Shadows.AltitudeFor();

            // 计算阴影大小
            float shadowSize = shadowSizeFactor * (1f - (cruiseAltitude / 100f));
            Vector2 size = new Vector2(shadowSize, shadowSize);

            // 绘制阴影
            Skyfaller.DrawDropSpotShadow(pos, base.Rotation, shadowMaterial, size, 0);
        }

        public void SetHoverMode(bool enabled, float radius = 100f, float speed = 0.01f)
        {
            hoverMode = enabled;
            hoverRadius = radius;
            hoverSpeed = speed;
        }

        public static CruiseFlyer CreateHoverFlyer(
            ThingDef flyerDef,
            Map map,
            float radius = 100f,
            float speed = 0.01f,
            float altitude = 15f)
        {
            CruiseFlyer flyer = (CruiseFlyer)ThingMaker.MakeThing(flyerDef, null);
            flyer.SetHoverMode(true, radius, speed);
            flyer.cruiseAltitude = altitude;
            GenSpawn.Spawn(flyer, map.Center, map);
            return flyer;
        }

        public override void ExposeData()
        {
            base.ExposeData();

            // 原有参数...
            Scribe_Values.Look(ref cruiseProgress, "cruiseProgress", 0f);
            Scribe_Values.Look(ref cruiseDirection, "cruiseDirection", -1);
            Scribe_Values.Look(ref cruiseSpeed, "cruiseSpeed", 0.004f);
            Scribe_Values.Look(ref cruiseAmplitude, "cruiseAmplitude", 0f);
            Scribe_Values.Look(ref cruiseAltitude, "cruiseAltitude", 10f);
            Scribe_Values.Look(ref waveHeightX, "waveHeightX", 40f);
            Scribe_Values.Look(ref waveHeightZ, "waveHeightZ", 40f);
            Scribe_Values.Look(ref waveFrequencyX, "waveFrequencyX", 2f);
            Scribe_Values.Look(ref waveFrequencyZ, "waveFrequencyZ", 1.5f);
            Scribe_Values.Look(ref shadowSizeFactor, "shadowSizeFactor", 1f);
            Scribe_Values.Look(ref autoReverse, "autoReverse", true);
            Scribe_Values.Look(ref waveMotion, "waveMotion", false);
            Scribe_Values.Look(ref drawShadow, "drawShadow", true);
            Scribe_Defs.Look(ref fleckToSpawn, "fleckToSpawn");
            Scribe_Values.Look(ref fleckSpawnInterval, "fleckSpawnInterval", 10f);
            Scribe_Values.Look(ref fleckOffset, "fleckOffset", 0f);
            Scribe_Values.Look(ref bombDropInterval, "bombDropInterval", 200);
            Scribe_Values.Look(ref bombDropCounter, "bombDropCounter", 0);
            Scribe_Defs.Look(ref bombDef, "bombDef");
            // 新增盘旋模式参数
            Scribe_Values.Look(ref hoverMode, "hoverMode", false);
            Scribe_Values.Look(ref hoverRadius, "hoverRadius", 100f);
            Scribe_Values.Look(ref hoverAngle, "hoverAngle", 0f);
            Scribe_Values.Look(ref hoverSpeed, "hoverSpeed", 0.01f);
            Scribe_Values.Look(ref currentRotation, "currentRotation", 0f);
        }

        // 创建巡航飞行器的便捷方法（从右向左）
        public static CruiseFlyer CreateRightToLeftFlyer(
            ThingDef flyerDef,
            Thing content,
            Map map,
            float speed = 0.004f,
            float amplitudeFactor = 0.8f,
            float altitude = 10f,
            float shadowSize = 1f)
        {
            CruiseFlyer flyer = (CruiseFlyer)ThingMaker.MakeThing(flyerDef, null);

            // 设置内容
            if (content != null)
            {
                flyer.innerContainer.TryAdd(content);
            }

            // 设置参数
            flyer.cruiseSpeed = speed;
            flyer.cruiseDirection = -1; // 从右向左
            flyer.cruiseAmplitude = map.Size.x * amplitudeFactor;
            flyer.cruiseAltitude = altitude;
            flyer.shadowSizeFactor = shadowSize;

            // 生成到地图中央
            GenSpawn.Spawn(flyer, map.Center, map);
            return flyer;
        }

        // 设置Y轴高度
        public void SetAltitude(float altitude)
        {
            cruiseAltitude = altitude;
        }

        public void SetBombDropInterval(int intervalTicks)
        {
            bombDropInterval = Mathf.Max(10, intervalTicks); // 最小间隔为10帧
        }

        // 设置炸弹类型的方法
        public void SetBombType(ThingDef newBombDef)
        {
            bombDef = newBombDef;
        }

        // 设置阴影大小
        public void SetShadowSize(float sizeFactor)
        {
            shadowSizeFactor = sizeFactor;
        }
    }
}


namespace NCL
{
    // 防空炮弹 - 专为击落巡航飞机设计
    public class AntiAircraftBullet : Bullet
    {
        private const float COLLISION_RADIUS = 5.0f; // 飞机碰撞检测半径（单位：格）
        private const float UPDATE_INTERVAL = 3;     // 飞机检测间隔（游戏刻）
        private const float CRUSH_DAMAGE_RADIUS = 1.5f; // 碾压伤害范围半径（格）
        private const int CRUSH_DAMAGE_AMOUNT = 10; // 每帧碾压伤害量

        private int lastCheckTick;
        private bool isDestroyed = false; // 标记子弹是否已被销毁
        private HashSet<Thing> crushedThisFrame = new HashSet<Thing>(); // 本帧已造成伤害的单位

        protected override void Tick()
        {
            // 如果子弹已被销毁，不再执行任何操作
            if (isDestroyed || this.Destroyed) return;

            base.Tick();

            // 检查子弹是否飞出地图边界
            if (!this.ExactPosition.InBounds(base.Map))
            {
                DestroyBullet();
                return;
            }

            crushedThisFrame.Clear(); // 清除上一帧记录

            // 每帧检测周围单位的碾压伤害
            CheckForCrushDamage();

            // 每3个游戏刻检测一次飞机碰撞
            if (Find.TickManager.TicksGame > lastCheckTick + UPDATE_INTERVAL)
            {
                CheckForAircraftCollision();
                lastCheckTick = Find.TickManager.TicksGame;
            }
        }

        // 安全销毁子弹的方法
        private void DestroyBullet()
        {
            if (isDestroyed) return;
            isDestroyed = true;
            this.Destroy(DestroyMode.Vanish);
        }

        // 每帧检测周围单位的碾压伤害
        private void CheckForCrushDamage()
        {
            // 将Vector3转换为IntVec3
            IntVec3 bulletCell = this.ExactPosition.ToIntVec3();

            // 获取子弹周围指定半径内的所有单位
            List<Thing> potentialTargets = GenRadial.RadialDistinctThingsAround(
                bulletCell,
                base.Map,
                CRUSH_DAMAGE_RADIUS,
                true
            ).ToList();

            foreach (Thing thing in potentialTargets)
            {
                // 跳过非单位、友军、已摧毁单位、本帧已处理单位
                if (thing == null ||
                    thing == this ||
                    crushedThisFrame.Contains(thing) ||
                    thing.Destroyed ||
                    !(thing is Pawn) ||
                    thing.Faction == this.launcher?.Faction)
                {
                    continue;
                }

                // 应用碾压伤害
                ApplyCrushDamage(thing);
                crushedThisFrame.Add(thing);
            }
        }

        // 应用碾压伤害
        private void ApplyCrushDamage(Thing target)
        {
            DamageInfo crushDamage = new DamageInfo(
                DamageDefOf.Crush, // 碾压伤害类型
                CRUSH_DAMAGE_AMOUNT,
                instigator: this.launcher,
                weapon: this.def   // 使用子弹定义作为武器定义
            );

            target.TakeDamage(crushDamage);

            // 添加视觉反馈
            FleckMaker.ThrowDustPuffThick(target.DrawPos, target.Map, 0.5f, Color.gray);
        }

        private void CheckForAircraftCollision()
        {
            // 1. 获取当前子弹位置
            Vector3 bulletPos = this.ExactPosition;

            // 2. 获取地图中所有巡航飞机
            var allFlyers = base.Map.listerThings.ThingsOfDef(ThingDef.Named("B2000Mech"));

            foreach (Thing thing in allFlyers)
            {
                CruiseFlyer flyer = thing as CruiseFlyer;
                if (flyer == null || flyer.Destroyed) continue;

                // 3. 获取飞机当前位置
                Vector3 flyerPos = flyer.GetCurrentDrawPosition();

                // 4. 计算距离并检查碰撞
                float distance = Vector3.Distance(bulletPos, flyerPos);
                if (distance < COLLISION_RADIUS)
                {
                    // 5. 触发命中效果
                    OnAircraftHit(flyer);
                    return; // 只命中一架飞机
                }
            }
        }

        private void OnAircraftHit(CruiseFlyer flyer)
        {
            // 1. 播放爆炸效果
            FleckMaker.ThrowSmoke(flyer.Position.ToVector3(), flyer.Map, 5.0f);

            // 2. 销毁飞机
            flyer.Destroy(DestroyMode.Vanish);

            // 3. 销毁子弹
            DestroyBullet();

            // 4. 创建战斗日志
            CreateCombatLog(flyer);
        }

        private void CreateCombatLog(CruiseFlyer aircraft)
        {
            BattleLogEntry_RangedImpact entry = new BattleLogEntry_RangedImpact(
                this.launcher,       // 发射者
                aircraft,            // 目标（被击落的飞机）
                this.intendedTarget.Thing,  // 原定目标
                this.equipmentDef,   // 使用的武器
                this.def,            // 子弹类型
                null                 // 目标掩体
            );

            Find.BattleLog.Add(entry);
        }

        // 重写Impact方法，确保子弹正常飞行
        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            // 1. 处理到达目标点的情况
            if (hitThing == null)
            {
                // 到达目标点，销毁子弹
                DestroyBullet();
                return;
            }

            // 2. 防空炮弹只对飞机生效，忽略其他碰撞
            if (hitThing is CruiseFlyer)
            {
                // 调用基类Impact方法处理飞机碰撞
                base.Impact(hitThing, blockedByShield);
            }
            // 3. 其他情况：子弹穿透不消失（不调用基类方法）
        }
    }
}


namespace NCL
{
    public class StraightLineCruiseFlyerRighttoLeft : CruiseFlyer
    {
        protected float startZ;                // 起始Z轴位置（随机值）
        protected float horizontalProgress;    // 水平飞行进度 (0: 最东端, 1: 最西端)
        protected const float WestRotation = 0f; // 西方朝向角度 (180度向左)

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);

            // 禁用盘旋模式
            hoverMode = false;

            // 禁用波浪运动和自动转向
            waveMotion = false;
            autoReverse = false;

            // 确保起始位置在地图范围内
            float safeMinZ = Mathf.Max(10f, map.Size.z * 0.1f);
            float safeMaxZ = Mathf.Min(map.Size.z - 10f, map.Size.z * 0.9f);
            startZ = Rand.Range(safeMinZ, safeMaxZ);

            // 初始位置：最东端
            horizontalProgress = 0f;
        }

        protected override void GetDrawPositionAndRotation(ref Vector3 drawLoc, out float extraRotation)
        {
            // 获取地图中心坐标
            float mapCenterX = base.Map.Center.ToVector3Shifted().x;
            float mapWidth = base.Map.Size.x;

            // 计算当前X坐标（从东到西）
            // 东边界：mapCenterX + mapWidth/2
            // 西边界：mapCenterX - mapWidth/2
            float currentX = Mathf.Lerp(mapCenterX + mapWidth / 2, mapCenterX - mapWidth / 2, horizontalProgress);

            // 确保Z位置在地图范围内
            float safeZ = Mathf.Clamp(startZ, 0f, base.Map.Size.z);

            // 创建世界空间位置
            drawLoc = new Vector3(
                currentX,
                cruiseAltitude,
                safeZ
            );

            // 朝西方向 (180度向左)
            extraRotation = WestRotation;
        }

        protected override void Tick()
        {
            // 基础Tick（处理炸弹投放和粒子效果）
            base.Tick();

            // 水平飞行进度增加（向西飞行）
            horizontalProgress += cruiseSpeed;

            // 到达地图最西端时销毁
            if (horizontalProgress >= 1f)
            {
                this.Destroy();
            }
        }

        // 获取前进方向（始终向西）
        protected override Vector3 GetForwardDirection()
        {
            return new Vector3(-1f, 0f, 0f).normalized; // 向西移动
        }

        // 机翼烟雾生成方法
        protected override void GenerateWingSmoke(Vector3 centerPos)
        {
            if (fleckToSpawn == null) return;

            Vector3 forward = GetForwardDirection();
            Vector3 right = new Vector3(forward.z, 0f, -forward.x).normalized;

            Vector3 leftWingPos = centerPos - right * WingOffsetDistance;
            leftWingPos.y += WingHeightOffset;

            Vector3 rightWingPos = centerPos + right * WingOffsetDistance;
            rightWingPos.y += WingHeightOffset;

            FleckMaker.Static(leftWingPos, base.Map, fleckToSpawn);
            FleckMaker.Static(rightWingPos, base.Map, fleckToSpawn);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref startZ, "startZ", 0f);
            Scribe_Values.Look(ref horizontalProgress, "horizontalProgress", 0f);
        }

        // 便捷创建方法
        public static StraightLineCruiseFlyerRighttoLeft Create(
            ThingDef flyerDef,
            Map map,
            float speed = 0.005f,
            float altitude = 10f)
        {
            var flyer = (StraightLineCruiseFlyerRighttoLeft)ThingMaker.MakeThing(flyerDef);
            flyer.cruiseSpeed = speed;
            flyer.cruiseAltitude = altitude;

            // 确保生成位置在地图中心（安全位置）
            IntVec3 spawnPos = map.Center;
            if (!spawnPos.Walkable(map))
            {
                spawnPos = CellFinder.RandomClosewalkCellNear(map.Center, map, 10);
            }

            GenSpawn.Spawn(flyer, spawnPos, map);
            return flyer;
        }

        // 确保正确应用旋转
        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            // 获取位置和旋转
            float extraRotation;
            GetDrawPositionAndRotation(ref drawLoc, out extraRotation);

            // 获取用于绘制的物体
            Thing thingForGraphic = GetThingForGraphic();

            // 绘制物体 - 应用旋转
            this.Graphic.Draw(drawLoc, thingForGraphic.Rotation, thingForGraphic, extraRotation);

            // 绘制阴影
            if (drawShadow && ShadowMaterial != null)
            {
                DrawCruiseShadow(drawLoc);
            }
        }

        // 修正炸弹投放位置（确保在地图内）
        private new void DropBomb()
        {
            Vector3 bombPos = GetCurrentDrawPosition();
            Projectile bomb = (Projectile)ThingMaker.MakeThing(bombDef, null);
            bomb.def = bombDef;

            // 获取炸弹的初始位置（飞行器当前位置下方一点）
            Vector3 spawnPos = bombPos - new Vector3(0f, 1f, 0f);
            IntVec3 spawnCell = spawnPos.ToIntVec3();

            // 确保生成点在地图边界内
            if (!spawnCell.InBounds(base.Map))
            {
                spawnCell = spawnCell.ClampInsideMap(base.Map);
            }

            // 生成炸弹到地图
            GenSpawn.Spawn(bomb, spawnCell, base.Map);

            // 获取炸弹落点（正下方的地面）
            IntVec3 targetCell = spawnCell + new IntVec3(0, 0, -1);

            // 确保目标位置在地图内
            if (!targetCell.InBounds(base.Map))
            {
                targetCell = targetCell.ClampInsideMap(base.Map);
            }

            // 设置炸弹的垂直向下飞行轨迹
            bomb.Launch(
                launcher: this,
                origin: spawnPos,
                usedTarget: new LocalTargetInfo(targetCell),
                intendedTarget: new LocalTargetInfo(targetCell),
                hitFlags: ProjectileHitFlags.All
            );

            // 添加视觉效果
            if (spawnCell.InBounds(base.Map))
            {
                FleckMaker.ThrowSmoke(spawnPos, base.Map, 1f);
                FleckMaker.ThrowLightningGlow(spawnPos, base.Map, 1f);
            }
        }
    }
}



namespace NCL
{
    public class StraightLineCruiseFlyerUptoDown : CruiseFlyer
    {
        protected float startX;                // 起始X轴位置（随机值）
        protected float verticalProgress;      // 垂直飞行进度 (0: 最北端, 1: 最南端)
        protected const float SouthRotation = 270f; // 南方朝向角度 (270度向下)

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);

            // 禁用盘旋模式
            hoverMode = false;

            // 禁用波浪运动和自动转向
            waveMotion = false;
            autoReverse = false;

            // 确保起始位置在地图范围内
            float safeMinX = Mathf.Max(10f, map.Size.x * 0.1f);
            float safeMaxX = Mathf.Min(map.Size.x - 10f, map.Size.x * 0.9f);
            startX = Rand.Range(safeMinX, safeMaxX);

            // 初始位置：最北端
            verticalProgress = 0f;
        }

        protected override void GetDrawPositionAndRotation(ref Vector3 drawLoc, out float extraRotation)
        {
            // 获取地图中心坐标
            float mapCenterZ = base.Map.Center.ToVector3Shifted().z;
            float mapHeight = base.Map.Size.z;

            // 计算当前Z坐标（从北到南）
            // 北边界：mapCenterZ + mapHeight/2
            // 南边界：mapCenterZ - mapHeight/2
            float currentZ = Mathf.Lerp(mapCenterZ + mapHeight / 2, mapCenterZ - mapHeight / 2, verticalProgress);

            // 确保X位置在地图范围内
            float safeX = Mathf.Clamp(startX, 0f, base.Map.Size.x);

            // 创建世界空间位置 - Z轴不做限制
            drawLoc = new Vector3(
                safeX,
                cruiseAltitude,
                currentZ
            );

            // 朝南方向 (270度向下)
            extraRotation = SouthRotation;
        }

        protected override void Tick()
        {
            // 基础Tick（处理炸弹投放和粒子效果）
            base.Tick();

            // 垂直飞行进度增加（向南飞行）
            verticalProgress += cruiseSpeed;

            // 到达地图最南端时销毁
            if (verticalProgress >= 1f)
            {
                this.Destroy();
            }
        }

        // 获取前进方向（始终向南）
        protected override Vector3 GetForwardDirection()
        {
            return new Vector3(0f, 0f, -1f).normalized;
        }

        // 机翼烟雾生成方法
        protected override void GenerateWingSmoke(Vector3 centerPos)
        {
            if (fleckToSpawn == null) return;

            Vector3 forward = GetForwardDirection();
            Vector3 right = new Vector3(forward.z, 0f, -forward.x).normalized;

            Vector3 leftWingPos = centerPos - right * WingOffsetDistance;
            leftWingPos.y += WingHeightOffset;

            Vector3 rightWingPos = centerPos + right * WingOffsetDistance;
            rightWingPos.y += WingHeightOffset;

            FleckMaker.Static(leftWingPos, base.Map, fleckToSpawn);
            FleckMaker.Static(rightWingPos, base.Map, fleckToSpawn);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref startX, "startX", 0f);
            Scribe_Values.Look(ref verticalProgress, "verticalProgress", 0f);
        }

        // 便捷创建方法
        public static StraightLineCruiseFlyerUptoDown Create(
            ThingDef flyerDef,
            Map map,
            float speed = 0.005f,
            float altitude = 10f)
        {
            var flyer = (StraightLineCruiseFlyerUptoDown)ThingMaker.MakeThing(flyerDef);
            flyer.cruiseSpeed = speed;
            flyer.cruiseAltitude = altitude;

            // 确保生成位置在地图中心（安全位置）
            IntVec3 spawnPos = map.Center;
            if (!spawnPos.Walkable(map))
            {
                spawnPos = CellFinder.RandomClosewalkCellNear(map.Center, map, 10);
            }

            GenSpawn.Spawn(flyer, spawnPos, map);
            return flyer;
        }

        // 确保正确应用旋转
        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            // 获取位置和旋转
            float extraRotation;
            GetDrawPositionAndRotation(ref drawLoc, out extraRotation);

            // 获取用于绘制的物体
            Thing thingForGraphic = GetThingForGraphic();

            // 绘制物体 - 应用旋转
            this.Graphic.Draw(drawLoc, thingForGraphic.Rotation, thingForGraphic, extraRotation);

            // 绘制阴影
            if (drawShadow && ShadowMaterial != null)
            {
                DrawCruiseShadow(drawLoc);
            }
        }

        // 修正炸弹投放位置（确保在地图内）
        private new void DropBomb()
        {
            Vector3 bombPos = GetCurrentDrawPosition();
            Projectile bomb = (Projectile)ThingMaker.MakeThing(bombDef, null);
            bomb.def = bombDef;

            // 获取炸弹的初始位置（飞行器当前位置下方一点）
            Vector3 spawnPos = bombPos - new Vector3(0f, 1f, 0f);
            IntVec3 spawnCell = spawnPos.ToIntVec3();

            // 确保生成点在地图边界内
            if (!spawnCell.InBounds(base.Map))
            {
                spawnCell = spawnCell.ClampInsideMap(base.Map);
            }

            // 生成炸弹到地图
            GenSpawn.Spawn(bomb, spawnCell, base.Map);

            // 获取炸弹落点（正下方的地面）
            IntVec3 targetCell = spawnCell + new IntVec3(0, 0, -1);

            // 确保目标位置在地图内
            if (!targetCell.InBounds(base.Map))
            {
                targetCell = targetCell.ClampInsideMap(base.Map);
            }

            // 设置炸弹的垂直向下飞行轨迹
            bomb.Launch(
                launcher: this,
                origin: spawnPos,
                usedTarget: new LocalTargetInfo(targetCell),
                intendedTarget: new LocalTargetInfo(targetCell),
                hitFlags: ProjectileHitFlags.All
            );

            // 添加视觉效果
            if (spawnCell.InBounds(base.Map))
            {
                FleckMaker.ThrowSmoke(spawnPos, base.Map, 1f);
                FleckMaker.ThrowLightningGlow(spawnPos, base.Map, 1f);
            }
        }
    }
}
