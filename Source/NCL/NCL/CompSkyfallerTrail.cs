using NCL;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace NCL
{
    public class CompProperties_SkyfallerTrail : CompProperties
    {
        // 每隔多少 tick 生成一次特效
        public int intervalTicks = 2;

        // 每次生成的粒子数量范围
        public IntRange fleckCountRange = new IntRange(2, 4);

        // 尾焰/火花用的 FleckDef
        public FleckDef mainFleck = FleckDefOf.FireGlow;

        // 接近终点时用的 Fleck（可选）
        public FleckDef lateFleck = FleckDefOf.MicroSparks;

        // 使用 lateFleck 的飞行进度阈值（0~1）
        public float latePhaseThreshold = 0.7f;

        // 粒子缩放倍率
        public FloatRange scaleRange = new FloatRange(0.4f, 0.6f);

        // 粒子速度
        public FloatRange speedRange = new FloatRange(0.05f, 0.15f);

        // 粒子随机角度
        public FloatRange angleOffsetRange = new FloatRange(-180f, 180f);

        // 粒子旋转速率
        public FloatRange rotationRateRange = new FloatRange(-30f, 30f);

        // 生成烟雾的概率
        public float smokeChance = 0.7f;

        // 烟雾大小倍率
        public float smokeScaleFactor = 1.2f;

        // 是否只在飞行中间阶段生成（避免刚生成或快消失时）
        public bool limitToMidFlight = true;

        // 中间阶段的范围（0~1，DistanceCoveredFraction 或 TimeInAnimation）
        public FloatRange midFlightRange = new FloatRange(0.1f, 0.95f);

        public CompProperties_SkyfallerTrail()
        {
            this.compClass = typeof(CompSkyfallerTrail);
        }
    }
}




namespace NCL
{
    public class CompSkyfallerTrail : ThingComp
    {
        public CompProperties_SkyfallerTrail Props => (CompProperties_SkyfallerTrail)props;

        private int tickCounter;

        // 缓存 Skyfaller 引用（可选，但方便）
        private Skyfaller skyfallerParent;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            skyfallerParent = parent as Skyfaller;
        }

        public override void CompTick()
        {
            base.CompTick();

            // Skyfaller 才处理
            if (skyfallerParent == null || parent.Map == null)
                return;

            tickCounter++;
            if (tickCounter < Props.intervalTicks)
                return;

            tickCounter = 0;

            // 计算飞行进度，用 Skyfaller 的 TimeInAnimation 或 age/ticksToImpact
            float progress = GetFlightProgress(skyfallerParent);

            // 限制只在中间阶段生成
            if (Props.limitToMidFlight &&
                (progress < Props.midFlightRange.min || progress > Props.midFlightRange.max))
            {
                return;
            }

            EmitTrailFlecks(progress);
        }

        /// <summary>
        /// 计算飞行进度（0~1）。这里复用 Skyfaller 的 TimeInAnimation 逻辑。
        /// 如果你想更精细，可以直接访问 skyfaller 的 ticksToImpact / ticksToImpactMax。
        /// </summary>
        private float GetFlightProgress(Skyfaller sf)
        {
            // 仿照 Skyfaller.TimeInAnimation 的实现
            // 这里不能直接访问 private 字段，只能用近似方式：
            // ageTicks / LeaveMapAfterTicks 之类的。
            // 如果你愿意，可以用反射去读私有字段，这里先用一个简单近似。

            // 简单版本：用 ageTicks / LeaveMapAfterTicks 估算
            int leaveTicks = sf.LeaveMapAfterTicks;
            if (leaveTicks <= 0) leaveTicks = 220;
            float progress = Mathf.Clamp01((float)sf.ageTicks / leaveTicks);
            return progress;
        }

        private void EmitTrailFlecks(float progress)
        {
            Map map = parent.Map;
            if (map == null) return;

            // 关键：使用 DrawPos，而不是 Position
            Vector3 pos = parent.DrawPos;
            // 一般 Skyfaller 的 Graphic 已经会把 y 设置为合适的高度，这里可以覆盖一下
            pos.y = parent.def.Altitude;

            // 选择使用哪种 fleck（前半段用 mainFleck，后半段用 lateFleck）
            FleckDef fleckDef = Props.mainFleck;
            if (progress > Props.latePhaseThreshold && Props.lateFleck != null)
                fleckDef = Props.lateFleck;

            if (fleckDef == null)
                return;

            int count = Props.fleckCountRange.RandomInRange;

            // 计算导弹/坠物的“前进方向”：这里用 Rotation 或者从 Skyfaller 的角度推
            // 简化：用 parent.Rotation 的朝向
            float baseAngle = parent.Rotation.AsAngle;

            for (int i = 0; i < count; i++)
            {
                float scale = Props.scaleRange.RandomInRange;
                float angle = baseAngle + Props.angleOffsetRange.RandomInRange;
                float speed = Props.speedRange.RandomInRange;
                float rotRate = Props.rotationRateRange.RandomInRange;

                try
                {
                    FleckCreationData data = FleckMaker.GetDataStatic(pos, map, fleckDef, scale);
                    data.velocityAngle = angle;
                    data.velocitySpeed = speed;
                    data.rotationRate = rotRate;
                    map.flecks.CreateFleck(data);

                    // 随机烟雾
                    if (Rand.Chance(Props.smokeChance))
                    {
                        FleckMaker.ThrowSmoke(pos, map, scale * Props.smokeScaleFactor);
                    }
                }
                catch (Exception ex)
                {
                    if (Prefs.DevMode)
                        Log.Error($"[CompSkyfallerTrail] 生成粒子时出错: {ex}");
                }
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref tickCounter, "tickCounter", 0);
        }
    }
}




namespace NCL
{
    public class Building_SkyfallerLauncher : Building
    {
        //=========== 常量和字段 ===========
        private enum LidState { Closed, Opening, Open, Closing }

        private LidState lidState = LidState.Closed;
        private float lidOffsetZ = 0f;
        private const float LidMoveSpeed = 0.08f;
        private const float LidOpenOffsetZ = -1.5f;
        private const float LidClosedOffsetZ = 0f;
        private int lidOpeningTimer = 0;
        private const int LidOpeningDuration = 30 * GenTicks.TicksPerRealSecond;

        private Graphic lidGraphic;
        private CompPowerTrader powerComp; // 用于检查电力状态


        private Material glowMaterial;
        private Material secondStageGlowMaterial;
        private Material completeStageGlowMaterial;
        private MaterialPropertyBlock mpb;

        private const float MaxAlpha = 0.5f;

        private float firstStageFactor = 0f;
        private float secondStageFactor = 0f;
        private float completeStageFactor = 0f;

        private const float Fade5sPerTick = 1f / (5f * GenTicks.TicksPerRealSecond);
        private const float CompleteFadeInPerTick = 1f / (1f * GenTicks.TicksPerRealSecond);
        private const float CompleteFadeOutPerTick = 1f / (3f * GenTicks.TicksPerRealSecond);

