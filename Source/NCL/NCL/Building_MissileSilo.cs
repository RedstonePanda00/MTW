using NyarsModPackTwo;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Noise;
using Verse.Sound;

namespace NCL
{
    public class Building_MissileSilo : Building
    {
        public enum LaunchCondition
        {
            HighAngleProjectilesOnly,    // 前提1: 只有高角投射物
            AnyEnemyThreats             // 前提2: 任何敌方威胁
        }

        public LaunchCondition launchCondition = LaunchCondition.AnyEnemyThreats; // 默认使用前提二
        private enum LidState
        {
            Closed,    // 盖子关闭
            Opening,   // 正在打开
            Open,      // 盖子打开
            Closing    // 正在关闭
        }

        // 导弹发射相关配置
        private const int MissileCount = 24;          // 总导弹数量
        private const int LaunchPositionsX = 4;        // X轴发射位置数量
        private const int LaunchPositionsZ = 2;        // Z轴发射位置数量
        private const float LaunchOffset = 1.5f;       // 发射位置偏移量
        private const int LaunchDistance = 500;        // 初始发射距离

        public const int PowerPerShot = 100; // 每次发射消耗电量
        private CompPowerTrader powerComp; // 电力组件
        // 发射间隔（秒） - 添加范围限制
        public float launchInterval = 3.0f;
        public const int SteelPerShot = 20;

        public CompSteelResource steelComp;
        // 盖子相关
        private Graphic lidGraphic;
        private LidState lidState = LidState.Closed;
        private float lidOffset = -1.1f;
        private float targetLidOffset = -1.1f;
        private const float LidMoveSpeed = 0.025f;
        private Graphic _lidGraphic;
        // 发射状态
        private int nextMissileIndex = 0;
        private float launchTimer = 0f;

        public override void PostMake()
        {
            base.PostMake();
            steelComp = GetComp<CompSteelResource>();
        }
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            steelComp = GetComp<CompSteelResource>();
            powerComp = GetComp<CompPowerTrader>(); // 初始化电力组件
        }

        public bool HasEnoughPowerToFire()
        {
            if (powerComp == null)
                return true; // 没有电力组件，不需要电力

            // 检查电源是否开启
            if (!powerComp.PowerOn)
                return false;

            // 获取电网
            PowerNet powerNet = powerComp.PowerNet;
            if (powerNet == null)
                return false;

            // 检查电网中存储的总电量是否足够
            return GetTotalStoredEnergy(powerNet) >= PowerPerShot;
        }

        // 获取电网当前能量增益率
        private float PowerNetCurrentEnergyGainRate(PowerNet net)
        {
            return net.CurrentEnergyGainRate() / CompPower.WattsToWattDaysPerTick;
        }

        // 获取电网中存储的总电量
        private float GetTotalStoredEnergy(PowerNet net)
        {
            float total = 0f;
            if (net?.batteryComps != null)
            {
                foreach (CompPowerBattery battery in net.batteryComps)
                {
                    total += battery.StoredEnergy;
                }
            }
            return total;
        }

        // 从电网消耗电力
        private void ConsumePowerFromNet(float amount)
        {
            if (powerComp == null || powerComp.PowerNet == null)
                return;

            PowerNet net = powerComp.PowerNet;

            // 首选从电池消耗
            if (net.batteryComps != null)
            {
                foreach (CompPowerBattery battery in net.batteryComps)
                {
                    if (amount <= 0f) break;
                    if (battery.StoredEnergy > 0f)
                    {
                        float consume = Mathf.Min(amount, battery.StoredEnergy);
                        battery.DrawPower(consume);
                        amount -= consume;
                    }
                }
            }
        }

        private Graphic LidGraphic
        {
            get
            {
                if (_lidGraphic == null)
                {
                    // 确保在主线程加载图形
                    if (Spawned && Current.ProgramState == ProgramState.Playing)
                    {
                        _lidGraphic = GraphicDatabase.Get<Graphic_Single>(
                            "Buildings/NCL_MissileSilo_Top",
                            ShaderDatabase.Cutout,
                            this.def.graphicData.drawSize,
                            Color.white
                        );
                    }
                }
                return _lidGraphic;
            }
        }

