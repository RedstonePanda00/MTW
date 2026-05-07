using NCL;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Text;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace NCL
{
    // 多状态燃料图形类
    public class Graphic_RefuelableMulti : Graphic_Multi
    {
        // 三种燃料状态对应的贴图
        private Graphic lowFuelGraphic;    // <20% 燃料
        private Graphic mediumFuelGraphic; // 20-70% 燃料
        private Graphic highFuelGraphic;   // >70% 燃料

        // 初始化图形
        public override void Init(GraphicRequest req)
        {
            base.Init(req);

            // 创建三种燃料状态对应的贴图
            lowFuelGraphic = CreateStateGraphic(
                req.path + "_low",
                req.shader,
                req.drawSize,
                req.color,
                req.colorTwo
            );

            mediumFuelGraphic = CreateStateGraphic(
                req.path + "_medium",
                req.shader,
                req.drawSize,
                req.color,
                req.colorTwo
            );

            highFuelGraphic = CreateStateGraphic(
                req.path + "_high",
                req.shader,
                req.drawSize,
                req.color,
                req.colorTwo
            );
        }

        // 创建状态贴图
        private Graphic CreateStateGraphic(string path, Shader shader, Vector2 size, Color color, Color colorTwo)
        {
            return GraphicDatabase.Get(
                typeof(Graphic_Single),
                path,
                shader,
                size,
                color,
                colorTwo
            );
        }

        // 获取当前燃料状态对应的图形
        public override Material MatAt(Rot4 rot, Thing thing = null)
        {
            // 默认返回中等燃料状态
            if (thing == null) return mediumFuelGraphic.MatAt(rot);

            // 获取燃料组件
            var compRefuelable = thing.TryGetComp<CompRefuelable>();
            if (compRefuelable == null) return mediumFuelGraphic.MatAt(rot);

            // 计算燃料百分比
            float fuelPercent = compRefuelable.Fuel / compRefuelable.Props.fuelCapacity;

            // 选择对应的贴图状态
            return GetFuelStateGraphic(fuelPercent).MatAt(rot);
        }

        // 根据燃料百分比选择合适的图形
        private Graphic GetFuelStateGraphic(float fuelPercent)
        {
            if (fuelPercent < 0.2f) return lowFuelGraphic;
            if (fuelPercent < 0.7f) return mediumFuelGraphic;
            return highFuelGraphic;
        }

        // 绘制方法
        public override void DrawWorker(
            Vector3 loc,
            Rot4 rot,
            ThingDef thingDef,
            Thing thing,
            float extraRotation)
        {
            // 获取当前燃料状态对应的材质
            Material mat = MatAt(rot, thing);

            // 设置材质属性
            if (mat != null)
            {
                // 设置随机变化（避免所有建筑看起来一样）
                if (thing != null)
                {
                    mat.SetFloat(ShaderPropertyIDs.RandomPerObject,
                        thing.thingIDNumber.HashOffset());
                }

                // 绘制建筑
                Graphics.DrawMesh(
                    MeshAt(rot),
                    loc,
                    Quaternion.AngleAxis(extraRotation, Vector3.up),
                    mat,
                    0
                );
            }

            // 绘制阴影
            if (ShadowGraphic != null)
            {
                ShadowGraphic.DrawWorker(
                    loc,
                    rot,
                    thingDef,
                    thing,
                    extraRotation
                );
            }
        }

        // 获取网格（使用中等燃料状态作为基础）
        public override Mesh MeshAt(Rot4 rot)
        {
            return mediumFuelGraphic.MeshAt(rot);
        }
    }
}



namespace NCL
{
    public class CompPowerAdjustable : CompPowerTrader
    {
        // 当前功率百分比 (0-100)
        public float powerPercent = 0f;

        // 引用组件
        private CompRefuelable refuelableComp;

        // 自定义参数
        public float basePowerOutput = 0f;
        public float maxPowerOutput = 140000f;
        public float baseFuelConsumption = 0.5f;
        public float maxFuelConsumption = 100f;

        // 发热参数
        public float minHeatOutput = 0f;
        public float maxHeatOutput = 100f;
        private float currentHeatOutput = 0f;