        private int closingDelayTimer = 0;
        private const int ClosingDelayDuration = 3 * GenTicks.TicksPerRealSecond;

        private int cooldownTicksRemaining = 0;
        private const int CooldownDuration = 30 * GenTicks.TicksPerRealSecond;

        private const int BurstCount = 3;
        private const int BurstIntervalTicks = (int)(2.1f * GenTicks.TicksPerRealSecond);

        private int openTicks = 0;
        private const int MaxOpenDurationTicks = 5 * 60 * GenTicks.TicksPerRealSecond;

        private int sleepTicksRemaining = 0;
        private const int SleepDurationTicks = 60000;

        private const int SteelPerShot = 10;

        private CompSteelResource steelComp;

        private bool aimingInProgress = false;
        private int aimTicksRemaining = 0;
        private const int AimDurationTicks = 5 * GenTicks.TicksPerRealSecond;

        private bool burstInProgress = false;
        private int burstShotsDone = 0;
        private int burstTicksUntilNextShot = 0;

        private LocalTargetInfo currentTarget = LocalTargetInfo.Invalid;

        private Material firingEffectMaterial;
        private float firingEffectAlpha = 0f;
        private int firingEffectTicks = 0;

        private const float FiringEffectDurationSeconds = 0.6f;
        private const float FiringEffectFadeSeconds = 0.2f;

        private int FiringEffectDurationTicks =>
            (int)(FiringEffectDurationSeconds * GenTicks.TicksPerRealSecond);
        private float FiringEffectFadePerTick =>
            1f / (FiringEffectFadeSeconds * GenTicks.TicksPerRealSecond);

        private CompGlower dynamicGlower;
        private bool glowerRegistered = false;
        private float currentGlowRadius = 0f;
        private const float MaxGlowRadius = 11f;

        private const int GlowFadeDurationSeconds = 12;
        private int GlowFadeDurationTicks => GlowFadeDurationSeconds * GenTicks.TicksPerRealSecond;

        private const int GlowFadeOutDurationSeconds = 5;
        private int GlowFadeOutDurationTicks => GlowFadeOutDurationSeconds * GenTicks.TicksPerRealSecond;

        private static readonly ColorInt GlowRedColor = new ColorInt(255, 0, 0, 0);

        private const float BaseSize = 6f;
        private const float CurrentSize = 7f;
        private float SizeScale => CurrentSize / BaseSize;

        private int effectTicksRemaining = 0;
        private const int EffectIntervalTicks = 5 * GenTicks.TicksPerRealSecond;
        private Effecter activeEffecter;

        private bool globalAimingInProgress = false; // 是否正在瞄准全屏打击
        private int globalAimTicksRemaining = 0; // 全屏打击瞄准时间
        private const int GlobalAimDurationTicks = 5 * GenTicks.TicksPerRealSecond; // 全屏打击瞄准时间
        //=========== 新增跨地图字段和常量 ===========
        private const int BurstCountGlobal = 3;
        private const int LaunchIntervalTicksGlobal = 24;
        private const int LaunchIntervalTicks = 24;
        private const int ArrivalIntervalTicks = 18;
        private const float ScatterRadius = 8f;

        private static readonly SimpleCurve ScatterDistanceChanceFactor = new SimpleCurve
        {
            new CurvePoint(0f, 1f),
            new CurvePoint(1f, 0.1f)
        };

        private int pendingLaunchTicks = -1;
        private int pendingShotsToLaunch;
        private int pendingDestinationTile = -1;
        private IntVec3 pendingTargetCell = IntVec3.Invalid;
        private List<IntVec3> pendingImpactOffsets;

        //=========== 初始化 ===========
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            steelComp = GetComp<CompSteelResource>();
            powerComp = GetComp<CompPowerTrader>(); // 初始化电力组件
        }

        private bool HasPower()
        {
            return powerComp == null || powerComp.PowerOn; // 如果没有电力组件，视为始终有电；否则检查电力状态
        }