        // 每帧更新
        protected override void Tick()
        {
            base.Tick();

            // 更新盖子位置
            if (lidState == LidState.Opening || lidState == LidState.Closing)
            {
                UpdateLidPosition();
            }

            // 在盖子开启状态下处理导弹发射
            if (lidState == LidState.Open)
            {
                launchTimer += 1f / 60f; // 每秒60帧，增加时间

                // 检查是否有足够资源
                bool hasEnoughSteel = steelComp != null && steelComp.HasEnoughResources(SteelPerShot);
                bool hasEnoughPower = HasEnoughPowerToFire();

                // 达到发射间隔且有敌人存在且资源充足
                if (launchTimer >= launchInterval && HasEnemyTargets() && hasEnoughSteel && hasEnoughPower)
                {
                    LaunchMissile();
                    launchTimer = 0f; // 重置计时器
                }
            }
        }


        // 更新盖子位置
        private void UpdateLidPosition()
        {
            lidOffset = Mathf.Lerp(lidOffset, targetLidOffset, LidMoveSpeed);

            if (Mathf.Abs(lidOffset - targetLidOffset) < 0.01f)
            {
                lidOffset = targetLidOffset;

                if (lidState == LidState.Opening) lidState = LidState.Open;
                else if (lidState == LidState.Closing) lidState = LidState.Closed;

                if (base.Spawned)
                {
                    base.Map.mapDrawer.MapMeshDirty(base.Position, MapMeshFlagDefOf.Things, true, false);
                }
            }
        }

        // 绘制建筑和盖子
        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            base.DrawAt(drawLoc, flip);

            // 使用属性而不是字段
            Graphic lidGraphic = LidGraphic;

