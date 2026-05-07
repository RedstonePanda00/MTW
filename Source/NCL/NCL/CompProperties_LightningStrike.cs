using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace NCL
{
    public class CompProperties_LightningStrike : CompProperties
    {
        public float requiredPower = 100000f;
        public int empRadius = 20;
        public int cooldownTicks = 60000;
        public bool consumeFromBatteriesOnly = true;

        public CompProperties_LightningStrike()
        {
            compClass = typeof(CompLightningStrike);
        }
    }

    public class CompLightningStrike : ThingComp
    {
        private CompPowerTrader powerComp;
        private int lastStrikeTick = -999999;
        private bool isTargeting = false;
        private IntVec3 currentTargetCell;

        public CompProperties_LightningStrike Props => (CompProperties_LightningStrike)props;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            powerComp = parent.TryGetComp<CompPowerTrader>();
        }

        public override void CompTick()
        {
            base.CompTick();

            if (isTargeting)
            {
                currentTargetCell = UI.MouseCell();
                DrawTargetingEffects(currentTargetCell);

                if (!Find.Targeter.IsTargeting)
                {
                    isTargeting = false;
                }
            }
        }

        private void DrawTargetingEffects(IntVec3 cell)
        {
            // 1. EMP范围环（白色不闪烁）
            GenDraw.DrawRadiusRing(cell, Props.empRadius, Color.white);

            // 2. 闪电中心点（白色实心圆）
            GenDraw.DrawRadiusRing(cell, 1.5f, Color.white);
        }


        private bool HasEnoughPower()
        {
            float totalAvailable = Props.consumeFromBatteriesOnly
                ? GetBatteriesStoredEnergy(powerComp.PowerNet)
                : GetTotalAvailableEnergy(powerComp.PowerNet);
            return totalAvailable >= Props.requiredPower;
        }

        private float GetBatteriesStoredEnergy(PowerNet net)
        {
            float total = 0f;
            foreach (CompPowerBattery battery in net.batteryComps)
                total += battery.StoredEnergy;
            return total;
        }

        private float GetTotalAvailableEnergy(PowerNet net)
        {
            return GetBatteriesStoredEnergy(net) + (net.CurrentEnergyGainRate() * 60000f);
        }

        private void DoLightningStrike(IntVec3 targetCell)
        {
            ConsumePower(Props.requiredPower);
            lastStrikeTick = Find.TickManager.TicksGame;

            if (parent.Map.weatherManager != null)
                parent.Map.weatherManager.eventHandler.AddEvent(new WeatherEvent_LightningStrike(parent.Map, targetCell));
            else
                FleckMaker.ThrowLightningGlow(targetCell.ToVector3(), parent.Map, 3f);

            GenExplosion.DoExplosion(
                targetCell,
                parent.Map,
                Props.empRadius,
                DamageDefOf.EMP,
                parent,
                damAmount: 50,
                armorPenetration: 0
            );

            Messages.Message("LightningStrike_Message".Translate(parent.LabelCap), MessageTypeDefOf.NeutralEvent);
        }

        private void ConsumePower(float amount)
        {
            if (Props.consumeFromBatteriesOnly)
            {
                foreach (CompPowerBattery battery in powerComp.PowerNet.batteryComps)
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
            else
            {
                float remaining = amount;
                foreach (CompPowerBattery battery in powerComp.PowerNet.batteryComps)
                {
                    if (remaining <= 0f) break;
                    if (battery.StoredEnergy > 0f)
                    {
                        float consume = Mathf.Min(remaining, battery.StoredEnergy);
                        battery.DrawPower(consume);
                        remaining -= consume;
                    }
                }
                if (remaining > 0f) powerComp.PowerOutput -= remaining;
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo g in base.CompGetGizmosExtra())
                yield return g;

            Command_Action lightningStrike = new Command_Action
            {
                icon = ContentFinder<Texture2D>.Get("ModIcon/EMPBurst"),
                defaultLabel = "LightningStrike".Translate(),
                defaultDesc = "LightningStrikeDesc".Translate(),
                action = () => BeginTargeting()
            };

            if (!CanActivate(out string reason))
                lightningStrike.Disable(reason);

            yield return lightningStrike;
        }

        private bool CanActivate(out string reason)
        {
            if (powerComp == null || !powerComp.PowerOn)
            {
                reason = "LightningStrike_NoPower".Translate();
                return false;
            }
            if (Find.TickManager.TicksGame < lastStrikeTick + Props.cooldownTicks)
            {
                reason = "LightningStrike_Cooldown".Translate(
                    (lastStrikeTick + Props.cooldownTicks - Find.TickManager.TicksGame).ToStringTicksToPeriod());
                return false;
            }
            if (!HasEnoughPower())
            {
                reason = "LightningStrike_NeedPower".Translate(Props.requiredPower.ToString("F0"));
                return false;
            }
            reason = null;
            return true;
        }

        private void BeginTargeting()
        {
            isTargeting = true;

            Find.Targeter.BeginTargeting(
                new TargetingParameters
                {
                    canTargetLocations = true,
                    canTargetPawns = true,
                    canTargetBuildings = true
                },
                (LocalTargetInfo t) =>
                {
                    GenDraw.DrawRadiusRing(t.Cell, Props.empRadius, Color.red);
                    DoLightningStrike(t.Cell);
                    isTargeting = false;
                }
            );
        }


        public override string CompInspectStringExtra()
        {
            StringBuilder sb = new StringBuilder();

            if (powerComp == null || !powerComp.PowerOn)
                return "LightningStrike_NoPower".Translate();

            sb.Append("LightningStrike_PowerStatus".Translate(
                GetAvailablePower().ToString("F0"),
                Props.requiredPower.ToString("F0")));

            sb.AppendLine();

            int ticksRemaining = Mathf.Max(0, (lastStrikeTick + Props.cooldownTicks) - Find.TickManager.TicksGame);
            sb.Append(ticksRemaining > 0
                ? "LightningStrike_Cooldown".Translate(ticksRemaining.ToStringTicksToPeriod())
                : "LightningStrike_Ready".Translate());

            return sb.ToString();
        }

        private float GetAvailablePower()
        {
            return Props.consumeFromBatteriesOnly
                ? GetBatteriesStoredEnergy(powerComp.PowerNet)
                : GetTotalAvailableEnergy(powerComp.PowerNet);
        }
    }
}