        //=========== Gizmos ===========
        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var gizmo in base.GetGizmos())
                yield return gizmo;

            // 添加钢铁资源 Gizmo
            if (steelComp != null)
            {
                yield return new SteelResourceGizmo(steelComp);
            }

            // 如果瞄准或全屏打击瞄准正在进行，则只显示取消按钮
            if (aimingInProgress || globalAimingInProgress)
            {
                yield return new Command_Action
                {
                    defaultLabel = "NCL.CancelAiming".Translate(),
                    defaultDesc = "NCL.CancelAimingDesc".Translate(),
                    icon = ContentFinder<Texture2D>.Get("ModIcon/Cancel", true),
                    action = CancelAiming
                };
                yield break; // 直接退出，不显示其他按钮
            }

            // 井盖开关 Gizmo
            yield return new Command_Action
            {
                icon = ContentFinder<Texture2D>.Get("ModIcon/NowIsTheTime", true),
                defaultLabel = (lidState == LidState.Closed || lidState == LidState.Closing)
                    ? "NCL.OpenLid".Translate()
                    : "NCL.CloseLid".Translate(),
                defaultDesc = (lidState == LidState.Closed || lidState == LidState.Closing)
                    ? "NCL.OpenLidDesc".Translate()
                    : "NCL.CloseLidDesc".Translate(),
                action = ToggleLid
            };

            // 冷却中 Gizmo
            if (cooldownTicksRemaining > 0)
            {
                yield return new Command_Action
                {
                    defaultLabel = "NCL.CoolingDownLabel".Translate(TicksToSeconds(cooldownTicksRemaining)),
                    defaultDesc = "NCL.CoolingDownDesc".Translate(),
                    icon = ContentFinder<Texture2D>.Get("ModIcon/NCL_Eagle_Artillery_Shell_Cooldown", true),
                    action = () => { }, // 冷却中按钮不可点击
                };
                yield break; // 直接退出，不显示其他按钮
            }

            // 局部攻击 Gizmo
            if (!burstInProgress && lidState == LidState.Open)
            {
                yield return new Command_Action
                {
                    defaultLabel = "NCL.LaunchSkyfaller".Translate(),
                    defaultDesc = "NCL.LaunchSkyfallerDesc".Translate(),
                    icon = ContentFinder<Texture2D>.Get("ModIcon/NCL_Eagle_Artillery_Shell_Aimming", true),
                    action = StartChoosingTarget
                };

                // 全球攻击 Gizmo
                if (steelComp != null && steelComp.HasEnoughResources(SteelPerShot * BurstCountGlobal))
                {
                    yield return new Command_Action
                    {
                        defaultLabel = "NCL.LaunchSkyfallerGlobal".Translate(),
                        defaultDesc = "NCL.LaunchSkyfallerGlobalDesc".Translate(),
                        icon = ContentFinder<Texture2D>.Get("ModIcon/WorldTarget", true),
                        action = StartChoosingWorldTarget
                    };
                }
            }
        }



        //=========== 井盖控制 ===========
        private void ToggleLid()
        {
            if (!HasPower())
            {
                Messages.Message("NCL.NoPower".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }

            if (sleepTicksRemaining > 0)
            {
                Messages.Message("The artillery is in sleep mode and cannot be activated.", MessageTypeDefOf.RejectInput);
                return;
            }

            if (lidState == LidState.Closed || lidState == LidState.Closing)
            {
                lidState = LidState.Opening;
                lidOpeningTimer = LidOpeningDuration;
                openTicks = 0;
            }
            else if (lidState == LidState.Open || lidState == LidState.Opening)
            {
                aimingInProgress = false;
                burstInProgress = false;
                currentTarget = LocalTargetInfo.Invalid;
                closingDelayTimer = ClosingDelayDuration;
                lidState = LidState.Closing;
                sleepTicksRemaining = SleepDurationTicks;
                CleanupEffecter();
            }
        }


        private void UpdateLidAndGlows()
        {
            if (lidState == LidState.Opening)
            {
                firstStageFactor = Mathf.Clamp01(firstStageFactor + Fade5sPerTick);

                if (lidOpeningTimer <= 15 * GenTicks.TicksPerRealSecond)
                    secondStageFactor = Mathf.Clamp01(secondStageFactor + Fade5sPerTick);

                UpdateDynamicGlowerDuringStartup();

                if (lidOpeningTimer == GenTicks.TicksPerRealSecond)
                {
                    SoundDef.Named("NCL_Artillery_Activated").PlayOneShot(new TargetInfo(Position, Map));
                }

                lidOpeningTimer--;

                if (lidOpeningTimer <= 0)
                {
                    lidOffsetZ = Mathf.Lerp(lidOffsetZ, LidOpenOffsetZ, LidMoveSpeed);
                    completeStageFactor = Mathf.Clamp01(completeStageFactor + CompleteFadeInPerTick);

                    if (Mathf.Abs(lidOffsetZ - LidOpenOffsetZ) < 0.01f)
                    {
                        lidOffsetZ = LidOpenOffsetZ;
                        lidState = LidState.Open;
                        EnsureDynamicGlowerAtMax();
                    }
                }
            }
            else if (lidState == LidState.Closing)
            {
                closingDelayTimer--;

                if (closingDelayTimer <= 0)
                {
                    lidOffsetZ = Mathf.Lerp(lidOffsetZ, LidClosedOffsetZ, LidMoveSpeed);
                }

                firstStageFactor = Mathf.Clamp01(firstStageFactor - Fade5sPerTick * (5f / 3f));
                secondStageFactor = Mathf.Clamp01(secondStageFactor - Fade5sPerTick * (5f / 3f));
                completeStageFactor = Mathf.Clamp01(completeStageFactor - CompleteFadeOutPerTick);

                UpdateDynamicGlowerDuringClose();

                if (Mathf.Abs(lidOffsetZ - LidClosedOffsetZ) < 0.01f)
                {
                    lidOffsetZ = LidClosedOffsetZ;
                    lidState = LidState.Closed;

                    firstStageFactor = 0f;
                    secondStageFactor = 0f;
                    completeStageFactor = 0f;

                    DisableDynamicGlower();
                }
            }
        }

        //=========== 动态 Glower 逻辑 ===========
        private void UpdateDynamicGlowerDuringStartup()
        {
            if (Map == null)
                return;

            if (lidOpeningTimer > GlowFadeDurationTicks)
                return;

            float t = 1f - Mathf.Clamp01(lidOpeningTimer / (float)GlowFadeDurationTicks);
            float targetRadius = MaxGlowRadius * t;

            if (dynamicGlower == null)
            {
                dynamicGlower = new CompGlower();
                dynamicGlower.parent = this;
                dynamicGlower.props = new CompProperties_Glower
                {
                    glowRadius = 0f,
                    glowColor = GlowRedColor
                };
            }

            if (Mathf.Approximately(currentGlowRadius, targetRadius) && glowerRegistered)
                return;

            currentGlowRadius = targetRadius;
            ((CompProperties_Glower)dynamicGlower.props).glowRadius = currentGlowRadius;
            ((CompProperties_Glower)dynamicGlower.props).glowColor = GlowRedColor;

            if (!glowerRegistered)
            {
                Map.glowGrid.RegisterGlower(dynamicGlower);
                glowerRegistered = true;
            }
            else
            {
                Map.glowGrid.DeRegisterGlower(dynamicGlower);
                Map.glowGrid.RegisterGlower(dynamicGlower);
            }
        }

        private void UpdateDynamicGlowerDuringClose()
        {
            if (Map == null || !glowerRegistered || dynamicGlower == null || dynamicGlower.props == null)
                return;

            float t = Mathf.Clamp01(closingDelayTimer / (float)GlowFadeOutDurationTicks);
            float targetRadius = MaxGlowRadius * t;

            if (Mathf.Approximately(currentGlowRadius, targetRadius))
                return;

            currentGlowRadius = targetRadius;
            ((CompProperties_Glower)dynamicGlower.props).glowRadius = currentGlowRadius;

            Map.glowGrid.DeRegisterGlower(dynamicGlower);
            Map.glowGrid.RegisterGlower(dynamicGlower);
        }

        private void EnsureDynamicGlowerAtMax()
        {
            if (Map == null)
                return;

            if (dynamicGlower == null)
            {
                dynamicGlower = new CompGlower
                {
                    parent = this,
                    props = new CompProperties_Glower
                    {
                        glowRadius = MaxGlowRadius,
                        glowColor = GlowRedColor
                    }
                };
            }
            else
            {
                ((CompProperties_Glower)dynamicGlower.props).glowRadius = MaxGlowRadius;
                ((CompProperties_Glower)dynamicGlower.props).glowColor = GlowRedColor;
            }

            currentGlowRadius = MaxGlowRadius;

            if (!glowerRegistered)
            {
                Map.glowGrid.RegisterGlower(dynamicGlower);
                glowerRegistered = true;
            }
            else
            {
                Map.glowGrid.DeRegisterGlower(dynamicGlower);
                Map.glowGrid.RegisterGlower(dynamicGlower);
            }
        }

        private void DisableDynamicGlower()
        {
            if (glowerRegistered && Map != null && dynamicGlower != null)
            {
                Map.glowGrid.DeRegisterGlower(dynamicGlower);
            }
            glowerRegistered = false;
            currentGlowRadius = 0f;
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            DisableDynamicGlower();
            CleanupEffecter();
            base.DeSpawn(mode);
        }

        //=========== 目标选择 & 瞄准 ===========
        private void StartChoosingTarget()
        {
            if (!HasPower())
            {
                Messages.Message("NCL.NoPower".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }

            if (cooldownTicksRemaining > 0 || aimingInProgress || globalAimingInProgress || burstInProgress || lidState != LidState.Open)
                return;

            // 在开始选择目标之前，清空之前的目标
            currentTarget = LocalTargetInfo.Invalid;

            // 设置目标选择参数
            var parms = new TargetingParameters
            {
                canTargetLocations = true,
                canTargetPawns = true,
                canTargetBuildings = true
            };

            // 启动目标选择器
            Find.Targeter.BeginTargeting(parms, delegate (LocalTargetInfo target)
            {
                if (!target.IsValid) return;

                // 用户选择了目标后，进入瞄准状态
                currentTarget = target;
                aimingInProgress = true; // 标记为前摇瞄准状态
                aimTicksRemaining = AimDurationTicks;

                // 播放瞄准音效
                SoundDef.Named("NCL_Artillery_Aimming").PlayOneShot(new TargetInfo(Position, Map));
            });
        }


        private void CancelAiming()
        {
            if (aimingInProgress)
            {
                aimingInProgress = false;
                // 清空目标
                currentTarget = LocalTargetInfo.Invalid;
            }

            if (globalAimingInProgress)
            {
                globalAimingInProgress = false;
                pendingShotsToLaunch = 0; // 清空发射数量
            }
        }



        private void CancelTarget()
        {
            currentTarget = LocalTargetInfo.Invalid;
            burstInProgress = false;
        }

        //=========== Tick 逻辑 ===========
        private void PlayEffect()
        {
            if (Map == null || !Spawned)
                return;

            if (activeEffecter == null)
            {
                activeEffecter = NCLDefOf.NCL_ShellFortFightingeffects.Spawn();
            }

            activeEffecter.Trigger(new TargetInfo(Position, Map), TargetInfo.Invalid);
        }

        private IntVec3 GetTargetCell(LocalTargetInfo target)
        {
            if (target.HasThing && target.Thing is Pawn pawn && pawn.Spawned)
                return pawn.Position;
            return target.Cell;
        }

        private void CleanupEffecter()
        {
            if (activeEffecter != null)
            {
                activeEffecter.Cleanup();
                activeEffecter = null;
            }
        }

        //=========== 发射 Skyfaller ===========
        private void LaunchSkyfallers(IntVec3 targetCell)
        {
            if (!HasPower())
            {
                Messages.Message("NCL.NoPower".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }

            if (steelComp != null)
            {
                if (!steelComp.HasEnoughResources(SteelPerShot))
                {
                    Messages.Message("NCL.NotEnoughSteel".Translate(SteelPerShot), MessageTypeDefOf.RejectInput);
                    return;
                }

                if (!steelComp.ConsumeResources(SteelPerShot))
                {
                    Messages.Message("NCL.NotEnoughSteel".Translate(SteelPerShot), MessageTypeDefOf.RejectInput);
                    return;
                }
            }

            LaunchSingleSkyfaller(NCLDefOf.NCL_Eagle_Artillery_Shell_Up, Position);
            LaunchSingleSkyfaller(NCLDefOf.NCL_Eagle_Artillery_Shell_Down, targetCell);

            SoundDef.Named("NCL_Artillery_Firing").PlayOneShot(new TargetInfo(Position, Map));
            firingEffectAlpha = 1f;
            firingEffectTicks = FiringEffectDurationTicks;
        }


        private void LaunchSingleSkyfaller(ThingDef skyfallerDef, IntVec3 cell)
        {
            if (Map == null || skyfallerDef == null)
            {
                return;
            }

            Skyfaller skyfaller = (Skyfaller)ThingMaker.MakeThing(skyfallerDef);
            GenSpawn.Spawn(skyfaller, cell, Map, WipeMode.Vanish);
        }
    

        //=========== 绘制 ===========
        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            base.DrawAt(drawLoc, flip);

            if (lidGraphic == null)
            {
                lidGraphic = GraphicDatabase.Get<Graphic_Single>(
                    "Buildings/NCL_Eagle_Artillery_Building_Top",
                    ShaderDatabase.Cutout,
                    new Vector2(CurrentSize, CurrentSize),
                    Color.white);
            }

            if (lidGraphic != null)
            {
                Vector3 lidPos = drawLoc;
                lidPos.y = AltitudeLayer.BuildingOnTop.AltitudeFor();
                lidPos.z += lidOffsetZ;
                lidGraphic.Draw(lidPos, Rotation, this);
            }

            if (mpb == null) mpb = new MaterialPropertyBlock();

            if (glowMaterial == null)
                glowMaterial = MaterialPool.MatFrom("Motes/NCL_Eagle_Artillery_Starting_Glow", ShaderDatabase.Transparent);

            if (glowMaterial != null && firstStageFactor > 0f)
            {
                Vector3 pos = drawLoc;
                pos.y = AltitudeLayer.MoteOverhead.AltitudeFor();
                pos.z += lidOffsetZ;

                Color c = new Color(1f, 1f, 1f, firstStageFactor * MaxAlpha);
                mpb.SetColor(ShaderPropertyIDs.Color, c);
                Graphics.DrawMesh(MeshPool.plane10,
                    Matrix4x4.TRS(pos, Quaternion.identity, new Vector3(6f * SizeScale, 1f, 6f * SizeScale)),
                    glowMaterial, 0, null, 0, mpb);
            }

            if (secondStageGlowMaterial == null)
                secondStageGlowMaterial = MaterialPool.MatFrom("Motes/NCL_Eagle_Artillery_Starting_Glow_SecondStage", ShaderDatabase.Transparent);

            if (secondStageGlowMaterial != null && secondStageFactor > 0f)
            {
                Vector3 pos = drawLoc;
                pos.y = AltitudeLayer.MoteOverhead.AltitudeFor();
                Color c = new Color(1f, 1f, 1f, secondStageFactor * MaxAlpha);
                mpb.SetColor(ShaderPropertyIDs.Color, c);
                Graphics.DrawMesh(MeshPool.plane10,
                    Matrix4x4.TRS(pos, Quaternion.identity, new Vector3(6f * SizeScale, 1f, 6f * SizeScale)),
                    secondStageGlowMaterial, 0, null, 0, mpb);
            }

            if (completeStageGlowMaterial == null)
                completeStageGlowMaterial = MaterialPool.MatFrom("Motes/NCL_Eagle_Artillery_Starting_Glow_Complete", ShaderDatabase.Transparent);

            if (completeStageGlowMaterial != null && completeStageFactor > 0f)
            {
                Vector3 pos = drawLoc;
                pos.y = AltitudeLayer.MoteOverhead.AltitudeFor();
                Color c = new Color(1f, 1f, 1f, completeStageFactor * MaxAlpha);
                mpb.SetColor(ShaderPropertyIDs.Color, c);
                Graphics.DrawMesh(MeshPool.plane10,
                    Matrix4x4.TRS(pos, Quaternion.identity, new Vector3(6f * SizeScale, 1f, 6f * SizeScale)),
                    completeStageGlowMaterial, 0, null, 0, mpb);
            }

            if (firingEffectAlpha > 0f)
            {
                if (firingEffectMaterial == null)
                {
                    firingEffectMaterial = MaterialPool.MatFrom(
                        "Motes/NCL_Eagle_Artillery_Starting_Firing",
                        ShaderDatabase.Transparent);
                }

                Vector3 firingEffectPos = drawLoc;
                firingEffectPos.y = AltitudeLayer.MoteOverhead.AltitudeFor();
                firingEffectPos.z += 2.1f * SizeScale;

                Color firingColor = new Color(1f, 1f, 1f, firingEffectAlpha);
                mpb.SetColor(ShaderPropertyIDs.Color, firingColor);

                Graphics.DrawMesh(
                    MeshPool.plane10,
                    Matrix4x4.TRS(
                        firingEffectPos,
                        Quaternion.identity,
                        new Vector3(4f * SizeScale, 1f, 4f * SizeScale)
                    ),
                    firingEffectMaterial,
                    0,
                    null,
                    0,
                    mpb);
            }
        }

        //=========== 信息面板字符串 ===========
        public override string GetInspectString()
        {
            List<string> statusParts = new List<string>();

            if (lidState == LidState.Opening)
            {
                float secondsRemaining = lidOpeningTimer / (float)GenTicks.TicksPerRealSecond;
                statusParts.Add("NCL.LidOpeningCountdown".Translate(secondsRemaining.ToString("F1")));
            }
            else if (lidState == LidState.Open)
            {
                statusParts.Add("NCL.LidOpened".Translate());

                float operationSecondsRemaining = (MaxOpenDurationTicks - openTicks) / (float)GenTicks.TicksPerRealSecond;
                statusParts.Add("NCL.LidRemainingTime".Translate(operationSecondsRemaining.ToString("F1")));
            }
            else if (lidState == LidState.Closing)
            {
                float secondsRemaining = closingDelayTimer / (float)GenTicks.TicksPerRealSecond;
                statusParts.Add("NCL.LidClosingCountdown".Translate(secondsRemaining.ToString("F1")));
            }

            if (aimingInProgress)
            {
                float aimSecondsRemaining = aimTicksRemaining / (float)GenTicks.TicksPerRealSecond;
                statusParts.Add("NCL.AimingRemainingTime".Translate(aimSecondsRemaining.ToString("F1")));
            }

            if (cooldownTicksRemaining > 0)
            {
                float cooldownSecondsRemaining = cooldownTicksRemaining / (float)GenTicks.TicksPerRealSecond;
                statusParts.Add("NCL.CooldownRemainingTime".Translate(cooldownSecondsRemaining.ToString("F1")));
            }

            if (sleepTicksRemaining > 0)
            {
                float sleepSecondsRemaining = sleepTicksRemaining / (float)GenTicks.TicksPerRealSecond;
                statusParts.Add("Sleep Mode: " + sleepSecondsRemaining.ToString("F1") + "s remaining.");
            }

            if (currentTarget.IsValid)
            {
                if (currentTarget.HasThing)
                {
                    statusParts.Add($"Target: {currentTarget.Thing.LabelCap} (ID: {currentTarget.Thing.thingIDNumber})");
                }
                else
                {
                    statusParts.Add($"Target: {currentTarget.Cell} (Location)");
                }
            }

            if (pendingLaunchTicks >= 0)
            {
                statusParts.Add($"Artillery strike queued: {pendingShotsToLaunch} rounds remaining");
            }

            string baseString = base.GetInspectString();
            if (!string.IsNullOrEmpty(baseString))
            {
                statusParts.Insert(0, baseString);
            }

            return string.Join("\n", statusParts.Where(part => !string.IsNullOrWhiteSpace(part)));
        }

        //=========== ExposeData 方法 ===========
        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref lidState, "lidState", LidState.Closed);
            Scribe_Values.Look(ref lidOffsetZ, "lidOffsetZ", LidClosedOffsetZ);

            Scribe_Values.Look(ref cooldownTicksRemaining, "cooldownTicksRemaining", 0);

            Scribe_Values.Look(ref burstInProgress, "burstInProgress", false);
            Scribe_Values.Look(ref burstShotsDone, "burstShotsDone", 0);
            Scribe_Values.Look(ref burstTicksUntilNextShot, "burstTicksUntilNextShot", 0);

            Scribe_Values.Look(ref aimingInProgress, "aimingInProgress", false);
            Scribe_Values.Look(ref aimTicksRemaining, "aimTicksRemaining", 0);

            Scribe_TargetInfo.Look(ref currentTarget, "currentTarget");

            Scribe_Values.Look(ref lidOpeningTimer, "lidOpeningTimer", 0);
            Scribe_Values.Look(ref closingDelayTimer, "closingDelayTimer", 0);

            Scribe_Values.Look(ref openTicks, "openTicks", 0);
            Scribe_Values.Look(ref sleepTicksRemaining, "sleepTicksRemaining", 0);

            Scribe_Values.Look(ref firstStageFactor, "firstStageFactor", 0f);
            Scribe_Values.Look(ref secondStageFactor, "secondStageFactor", 0f);
            Scribe_Values.Look(ref completeStageFactor, "completeStageFactor", 0f);
            Scribe_Values.Look(ref firingEffectAlpha, "firingEffectAlpha", 0f);
            Scribe_Values.Look(ref firingEffectTicks, "firingEffectTicks", 0);

            Scribe_Values.Look(ref currentGlowRadius, "currentGlowRadius", 0f);
            Scribe_Values.Look(ref glowerRegistered, "glowerRegistered", false);

            // 跨地图状态
            Scribe_Values.Look(ref pendingLaunchTicks, "pendingLaunchTicks", -1);
            Scribe_Values.Look(ref pendingShotsToLaunch, "pendingShotsToLaunch", 0);
            Scribe_Values.Look(ref pendingDestinationTile, "pendingDestinationTile", -1);
            Scribe_Values.Look(ref pendingTargetCell, "pendingTargetCell");
            Scribe_Collections.Look(ref pendingImpactOffsets, "pendingImpactOffsets", LookMode.Value);

            if (Scribe.mode == LoadSaveMode.PostLoadInit && pendingImpactOffsets == null)
            {
                pendingImpactOffsets = new List<IntVec3>();
            }
        }

        //=========== Tick 方法 ===========
        protected override void Tick()
        {
            base.Tick();

            // 注册动态光源
            if (glowerShouldBeRegistered && !glowerRegistered && dynamicGlower != null && Map != null)
            {
                Map.glowGrid.RegisterGlower(dynamicGlower);
                glowerRegistered = true;
            }

            // 更新井盖状态和光源
            UpdateLidAndGlows();

            // 处理井盖打开状态
            if (lidState == LidState.Open)
            {
                openTicks++;
                if (openTicks >= MaxOpenDurationTicks)
                {
                    ToggleLid();
                }
            }

            // 处理睡眠计时
            if (sleepTicksRemaining > 0)
            {
                sleepTicksRemaining--;
            }

            // 处理冷却计时
            if (cooldownTicksRemaining > 0)
            {
                cooldownTicksRemaining--;
            }

            // 更新开火特效
            if (firingEffectTicks > 0)
            {
                firingEffectTicks--;

                if (firingEffectTicks > 0)
                {
                    firingEffectAlpha -= FiringEffectFadePerTick;
                    if (firingEffectAlpha < 0f)
                        firingEffectAlpha = 0f;
                }
                else
                {
                    firingEffectAlpha = 0f;
                }
            }

            // 播放效果
            if (lidState == LidState.Open)
            {
                if (effectTicksRemaining <= 0)
                {
                    PlayEffect();
                    effectTicksRemaining = EffectIntervalTicks;
                }
                else
                {
                    effectTicksRemaining--;
                }
            }

            // 处理效果器逻辑
            if (activeEffecter != null)
            {
                activeEffecter.EffectTick(new TargetInfo(Position, Map), TargetInfo.Invalid);
            }

            // 处理瞄准中的逻辑（正常攻击）
            if (aimingInProgress)
            {
                if (aimTicksRemaining > 0)
                {
                    aimTicksRemaining--;
                    return;
                }

                aimingInProgress = false;
                if (currentTarget.IsValid && lidState == LidState.Open)
                {
                    burstInProgress = true;
                    burstShotsDone = 0;
                    burstTicksUntilNextShot = 0;
                }
                else
                {
                    currentTarget = LocalTargetInfo.Invalid;
                }
            }

            // 处理瞄准中的逻辑（全屏打击）
            if (globalAimingInProgress)
            {
                if (globalAimTicksRemaining > 0)
                {
                    globalAimTicksRemaining--;
                    return;
                }

                globalAimingInProgress = false;

                // 启动全屏打击
                if (pendingShotsToLaunch > 0 && lidState == LidState.Open)
                {
                    pendingLaunchTicks = 0; // 初始化发射计时器
                }
                else
                {
                    pendingShotsToLaunch = 0; // 清空发射数量
                }
            }

            // 处理局部攻击逻辑
            // 处理局部攻击逻辑
            if (burstInProgress && lidState == LidState.Open)
            {
                if (burstTicksUntilNextShot > 0)
                {
                    burstTicksUntilNextShot--;
                    return;
                }

                if (burstShotsDone < BurstCount && currentTarget.IsValid)
                {
                    IntVec3 targetCell = GetTargetCell(currentTarget);
                    LaunchSkyfallers(targetCell);
                    burstShotsDone++;

                    if (burstShotsDone < BurstCount)
                    {
                        burstTicksUntilNextShot = BurstIntervalTicks;
                    }
                    else
                    {
                        burstInProgress = false;
                        cooldownTicksRemaining = CooldownDuration; // 进入冷却

                        // **新增代码：清空目标**
                        currentTarget = LocalTargetInfo.Invalid;
                    }
                }
                else
                {
                    burstInProgress = false;

                    // **新增代码：清空目标**
                    currentTarget = LocalTargetInfo.Invalid;
                }
            }


            // 处理全屏打击逻辑
            if (pendingLaunchTicks >= 0)
            {
                if (pendingLaunchTicks > 0)
                {
                    pendingLaunchTicks--;
                    return;
                }

                if (pendingShotsToLaunch > 0)
                {
                    if (!HasPower())
                    {
                        Messages.Message("NCL.NoPower".Translate(), MessageTypeDefOf.RejectInput);
                        pendingShotsToLaunch = 0; // 停止全屏打击
                        return;
                    }

                    if (steelComp != null)
                    {
                        if (!steelComp.HasEnoughResources(SteelPerShot))
                        {
                            Messages.Message("NCL.NotEnoughSteel".Translate(SteelPerShot), MessageTypeDefOf.RejectInput);
                            pendingShotsToLaunch = 0; // 停止全屏打击
                            return;
                        }

                        if (!steelComp.ConsumeResources(SteelPerShot))
                        {
                            Messages.Message("NCL.NotEnoughSteel".Translate(SteelPerShot), MessageTypeDefOf.RejectInput);
                            pendingShotsToLaunch = 0; // 停止全屏打击
                            return;
                        }
                    }

                    // 发射一枚炮弹
                    LaunchSingleSkyfaller(NCLDefOf.NCL_Eagle_Artillery_Shell_Up, Position);

                    // 播放发射音效
                    NCLDefOf.NCL_Artillery_Firing?.PlayOneShot(new TargetInfo(Position, Map));

                    // 更新剩余发射数量和计时器
                    pendingShotsToLaunch--;
                    pendingLaunchTicks = (pendingShotsToLaunch > 0) ? BurstIntervalTicks : -1;

                    // 如果发射完成，则进入冷却
                    if (pendingShotsToLaunch <= 0)
                    {
                        cooldownTicksRemaining = CooldownDuration;
                    }
                }
                else
                {
                    pendingLaunchTicks = -1;
                }
            }
        }







        private string TicksToSeconds(int ticks)
        {
            return (ticks / GenTicks.TicksPerRealSecond).ToString("F1");
        }

        //=========== 跨地图目标选择 ===========
        private void StartChoosingWorldTarget()
        {
            if (!HasPower())
            {
                Messages.Message("NCL.NoPower".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }

            if (!Spawned || Map == null || lidState != LidState.Open || cooldownTicksRemaining > 0 || aimingInProgress || globalAimingInProgress)
            {
                Messages.Message("NCL.CannotLaunchNow".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }

            globalAimingInProgress = true; // 标记为正在瞄准全屏打击
            globalAimTicksRemaining = GlobalAimDurationTicks;

            SoundDef.Named("NCL_Artillery_Aimming").PlayOneShot(new TargetInfo(Position, Map));

            CameraJumper.TryJump(CameraJumper.GetWorldTarget(this));
            Find.WorldSelector.ClearSelection();
            int sourceTile = Map.Tile;

            Find.WorldTargeter.BeginTargeting(
                target => ChoseWorldTarget(target, sourceTile),
                true,
                null,
                true,
                () => GenDraw.DrawWorldRadiusRing(sourceTile, 9999),
                target => GetTargetingLabel(target));
        }



        private bool ChoseWorldTarget(GlobalTargetInfo target, int sourceTile)
        {
            if (!target.IsValid)
            {
                Messages.Message("MessageTransportPodsDestinationIsInvalid".Translate(), MessageTypeDefOf.RejectInput, false);
                return false;
            }

            MapParent mapParent = target.WorldObject as MapParent ?? Find.WorldObjects.MapParentAt(target.Tile);
            if (mapParent == null || !mapParent.HasMap || mapParent.Map == null)
            {
                Messages.Message("InvalidTarget".Translate(), MessageTypeDefOf.RejectInput, false);
                return false;
            }

            Map originalMap = Map;
            Map targetMap = mapParent.Map;
            Current.Game.CurrentMap = targetMap;
            Find.Targeter.BeginTargeting(
                TargetingParameters.ForDropPodsDestination(),
                localTarget => ConfirmTargetCell(mapParent.Tile, localTarget.Cell),
                null,
                () =>
                {
                    if (Find.Maps.Contains(originalMap))
                    {
                        Current.Game.CurrentMap = originalMap;
                    }
                },
                CompLaunchable.TargeterMouseAttachment,
                true);
            return true;
        }

        private string GetTargetingLabel(GlobalTargetInfo target)
        {
            MapParent mapParent = target.WorldObject as MapParent ?? Find.WorldObjects.MapParentAt(target.Tile);
            if (mapParent != null && mapParent.HasMap)
            {
                return "ClickToSeeAvailableOrders_WorldObject".Translate(mapParent.LabelCap);
            }

            return "InvalidTarget".Translate();
        }

        private void ConfirmTargetCell(int destinationTile, IntVec3 targetCell)
        {
            if (!Spawned || Map == null)
            {
                return;
            }

            Map destinationMap = Current.Game.FindMap(destinationTile);
            if (destinationMap == null)
            {
                Log.Warning($"[NCL] Eagle artillery target map missing for tile {destinationTile} during target confirmation.");
                Messages.Message("InvalidTarget".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }
            // 播放瞄准音效
            SoundDef.Named("NCL_Artillery_Aimming").PlayOneShot(new TargetInfo(Position, Map));
            // 生成随机散布偏移
            List<IntVec3> impactOffsets = GenerateImpactOffsets(BurstCountGlobal, ScatterRadius);

            // 设置跨地图打击参数
            pendingDestinationTile = destinationTile;
            pendingTargetCell = targetCell;
            pendingImpactOffsets = impactOffsets;
            pendingShotsToLaunch = BurstCountGlobal; // 设置总发射数量
            pendingLaunchTicks = 0; // 初始化发射计时器

            // 创建世界对象用于显示打击效果
            WorldObject_SkyfallerArtillery worldObject = (WorldObject_SkyfallerArtillery)WorldObjectMaker.MakeWorldObject(NCLDefOf.NCL_WorldObject_SkyfallerArtillery);
            worldObject.Tile = Map.Tile;
            worldObject.destinationTile = destinationTile;
            worldObject.targetCell = targetCell;
            worldObject.burstIntervalTicks = ArrivalIntervalTicks;
            worldObject.projectileDef = NCLDefOf.NCL_Eagle_Artillery_Shell_Down;
            worldObject.launcherLabel = LabelCap;
            worldObject.SetImpactOffsets(impactOffsets);

            Find.WorldObjects.Add(worldObject);

            Log.Message($"[NCL] Eagle artillery strike queued from tile {Map.Tile} to tile {destinationTile} at cell {targetCell} with {impactOffsets.Count} impacts.");

            // 跳回炮塔视角
            CameraJumper.TryJump(this);
        }






        private bool glowerShouldBeRegistered = false;
        private static List<IntVec3> GenerateImpactOffsets(int count, float radius)
        {
            List<IntVec3> offsets = new List<IntVec3>(count);
            IEnumerable<IntVec3> cells = GenRadial.RadialCellsAround(IntVec3.Zero, radius, true)
                .Where(cell => cell.x != 0 || cell.z != 0 || offsets.Count == 0)
                .Distinct();

            for (int i = 0; i < count; i++)
            {
                IntVec3 offset = cells.RandomElementByWeight(cell => ScatterDistanceChanceFactor.Evaluate(cell.LengthHorizontal / radius));
                offsets.Add(offset);
            }

            // 确保至少生成一个中心点冲击
            if (offsets.Count > 0)
            {
                offsets[0] = IntVec3.Zero;
            }

            return offsets;
        }
    }
}

public class WorldObject_SkyfallerArtillery : WorldObject
{
    public int destinationTile = -1;
    public IntVec3 targetCell = IntVec3.Invalid;
    public ThingDef projectileDef = null;
    public int burstIntervalTicks = 18;
    public string launcherLabel;
    private const int BurstCount = 1; // 每轮发射的炮弹数量
    private const int BurstIntervalTicks = (int)(2.1f * GenTicks.TicksPerRealSecond); // 每轮之间的间隔（2.1 秒）

    private int burstShotsDone = 0; // 已发射的炮弹数量
    private int burstTicksUntilNextShot = 0; // 距离下一轮发射的计时
    private bool burstInProgress = false; // 是否正在进行发射

    private int pendingShotsToLaunch = 0; // 全图打击剩余的炮弹数量
    private int pendingLaunchTicks = 0; // 距离下一次全图打击的计时

    private int initialTile = -1;
    private bool arrived;
    private float traveledPct;
    private int ticksUntilNextImpact;
    private int impactsDropped;
    private List<IntVec3> impactOffsets = new List<IntVec3>();

    private static readonly Predicate<IntVec3> TruePredicate = _ => true;

    private Vector3 Start => Find.WorldGrid.GetTileCenter(initialTile);

    private Vector3 End => Find.WorldGrid.GetTileCenter(destinationTile);

    public override Vector3 DrawPos => Vector3.Slerp(Start, End, traveledPct);

    private float TraveledPctStepPerTick
    {
        get
        {
            Vector3 start = Start;
            Vector3 end = End;
            if (start == end)
            {
                return 1f;
            }

            float distance = GenMath.SphericalDistance(start.normalized, end.normalized);
            return distance == 0f ? 1f : 0.0001f / distance;
        }
    }

    public void SetImpactOffsets(List<IntVec3> offsets)
    {
        impactOffsets = offsets != null ? new List<IntVec3>(offsets) : new List<IntVec3>();
    }

    public override void PostAdd()
    {
        base.PostAdd();
        initialTile = Tile;
        if (projectileDef == null)
        {
            projectileDef = NCLDefOf.NCL_Eagle_Artillery_Shell_Down;
        }
    }

    protected override void Tick()
    {
        base.Tick();

        // 如果尚未到达目标地图
        if (!arrived)
        {
            traveledPct += TraveledPctStepPerTick; // 更新飞行进度
            if (traveledPct >= 1f)
            {
                traveledPct = 1f;
                arrived = true; // 到达目标地图
                ticksUntilNextImpact = 0; // 重置计时器
            }
            return;
        }

        // 如果距离下一次冲击还有时间，则减少计时器
        if (ticksUntilNextImpact > 0)
        {
            ticksUntilNextImpact--;
            return;
        }

        // 如果所有冲击都已经完成，移除世界对象
        if (impactsDropped >= impactOffsets.Count)
        {
            Find.WorldObjects.Remove(this);
            return;
        }

        // 获取目标地图
        Map map = Current.Game.FindMap(destinationTile) ?? Find.WorldObjects.MapParentAt(destinationTile)?.Map;
        if (map == null)
        {
            Log.Warning($"[NCL] Eagle artillery world object arrived at tile {destinationTile}, but no map was available.");
            Find.WorldObjects.Remove(this);
            return;
        }

        // 发射一轮冲击
        for (int i = 0; i < BurstCount && impactsDropped < impactOffsets.Count; i++)
        {
            IntVec3 desiredCell = targetCell + impactOffsets[impactsDropped];
            IntVec3 impactCell = ResolveImpactCell(map, desiredCell);

            if (impactCell.IsValid && projectileDef != null)
            {
                // 在目标位置生成冲击效果
                GenSpawn.Spawn(projectileDef, impactCell, map, WipeMode.Vanish);
                Log.Message($"[NCL] Eagle artillery impact {impactsDropped + 1}/{impactOffsets.Count} spawned at {impactCell} on tile {destinationTile}.");
            }
            else
            {
                Log.Warning($"[NCL] Eagle artillery failed to resolve impact cell on tile {destinationTile}. desired={desiredCell}, projectileDef={(projectileDef == null ? "null" : projectileDef.defName)}");
            }

            impactsDropped++;
        }

        // 更新计时器
        ticksUntilNextImpact = BurstIntervalTicks;
    }



    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref destinationTile, "destinationTile", -1);
        Scribe_Values.Look(ref targetCell, "targetCell");
        Scribe_Values.Look(ref initialTile, "initialTile", -1);
        Scribe_Values.Look(ref arrived, "arrived", false);
        Scribe_Values.Look(ref traveledPct, "traveledPct", 0f);
        Scribe_Values.Look(ref ticksUntilNextImpact, "ticksUntilNextImpact", 0);
        Scribe_Values.Look(ref impactsDropped, "impactsDropped", 0);
        Scribe_Values.Look(ref burstIntervalTicks, "burstIntervalTicks", 18);
        Scribe_Values.Look(ref launcherLabel, "launcherLabel");
        Scribe_Defs.Look(ref projectileDef, "projectileDef");
        Scribe_Collections.Look(ref impactOffsets, "impactOffsets", LookMode.Value);
        if (Scribe.mode == LoadSaveMode.PostLoadInit && impactOffsets == null)
        {
            impactOffsets = new List<IntVec3>();
        }
    }

    private static IntVec3 ResolveImpactCell(Map map, IntVec3 desiredCell)
    {
        if (desiredCell.InBounds(map) && desiredCell.Standable(map))
        {
            return desiredCell;
        }

        if (desiredCell.InBounds(map) && CellFinder.TryFindRandomCellNear(desiredCell, map, 6, cell => cell.Standable(map), out IntVec3 nearCell))
        {
            return nearCell;
        }

        IntVec3 clamped = desiredCell.ClampInsideMap(map);
        if (clamped.InBounds(map) && clamped.Standable(map))
        {
            return clamped;
        }

        if (CellFinder.TryFindRandomCellNear(map.Center, map, 20, TruePredicate, out IntVec3 fallbackCell))
        {
            return fallbackCell;
        }

        return IntVec3.Invalid;
    }
}
public class CompProperties_PowerConsumingExploder : CompProperties
{
    public CompProperties_PowerConsumingExploder()
    {
        compClass = typeof(CompPowerConsumingExploder);
    }
}