        // 发热相关
        private const int HeatPushInterval = 60; // 60 ticks = 1 second
        private int ticksSinceLastHeatPush = 0;
        private bool shouldPushHeat = false;

        private float lastFuelPercent = -1f;
        private int ticksSinceLastCheck = 0;
        private const int CheckInterval = 250;

        // 平滑过渡的当前功率
        private float currentSmoothedOutput = 0f;
        private const float SmoothingFactor = 0.1f;

        // 燃料功率乘数（90%燃料时达到200%峰值）
        private float FuelPowerMultiplier
        {
            get
            {
                // 当功率设置为0时，忽略燃料变化
                if (powerPercent <= 0f) return 0f;

                if (refuelableComp == null || !refuelableComp.HasFuel)
                    return 0f;

                float fuelPercent = refuelableComp.FuelPercentOfMax;

                // 90%燃料时达到200%峰值
                if (fuelPercent >= 0.9f)
                    return 2.0f;

                // 0-90%线性增长：0%→0.0x, 90%→2.0x
                return (fuelPercent / 0.9f) * 2.0f;
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref powerPercent, "powerPercent", 0f);
            Scribe_Values.Look(ref basePowerOutput, "basePowerOutput", 0f);
            Scribe_Values.Look(ref maxPowerOutput, "maxPowerOutput", 140000f);
            Scribe_Values.Look(ref baseFuelConsumption, "baseFuelConsumption", 0.5f);
            Scribe_Values.Look(ref maxFuelConsumption, "maxFuelConsumption", 50f);
            Scribe_Values.Look(ref lastFuelPercent, "lastFuelPercent", -1f);
            Scribe_Values.Look(ref currentSmoothedOutput, "currentSmoothedOutput", 0f);

            // 序列化热量相关变量
            Scribe_Values.Look(ref minHeatOutput, "minHeatOutput", 0f);
            Scribe_Values.Look(ref maxHeatOutput, "maxHeatOutput", 500f);
            Scribe_Values.Look(ref currentHeatOutput, "currentHeatOutput", 0f);
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            refuelableComp = parent.GetComp<CompRefuelable>();
            UpdateDesiredPowerOutput();
            UpdateHeatOutput(); // 初始化热量输出

            if (refuelableComp != null)
            {
                lastFuelPercent = refuelableComp.FuelPercentOfMax;
            }

            currentSmoothedOutput = base.PowerOutput;
        }

        // 更新电力输出
        public void UpdateDesiredPowerOutput()
        {
            // 设置设备状态
            base.PowerOn = powerPercent > 0 && (refuelableComp?.HasFuel ?? false);

            // 更新燃料消耗率 - 当功率为0时设置为0
            if (refuelableComp != null && refuelableComp.HasFuel)
            {
                refuelableComp.Props.fuelConsumptionRate = powerPercent > 0
                    ? Mathf.Lerp(baseFuelConsumption, maxFuelConsumption, powerPercent / 100f)
                    : 0f; // 功率为0时无燃料消耗
            }
        }

        // 更新热量输出计算
        private void UpdateHeatOutput()
        {
            // 当功率为0时无热量输出
            if (powerPercent <= 0f)
            {
                currentHeatOutput = 0f;
                shouldPushHeat = false;
                return;
            }

            // 计算基础热量输出
            currentHeatOutput = Mathf.Lerp(
                minHeatOutput,
                maxHeatOutput,
                powerPercent / 100f
            );

            // 应用燃料乘数
            currentHeatOutput *= FuelPowerMultiplier;

            // 确定是否应推送热量
            shouldPushHeat = currentHeatOutput > 0 &&
                             parent.Spawned &&
                             parent.Map != null &&
                             (refuelableComp?.HasFuel ?? true);
        }