            if (lidGraphic != null)
            {
                Vector3 lidPos = drawLoc;
                lidPos.y = AltitudeLayer.Filth.AltitudeFor();
                lidPos.z += lidOffset;
                lidGraphic.Draw(lidPos, this.Rotation, this);
            }
        }

        // 检测地图上是否有敌人
        // 修改 HasEnemyTargets() 方法
        private bool HasEnemyTargets()
        {
            if (!Spawned || Map == null || Faction == null)
                return false;

            // 根据当前设置的发射条件进行检查
            switch (launchCondition)
            {
                case LaunchCondition.HighAngleProjectilesOnly:
                    // 只有高角投射物时才发射
                    return HasHostileAirProjectiles();

                case LaunchCondition.AnyEnemyThreats:
                    // 任何敌方威胁时发射
                    return HasHostileAirProjectiles() || HasEnemyPawns();

                default:
                    return false;
            }
        }


        /// <summary>
        /// 检测是否存在敌方单位
        /// </summary>
        private bool HasEnemyPawns()
        {
            foreach (Pawn pawn in Map.mapPawns.AllPawnsSpawned)
            {
                if (IsHostilePawn(pawn))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 判断单位是否敌对
        /// </summary>
        private bool IsHostilePawn(Pawn pawn)
        {
            // 确保不是囚犯且未倒地
            bool isPrisonerOrDowned = pawn.IsPrisoner || pawn.Downed;

            return pawn != null &&
                   pawn.Spawned &&
                   pawn.Faction != null &&
                   Faction != null &&
                   Faction.HostileTo(pawn.Faction) &&
                   !isPrisonerOrDowned;
        }

        /// <summary>
        /// 检测是否存在敌方高空投射物
        /// </summary>
        private bool HasHostileAirProjectiles()
        {
            List<Thing> projectiles = Map.listerThings.ThingsInGroup(ThingRequestGroup.Projectile);
            foreach (Thing thing in projectiles)
            {
                Projectile projectile = thing as Projectile;
                if (projectile != null && IsHostileAirProjectile(projectile))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 判断是否是敌方高空投射物
        /// </summary>
        private bool IsHostileAirProjectile(Projectile projectile)
        {
            // 检查是否为高空投射物
            if (projectile.def.projectile == null || !projectile.def.projectile.flyOverhead)
                return false;

            // 检查发射者是否敌对
            Thing launcher = projectile.Launcher;
            if (launcher == null || launcher.Faction == null || Faction == null)
                return false;

            // 检查是否敌对
            return Faction.HostileTo(launcher.Faction);
        }

        // 获取下一个发射位置
        private Vector3 GetNextLaunchPosition()
        {
            // 计算导弹在网格中的位置 (4x2网格)
            int xIndex = nextMissileIndex % LaunchPositionsX;
            int zIndex = (nextMissileIndex / LaunchPositionsX) % LaunchPositionsZ;

            // 调整后的参数
            float horizontalOffsetFactor = 1.2f; // 减小水平分散（原1.5）
            float verticalOffsetFactor = 1.0f;   // 减小垂直分散（原1.5）
            float verticalShift = -1.5f;          // 整体下移0.3单位

            // 转换为世界坐标偏移
            Vector3 offset = new Vector3(
                (xIndex - LaunchPositionsX / 2f + 0.5f) * horizontalOffsetFactor,
                0f,
                (zIndex - LaunchPositionsZ / 2f + 0.5f) * verticalOffsetFactor + verticalShift
            );

            // 更新下一个导弹索引
            nextMissileIndex = (nextMissileIndex + 1) % MissileCount;

            // 返回实际位置
            return DrawPos + offset;
        }


        // 获取初始目标位置（正上方）
        private LocalTargetInfo GetInitialTarget(Vector3 spawnPos)
        {
            // 计算正上方100格的目标位置
            IntVec3 targetCell = new IntVec3(
                spawnPos.ToIntVec3().x,
                spawnPos.ToIntVec3().y,
                Mathf.Min(spawnPos.ToIntVec3().z + LaunchDistance, Map.Size.z - 1)
            );

            return new LocalTargetInfo(targetCell);
        }

        // 发射导弹
        private void LaunchMissile()
        {
            try
            {
                // 检查是否有足够钢铁
                if (steelComp == null || !steelComp.ConsumeResources(SteelPerShot))
                {
                    Messages.Message("Not enough steel to launch missile", this, MessageTypeDefOf.RejectInput);
                    return;
                }
                ConsumePowerFromNet(PowerPerShot);

                if (!Spawned || Map == null)
                {
                    return;
                }

                // 获取发射位置
                Vector3 launchPos = GetNextLaunchPosition();
                IntVec3 spawnCell = launchPos.ToIntVec3();

                if (!spawnCell.InBounds(Map))
                {
                    return;
                }

                // 创建导弹
                ThingDef missileDef = ThingDef.Named("Nyar_IronRain_Rocket");
                if (missileDef == null)
                {
                    return;
                }

                Thing missile = ThingMaker.MakeThing(missileDef);
                if (missile == null)
                {
                    return;
                }

                // 强制设置导弹关键属性
                missile.Position = spawnCell;

                // 生成导弹
                GenSpawn.Spawn(missile, spawnCell, Map);

                // 获取导弹组件
                Bullet_TracingEnemies tracingMissile = missile as Bullet_TracingEnemies;
                if (tracingMissile != null)
                {
                    // 确保初始位置正确
                    tracingMissile.trackingPosNow = spawnCell.ToVector3Shifted();

                    // 通过反射初始化关键字段
                    InitializeMissileFields(tracingMissile, missileDef);
                }

                // 发射导弹
                Projectile projectile = missile as Projectile;
                if (projectile == null)
                {
                    return;
                }

                projectile.Launch(
                    this,                                       // 发射者
                    spawnCell.ToVector3Shifted(),               // 起始位置
                    GetInitialTarget(launchPos),                // 初始目标位置
                    LocalTargetInfo.Invalid,                    // 预定目标（由导弹自身追踪）
                    ProjectileHitFlags.All,                     // 碰撞标志
                    false,                                      // 防止友军伤害
                    null,                                       // 装备
                    null                                        // 目标掩护定义
                );

                // 播放效果
                FleckMaker.ThrowSmoke(launchPos, Map, 1.5f);
                FleckMaker.ThrowLightningGlow(launchPos, Map, 1.2f);
                SoundDef.Named("MissileLauncher_Fire").PlayOneShot(new TargetInfo(Position, Map));

                // 调试信息
                if (Prefs.DevMode)
                {
                    // 添加调试日志
                }
            }
            catch (Exception ex)
            {
                // 错误处理 - 记录错误，但不要让游戏崩溃
                Log.Error($"Error launching missile: {ex}");
            }
        }





        private void InitializeMissileFields(Bullet_TracingEnemies missile, ThingDef missileDef)
        {
            try
            {
                Type missileType = typeof(Bullet_TracingEnemies);

                // 设置初始飞行角度
                FieldInfo flyingAngleField = missileType.GetField("flyingAngle", BindingFlags.Public | BindingFlags.Instance);
                if (flyingAngleField != null) flyingAngleField.SetValue(missile, 0f);

                // 设置高度 - 关键修正
                FieldInfo trackingPosField = missileType.GetField("trackingPosNow", BindingFlags.Public | BindingFlags.Instance);
                if (trackingPosField != null)
                {
                    Vector3 correctedPos = missile.Position.ToVector3Shifted();
                    correctedPos.y = missileDef.Altitude; // 使用导弹定义的高度
                    trackingPosField.SetValue(missile, correctedPos);
                }

                // 初始化飞行时间
                FieldInfo flyingTimeField = missileType.GetField("_flyingTime", BindingFlags.NonPublic | BindingFlags.Instance);
                if (flyingTimeField != null) flyingTimeField.SetValue(missile, 0);

                // 初始化属性
                FieldInfo propsField = missileType.GetField("_props", BindingFlags.NonPublic | BindingFlags.Instance);
                if (propsField != null && propsField.GetValue(missile) == null)
                {
                    propsField.SetValue(missile, missileDef.GetModExtension<ModExtension_BulletProperties>());
                }

                // 初始化缓存
                FieldInfo cacheField = missileType.GetField("_localTargetCache", BindingFlags.NonPublic | BindingFlags.Instance);
                if (cacheField != null && cacheField.GetValue(missile) == null)
                {
                    cacheField.SetValue(missile, new List<Thing>());
                }

                // 初始化反射参数
                FieldInfo interceptParamsField = missileType.GetField("_interceptParams", BindingFlags.NonPublic | BindingFlags.Instance);
                if (interceptParamsField != null && interceptParamsField.GetValue(missile) == null)
                {
                    interceptParamsField.SetValue(missile, new object[2]);
                }
            }
            catch (Exception ex)
            {
            }
        }


        // 激活/关闭发射井盖子
        private void ToggleSilo()
        {
            if (lidState == LidState.Closed || lidState == LidState.Closing)
            {
                lidState = LidState.Opening;
                targetLidOffset = 1.3f;
                launchTimer = 0f; // 激活时重置发射计时器
            }
            else if (lidState == LidState.Open || lidState == LidState.Opening)
            {
                lidState = LidState.Closing;
                targetLidOffset = -1.1f;
            }
        }

        // 在 Building_MissileSilo 类中添加辅助方法
        private string GetLaunchConditionDescription()
        {
            switch (launchCondition)
            {
                case LaunchCondition.HighAngleProjectilesOnly:
                    return "NCL.ConditionDesc.HighAngleOnly".Translate();

                case LaunchCondition.AnyEnemyThreats:
                    return "NCL.ConditionDesc.AnyEnemyThreat".Translate();

                default:
                    return string.Empty;
            }
        }

        private void LaunchConditionSelector()
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();

            options.Add(new FloatMenuOption(
                "NCL.Condition.HighAngleOnly".Translate(),
                () => launchCondition = LaunchCondition.HighAngleProjectilesOnly,
                MenuOptionPriority.Default,
                null, null, 0f, null, null
            ));

            options.Add(new FloatMenuOption(
                "NCL.Condition.AnyEnemyThreat".Translate(),
                () => launchCondition = LaunchCondition.AnyEnemyThreats,
                MenuOptionPriority.Default,
                null, null, 0f, null, null
            ));

            Find.WindowStack.Add(new FloatMenu(options));
        }

        // 添加控制按钮
        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo baseGizmo in base.GetGizmos())
            {
                yield return baseGizmo;
            }
            if (steelComp != null)
            {
                yield return new SteelResourceGizmo(steelComp);
            }
            yield return new Command_Action
            {
                icon = ContentFinder<Texture2D>.Get("ModIcon/NowIsTheTime", true),
                defaultLabel = (lidState == LidState.Closed || lidState == LidState.Closing) ?
                    "NCL.ActivateSilo".Translate() : "NCL.DeactivateSilo".Translate(),
                defaultDesc = (lidState == LidState.Closed || lidState == LidState.Closing) ?
                    "NCL.ActivateSiloDesc".Translate() : "NCL.DeactivateSiloDesc".Translate(),
                action = ToggleSilo
            };

            yield return new Command_Action
            {
                icon = ContentFinder<Texture2D>.Get("ModIcon/Adjustment", true),
                defaultLabel = "NCL.LaunchCondition".Translate(),
                defaultDesc = GetLaunchConditionDescription(),
                action = LaunchConditionSelector,
                disabledReason = "NCL.MustBeDisabledWhenActive".Translate()
            };

            // 发射间隔调节按钮
            yield return new Command_Action
            {
                icon = ContentFinder<Texture2D>.Get("ModIcon/FrequencyChange", true),
                defaultLabel = "NCL.LaunchInterval".Translate(launchInterval.ToString("F1")),
                defaultDesc = "NCL.LaunchIntervalDesc".Translate(),
                action = () => {
                    Find.WindowStack.Add(new Dialog_FloatSlider(
                        "NCL.SetLaunchInterval".Translate(), // 窗口标题
                        0.1f,  // 最小值0.1秒 (最快10发/秒)
                        3.0f,  // 最大值3.0秒 (最慢3秒/发)
                        val => launchInterval = val,
                        launchInterval
                    ));
                }
            };
        }

        // 自定义浮点数滑块对话框
        public class Dialog_FloatSlider : Window
        {
            private readonly Action<float> callback;
            private readonly string label;
            private readonly float min;
            private readonly float max;
            private float value;

            public Dialog_FloatSlider(
                string label,
                float min,
                float max,
                Action<float> callback,
                float startingValue)
            {
                this.label = label;
                this.min = min;
                this.max = max;
                this.callback = callback;
                this.value = startingValue;

                // 窗口设置
                forcePause = true;            // 游戏暂停
                absorbInputAroundWindow = true; // 阻止背景交互
                closeOnClickedOutside = true;  // 点击外部关闭窗口
            }

            public override Vector2 InitialSize => new Vector2(400f, 150f);

            public override void DoWindowContents(Rect inRect)
            {
                // 标题
                Text.Font = GameFont.Small;
                Rect labelRect = new Rect(inRect.x, inRect.y, inRect.width, 30f);
                Widgets.Label(labelRect, label);

                // 滑块
                Rect sliderRect = new Rect(inRect.x + 10f, inRect.y + 40f, inRect.width - 20f, 30f);
                value = Widgets.HorizontalSlider(
                    sliderRect,
                    value,
                    min,
                    max,
                    label: "NCL.CurrentValue".Translate(value.ToString("F1")),
                    leftAlignedLabel: "NCL.MinValue".Translate(min.ToString("F1")),
                    rightAlignedLabel: "NCL.MaxValue".Translate(max.ToString("F1")),
                    roundTo: 0.1f
                );

                // 确认按钮
                Rect buttonRect = new Rect(inRect.width / 2f - 50f, inRect.y + 80f, 100f, 30f);
                if (Widgets.ButtonText(buttonRect, "OK"))
                {
                    callback?.Invoke(value);
                    Close();
                }
            }
        }


        // 保存和加载状态
        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref launchCondition, "launchCondition", LaunchCondition.AnyEnemyThreats);
            Scribe_Values.Look(ref lidState, "lidState", LidState.Closed);
            Scribe_Values.Look(ref lidOffset, "lidOffset", -1.5f);
            Scribe_Values.Look(ref targetLidOffset, "targetLidOffset", -1.5f);
            Scribe_Values.Look(ref nextMissileIndex, "nextMissileIndex", 0);
            Scribe_Values.Look(ref launchTimer, "launchTimer", 0f);
            Scribe_Values.Look(ref launchInterval, "launchInterval", 3.0f);
        }

        // 在场景中显示发射位置（开发模式）
        public override void DrawExtraSelectionOverlays()
        {
            base.DrawExtraSelectionOverlays();

            if (DebugSettings.godMode && Spawned)
            {
                // 绘制所有发射位置
                for (int i = 0; i < MissileCount; i++)
                {
                    Vector3 pos = GetNextLaunchPosition();
                    GenDraw.DrawCircleOutline(pos, 0.5f, SimpleColor.Red);

                    // 绘制发射方向
                    IntVec3 targetCell = GetInitialTarget(pos).Cell;
                    GenDraw.DrawLineBetween(pos, targetCell.ToVector3Shifted(), SimpleColor.Green);
                }
            }
        }
    }
}