public class CompPowerConsumingExploder : ThingComp
{
    private const float BaseRadius = 4.5f;
    private const float BaseDamage = 200f;
    private const float MaxRadius = 24.9f;
    private const float MaxDamage = 1600f;
    private const float PowerConsumptionForMaxEffect = 50000f;

    private float consumedPower;

    public CompProperties_PowerConsumingExploder Props => (CompProperties_PowerConsumingExploder)props;

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        base.PostSpawnSetup(respawningAfterLoad);
        ConsumePower();
    }

    public override void PostDestroy(DestroyMode mode, Map previousMap)
    {
        base.PostDestroy(mode, previousMap);
        if (previousMap != null)
        {
            ExplodeAndMakeCrater(previousMap);
        }
    }

    private void ConsumePower()
    {
        Map map = parent.Map;
        if (map == null)
        {
            return;
        }

        float storedPower = map.powerNetManager.AllNetsListForReading.Sum(net => net.batteryComps.Sum(b => b.StoredEnergy));
        float powerToConsume = storedPower * 0.05f;
        consumedPower = powerToConsume;
        float remainingPower = powerToConsume;

        foreach (PowerNet net in map.powerNetManager.AllNetsListForReading)
        {
            foreach (CompPowerBattery battery in net.batteryComps)
            {
                if (battery.StoredEnergy <= 0f)
                {
                    continue;
                }

                float toDraw = Mathf.Min(battery.StoredEnergy, remainingPower);
                battery.DrawPower(toDraw);
                remainingPower -= toDraw;
                if (remainingPower <= 0f)
                {
                    break;
                }
            }

            if (remainingPower <= 0f)
            {
                break;
            }
        }
    }

    private void ExplodeAndMakeCrater(Map map)
    {
        float powerFactor = Mathf.Clamp01(consumedPower / PowerConsumptionForMaxEffect);
        float radius = Mathf.Lerp(BaseRadius, MaxRadius, powerFactor);
        float damage = Mathf.Lerp(BaseDamage, MaxDamage, powerFactor);
        float yield = damage;

        GenExplosion.DoExplosion(
            parent.Position,
            map,
            radius,
            NCLDefOf.TW_Antiparticle_explosion,
            parent,
            (int)damage,
            -1f,
            NCLDefOf.NCL_Artillery_Landed,
            null,
            null,
            null,
            null,
            0f,
            1);

        MakeCrater(map, yield);
    }

    private void MakeCrater(Map map, float yield)
    {
        ThingDef craterDef = null;
        if (yield < 400f)
        {
            craterDef = NCLDefOf.CraterSmall;
        }
        else if (yield <= 600f)
        {
            craterDef = NCLDefOf.CraterMedium;
        }
        else
        {
            craterDef = NCLDefOf.CraterLarge;
        }

        if (craterDef != null)
        {
            GenSpawn.Spawn(craterDef, parent.Position, map, WipeMode.Vanish);
        }
    }
}

namespace NCL
{
    [DefOf]
    public static class NCLDefOf_ArtilleryRefs
    {
        public static ThingDef CraterSmall;
        public static ThingDef CraterMedium;
        public static ThingDef CraterLarge;
        public static SoundDef NCL_Artillery_Landed;
        public static SoundDef NCL_Artillery_Firing;
        public static ThingDef NCL_Eagle_Artillery_Shell_Up;
        public static ThingDef NCL_Eagle_Artillery_Shell_Down;
        public static DamageDef TW_Antiparticle_explosion;
        public static WorldObjectDef NCL_WorldObject_SkyfallerArtillery;
    }
}