        public override void CompTick()
        {
            base.CompTick(); // 先调用基类

            ticksSinceLastCheck++;
            ticksSinceLastHeatPush++;
            bool needsUpdate = false;

            // 计算目标输出功率 - 当功率为0时直接设置为0
            float targetOutput;
            if (powerPercent <= 0f)
            {
                // 功率设置为0时，直接平滑过渡到0
                targetOutput = 0f;
            }
            else
            {
                float baseOutput = Mathf.Lerp(basePowerOutput, maxPowerOutput, powerPercent / 100f);
                targetOutput = baseOutput * FuelPowerMultiplier;
            }

            // 应用平滑过渡
            currentSmoothedOutput = Mathf.Lerp(
                currentSmoothedOutput,
                targetOutput,
                SmoothingFactor
            );

            // 设置实际功率输出
            base.PowerOutput = currentSmoothedOutput;

            // 更新热量输出计算
            UpdateHeatOutput();

            // 推送热量 (每秒一次)
            if (ticksSinceLastHeatPush >= HeatPushInterval && shouldPushHeat)
            {
                ticksSinceLastHeatPush = 0;

                // 向当前位置推送热量
                GenTemperature.PushHeat(parent.Position, parent.Map, currentHeatOutput);
            }

            // 当功率为0时，跳过燃料状态检查
            if (powerPercent <= 0f) return;

            // 定期检查燃料状态变化
            if (ticksSinceLastCheck >= CheckInterval)
            {
                ticksSinceLastCheck = 0;

                if (refuelableComp != null)
                {
                    float currentFuelPercent = refuelableComp.FuelPercentOfMax;

                    // 检查燃料百分比变化或燃料状态变化
                    if (Mathf.Abs(currentFuelPercent - lastFuelPercent) > 0.01f ||
                        refuelableComp.HasFuel != (lastFuelPercent > 0))
                    {
                        needsUpdate = true;
                    }

                    lastFuelPercent = currentFuelPercent;
                }
            }

            // 需要更新时才调用
            if (needsUpdate)
            {
                UpdateDesiredPowerOutput();
            }
        }

        // 提供UI调节滑块
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo g in base.CompGetGizmosExtra())
            {
                yield return g;
            }

            yield return new Command_Action
            {
                icon = ContentFinder<Texture2D>.Get("ModIcon/SetPowerLevel"),
                defaultLabel = "NCL.ADJUST_POWER_OUTPUT".Translate(),
                defaultDesc = "NCL.CURRENT_POWER_LEVEL".Translate(powerPercent) + "\n" +
                             GetFuelMultiplierInfo() + "\n" +
                             GetHeatInfo(),
                action = () => {
                    Find.WindowStack.Add(new Dialog_Slider(
                        value => "NCL.SET_POWER_OUTPUT".Translate(value, Mathf.Lerp(minHeatOutput, maxHeatOutput, value / 100f).ToString("F1")),
                        0,      // 最小值
                        100,    // 最大值
                        value => {
                            powerPercent = value;
                            UpdateDesiredPowerOutput();
                            UpdateHeatOutput();
                        },
                        (int)powerPercent
                    ));
                }
            };
        }

        // 获取燃料乘数信息
        private string GetFuelMultiplierInfo()
        {
            if (powerPercent <= 0f)
                return "NCL.POWER_DISABLED".Translate();

            if (refuelableComp == null)
                return "NCL.NO_FUEL_SYSTEM".Translate();

            if (!refuelableComp.HasFuel)
                return "NCL.NO_FUEL".Translate();

            float fuelPercent = refuelableComp.FuelPercentOfMax;
            float multiplier = FuelPowerMultiplier;

            string powerEffect = fuelPercent >= 0.9f ? "NCL.PEAK_POWER".Translate() : "";

            return "NCL.FUEL_STATUS".Translate(fuelPercent * 100f, multiplier, powerEffect);
        }

        // 获取热量信息
        private string GetHeatInfo()
        {
            return powerPercent > 0
                ? "NCL.HEAT_OUTPUT".Translate(currentHeatOutput.ToString("F1"))
                : "NCL.NO_HEAT_OUTPUT".Translate();
        }

        // 在信息面板显示状态
        public override string CompInspectStringExtra()
        {
            string baseStr = base.CompInspectStringExtra();
            if (baseStr == null) baseStr = "";

            string fuelStr = refuelableComp != null
                ? "NCL.FUEL_CONSUMPTION".Translate(refuelableComp.Props.fuelConsumptionRate.ToString("F2"))
                : "";

            string multiplierInfo = "NCL.NO_FUEL".Translate();
            if (powerPercent <= 0f)
            {
                multiplierInfo = "NCL.POWER_DISABLED".Translate();
            }
            else if (refuelableComp != null && refuelableComp.HasFuel)
            {
                float fuelPercent = refuelableComp.FuelPercentOfMax;
                multiplierInfo = "NCL.FUEL_MULTIPLIER".Translate(FuelPowerMultiplier.ToString("F1"));

                if (fuelPercent >= 0.9f)
                {
                    multiplierInfo += "NCL.PEAK".Translate();
                }
                else
                {
                    float neededPercent = (0.9f - fuelPercent) * 100f;
                    multiplierInfo += "\n" + "NCL.NEED_MORE_FUEL".Translate(neededPercent.ToString("F0"));
                }
            }

            string heatStr = "NCL.HEAT_OUTPUT".Translate(currentHeatOutput.ToString("F1"));

            // Build the final string, filtering out empty entries
            List<string> parts = new List<string>();
            if (!baseStr.NullOrEmpty()) parts.Add(baseStr);
            if (!fuelStr.NullOrEmpty()) parts.Add(fuelStr);
            if (!multiplierInfo.NullOrEmpty()) parts.Add(multiplierInfo);
            if (!heatStr.NullOrEmpty()) parts.Add(heatStr);

            return string.Join("\n", parts.Where(s => !s.NullOrEmpty()));
        }

    }
}


