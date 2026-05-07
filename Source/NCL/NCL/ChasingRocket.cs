using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using static TY_Mora_Gene_B.com.Projectile_BezierTrackingMissile;

namespace TY_Mora_Gene_B.com
{
    /// <summary>
    /// 追踪导弹 - 使用曲线实现平滑的飞行轨迹
    /// </summary>
    [StaticConstructorOnStartup]
    public class Projectile_BezierTrackingMissile : Projectile_Explosive
    {
        #region 静态字段

        // 视觉效果材质
        private static readonly Material tailMaterial = MaterialPool.MatFrom("Things/Mote/Smoke", ShaderDatabase.MoteGlow, new Color(1f, 0.7f, 0.2f));
        private static readonly Material shadowMaterial = MaterialPool.MatFrom("Things/Mote/Smoke", ShaderDatabase.Transparent);

        #endregion

        #region 导弹参数

        // 导弹性能参数 - 从组件中获取
        private float maxTrackingRadius = 30f;      // 最大跟踪半径
        private float maxTurnAngle = 45f;           // 最大转向角度（度）
        private float turnRatePerTick = 1.2f;       // 每Tick最大转向角度
        private bool canSwitchTargets = true;       // 是否可以切换目标
        private float targetSwitchChance = 0.1f;    // 寻找新目标的几率
        private float minTargetSwitchDistance = 5f; // 目标切换的最小距离
        private float agilePhaseStart = 0.3f;       // 开始敏捷相位的飞行完成百分比
        private float agilePhaseEnd = 0.8f;         // 结束敏捷相位的飞行完成百分比
        private float heightMultiplier = 1.0f;      // 高度倍数

        // 新增：轨迹参数
        private MissileTrajectoryType trajectoryType = MissileTrajectoryType.BezierCurve; // 轨迹类型
        private float searchRadius = 30f;           // 目标搜索半径
        private float trajectoryAmplitude = 1.0f;   // 轨迹振幅
        private float trajectoryFrequency = 1.0f;   // 轨迹频率
        private float spiralRadius = 3.0f;          // 螺旋半径
        private float spiralTightness = 0.5f;       // 螺旋紧密度

        // 视觉效果参数 - 从组件中获取
        private float tailWidth = 1.2f;             // 尾焰宽度
        private float tailLength = 3f;              // 尾焰长度
        private float smokeChance = 0.7f;           // 产生烟雾的几率
        private int flecksPerBurst = 3;             // 每次产生的粒子数量
        private int fleckInterval = 2;              // 产生粒子的间隔Tick

        #endregion


        #region 实例字段

        // 贝塞尔曲线控制点
        private Vector3 p0; // 起点
        private Vector3 p1; // 控制点1
        private Vector3 p2; // 控制点2
        private Vector3 p3; // 终点

        // 随机偏移量，使每个导弹轨迹不同
        private float randOffset1;
        private float randOffset2;
        private float heightOffset;

        // 追踪相关
        private bool curveInitialized = false;
        private Vector3 lastTargetPos = Vector3.zero;
        private bool targetAcquired = false;
        private int losTargetCountdown = 0; // 丢失目标倒计时
        private float currentTurnAngle = 0f; // 当前转向角度

        // 视觉效果
        private Vector2 tailDrawSize;
        private int fleckEmitTick = 0;
        private IntRange fleckCountRange;
        private FloatRange fleckAngleRange = new FloatRange(-180f, 180f);
        private FloatRange fleckSpeedRange = new FloatRange(0.05f, 0.15f);
        private FloatRange fleckRotationRange = new FloatRange(-30f, 30f);

        // 飞行状态
        private Vector3 previousPosition;
        private bool reachedApex = false;
        private float lastTurnDirection = 0f; // 上次转向方向
        private float currentVelocity = 0f;   // 当前速度

        #endregion

        /// <summary>
        /// 导弹轨迹类型枚举 - 定义支持的各种曲线类型
        /// </summary>
        public enum MissileTrajectoryType
        {
            BezierCurve,       // 三次贝塞尔曲线
            Parabolic,         // 抛物线轨迹
            Sinusoidal,        // 正弦波轨迹
            Spiral,            // 螺旋轨迹
            Linear,            // 线性轨迹
            SmoothStep,        // 平滑步进轨迹
            Lemniscate,        // 双纽线轨迹
            Random             // 随机选择一种轨迹
        }


        /// <summary>
        /// 初始化导弹，读取XML定义的属性
        /// </summary>
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);

            // 从组件加载配置参数
            LoadConfigFromComp();

            // 初始化随机值
            randOffset1 = Rand.Range(-0.15f, 0.15f);
            randOffset2 = Rand.Range(-0.1f, 0.1f);
            heightOffset = Rand.Range(25f, 40f) * heightMultiplier;