namespace NCL
{
    public class CompProperties_AutoLightningStrike : CompProperties
    {
        public int empRadius = 20;
        public bool consumeFromBatteriesOnly = true;
        public int autoStrikeInterval = 300; // 5秒 = 300 ticks
        public float autoStrikePowerCost = 500f;
        public DamageDef damageType;
        public int maxTargets = 3;
        public int maxConcurrentStrikes = 3;
        public string uiIconPath = "ModIcon/AutoLightningStrike";
        public string uiLabel = "NCL_AutoStrike_Mode";
        public string uiDescription = "NCL_AutoStrike_ToggleDesc";
        public string overdriveLabel = "NCL_Overdrive_Mode";
        public string overdriveDescription = "NCL_Overdrive_Description";
        public int damageAmount = 50;

        // 超载模式设置
        public bool enableOverdrive = true;
        public string overdriveIconPath = "ModIcon/overdrive";
        public float overdrivePowerMultiplier = 2f;
        public int overdriveTargetMultiplier = 2;
        public float overdriveIntervalDivider = 2f;

        public CompProperties_AutoLightningStrike()
        {
            compClass = typeof(CompAutoLightningStrike);
        }
    }

    public class CompAutoLightningStrike : ThingComp
    {
        private CompPowerTrader powerComp;
        private int lastStrikeTick = -999999;
        private bool autoStrikeEnabled = false;
        private bool overdriveEnabled = false;
        private List<int> activeStrikeTicks = new List<int>();

        public CompProperties_AutoLightningStrike Props => (CompProperties_AutoLightningStrike)props;

        // 动态属性（根据超载模式状态变化）
        private int CurrentMaxTargets => overdriveEnabled ?
            Props.maxTargets * Props.overdriveTargetMultiplier : Props.maxTargets;

        private int CurrentMaxConcurrentStrikes => overdriveEnabled ?
            Props.maxConcurrentStrikes * Props.overdriveTargetMultiplier : Props.maxConcurrentStrikes;

        private int CurrentStrikeInterval => overdriveEnabled ?
            (int)(Props.autoStrikeInterval / Props.overdriveIntervalDivider) : Props.autoStrikeInterval;

        private float CurrentPowerCost => overdriveEnabled ?
            Props.autoStrikePowerCost * Props.overdrivePowerMultiplier : Props.autoStrikePowerCost;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            powerComp = parent.TryGetComp<CompPowerTrader>();
        }

        public override void CompTick()
        {
            base.CompTick();

            activeStrikeTicks.RemoveAll(tick => Find.TickManager.TicksGame > tick + 10);

            if (autoStrikeEnabled &&
                Find.TickManager.TicksGame % CurrentStrikeInterval == 0 &&
                CanAutoStrike() &&
                activeStrikeTicks.Count < CurrentMaxConcurrentStrikes)
            {
                TryAutoStrike();
            }
        }