namespace NCL
{
    public class CompProperties_ExplosiveRefuelable : CompProperties
    {
        public float minExplosionRadius = 5f;
        public float maxExplosionRadius = 50f;
        public float minClearRadius = 0f;
        public float maxClearRadius = 15f;

        // 移除默认赋值，改为在ResolveReferences中处理
        public DamageDef damageDef;

        public float explosionDamageFactor = 30f;
        public bool requiresFuelForExplosion = true;

        public CompProperties_ExplosiveRefuelable()
        {
            compClass = typeof(CompExplosiveRefuelable);
        }

        // 添加ResolveReferences方法
        public override void ResolveReferences(ThingDef parentDef)
        {
            base.ResolveReferences(parentDef);

            // 安全的DefOf初始化点
            if (damageDef == null)
            {
                damageDef = DamageDefOf.Bomb; // 现在可以安全访问DefOf
            }
        }
    }

    public class CompExplosiveRefuelable : ThingComp
    {
        public CompProperties_ExplosiveRefuelable Props => (CompProperties_ExplosiveRefuelable)props;
        private CompRefuelable refuelableComp;
        private bool deconstructing;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            refuelableComp = parent.TryGetComp<CompRefuelable>();
        }

        public override void ReceiveCompSignal(string signal)
        {
            if (signal == "StartDeconstruct")
            {
                deconstructing = true;
            }
            base.ReceiveCompSignal(signal);
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);

            if (deconstructing || mode == DestroyMode.Deconstruct)
            {
                return;
            }

            if (Props.requiresFuelForExplosion && (refuelableComp == null || refuelableComp.Fuel <= 0))
            {
                return;
            }