            // 初始化视觉效果参数
            tailDrawSize = new Vector2(tailWidth, tailLength);
            fleckCountRange = new IntRange(Mathf.Max(1, flecksPerBurst - 1), flecksPerBurst + 1);
            fleckAngleRange = new FloatRange(-180f, 180f);
            fleckSpeedRange = new FloatRange(0.05f, 0.15f);
            fleckRotationRange = new FloatRange(-30f, 30f);

            // 初始化位置记录
            previousPosition = ExactPosition;

            // 初始化速度
            currentVelocity = def.projectile.speed;
        }

        /// <summary>
        /// 从组件中加载配置参数
        /// </summary>
        private void LoadConfigFromComp()
        {
            CompBezierMissile comp = this.TryGetComp<CompBezierMissile>();
            if (comp != null)
            {
                // 导弹性能参数
                maxTrackingRadius = comp.Props.maxTrackingRadius;
                maxTurnAngle = comp.Props.maxTurnAngle;
                turnRatePerTick = comp.Props.turnRatePerTick;
                canSwitchTargets = comp.Props.canSwitchTargets;
                targetSwitchChance = comp.Props.targetSwitchChance;
                minTargetSwitchDistance = comp.Props.minTargetSwitchDistance;
                agilePhaseStart = comp.Props.agilePhaseStart;
                agilePhaseEnd = comp.Props.agilePhaseEnd;
                heightMultiplier = comp.Props.heightMultiplier;

                // 新增：轨迹参数
                trajectoryType = comp.Props.trajectoryType;
                searchRadius = comp.Props.searchRadius;
                trajectoryAmplitude = comp.Props.trajectoryAmplitude;
                trajectoryFrequency = comp.Props.trajectoryFrequency;
                spiralRadius = comp.Props.spiralRadius;
                spiralTightness = comp.Props.spiralTightness;

                // 视觉效果参数
                tailWidth = comp.Props.tailWidth;
                tailLength = comp.Props.tailLength;
                smokeChance = comp.Props.smokeChance;
                flecksPerBurst = comp.Props.flecksPerBurst;
                fleckInterval = comp.Props.fleckInterval;
            }
            // 如果找不到组件，就使用默认值（已经在字段中设置）

            // 如果轨迹类型是随机，则随机选择一种轨迹
            if (trajectoryType == MissileTrajectoryType.Random)
            {
                trajectoryType = (MissileTrajectoryType)Rand.Range(0, Enum.GetValues(typeof(MissileTrajectoryType)).Length - 1);
            }
        }
        /// <summary>
        /// 根据当前轨迹类型计算导弹位置
        /// </summary>
        /// <summary>
        /// 根据当前轨迹类型计算导弹位置
        /// </summary>
        private Vector3 CalculateTrajectoryPoint(float t)
        {
            // 确保t值在有效范围内
            t = Mathf.Clamp01(t);

            // 初始化曲线控制点
            if (!curveInitialized)
            {
                InitializeBezierCurve();
            }

            Vector3 result;

            try
            {
                // 根据轨迹类型选择不同的计算方法
                switch (trajectoryType)
                {
                    case MissileTrajectoryType.BezierCurve:
                        result = CalculateBezierPoint(t);
                        break;
                    case MissileTrajectoryType.Parabolic:
                        result = CalculateParabolicPoint(t);
                        break;
                    case MissileTrajectoryType.Sinusoidal:
                        result = CalculateSinusoidalPoint(t);
                        break;
                    case MissileTrajectoryType.Spiral:
                        result = CalculateSpiralPoint(t);
                        break;
                    case MissileTrajectoryType.Linear:
                        result = CalculateLinearPoint(t);
                        break;
                    case MissileTrajectoryType.SmoothStep:
                        result = CalculateSmoothStepPoint(t);
                        break;
                    case MissileTrajectoryType.Lemniscate:
                        result = CalculateLemniscatePoint(t);
                        break;
                    default:
                        result = CalculateBezierPoint(t);
                        break;
                }
            }
            catch (Exception ex)
            {
                if (Prefs.DevMode)
                {
                    Log.Error($"轨迹计算出错: {ex.Message}, 使用简单线性轨迹");
                }
                // 出错时使用最简单的线性轨迹作为后备
                result = Vector3.Lerp(origin, destination, t);
            }

            // 验证结果是否有效
            if (float.IsNaN(result.x) || float.IsNaN(result.y) || float.IsNaN(result.z) ||
                float.IsInfinity(result.x) || float.IsInfinity(result.y) || float.IsInfinity(result.z))
            {
                // 返回简单的线性轨迹点
                result = Vector3.Lerp(origin, destination, t);
            }

            return result;
        }

        /// <summary>
        /// 贝塞尔曲线计算 - 根据t参数(0-1)计算曲线上的点
        /// </summary>
        private Vector3 CalculateBezierPoint(float t)
        {
            // 计算贝塞尔曲线上的点
            // P(t) = (1-t)³P₀ + 3(1-t)²tP₁ + 3(1-t)t²P₂ + t³P₃
            float oneMinusT = 1f - t;
            float oneMinusTSq = oneMinusT * oneMinusT;
            float oneMinusTCube = oneMinusTSq * oneMinusT;
            float tSq = t * t;
            float tCube = tSq * t;

            return oneMinusTCube * p0 +
                   3f * oneMinusTSq * t * p1 +
                   3f * oneMinusT * tSq * p2 +
                   tCube * p3;
        }

        /// <summary>
        /// 抛物线轨迹计算
        /// </summary>
        private Vector3 CalculateParabolicPoint(float t)
        {
            Vector3 start = origin;
            Vector3 end = destination;
            Vector3 direct = end - start;

            // 计算抛物线顶点高度
            float height = Vector3.Distance(start, end) * heightMultiplier * 0.5f;

            // 抛物线方程: y = 4 * h * t * (1 - t)
            float parabolicHeight = 4f * height * t * (1f - t);

            // 线性插值 + 抛物线高度
            Vector3 position = Vector3.Lerp(start, end, t);
            position.y += parabolicHeight;

            return position;
        }

        /// <summary>
        /// 正弦波轨迹计算
        /// </summary>
        private Vector3 CalculateSinusoidalPoint(float t)
        {
            Vector3 start = origin;
            Vector3 end = destination;
            Vector3 direct = (end - start).normalized;
            float distance = Vector3.Distance(start, end);

            // 基本线性路径
            Vector3 position = Vector3.Lerp(start, end, t);

            // 计算正交方向（垂直于飞行方向）
            Vector3 orthogonal = Vector3.Cross(direct, Vector3.up).normalized;

            // 添加正弦波摆动
            float sinValue = Mathf.Sin(t * trajectoryFrequency * Mathf.PI * 2f) * trajectoryAmplitude;
            position += orthogonal * sinValue;

            // 添加高度变化（抛物线）
            float parabolicHeight = 4f * (distance * heightMultiplier * 0.25f) * t * (1f - t);
            position.y += parabolicHeight;

            return position;
        }

        /// <summary>
        /// 螺旋轨迹计算
        /// </summary>
        private Vector3 CalculateSpiralPoint(float t)
        {
            Vector3 start = origin;
            Vector3 end = destination;
            Vector3 direct = (end - start).normalized;
            float distance = Vector3.Distance(start, end);

            // 基本线性路径
            Vector3 position = Vector3.Lerp(start, end, t);

            // 计算正交方向（垂直于飞行方向）
            Vector3 orthogonal1 = Vector3.Cross(direct, Vector3.up).normalized;
            Vector3 orthogonal2 = Vector3.Cross(direct, orthogonal1).normalized;

            // 螺旋参数
            float angle = t * spiralTightness * Mathf.PI * 2f * 3f; // 3圈螺旋
            float radius = spiralRadius * (1f - t); // 半径逐渐减小

            // 添加螺旋运动
            position += orthogonal1 * Mathf.Cos(angle) * radius;
            position += orthogonal2 * Mathf.Sin(angle) * radius;

            // 添加高度变化
            float parabolicHeight = 4f * (distance * heightMultiplier * 0.25f) * t * (1f - t);
            position.y += parabolicHeight;

            return position;
        }

        /// <summary>
        /// 线性轨迹计算
        /// </summary>
        private Vector3 CalculateLinearPoint(float t)
        {
            // 简单的线性插值
            return Vector3.Lerp(origin, destination, t);
        }

        /// <summary>
        /// 平滑步进轨迹计算
        /// </summary>
        private Vector3 CalculateSmoothStepPoint(float t)
        {
            // 使用平滑步进函数
            float smoothT = Mathf.SmoothStep(0f, 1f, t);
            Vector3 position = Vector3.Lerp(origin, destination, smoothT);

            // 添加高度变化
            float distance = Vector3.Distance(origin, destination);
            float parabolicHeight = 4f * (distance * heightMultiplier * 0.25f) * t * (1f - t);
            position.y += parabolicHeight;

            return position;
        }

        /// <summary>
        /// 双纽线轨迹计算（8字形）
        /// </summary>
        private Vector3 CalculateLemniscatePoint(float t)
        {
            Vector3 start = origin;
            Vector3 end = destination;
            Vector3 direct = (end - start).normalized;
            float distance = Vector3.Distance(start, end);

            // 基本线性路径
            Vector3 position = Vector3.Lerp(start, end, t);

            // 计算正交方向（垂直于飞行方向）
            Vector3 orthogonal1 = Vector3.Cross(direct, Vector3.up).normalized;
            Vector3 orthogonal2 = Vector3.Cross(direct, orthogonal1).normalized;

            // 双纽线参数 (8字形)
            float angle = t * trajectoryFrequency * Mathf.PI * 4f; // 2个完整的8字
            float a = trajectoryAmplitude * (1f - t * 0.8f); // 振幅逐渐减小

            // 双纽线公式
            float denom = 1f + Mathf.Pow(Mathf.Sin(angle), 2);
            float x = a * Mathf.Cos(angle) / denom;
            float y = a * Mathf.Sin(angle) * Mathf.Cos(angle) / denom;

            // 添加8字形运动
            position += orthogonal1 * x;
            position += orthogonal2 * y;

            // 添加高度变化
            float parabolicHeight = 4f * (distance * heightMultiplier * 0.25f) * t * (1f - t);
            position.y += parabolicHeight;

            return position;
        }

        // 修改原有方法，使用新的轨迹计算
        private Vector3 BezierPoint(float t)
        {
            return CalculateTrajectoryPoint(t);
        }

        /// <summary>
        /// 初始化贝塞尔曲线控制点
        /// </summary>
        private void InitializeBezierCurve()
        {
            p0 = origin;
            p3 = destination;

            // 控制点1：沿飞行方向偏移30%
            p1 = origin + (destination - origin) * (0.3f + randOffset1);

            // 控制点2：沿飞行方向偏移70%，并增加高度
            p2 = origin + (destination - origin) * (0.7f + randOffset2) + new Vector3(0f, heightOffset, 0f);

            curveInitialized = true;
        }

        /// <summary>
        /// 重新计算贝塞尔曲线以追踪移动的目标
        /// </summary>
        private void RecalculateCurve()
        {
            if (!targetAcquired || intendedTarget.Thing == null || intendedTarget.Thing.Destroyed)
            {
                return;
            }

            Vector3 currentPos = ExactPosition; // 使用实际位置而不是曲线上的点
            Vector3 targetPos = intendedTarget.Thing.DrawPos;

            // 保存当前状态
            float currentFraction = DistanceCoveredFraction;

            // 计算目标位置
            Vector3 predictedTargetPos = targetPos;
            if (intendedTarget.Thing is Pawn pawn && !pawn.Dead && !pawn.Downed && pawn.pather != null && pawn.pather.Moving)
            {
                // 预测目标移动 - 改进的预测逻辑
                Vector3 targetVelocity = (targetPos - lastTargetPos);
                predictedTargetPos = targetPos + targetVelocity * 3f; // 预测三帧后的位置
            }

            // 更新目标位置
            destination = predictedTargetPos;
            p3 = predictedTargetPos;
            lastTargetPos = targetPos;

            // 根据当前飞行阶段调整轨迹
            float flightPhase = DistanceCoveredFraction;

            // 计算当前方向和到目标的方向
            Vector3 currentDirection = (flightPhase > 0.01f) ?
                (ExactPosition - previousPosition).normalized :
                (destination - origin).normalized;

            Vector3 directionToTarget = (predictedTargetPos - currentPos).normalized;

            // 计算所需转向角度
            float angleDiff = Vector3.Angle(currentDirection, directionToTarget);

            // 根据飞行阶段调整导弹的灵活性
            float agileMultiplier = 1.0f;
            if (flightPhase > agilePhaseStart && flightPhase < agilePhaseEnd)
            {
                // 导弹在敏捷相位，增强机动性
                agileMultiplier = 1.5f;
            }
            else if (flightPhase >= agilePhaseEnd)
            {
                // 接近终点，进入直线冲刺模式
                agileMultiplier = 2.0f;
            }

            // 限制最大转向角度
            float maxAllowedTurn = Mathf.Min(maxTurnAngle, turnRatePerTick * agileMultiplier);
            float turnAngle = Mathf.Min(angleDiff, maxAllowedTurn);

            // 计算转向方向
            Vector3 cross = Vector3.Cross(currentDirection, directionToTarget);
            float turnDirection = Mathf.Sign(cross.y);

            // 应用平滑转向
            if (lastTurnDirection != 0f && Mathf.Sign(lastTurnDirection) != Mathf.Sign(turnDirection) && angleDiff > 10f)
            {
                // 转向方向改变，平滑过渡
                turnAngle *= 0.5f;
            }

            lastTurnDirection = turnDirection;

            // 创建旋转
            Quaternion rotation = Quaternion.AngleAxis(turnAngle * turnDirection, Vector3.up);
            Vector3 adjustedDirection = rotation * currentDirection;

            // 计算剩余距离
            float remainingDistance = Vector3.Distance(currentPos, predictedTargetPos);

            // 根据不同的轨迹类型调整中间控制点
            if (trajectoryType == MissileTrajectoryType.BezierCurve)
            {
                // 如果已经过了顶点，使用更直接的路径
                if (flightPhase > 0.5f || reachedApex)
                {
                    reachedApex = true;

                    // 顶点后使用更直接的路径，确保命中目标
                    p1 = currentPos + adjustedDirection * (remainingDistance * 0.3f);
                    p2 = currentPos + adjustedDirection * (remainingDistance * 0.6f) +
                         new Vector3(0f, Mathf.Max(2f, heightOffset * (1f - currentFraction) * 0.5f), 0f);
                }
                else
                {
                    // 顶点前保持原有曲线特性，但调整以适应目标位置
                    Vector3 flightDirection = (predictedTargetPos - origin).normalized;
                    float distance = Vector3.Distance(origin, predictedTargetPos);

                    p1 = origin + flightDirection * (distance * (0.3f + randOffset1));
                    p2 = origin + flightDirection * (distance * (0.7f + randOffset2)) +
                         new Vector3(0f, heightOffset, 0f);
                }
            }
            else
            {
                // 对于非贝塞尔曲线轨迹，在终点阶段强制转为直线轨迹确保命中
                if (flightPhase > 0.7f)
                {
                    trajectoryType = MissileTrajectoryType.Linear;

                    // 重新初始化为简单直线以确保命中
                    p0 = currentPos;
                    p3 = predictedTargetPos;
                    p1 = Vector3.Lerp(p0, p3, 0.33f);
                    p2 = Vector3.Lerp(p0, p3, 0.66f);
                }
            }
        }

        /// <summary>
        /// 尝试寻找新目标 - 使用XML配置的搜索半径
        /// </summary>
        private void FindNewTarget()
        {
            if (!canSwitchTargets) return;

            if (DistanceCoveredFraction > agilePhaseStart && DistanceCoveredFraction < agilePhaseEnd && Rand.Chance(targetSwitchChance))
            {
                IntVec3 searchCenter = IntVec3.FromVector3(ExactPosition);

                // 从导弹当前位置搜索敌人，使用XML配置的搜索半径
                IEnumerable<Pawn> potentialTargets = from p in GenRadial.RadialCellsAround(searchCenter, searchRadius, true)
                                                     where p.InBounds(Map)
                                                     let pawn = p.GetFirstPawn(Map)
                                                     where pawn != null && !pawn.Dead && !pawn.Downed &&
                                                           pawn.Faction != null &&
                                                           (launcher == null || pawn.Faction.HostileTo(launcher.Faction)) &&
                                                           Vector3.Distance(ExactPosition, pawn.DrawPos) >= minTargetSwitchDistance
                                                     orderby Vector3.Distance(ExactPosition, pawn.DrawPos)
                                                     select pawn;

                Pawn newTarget = potentialTargets.FirstOrDefault();
                if (newTarget != null)
                {
                    intendedTarget = newTarget;
                    targetAcquired = true;
                    lastTargetPos = newTarget.DrawPos;

                    if (Prefs.DevMode)
                    {
                        Log.Message($"导弹锁定新目标: {newTarget.Label}, 距离: {Vector3.Distance(ExactPosition, newTarget.DrawPos):F1}");
                    }
                }
            }
        }

        /// <summary>
        /// 检查目标状态 - 使用XML配置的搜索半径
        /// </summary>
        private void CheckTargetStatus()
        {
            if (intendedTarget.Thing != null && !intendedTarget.Thing.Destroyed)
            {
                // 目标存在
                if (!targetAcquired)
                {
                    targetAcquired = true;
                    lastTargetPos = intendedTarget.Thing.DrawPos;
                }

                // 检查目标是否移动太远，使用XML配置的搜索半径
                float targetDistance = Vector3.Distance(ExactPosition, intendedTarget.Thing.DrawPos);
                if (targetDistance > searchRadius)
                {
                    // 目标超出跟踪范围
                    losTargetCountdown++;
                    if (losTargetCountdown > 5)
                    {
                        // 丢失目标
                        if (Prefs.DevMode)
                        {
                            Log.Message($"导弹丢失目标 - 目标超出跟踪范围 ({targetDistance:F1} > {searchRadius:F1})");
                        }
                        targetAcquired = false;
                        losTargetCountdown = 0;
                    }
                }
                else
                {
                    losTargetCountdown = 0;
                    // 无论轨迹类型，均重新计算曲线以确保跟踪目标
                    RecalculateCurve();
                }
            }
            else if (targetAcquired)
            {
                // 目标消失
                targetAcquired = false;
                FindNewTarget();
            }
        }


        protected override void Tick()
        {
            try
            {
                // 如果已经被销毁或没有地图，直接返回
                if (Destroyed || Map == null)
                {
                    return;
                }

                // 记录当前位置，用于后续安全检查
                Vector3 currentExactPosition = ExactPosition;
                IntVec3 currentPosition = Position;

                // 检查目标状态
                if (DistanceCoveredFraction < 0.95f)
                {
                    CheckTargetStatus();

                    // 如果没有目标，尝试寻找新目标
                    if (!targetAcquired)
                    {
                        FindNewTarget();
                    }
                }

                // 计算新位置前，确保贝塞尔曲线已初始化
                if (!curveInitialized)
                {
                    InitializeBezierCurve();
                }

                // 记录上一个位置
                previousPosition = ExactPosition;

                // 安全调用基类Tick
                try
                {
                    // 为基类的位置更新增加安全措施
                    // 检查计算出的新位置是否有效
                    Vector3 newPosition = BezierPoint(DistanceCoveredFraction + 0.01f);
                    if (!newPosition.InBounds(Map) || float.IsNaN(newPosition.x) || float.IsNaN(newPosition.y) || float.IsNaN(newPosition.z))
                    {
                        // 如果新位置无效，触发撞击逻辑而不是继续飞行
                        Impact(null);
                        return;
                    }

                    base.Tick();
                }
                catch (Exception ex)
                {
                    // 如果基类Tick失败，尝试使用我们自己的逻辑来安全更新
                    if (Prefs.DevMode)
                    {
                        Log.Warning($"导弹基类Tick失败: {ex.Message}，使用备用更新逻辑");
                    }

                    // 计算新位置
                    Vector3 newExactPos = BezierPoint(DistanceCoveredFraction + 0.01f);
                    IntVec3 newCell = newExactPos.ToIntVec3();

                    // 安全检查新位置是否在地图范围内
                    if (newCell.InBounds(Map))
                    {
                        // 手动更新位置
                        Position = newCell;
                        destination = origin;
                        origin = newExactPos;
                        ticksToImpact = 1;
                    }
                    else
                    {
                        // 如果超出边界，触发撞击
                        Impact(null);
                        return;
                    }
                }

                // 粒子效果
                if (!Destroyed && Map != null)
                {
                    EmitFlecks();
                }

                // 检查是否到达顶点
                if (!reachedApex && DistanceCoveredFraction > 0.5f)
                {
                    reachedApex = true;
                }

                // 接近目标检测 - 改进的命中检测逻辑
                if (targetAcquired && intendedTarget.Thing != null && !intendedTarget.Thing.Destroyed)
                {
                    float distToTarget = Vector3.Distance(ExactPosition, intendedTarget.Thing.DrawPos);

                    // 根据飞行阶段调整命中半径
                    float hitRadius = 1.5f;
                    if (DistanceCoveredFraction > 0.9f)
                    {
                        hitRadius = 2.5f; // 接近终点时增大命中半径，提高命中率
                    }

                    if (distToTarget < hitRadius)
                    {
                        try
                        {
                            // 命中目标
                            if (intendedTarget.Cell.InBounds(Map))
                            {
                                Position = intendedTarget.Cell;
                                ImpactSomething();
                            }
                            else
                            {
                                // 目标位置无效，在当前位置引爆
                                Impact(null);
                            }
                        }
                        catch (Exception ex)
                        {
                            if (Prefs.DevMode)
                            {
                                Log.Error($"导弹命中目标时出错: {ex.Message}");
                            }
                            // 在异常时尝试在当前位置引爆
                            try
                            {
                                Impact(null);
                            }
                            catch
                            {
                                // 最后的安全网，强制销毁导弹
                                Destroy();
                            }
                        }
                    }
                }

                // 额外检查：如果位置更新后超出地图范围，强制触发撞击
                if (!Destroyed && !Position.InBounds(Map))
                {
                    Impact(null);
                }
            }
            catch (Exception ex)
            {
                // 全局异常处理
                if (Prefs.DevMode)
                {
                    Log.Error($"导弹Tick时发生未处理的异常: {ex.Message}\n{ex.StackTrace}");
                }

                // 发生异常时，尽可能安全地处理导弹
                try
                {
                    if (!Destroyed && Map != null)
                    {
                        Impact(null);
                    }
                    else
                    {
                        Destroy();
                    }
                }
                catch
                {
                    // 最终安全网：如果所有失败，强制销毁
                    Destroy();
                }
            }
        }


        /// <summary>
        /// 生成粒子效果
        /// </summary>
        private void EmitFlecks()
        {
            // 检查地图是否为null
            if (Map == null || Destroyed)
            {
                return;
            }

            fleckEmitTick++;
            if (fleckEmitTick >= fleckInterval)
            {
                fleckEmitTick = 0;

                Vector3 position = BezierPoint(DistanceCoveredFraction);
                position.y = def.Altitude;

                // 确保fleckCountRange已初始化
                if (fleckCountRange == null)
                {
                    fleckCountRange = new IntRange(Mathf.Max(1, flecksPerBurst - 1), flecksPerBurst + 1);
                }

                int fleckCount = fleckCountRange.RandomInRange;
                for (int i = 0; i < fleckCount; i++)
                {
                    // 检查FleckDef是否存在
                    FleckDef fleckDef = DistanceCoveredFraction < 0.7f ?
                        FleckDefOf.FireGlow : FleckDefOf.MicroSparks;

                    if (fleckDef == null)
                    {
                        continue;
                    }

                    // 确保角度范围已初始化
                    if (fleckAngleRange == null)
                    {
                        fleckAngleRange = new FloatRange(-180f, 180f);
                    }
                    if (fleckSpeedRange == null)
                    {
                        fleckSpeedRange = new FloatRange(0.05f, 0.15f);
                    }
                    if (fleckRotationRange == null)
                    {
                        fleckRotationRange = new FloatRange(-30f, 30f);
                    }

                    float angle = (position - destination).AngleFlat() + fleckAngleRange.RandomInRange;
                    float speed = fleckSpeedRange.RandomInRange;
                    float rotation = fleckRotationRange.RandomInRange;

                    // 确保def和graphicData不为null
                    if (def?.graphicData == null)
                    {
                        continue;
                    }

                    float scale = def.graphicData.drawSize.x * 0.4f;

                    try
                    {
                        FleckCreationData data = FleckMaker.GetDataStatic(position, Map, fleckDef, scale);
                        data.velocityAngle = angle;
                        data.velocitySpeed = speed;
                        data.rotationRate = rotation;
                        Map.flecks.CreateFleck(data);

                        // 烟雾粒子
                        if (Rand.Chance(smokeChance))
                        {
                            FleckMaker.ThrowSmoke(position, Map, scale * 1.2f);
                        }
                    }
                    catch (Exception ex)
                    {
                        if (Prefs.DevMode)
                        {
                            Log.Error($"导弹粒子效果生成错误: {ex.Message}");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 绘制导弹
        /// </summary>
        protected override void DrawAt(Vector3 drawPos, bool flip = false)
        {
            // 计算实际位置
            Vector3 actualPos = BezierPoint(DistanceCoveredFraction);
            Vector3 prevPos = DistanceCoveredFraction > 0.01f ?
                BezierPoint(DistanceCoveredFraction - 0.01f) : previousPosition;

            // 计算朝向
            Quaternion rotation = Quaternion.LookRotation(actualPos - prevPos);

            // 绘制阴影
            Vector3 shadowPos = new Vector3(actualPos.x, 0f, actualPos.z);
            float shadowSize = def.graphicData.drawSize.x * 0.7f;
            Graphics.DrawMesh(MeshPool.GridPlane(new Vector2(shadowSize, shadowSize)),
                shadowPos, Quaternion.identity, shadowMaterial, 0);

            // 设置高度
            actualPos.y = def.Altitude;

            // 绘制尾焰
            if (DistanceCoveredFraction > 0.1f && DistanceCoveredFraction < 0.95f)
            {
                Graphics.DrawMesh(MeshPool.GridPlane(tailDrawSize), actualPos, rotation, tailMaterial, 0);
            }

            // 绘制导弹本体
            Graphics.DrawMesh(MeshPool.GridPlane(def.graphicData.drawSize), actualPos, rotation, DrawMat, 0);

            // 绘制组件
            Comps_PostDraw();
        }

        /// <summary>
        /// 撞击处理
        /// </summary>
        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            try
            {
                // 保存当前地图和位置引用，防止在过程中被清除
                Map map = Map;
                IntVec3 position = Position;

                // 检查地图是否有效
                if (map == null)
                {
                    Destroy();
                    return;
                }

                // 基础爆炸处理
                base.Impact(hitThing, blockedByShield);

                // 增强爆炸视觉效果
                if (map != null && map.regionAndRoomUpdater.Enabled)
                {
                    // 爆炸闪光
                    FleckMaker.Static(position.ToVector3Shifted(), map, FleckDefOf.ExplosionFlash, 12f);

                    // 爆炸烟雾
                    for (int i = 0; i < 6; i++)
                    {
                        Vector3 loc = position.ToVector3Shifted() + new Vector3(Rand.Range(-1f, 1f), 0f, Rand.Range(-1f, 1f));
                        FleckMaker.ThrowSmoke(loc, map, Rand.Range(1.5f, 2.5f));
                    }

                    // 爆炸碎片
                    for (int i = 0; i < 6; i++)
                    {
                        Vector3 loc = position.ToVector3Shifted() + new Vector3(Rand.Range(-1f, 1f), 0f, Rand.Range(-1f, 1f));
                        FleckMaker.ThrowMicroSparks(loc, map);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"导弹爆炸时出错: {ex.Message}");

                // 确保导弹被销毁，即使爆炸逻辑失败
                try
                {
                    Destroy();
                }
                catch
                {
                    // 最后的安全网
                    if (this != null && !Destroyed)
                    {
                        Destroy(DestroyMode.Vanish);
                    }
                }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();

            // 保存参数
            Scribe_Values.Look(ref maxTrackingRadius, "maxTrackingRadius", 30f);
            Scribe_Values.Look(ref maxTurnAngle, "maxTurnAngle", 45f);
            Scribe_Values.Look(ref turnRatePerTick, "turnRatePerTick", 1.2f);
            Scribe_Values.Look(ref canSwitchTargets, "canSwitchTargets", true);
            Scribe_Values.Look(ref targetSwitchChance, "targetSwitchChance", 0.1f);
            Scribe_Values.Look(ref minTargetSwitchDistance, "minTargetSwitchDistance", 5f);
            Scribe_Values.Look(ref agilePhaseStart, "agilePhaseStart", 0.3f);
            Scribe_Values.Look(ref agilePhaseEnd, "agilePhaseEnd", 0.8f);
            Scribe_Values.Look(ref heightMultiplier, "heightMultiplier", 1.0f);
            Scribe_Values.Look(ref tailWidth, "tailWidth", 1.2f);
            Scribe_Values.Look(ref tailLength, "tailLength", 3f);
            Scribe_Values.Look(ref smokeChance, "smokeChance", 0.7f);
            Scribe_Values.Look(ref flecksPerBurst, "flecksPerBurst", 3);
            Scribe_Values.Look(ref fleckInterval, "fleckInterval", 2);

            // 新增：保存轨迹参数
            Scribe_Values.Look(ref trajectoryType, "trajectoryType", MissileTrajectoryType.BezierCurve);
            Scribe_Values.Look(ref searchRadius, "searchRadius", 30f);
            Scribe_Values.Look(ref trajectoryAmplitude, "trajectoryAmplitude", 1.0f);
            Scribe_Values.Look(ref trajectoryFrequency, "trajectoryFrequency", 1.0f);
            Scribe_Values.Look(ref spiralRadius, "spiralRadius", 3.0f);
            Scribe_Values.Look(ref spiralTightness, "spiralTightness", 0.5f);

            // 保存状态变量
            Scribe_Values.Look(ref curveInitialized, "curveInitialized", false);
            Scribe_Values.Look(ref targetAcquired, "targetAcquired", false);
            Scribe_Values.Look(ref losTargetCountdown, "losTargetCountdown", 0);
            Scribe_Values.Look(ref reachedApex, "reachedApex", false);
            Scribe_Values.Look(ref lastTurnDirection, "lastTurnDirection", 0f);

            // 保存曲线参数
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                if (curveInitialized)
                {
                    Vector3 p0Value = p0;
                    Vector3 p1Value = p1;
                    Vector3 p2Value = p2;
                    Vector3 p3Value = p3;
                    Scribe_Values.Look(ref p0Value, "p0");
                    Scribe_Values.Look(ref p1Value, "p1");
                    Scribe_Values.Look(ref p2Value, "p2");
                    Scribe_Values.Look(ref p3Value, "p3");
                }
            }

            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                Vector3 p0Value = Vector3.zero;
                Vector3 p1Value = Vector3.zero;
                Vector3 p2Value = Vector3.zero;
                Vector3 p3Value = Vector3.zero;
                Scribe_Values.Look(ref p0Value, "p0");
                Scribe_Values.Look(ref p1Value, "p1");
                Scribe_Values.Look(ref p2Value, "p2");
                Scribe_Values.Look(ref p3Value, "p3");

                if (curveInitialized)
                {
                    p0 = p0Value;
                    p1 = p1Value;
                    p2 = p2Value;
                    p3 = p3Value;
                }
            }
        }

    }
    /// <summary>
    /// 导弹属性组件 - 用于配置导弹参数
    /// </summary>
    public class BezierMissileProperties : CompProperties
    {
        // 导弹性能参数
        public float maxTrackingRadius = 30f;      // 最大跟踪半径
        public float maxTurnAngle = 45f;           // 最大转向角度（度）
        public float turnRatePerTick = 1.2f;       // 每Tick最大转向角度
        public bool canSwitchTargets = true;       // 是否可以切换目标
        public float targetSwitchChance = 0.1f;    // 寻找新目标的几率
        public float minTargetSwitchDistance = 5f; // 目标切换的最小距离
        public float agilePhaseStart = 0.3f;       // 开始敏捷相位的飞行完成百分比
        public float agilePhaseEnd = 0.8f;         // 结束敏捷相位的飞行完成百分比
        public float heightMultiplier = 1.0f;      // 高度倍数

        // 新增：轨迹类型和搜索参数
        public MissileTrajectoryType trajectoryType = MissileTrajectoryType.BezierCurve; // 轨迹类型
        public float searchRadius = 30f;           // 目标搜索半径
        public float trajectoryAmplitude = 1.0f;   // 轨迹振幅
        public float trajectoryFrequency = 1.0f;   // 轨迹频率
        public float spiralRadius = 3.0f;          // 螺旋半径
        public float spiralTightness = 0.5f;       // 螺旋紧密度

        // 视觉效果参数
        public float tailWidth = 1.2f;             // 尾焰宽度
        public float tailLength = 3f;              // 尾焰长度
        public float smokeChance = 0.7f;           // 产生烟雾的几率
        public int flecksPerBurst = 3;             // 每次产生的粒子数量
        public int fleckInterval = 2;              // 产生粒子的间隔Tick

        public BezierMissileProperties()
        {
            compClass = typeof(CompBezierMissile);
        }
    }

    /// <summary>
    /// 贝塞尔导弹组件 - 存储和管理导弹的配置参数
    /// </summary>
    public class CompBezierMissile : ThingComp
    {
        public BezierMissileProperties Props => (BezierMissileProperties)props;
    }
}