        private bool HasEnoughPower(float amount)
        {
            if (powerComp == null || powerComp.PowerNet == null)
                return false;

            float totalAvailable = Props.consumeFromBatteriesOnly
                ? GetBatteriesStoredEnergy(powerComp.PowerNet)
                : GetTotalAvailableEnergy(powerComp.PowerNet);

            return totalAvailable >= amount;
        }

        private bool CanAutoStrike()
        {
            return powerComp != null &&
                   powerComp.PowerOn &&
                   powerComp.PowerNet != null &&
                   Find.TickManager.TicksGame >= lastStrikeTick + CurrentStrikeInterval &&
                   HasEnoughPower(CurrentPowerCost);
        }

        private List<IntVec3> FindTargetCells()
        {
            return parent.Map.mapPawns.AllPawnsSpawned
                .Where(p => ShouldTargetPawn(p))
                .OrderBy(p => p.Position.DistanceTo(parent.Position))
                .Take(CurrentMaxTargets)
                .Select(t => t.Position)
                .ToList();
        }

        private bool ShouldTargetPawn(Pawn p)
        {
            // 1. 排除无效目标
            if (p == null || p.Dead || p.Downed || p.Destroyed)
                return false;

            // 2. 新增：检查目标是否在屋顶下 - 关键修改
            if (p.Map.roofGrid.Roofed(p.Position))
            {
                return false;
            }

            // 3. 检查派系敌对性
            bool isHostile = p.Faction?.HostileTo(parent.Faction) ?? false;
            if (!isHostile)
                return false;

            // 4. 检查囚犯状态（关键逻辑）
            if (IsPrisoner(p))
            {
                // 检查囚犯是否在监狱区域内
                bool isInPrisonArea = p.Map.areaManager.Home[p.Position];

                // 如果当前位置不是监狱区域，检查相邻区域
                if (!isInPrisonArea)
                {
                    var region = p.Map.regionGrid.GetValidRegionAt(p.Position);
                    if (region != null)
                    {
                        var district = region.District;
                        if (district != null)
                        {
                            isInPrisonArea = district.Cells.Any(c => p.Map.areaManager.Home[c]);
                        }
                    }
                }

                // 仅攻击越狱或不在监狱区域的囚犯
                return !isInPrisonArea;
            }

            return true;
        }


        private bool IsPrisoner(Pawn p)
        {
            // 修复：确保所有表达式都是布尔类型（非可空）
            return p.IsPrisoner ||
                   (p.guest != null && p.guest.IsPrisoner) ||
                   p.HostFaction == Faction.OfPlayer;
        }


        private void TryAutoStrike()
        {
            foreach (var cell in FindTargetCells().Where(c => c.IsValid))
            {
                ConsumePower(CurrentPowerCost);
                DoLightningStrike(cell);
                activeStrikeTicks.Add(Find.TickManager.TicksGame);
            }
        }

        private void DoLightningStrike(IntVec3 targetCell)
        {
            // 添加最终防线检查：确保目标位置没有屋顶
            if (parent.Map.roofGrid.Roofed(targetCell))
                return;

            lastStrikeTick = Find.TickManager.TicksGame;

            if (parent.Map.weatherManager != null)
                parent.Map.weatherManager.eventHandler.AddEvent(new WeatherEvent_LightningStrike(parent.Map, targetCell));
            else
                FleckMaker.ThrowLightningGlow(targetCell.ToVector3(), parent.Map, 3f);

            GenExplosion.DoExplosion(
                targetCell,
                parent.Map,
                Props.empRadius,
                Props.damageType,
                parent,
                damAmount: Props.damageAmount,
                armorPenetration: 0
            );
        }