            TriggerExplosion(previousMap);
        }

        private void TriggerExplosion(Map map)
        {
            if (map == null) return;

            float fuelAmount = refuelableComp?.Fuel ?? 0f;
            float maxFuel = refuelableComp?.Props.fuelCapacity ?? 1f;
            float fuelRatio = fuelAmount / maxFuel;

            float explosionRadius = Mathf.Lerp(
                Props.minExplosionRadius,
                Props.maxExplosionRadius,
                fuelRatio
            );

            float clearRadius = Mathf.Lerp(
                Props.minClearRadius,
                Props.maxClearRadius,
                fuelRatio
            );

            int damage = Mathf.RoundToInt(fuelAmount * Props.explosionDamageFactor);

            // 清除掉落物
            ClearDropsInRadius(map, parent.Position, clearRadius);

            // === 立即创建中心点爆炸闪光 ===
            CreateCenterFlecks(map, fuelRatio);

            // === 立即创建中心点烟雾 ===
            CreateCenterSmoke(map, fuelRatio, explosionRadius);

            // 生成爆炸
            GenExplosion.DoExplosion(
                center: parent.Position,
                map: map,
                radius: explosionRadius,
                damType: Props.damageDef,
                instigator: parent,
                damAmount: Mathf.Max(10, damage),
                armorPenetration: -1f,
                weapon: null,
                projectile: null,
                intendedTarget: null,
                postExplosionSpawnThingDef: ThingDefOf.Filth_Fuel,
                postExplosionSpawnChance: 0.75f,
                postExplosionSpawnThingCount: Mathf.RoundToInt(fuelAmount * 0.1f),
                applyDamageToExplosionCellsNeighbors: true,
                chanceToStartFire: 0.8f
            );

            // === 延迟创建周围烟雾和闪光 ===
            CreateDelayedEffects(map, fuelRatio, explosionRadius);
        }

        // === 立即创建中心点爆炸闪光 ===
        private void CreateCenterFlecks(Map map, float fuelRatio)
        {
            const int CenterFleckCount = 10;
            const float MaxFleckSize = 50f;

            // 中心点10个超大闪光
            for (int i = 0; i < CenterFleckCount; i++)
            {
                // 基础位置（添加小偏移避免完全重叠）
                Vector3 spawnPos = parent.Position.ToVector3Shifted() + Gen.RandomHorizontalVector(0.3f);

                // 大小从10倍到50倍线性过渡
                float fleckSize = Mathf.Lerp(10f, MaxFleckSize, fuelRatio);

                // 添加随机变化 (80%-120%)
                fleckSize *= Rand.Range(0.8f, 1.2f);

                // 创建爆炸闪光
                FleckCreationData data = FleckMaker.GetDataStatic(
                    spawnPos,
                    map,
                    FleckDefOf.ExplosionFlash,
                    fleckSize
                );
                map.flecks.CreateFleck(data);
            }
        }

        // === 立即创建中心点烟雾 ===
        private void CreateCenterSmoke(Map map, float fuelRatio, float explosionRadius)
        {
            // 中心点烟雾数量（基于燃料比例）
            int centerSmokeCount = Mathf.RoundToInt(5 + fuelRatio * 5);
            float centerSmokeSize = 2f + fuelRatio * 3f;

            // 在中心点生成烟雾粒子
            for (int i = 0; i < centerSmokeCount; i++)
            {
                Vector3 spawnPos = parent.Position.ToVector3Shifted() + Gen.RandomHorizontalVector(0.5f);
                float smokeSize = centerSmokeSize * Rand.Range(0.8f, 1.2f);
                FleckMaker.ThrowSmoke(spawnPos, map, smokeSize);
            }
        }

        // === 延迟创建周围烟雾和闪光 ===
        private void CreateDelayedEffects(Map map, float fuelRatio, float explosionRadius)
        {
            // 创建延迟效果管理器
            DelayedEffectManager delayedEffects = map.GetComponent<DelayedEffectManager>();
            if (delayedEffects == null)
            {
                delayedEffects = new DelayedEffectManager(map);
                map.components.Add(delayedEffects);
            }

            // 添加周围闪光效果（0.5秒后开始出现）
            delayedEffects.AddDelayedAction(new DelayedEffect
            {
                Action = () => CreateSurroundingFlecks(map, fuelRatio, explosionRadius),
                DelayTicks = 10, // 0.5秒（60 ticks/秒）
                StartTick = Find.TickManager.TicksGame
            });

            // 添加周围烟雾效果（0.5秒后开始出现）
            delayedEffects.AddDelayedAction(new DelayedEffect
            {
                Action = () => CreateSurroundingSmoke(map, fuelRatio, explosionRadius),
                DelayTicks = 10,
                StartTick = Find.TickManager.TicksGame
            });
        }

        // === 创建周围爆炸闪光 ===
        private void CreateSurroundingFlecks(Map map, float fuelRatio, float explosionRadius)
        {
            // 额外闪光（数量与燃料量挂钩）
            int extraFleckCount = Mathf.RoundToInt(fuelRatio * 100); // 0-30个
            for (int i = 0; i < extraFleckCount; i++)
            {
                // 在爆炸范围内随机位置
                IntVec3 cell = parent.Position + GenRadial.RadialPattern[Rand.Range(0, GenRadial.NumCellsInRadius(explosionRadius))];
                if (!cell.InBounds(map)) continue;

                Vector3 spawnPos = cell.ToVector3Shifted() + Gen.RandomHorizontalVector(0.5f);

                // 大小从1倍到15倍
                float fleckSize = Mathf.Lerp(1f, 30f, fuelRatio) * Rand.Range(0.7f, 1.3f);

                // 创建爆炸闪光
                FleckCreationData data = FleckMaker.GetDataStatic(
                    spawnPos,
                    map,
                    FleckDefOf.ExplosionFlash,
                    fleckSize
                );
                map.flecks.CreateFleck(data);
            }
        }

        // === 创建周围烟雾效果 ===
        private void CreateSurroundingSmoke(Map map, float fuelRatio, float explosionRadius)
        {
            // 计算烟雾数量和大小
            int smokeCount = Mathf.RoundToInt(10 + explosionRadius * 5f);
            float baseSmokeSize = 0.7f + (explosionRadius / Props.maxExplosionRadius) * 3f;

            // 在爆炸范围内生成烟雾粒子
            for (int i = 0; i < smokeCount; i++)
            {
                // 在爆炸范围内随机位置（不包括中心点）
                IntVec3 randomCell = parent.Position + GenRadial.RadialPattern[Rand.Range(0, GenRadial.NumCellsInRadius(explosionRadius))];

                // 跳过中心点（中心点烟雾已经立即出现）
                if (randomCell.DistanceTo(parent.Position) < 3f) continue;

                if (!randomCell.InBounds(map)) continue;

                // 计算随机尺寸
                float smokeSize = baseSmokeSize * Rand.Range(0.7f, 13f);

                // 生成烟雾粒子
                Vector3 spawnPos = randomCell.ToVector3Shifted() + Gen.RandomHorizontalVector(0.5f);
                FleckMaker.ThrowSmoke(spawnPos, map, smokeSize);
            }
        }

        // === 清除半径内的掉落物 ===
        private void ClearDropsInRadius(Map map, IntVec3 center, float radius)
        {
            if (map == null || radius <= 0) return;

            int numCellsInRadius = GenRadial.NumCellsInRadius(radius);
            for (int i = 0; i < numCellsInRadius; i++)
            {
                IntVec3 cell = center + GenRadial.RadialPattern[i];
                if (!cell.InBounds(map)) continue;

                List<Thing> thingsAtCell = map.thingGrid.ThingsListAt(cell);
                for (int j = thingsAtCell.Count - 1; j >= 0; j--)
                {
                    Thing thing = thingsAtCell[j];
                    if (thing == parent) continue;
                    thing.Destroy(DestroyMode.Vanish);
                }
            }
        }
    }

    // === 延迟效果管理器 ===
    public class DelayedEffectManager : MapComponent
    {
        private List<DelayedEffect> delayedActions = new List<DelayedEffect>();

        public DelayedEffectManager(Map map) : base(map)
        {
        }

        public void AddDelayedAction(DelayedEffect effect)
        {
            delayedActions.Add(effect);
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();

            for (int i = delayedActions.Count - 1; i >= 0; i--)
            {
                DelayedEffect effect = delayedActions[i];
                int elapsedTicks = Find.TickManager.TicksGame - effect.StartTick;

                if (elapsedTicks >= effect.DelayTicks)
                {
                    try
                    {
                        effect.Action?.Invoke();
                    }
                    catch (System.Exception ex)
                    {
                        Log.Error($"Error executing delayed effect: {ex}");
                    }
                    delayedActions.RemoveAt(i);
                }
            }
        }
    }

    public struct DelayedEffect
    {
        public System.Action Action;
        public int DelayTicks;
        public int StartTick;
    }
}