namespace NCL
{
    // 钢铁资源组件（完全复制 CompDroneCarrier 逻辑）
    public class CompSteelResource : ThingComp, IThingHolder
    {
        public bool AutoFill
        {
            get => autoFill;
            set => autoFill = value;
        }

        public bool HasEnoughResources(int amount)
        {
            return IngredientCount >= amount;
        }
        public CompProperties_SteelResource Props => (CompProperties_SteelResource)props;
        public int IngredientCount => innerContainer?.TotalStackCountOfDef(Props.fixedIngredient) ?? 0;
        public int AmountToAutofill => Mathf.Max(0, maxToFill - IngredientCount);
        public float FillPercentage => (float)IngredientCount / Props.maxIngredientCount;

        public int MaxToFill
        {
            get => maxToFill;
            set => maxToFill = Mathf.Clamp(value, 0, Props.maxIngredientCount);
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (!respawningAfterLoad && innerContainer == null)
            {
                innerContainer = new ThingOwner<Thing>(this);
                if (Props.startingIngredientCount > 0)
                {
                    AddIngredient(Props.fixedIngredient, Props.startingIngredientCount);
                }
                maxToFill = Props.startingIngredientCount;
            }
        }

        public void AddIngredient(ThingDef ingredientDef, int amount)
        {
            if (innerContainer == null) return;

            int num = Mathf.Min(amount, Props.maxIngredientCount - IngredientCount);
            if (num <= 0) return;

            Thing thing = ThingMaker.MakeThing(ingredientDef);
            thing.stackCount = num;
            innerContainer.TryAdd(thing, true);
        }