        private void ConsumePower(float amount)
        {
            if (Props.consumeFromBatteriesOnly)
            {
                foreach (CompPowerBattery battery in powerComp.PowerNet.batteryComps)
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
            else
            {
                float remaining = amount;
                foreach (CompPowerBattery battery in powerComp.PowerNet.batteryComps)
                {
                    if (remaining <= 0f) break;
                    if (battery.StoredEnergy > 0f)
                    {
                        float consume = Mathf.Min(remaining, battery.StoredEnergy);
                        battery.DrawPower(consume);
                        remaining -= consume;
                    }
                }
                if (remaining > 0f) powerComp.PowerOutput -= remaining;
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo g in base.CompGetGizmosExtra())
                yield return g;

            // 自动攻击开关
            yield return CreateAutoStrikeGizmo();

            // 超载模式开关
            if (Props.enableOverdrive)
                yield return CreateOverdriveGizmo();
        }

        private Command_Toggle CreateAutoStrikeGizmo()
        {
            var cmd = new Command_Toggle
            {
                icon = ContentFinder<Texture2D>.Get(Props.uiIconPath),
                // 使用翻译键
                defaultLabel = Props.uiLabel.Translate(),
                defaultDesc = Props.uiDescription.Translate(),
                isActive = () => autoStrikeEnabled,
                toggleAction = () => autoStrikeEnabled = !autoStrikeEnabled
            };

            if (!CanToggleAutoStrike(out string reason))
            {
                cmd.Disable(reason); // 使用Disable方法而不是直接设置disabled字段
            }

            return cmd;
        }

        private Command_Toggle CreateOverdriveGizmo()
        {
            var cmd = new Command_Toggle
            {
                icon = ContentFinder<Texture2D>.Get(Props.overdriveIconPath),
                // 使用翻译键
                defaultLabel = Props.overdriveLabel.Translate(),
                defaultDesc = Props.overdriveDescription.Translate(),
                isActive = () => overdriveEnabled,
                toggleAction = ToggleOverdrive
            };

            if (!CanToggleOverdrive(out string reason))
            {
                cmd.Disable(reason); // 使用Disable方法而不是直接设置disabled字段
            }

            return cmd;
        }
        private void ToggleOverdrive()
        {
            overdriveEnabled = !overdriveEnabled;
            Messages.Message(
                overdriveEnabled ?
                    "NCL_OverdriveMode_Enabled".Translate(parent.LabelCap) :
                    "NCL_OverdriveMode_Disabled".Translate(parent.LabelCap),
                MessageTypeDefOf.NeutralEvent);
        }

        private bool CanToggleAutoStrike(out string reason)
        {
            if (powerComp == null || !powerComp.PowerOn)
            {
                reason = "NCL_AutoLightningStrike_NoPower".Translate();
                return false;
            }
            if (!HasEnoughPower(CurrentPowerCost))
            {
                reason = "NCL_AutoLightningStrike_NeedPower".Translate(CurrentPowerCost.ToString("F0"));
                return false;
            }
            reason = null;
            return true;
        }

        private bool CanToggleOverdrive(out string reason)
        {
            if (!autoStrikeEnabled)
            {
                reason = "NCL_OverdriveMode_RequiresAuto".Translate();
                return false;
            }
            return CanToggleAutoStrike(out reason);
        }

        public override string CompInspectStringExtra()
        {
            StringBuilder sb = new StringBuilder();

            if (powerComp == null || !powerComp.PowerOn)
                return "NCL_AutoLightningStrike_NoPower".Translate();

            sb.Append("NCL_AutoLightningStrike_PowerStatus".Translate(
                GetAvailablePower().ToString("F0"),
                CurrentPowerCost.ToString("F0")));

            sb.AppendLine();

            if (autoStrikeEnabled)
            {
                sb.Append("NCL_AutoLightningStrike_AutoActive".Translate());
                sb.AppendLine();
                sb.Append("NCL_AutoLightningStrike_ConcurrentStrikes".Translate(
                    activeStrikeTicks.Count,
                    CurrentMaxConcurrentStrikes));
                sb.AppendLine();
            }

            if (overdriveEnabled)
            {
                sb.Append("NCL_OverdriveMode_Active".Translate());
                sb.AppendLine();
            }

            int ticksRemaining = Mathf.Max(0, (lastStrikeTick + CurrentStrikeInterval) - Find.TickManager.TicksGame);
            sb.Append(ticksRemaining > 0 ?
                "NCL_AutoLightningStrike_NextStrike".Translate(ticksRemaining.ToStringTicksToPeriod()) :
                "NCL_AutoLightningStrike_Ready".Translate());

            return sb.ToString();
        }

        private float GetAvailablePower()
        {
            return Props.consumeFromBatteriesOnly ?
                GetBatteriesStoredEnergy(powerComp.PowerNet) :
                GetTotalAvailableEnergy(powerComp.PowerNet);
        }

        private float GetBatteriesStoredEnergy(PowerNet net)
        {
            return net?.batteryComps.Sum(b => b.StoredEnergy) ?? 0f;
        }

        private float GetTotalAvailableEnergy(PowerNet net)
        {
            return GetBatteriesStoredEnergy(net) + (net?.CurrentEnergyGainRate() * 60000f ?? 0f);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref autoStrikeEnabled, "autoStrikeEnabled", false);
            Scribe_Values.Look(ref overdriveEnabled, "overdriveEnabled", false);
            Scribe_Values.Look(ref lastStrikeTick, "lastStrikeTick", -999999);
            Scribe_Collections.Look(ref activeStrikeTicks, "activeStrikeTicks", LookMode.Value);
        }
    }
}