namespace NCL
{
    // ===== 温度监控组件的属性定义 =====
    public class CompProperties_TemperatureMonitor : CompProperties
    {
        // 高温预警阈值 (°C)
        public float warningTemperature = 80f;

        // 临界温度阈值 (°C)
        public float criticalTemperature = 120f;

        // 温度检查间隔 (ticks)
        public int checkInterval = 250;

        public CompProperties_TemperatureMonitor()
        {
            compClass = typeof(CompTemperatureMonitor);
        }
    }

    // ===== 温度监控组件实现 =====
    public class CompTemperatureMonitor : ThingComp
    {
        public CompProperties_TemperatureMonitor Props =>
            (CompProperties_TemperatureMonitor)props;

        // 警报状态
        private enum AlertState
        {
            Normal,
            Warning,
            Critical
        }

        private AlertState currentState = AlertState.Normal;
        private int ticksSinceLastCheck = 0;
        public bool AlreadyDestroyed { get; private set; } = false;

        public override void CompTick()
        {
            base.CompTick();

            if (AlreadyDestroyed) return;

            ticksSinceLastCheck++;
            if (ticksSinceLastCheck >= Props.checkInterval)
            {
                ticksSinceLastCheck = 0;
                CheckTemperature();
            }
        }

        private void CheckTemperature()
        {
            if (!parent.Spawned || parent.Map == null) return;

            // 获取建筑所在房间的温度
            Room room = parent.GetRoom();
            if (room == null) return;

            float temperature = room.Temperature;
            AlertState newState;

            if (temperature >= Props.criticalTemperature)
            {
                newState = AlertState.Critical;
            }
            else if (temperature >= Props.warningTemperature)
            {
                newState = AlertState.Warning;
            }
            else
            {
                newState = AlertState.Normal;
            }

            // 状态变化处理
            if (newState != currentState)
            {
                currentState = newState;
            }

            // 临界温度处理
            if (currentState == AlertState.Critical)
            {
                DestroyStructure();
            }
        }