        public bool ConsumeResources(int amount)
        {
            int num = amount;
            List<Thing> list = new List<Thing>(innerContainer);
            foreach (Thing thing in list)
            {
                if (thing.def != Props.fixedIngredient || thing.stackCount <= 0) continue;

                int num2 = Mathf.Min(thing.stackCount, num);
                if (num2 >= thing.stackCount)
                {
                    innerContainer.Remove(thing);
                    thing.Destroy(DestroyMode.Vanish);
                }
                else
                {
                    Thing thing2 = thing.SplitOff(num2);
                    thing2?.Destroy(DestroyMode.Vanish);
                }
                num -= num2;
                if (num <= 0) break;
            }
            return num <= 0;
        }

        public void EjectResources()
        {
            if (innerContainer == null || innerContainer.Count == 0)
                return;

            // 在原地吐出所有资源
            innerContainer.TryDropAll(parent.Position, parent.Map, ThingPlaceMode.Near);

            // 重置目标量为0
            maxToFill = 0;



        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Deep.Look(ref innerContainer, "innerContainer", this);
            Scribe_Values.Look(ref maxToFill, "maxToFill", 0, false);
            Scribe_Values.Look(ref autoFill, "autoFill", true, false);
        }

        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
        }

        public ThingOwner GetDirectlyHeldThings() => innerContainer;

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);
            innerContainer?.ClearAndDestroyContents(DestroyMode.Vanish);
        }

        public ThingOwner innerContainer;
        public int maxToFill;
        public bool autoFill = true;
    }

    // 钢铁资源组件属性（完全复制 CompProperties_DroneCarrier 逻辑）
    public class CompProperties_SteelResource : CompProperties
    {
        public CompProperties_SteelResource()
        {
            compClass = typeof(CompSteelResource); 
        }
        public string fixedIngredientDefName = "Steel"; // 默认值

        [Unsaved]
        public ThingDef fixedIngredient;

        public int maxIngredientCount = 5000;
        public int startingIngredientCount = 1000;
    }

    // 钢铁资源 Gizmo（完全复制 DroneCarrierGizmo 逻辑）
    [StaticConstructorOnStartup]
    public class SteelResourceGizmo : Gizmo
    {
        private readonly CompSteelResource comp;
        private float lastTargetValue;
        private float targetValue;
        private static bool draggingBar;
        private List<float> bandPercentages;

        private static readonly Texture2D ClearIcon = ContentFinder<Texture2D>.Get("UI/Icons/EjectContentFromAtomizer", true);

        public SteelResourceGizmo(CompSteelResource comp)
        {
            this.comp = comp;
            this.targetValue = (float)comp.maxToFill / (float)comp.Props.maxIngredientCount;
            this.bandPercentages = new List<float>();
            int maxIngredientCount = comp.Props.maxIngredientCount;

            if (maxIngredientCount >= 50)
            {
                int num = 50;
                int num2 = maxIngredientCount / num;
                for (int i = 0; i <= num2; i++)
                {
                    float item = Mathf.Clamp01((float)(i * num) / (float)maxIngredientCount);
                    bandPercentages.Add(item);
                }
            }
            else
            {
                int num3 = 12;
                for (int j = 0; j <= num3; j++)
                {
                    bandPercentages.Add((float)j / (float)num3);
                }
            }
        }

        public override float GetWidth(float maxWidth) => 160f;

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            Rect rect = new Rect(topLeft.x, topLeft.y, 160f, 75f);
            Rect rect2 = rect.ContractedBy(10f);
            Widgets.DrawWindowBackground(rect);
            Text.Font = GameFont.Small;

            // 添加清空按钮 - 右上角
            Rect clearButtonRect = new Rect(rect.x + rect.width - 25f, rect.y + 5f, 20f, 20f);
            if (Widgets.ButtonImage(clearButtonRect, ClearIcon, Color.white, Color.grey))
            {
                // 确认对话框
                Find.WindowStack.Add(new Dialog_MessageBox(
                    "ConfirmClearSteel".Translate(comp.IngredientCount),
                    "Confirm".Translate(),
                    () => comp.EjectResources(),
                    "Cancel".Translate(),
                    null,
                    "ClearSteelTitle".Translate()
                ));
                return new GizmoResult(GizmoState.Interacted);
            }
            TooltipHandler.TipRegion(clearButtonRect, "ClearSteelTooltip".Translate());

            // 标题区域
            string text = comp.Props.fixedIngredient.LabelCap;
            float height = Text.CalcHeight(text, rect2.width);
            Rect rect3 = new Rect(rect2.x, rect2.y, rect2.width, height);

            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(rect3, text);
            Text.Anchor = TextAnchor.UpperLeft;

            lastTargetValue = targetValue;
            float num = rect2.height - rect3.height;
            float num2 = num - 4f;
            float num3 = (num - num2) / 2f;

            Rect rect4 = new Rect(rect2.x, rect3.yMax + num3, rect2.width, num2);
            Widgets.DraggableBar(rect4, BarTex, BarHighlightTex, EmptyBarTex, DragBarTex,
                ref draggingBar, comp.FillPercentage, ref targetValue, bandPercentages, 24, 0f, 1f);

            Text.Anchor = TextAnchor.MiddleCenter;
            rect4.y -= 2f;
            string label = $"{comp.IngredientCount} / {comp.Props.maxIngredientCount} ";
            Widgets.Label(rect4, label);
            Text.Anchor = TextAnchor.UpperLeft;

            TooltipHandler.TipRegion(rect4, () => GetResourceBarTip(), Gen.HashCombineInt(comp.GetHashCode(), 34242369));

            if (lastTargetValue != targetValue)
            {
                comp.maxToFill = Mathf.RoundToInt(targetValue * comp.Props.maxIngredientCount);
            }

            return new GizmoResult(GizmoState.Clear);
        }

        private string GetResourceBarTip()
        {
            return $"Steel Storage: {comp.IngredientCount}/{comp.Props.maxIngredientCount}\n"
                 + $"Consumes {Building_MissileSilo.SteelPerShot} steel per missile";
        }

        private static readonly Texture2D BarTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.34f, 0.42f, 0.43f));
        private static readonly Texture2D BarHighlightTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.43f, 0.54f, 0.55f));
        private static readonly Texture2D EmptyBarTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.03f, 0.035f, 0.05f));
        private static readonly Texture2D DragBarTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.74f, 0.97f, 0.8f));
        private const int Increments = 24;
    }

    // 填充钢铁的工作驱动（复制 JobDriver_FillDroneCarrier 逻辑）
    public class JobDriver_FillMissileSilo : JobDriver
    {
        protected Thing Silo => job.GetTarget(TargetIndex.A).Thing;
        protected Thing Resource => job.GetTarget(TargetIndex.B).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(Silo, job, 1, -1, null, errorOnFailed)
                && pawn.Reserve(Resource, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOnBurningImmobile(TargetIndex.A);
            this.FailOn(() => Silo.TryGetComp<CompSteelResource>().AmountToAutofill <= 0);

            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch)
                .FailOnSomeonePhysicallyInteracting(TargetIndex.B);

            yield return Toils_Haul.StartCarryThing(TargetIndex.B);

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);

            yield return Toils_General.Wait(25).WithProgressBarToilDelay(TargetIndex.A);

            yield return new Toil
            {
                initAction = () =>
                {
                    Thing resource = pawn.CurJob.GetTarget(TargetIndex.B).Thing;
                    int fillAmount = Mathf.Min(resource.stackCount, Silo.TryGetComp<CompSteelResource>().AmountToAutofill);

                    if (fillAmount > 0)
                    {
                        Silo.TryGetComp<CompSteelResource>().AddIngredient(resource.def, fillAmount);
                        resource.SplitOff(fillAmount).Destroy(DestroyMode.Vanish);
                    }
                }
            };
        }
    }

    public class WorkGiver_HaulSteelToMissileSilo : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest =>
            ThingRequest.ForGroup(ThingRequestGroup.BuildingArtificial);

        public override PathEndMode PathEndMode => PathEndMode.Touch;

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (pawn == null || t == null || !t.Spawned || t.IsBurning())
                return false;

            // 检查是否是我们需要的建筑类型
            if (t.def.defName != "NCL_Building_MissileSilo" && t.def.defName != "NCL_Eagle_Artillery_Building")
                return false;

            CompSteelResource comp = t.TryGetComp<CompSteelResource>();
            if (comp == null || !comp.AutoFill)
                return false;

            int amount = comp.AmountToAutofill;
            if (amount <= 0)
                return false;

            if (!pawn.CanReserve(t, 1, -1, null, forced))
                return false;

            ThingDef steelDef = comp.Props.fixedIngredient;

            // 快速最近单堆查找（廉价）
            Predicate<Thing> validator = (Thing x) =>
            {
                if (x == null || x.IsForbidden(pawn) || x.IsBurning())
                    return false;

                if (!pawn.CanReserve(x, 1, -1, null, forced))
                    return false;

                return true;
            };

            Thing nearest = GenClosest.ClosestThingReachable(
                pawn.Position,
                pawn.Map,
                ThingRequest.ForDef(steelDef),
                PathEndMode.ClosestTouch,
                TraverseParms.For(pawn),
                9999f,
                validator);

            return nearest != null;
        }


        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            CompSteelResource comp = t.TryGetComp<CompSteelResource>();
            if (comp == null)
                return null;

            int amount = comp.AmountToAutofill;
            if (amount <= 0)
                return null;

            List<Thing> resources = HaulAIUtility.FindFixedIngredientCount(
                pawn,
                comp.Props.fixedIngredient,
                amount);

            if (resources.NullOrEmpty())
                return null;

            // 再次确认资源可操作（避免瞬间被别人拿走）
            if (!pawn.CanReserve(resources[0], 1, -1, null, forced))
                return null;

            if (!pawn.CanReach(resources[0], PathEndMode.ClosestTouch, Danger.Deadly))
                return null;

            Job job = HaulAIUtility.HaulToContainerJob(pawn, resources[0], t);
            job.count = Mathf.Min(job.count, amount);

            // 多堆资源队列
            if (resources.Count > 1)
            {
                job.targetQueueB = new List<LocalTargetInfo>();
                for (int i = 1; i < resources.Count; i++)
                    job.targetQueueB.Add(resources[i]);
            }

            return job;
        }


        private List<Thing> FindResources(Pawn pawn, ThingDef resourceDef, int amountNeeded)
        {
            return HaulAIUtility.FindFixedIngredientCount(pawn, resourceDef, amountNeeded);
        }
    }
}