        private void DestroyStructure()
        {
            // 发送消息通知
            Messages.Message(
                "NCL.STRUCTURE_DESTROYED_HEAT".Translate(parent.LabelCap),
                parent,
                MessageTypeDefOf.NegativeEvent
            );

            // 销毁建筑
            parent.Destroy(DestroyMode.KillFinalize);
            AlreadyDestroyed = true;
        }

        // 在信息面板显示状态
        public override string CompInspectStringExtra()
        {
            if (!parent.Spawned || parent.Map == null || AlreadyDestroyed)
                return null;

            Room room = parent.GetRoom();
            if (room == null)
                return null;

            float temperature = room.Temperature;
            string status = "";

            if (temperature >= Props.criticalTemperature)
            {
                status = "NCL.CRITICAL_OVERHEAT".Translate();
            }
            else if (temperature >= Props.warningTemperature)
            {
                status = "NCL.HIGH_TEMP_WARNING".Translate();
            }

            // 核心修改：避免空行
            StringBuilder sb = new StringBuilder();
            if (!status.NullOrEmpty())
            {
                sb.AppendLine(status); // 只在有状态时添加
            }
            sb.Append("NCL.CURRENT_TEMP".Translate(temperature.ToString("F1")));
            return sb.ToString();
        }


        // 提供紧急关闭按钮
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (AlreadyDestroyed) yield break;

            foreach (Gizmo g in base.CompGetGizmosExtra())
            {
                yield return g;
            }

            if (currentState >= AlertState.Warning)
            {
                yield return new Command_Action
                {
                    icon = ContentFinder<Texture2D>.Get("UI/Commands/Halt"),
                    defaultLabel = "NCL.EMERGENCY_SHUTDOWN".Translate(),
                    defaultDesc = "NCL.EMERGENCY_SHUTDOWN_DESC".Translate(),
                    action = () => {
                        // 尝试关闭可关闭的组件
                        TryShutdownPowerComponents();

                        // 发送消息
                        Messages.Message(
                            "NCL.STRUCTURE_SHUTDOWN".Translate(parent.LabelCap),
                            parent,
                            MessageTypeDefOf.CautionInput
                        );
                    }
                };
            }
        }

        private void TryShutdownPowerComponents()
        {
            // 尝试关闭可调节功率的组件
            var powerAdjustable = parent.GetComp<CompPowerAdjustable>();
            if (powerAdjustable != null)
            {
                powerAdjustable.powerPercent = 0;
                powerAdjustable.UpdateDesiredPowerOutput();
            }

            // 尝试关闭电力组件
            var powerTrader = parent.GetComp<CompPowerTrader>();
            if (powerTrader != null)
            {
                powerTrader.PowerOn = false;
            }

            // 尝试关闭可关闭的组件
            var flickable = parent.GetComp<CompFlickable>();
            if (flickable != null)
            {
                flickable.SwitchIsOn = false;
            }
        }
    }

    // ===== 自定义警报系统 =====

    [StaticConstructorOnStartup]
    public class Alert_HighTemperature : Alert
    {
        private static readonly Color WarningColor = new Color(0.9f, 0.1f, 0.1f); // 橙色
        private static readonly Color CriticalColor = new Color(0.9f, 0.1f, 0.1f); // 红色

        public Alert_HighTemperature()
        {
            defaultPriority = AlertPriority.High;
        }

        private List<Thing> GetCriticalDevices()
        {
            List<Thing> result = new List<Thing>();
            foreach (Map map in Find.Maps)
            {
                foreach (Thing thing in map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial))
                {
                    var comp = thing.TryGetComp<CompTemperatureMonitor>();
                    if (comp != null && !comp.AlreadyDestroyed && thing.Spawned)
                    {
                        Room room = thing.GetRoom();
                        if (room != null && room.Temperature >= comp.Props.criticalTemperature)
                        {
                            result.Add(thing);
                        }
                    }
                }
            }
            return result;
        }

        private List<Thing> GetWarningDevices()
        {
            List<Thing> result = new List<Thing>();
            foreach (Map map in Find.Maps)
            {
                foreach (Thing thing in map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial))
                {
                    var comp = thing.TryGetComp<CompTemperatureMonitor>();
                    if (comp != null && !comp.AlreadyDestroyed && thing.Spawned)
                    {
                        Room room = thing.GetRoom();
                        if (room != null &&
                            room.Temperature >= comp.Props.warningTemperature &&
                            room.Temperature < comp.Props.criticalTemperature)
                        {
                            result.Add(thing);
                        }
                    }
                }
            }
            return result;
        }

        public override string GetLabel()
        {
            var criticalDevices = GetCriticalDevices();
            var warningDevices = GetWarningDevices();

            if (criticalDevices.Count > 0)
            {
                return "NCL.ALERT_CRITICAL_TEMP".Translate();
            }

            if (warningDevices.Count > 0)
            {
                return "NCL.ALERT_WARNING_TEMP".Translate();
            }

            return "";
        }

        public override TaggedString GetExplanation()
        {
            var criticalDevices = GetCriticalDevices();
            var warningDevices = GetWarningDevices();

            if (criticalDevices.Count > 0)
            {
                return "NCL.ALERT_CRITICAL_EXPLANATION".Translate(
                    string.Join("\n", criticalDevices.Select(d => d.LabelCap))
                );
            }

            if (warningDevices.Count > 0)
            {
                return "NCL.ALERT_WARNING_EXPLANATION".Translate(
                    string.Join("\n", warningDevices.Select(d => d.LabelCap))
                );
            }

            return "";
        }

        protected override Color BGColor
        {
            get
            {
                if (GetCriticalDevices().Count > 0)
                    return CriticalColor;

                if (GetWarningDevices().Count > 0)
                    return WarningColor;

                return Color.clear;
            }
        }

        public override AlertReport GetReport()
        {
            List<Thing> culprits = GetCriticalDevices().Concat(GetWarningDevices()).ToList();
            return culprits.Count > 0 ? AlertReport.CulpritsAre(culprits) : AlertReport.Inactive;
        }
    }
}