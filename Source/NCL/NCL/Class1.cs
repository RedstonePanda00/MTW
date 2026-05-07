using NCL;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;



namespace NCL
{

    [DefOf]
    public static class TWDefOf
    {
        public static HediffDef TW_TacticalArtilleryCoordinationModule_AutoMortar;
        public static HediffDef TW_TacticalArtilleryCoordinationModule_AutoMortarEMP;
        public static ThingDef NCL_MechNode;
        internal static int AdvancedNodeCount = 5;
    }
}

namespace NCL
{

    public class CompProperties_TotalWarfareHediff : CompProperties
    {

        public CompProperties_TotalWarfareHediff()
        {
            this.compClass = typeof(CompTotalWarfareHediff);
        }


        public List<HediffDef> TWhediffsRange;


        public List<HediffDef> TWhediffsMelee;
    }
}

namespace NCL
{
    public class TipComponent : WorldComponent
    {
        // 计数器相关字段
        public int TWCurrentDaySpecialHediffCount;
        private int lastCheckedDay = -1;
        public int TWCurrentDaySpecialHediffCountA;
        // 触发标志字段
        public bool TWtriggered3 = false; // 核心建筑触发的标志
        public int lastActivationTick = -1; // 最后激活时间
        public int TWCurrentDayAirstrikeCount = 0;
        // 财富跟踪字段
        public float num = 0f;
        public bool TWtriggered = false;
        public bool TWtriggered2 = false;
        public bool TWtriggered1 = false;
        public int defeated = 0;
        private bool puzzleFlag = false;
        public List<string> list_str = new List<string>();

        // 静态设置
        public static bool ReinforceNotApply = false;
        public static bool DeveloperMode = false;

        public TipComponent(World world) : base(world) { }

        public override void WorldComponentTick()
        {
            base.WorldComponentTick();

            // 安全地获取当前地图
            Map currentMap = Find.CurrentMap;

            // 只有当存在活动地图时才更新日期
            if (currentMap != null)
            {
                int currentDay = GenLocalDate.DayOfYear(currentMap);
                if (currentDay != lastCheckedDay)
                {
                    TWCurrentDaySpecialHediffCount = 0;
                    TWCurrentDaySpecialHediffCountA = 0;
                    TWCurrentDayAirstrikeCount = 0; // 重置空袭计数器
                    lastCheckedDay = currentDay;
                }
            }

            // 财富检查和触发逻辑（每1024 tick执行一次）
            if (!TWSettings.ReinforceNotApply && (Find.TickManager.TicksGame & 1023) == 511)
            {
                // 安全计算财富值 - 检查是否有任何玩家家园地图
                bool hasPlayerHomeMaps = false;
                float totalWealth = 0f;

                foreach (Map map in Find.Maps)
                {
                    // 确保地图不为null且是玩家家园
                    if (map != null && map.IsPlayerHome)
                    {
                        totalWealth += map.wealthWatcher.WealthTotal;
                        hasPlayerHomeMaps = true;
                    }
                }

                if (DebugSettings.ShowDevGizmos)
                {
                    this.num = totalWealth;

                    // 使用自定义阈值
                    if (totalWealth < TotalWarfareSettings.WealthTriggerThreshold)
                    {
                        this.TWtriggered2 = false;
                    }
                }

                if (TWSettings.DeveloperMode)
                {
                    // 在日志中显示当前阈值
                    Log.Message($"NCL_TOTALWARFARE_START_LOG: Wealth={totalWealth}, Threshold={TotalWarfareSettings.WealthTriggerThreshold}, Triggered={this.TWtriggered2}");
                }

                if (!this.TWtriggered2 && hasPlayerHomeMaps) // 确保有玩家家园地图
                {
                    this.num = totalWealth;

                    // 使用自定义阈值
                    if (totalWealth > TotalWarfareSettings.WealthTriggerThreshold)
                    {
                        this.TWtriggered2 = true;

                        // 在信件中显示实际阈值
                        string triggerText = "NCL_TOTALWARFARE_AUTO_TRIGGER_TEXT".Translate(
                            TotalWarfareSettings.WealthTriggerThreshold.ToString("N0")
                        );

                        // 安全发送信件
                        if (Find.LetterStack != null)
                        {
                            Find.LetterStack.ReceiveLetter(
                                "NCL_TOTALWARFARE_AUTO_TRIGGER_TITLE".Translate(),
                                triggerText,
                                LetterDefOf.NeutralEvent,
                                null,
                                0,
                                true
                            );
                        }
                    }
                }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look<bool>(ref TWSettings.ReinforceNotApply, "ReinforceNotApply", false, false);
            Scribe_Values.Look<bool>(ref TWSettings.DeveloperMode, "DeveloperMode", false, false);
            Scribe_Values.Look<bool>(ref this.TWtriggered, "TWtriggered", false, false);
            Scribe_Values.Look<bool>(ref this.TWtriggered2, "TWtriggered2", false, false);
            Scribe_Values.Look<bool>(ref this.TWtriggered1, "TWtriggered1", false, false);
            Scribe_Values.Look<bool>(ref this.TWtriggered3, "TWtriggered3", false, false);
            Scribe_Values.Look<int>(ref this.lastActivationTick, "lastActivationTick", -1, false);
            Scribe_Values.Look<int>(ref TWCurrentDaySpecialHediffCount, "TWCurrentDaySpecialHediffCount", 0);
            Scribe_Values.Look<int>(ref TWCurrentDaySpecialHediffCountA, "TWCurrentDaySpecialHediffCountA", 0);
            Scribe_Values.Look(ref TWCurrentDayAirstrikeCount, "TWCurrentDayAirstrikeCount", 0);
            Scribe_Values.Look<int>(ref lastCheckedDay, "lastCheckedDay", -1);
        }
    }
}


public class TWSettings : ModSettings
{
    public override void ExposeData()
    {
        base.ExposeData();


        Scribe_Values.Look<bool>(ref TWSettings.ReinforceNotApply, "ReinforceNotApply", false, true);
        Scribe_Values.Look<bool>(ref TWSettings.DeveloperMode, "DeveloperMode", false, true);
        Scribe_Values.Look<bool>(ref this.TWtriggered, "TWtriggered", false, false);
        Scribe_Values.Look<bool>(ref this.TWtriggered2, "TWtriggered2", false, false);
        Scribe_Values.Look<bool>(ref this.TWtriggered1, "TWtriggered1", false, false);
        Scribe_Values.Look(ref TWtriggered3, "enableCoreTrigger", false);

    }

    public static bool ReinforceNotApply = false;

    public static bool DeveloperMode = false;

    public bool TWtriggered = false;

    public bool TWtriggered2 = false;

    public bool TWtriggered1 = false;

    public bool TWtriggered3 = false;

}



namespace NCL
{
    public class CompTotalWarfareHediff : ThingComp
    {
        // 获取当前已应用的特殊 Hediff 数量（每日重置）
        private int CurrentDaySpecialHediffCount
        {
            get
            {
                var comp = Find.World.GetComponent<TipComponent>();
                return comp?.TWCurrentDaySpecialHediffCount ?? 0;
            }
        }

        // 检查是否达到每日特殊 Hediff 上限
        private bool ReachedDailySpecialHediffLimit
        {
            get
            {
                return CurrentDaySpecialHediffCount >= TotalWarfareSettings.MaxSpecialHediffsPerDay;
            }
        }

        public CompProperties_TotalWarfareHediff Props
        {
            get
            {
                return (CompProperties_TotalWarfareHediff)this.props;
            }
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            bool shouldSkip = !TotalWarfareSettings.EnableAutoTrigger ||
                            TWSettings.ReinforceNotApply ||
                            !Find.World.GetComponent<TipComponent>().TWtriggered2 ||
                            this.applyed;

            if (shouldSkip)
            {
                this.applyed = true;
                return;
            }

            Pawn pawn = this.parent as Pawn;
            bool isExcludedMech = pawn.def.defName == "TW_Mech_Doxa" ||
                                 pawn.def.defName == "TW_Mech_Shell_Fortification" ||
                                 pawn.def.defName == "Mech_BlackApocriton";

            if (isExcludedMech) return;

            BodyPartRecord brain = pawn.health.hediffSet.GetBrain();
            bool isHostileWithBrain = brain != null &&
                                    (pawn.Faction == null || pawn.Faction.HostileTo(Faction.OfPlayer));

            if (!isHostileWithBrain) return;

            var tipComp = Find.World.GetComponent<TipComponent>();

            // ===== 重构特殊 Hediff 应用逻辑 =====
            HediffDef selectedDef = null;

            // 决定是否尝试应用特殊 Hediff
            bool trySpecialHediff = false;
            if (tipComp != null &&
                tipComp.TWCurrentDaySpecialHediffCount < TotalWarfareSettings.MaxSpecialHediffsPerDay)
            {
                trySpecialHediff = Rand.Chance(0.25f); // 15% 概率尝试应用特殊 Hediff
            }

            if (trySpecialHediff)
            {
                // 尝试应用特殊 EMP Hediff
                selectedDef = TWDefOf.TW_TacticalArtilleryCoordinationModule_AutoMortarEMP;

                // 检查 Hediff 是否可应用
                if (pawn.health.hediffSet.GetFirstHediffOfDef(selectedDef) == null)
                {
                    // 应用特殊 Hediff
                    Hediff hediff = HediffMaker.MakeHediff(selectedDef, pawn, brain);
                    pawn.health.AddHediff(hediff);
                    this.applyed = true;

                    // 更新计数器
                    tipComp.TWCurrentDaySpecialHediffCount++;
                    return; // 应用后直接退出
                }
            }

            // ===== 应用普通 Hediff =====
            // 仅当特殊 Hediff 未应用时才执行
            if (pawn.equipment.Primary != null && pawn.equipment.Primary.def.IsRangedWeapon)
            {
                selectedDef = this.Props.TWhediffsRange.RandomElement();
            }
            else
            {
                selectedDef = this.Props.TWhediffsMelee.RandomElement();
            }

            // 确保 Hediff 未应用
            if (selectedDef != null &&
                pawn.health.hediffSet.GetFirstHediffOfDef(selectedDef) == null)
            {
                Hediff hediff = HediffMaker.MakeHediff(selectedDef, pawn, brain);
                pawn.health.AddHediff(hediff);
                this.applyed = true;
            }
        }


        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look<bool>(ref this.applyed, "applyed", false, false);
        }

        public bool applyed = false;
    }
}



namespace NCL
{
    public class Building_TotalWarfareActivator : Building
    {
        private bool _activated;

        public bool Activated
        {
            get => _activated;
            private set => _activated = value;
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            Find.World.GetComponent<TipComponent>();
        }

        public void ActivateTotalWarfare()
        {
            if (!Activated && Find.World.GetComponent<TipComponent>() is TipComponent comp)
            {
                Activated = true;
                comp.TWtriggered1 = true;

                Find.LetterStack.ReceiveLetter(
                    "NCL_TOTALWARFARE_MANUAL_TRIGGER_TITLE".Translate(),
                    "NCL_TOTALWARFARE_MANUAL_TRIGGER_TEXT".Translate(),
                    LetterDefOf.NeutralEvent);

                FleckMaker.ThrowExplosionCell(Position, Map, FleckDefOf.ExplosionFlash, Color.red);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref _activated, "activated", false);
        }
    }
}


namespace NCL
{
    public class CompProperties_UseEffect_ActivateWarfare : CompProperties_UseEffect
    {
        public int delayTicks = -1;
        public EffecterDef activateEffect;

        public CompProperties_UseEffect_ActivateWarfare()
        {
            compClass = typeof(CompUseEffect_ActivateWarfare);
        }
    }
}


namespace NCL
{
    public class CompUseEffect_ActivateWarfare : CompUseEffect
    {
        public new CompProperties_UseEffect_ActivateWarfare Props =>
            (CompProperties_UseEffect_ActivateWarfare)props;


        public override void DoEffect(Pawn usedBy)
        {
            if (parent is Building_TotalWarfareActivator activator && !activator.Activated)
            {
                activator.ActivateTotalWarfare();

                if (Props.activateEffect != null)
                    Props.activateEffect.Spawn(parent.Position, parent.Map, 1f).Cleanup();
            }
        }
    }
}

namespace NCL
{

    public class CompProperties_TotalWarfareBetaHediff : CompProperties
    {

        public CompProperties_TotalWarfareBetaHediff()
        {
            this.compClass = typeof(CompTotalWarfareBetaHediff);
        }


        public List<HediffDef> TWhediffsRange;


        public List<HediffDef> TWhediffsMelee;
    }
}

namespace NCL
{
    public class CompTotalWarfareBetaHediff : ThingComp
    {
        private int CurrentDaySpecialHediffCount
        {
            get
            {
                var comp = Find.World.GetComponent<TipComponent>();
                return comp?.TWCurrentDaySpecialHediffCountA ?? 0;
            }
        }

        // 新增：获取空袭信号传送器每日计数器
        private int CurrentDayAirstrikeCount
        {
            get
            {
                var comp = Find.World.GetComponent<TipComponent>();
                return comp?.TWCurrentDayAirstrikeCount ?? 0;
            }
        }

        private bool ReachedDailySpecialHediffLimit
        {
            get
            {
                return CurrentDaySpecialHediffCount >= TotalWarfareSettings.MaxSpecialHediffsPerDayA;
            }
        }

        // 新增：检查是否达到每日空袭信号器上限
        private bool ReachedDailyAirstrikeLimit
        {
            get
            {
                return CurrentDayAirstrikeCount >= TotalWarfareSettings.MaxAirstrikeHediffsPerDay;
            }
        }

        public CompProperties_TotalWarfareBetaHediff Props
        {
            get
            {
                return (CompProperties_TotalWarfareBetaHediff)this.props;
            }
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            bool shouldSkip = !TotalWarfareSettings.EnableMechEnhancement ||
                             TWSettings.ReinforceNotApply ||
                             !Find.World.GetComponent<TipComponent>().TWtriggered1 ||
                             this.applyed;

            if (shouldSkip)
            {
                this.applyed = true;
                return;
            }

            Pawn pawn = this.parent as Pawn;
            bool isExcludedMech = pawn.def.defName == "TW_Mech_Doxa" ||
                                 pawn.def.defName == "TW_Mech_Shell_Fortification" ||
                                 pawn.def.defName == "Mech_BlackApocriton";

            if (isExcludedMech) return;

            BodyPartRecord brain = pawn.health.hediffSet.GetBrain();
            bool isHostileWithBrain = brain != null &&
                                    (pawn.Faction == null || pawn.Faction.HostileTo(Faction.OfPlayer));

            if (!isHostileWithBrain) return;

            var tipComp = Find.World.GetComponent<TipComponent>();
            float airstrikeThreshold = TotalWarfareSettings.AirstrikeWealthThreshold; // 获取配置的阈值

            // === 新增：财富值达到阈值的特殊 Hediff 生成逻辑 ===
            if (tipComp != null &&
                tipComp.num >= airstrikeThreshold && // 使用配置值替换硬编码
                tipComp.TWCurrentDayAirstrikeCount < TotalWarfareSettings.MaxAirstrikeHediffsPerDay &&
                Rand.Chance(0.1f)) // 保持5%概率
            {
                HediffDef airstrikeDef = DefDatabase<HediffDef>.GetNamed(
                    "TW_TacticalArtilleryCoordinationModule_Airstrike_Signal_Transmitter",
                    false
                );

                if (airstrikeDef != null &&
                    pawn.health.hediffSet.GetFirstHediffOfDef(airstrikeDef) == null)
                {
                    Hediff hediff = HediffMaker.MakeHediff(airstrikeDef, pawn, brain);
                    pawn.health.AddHediff(hediff);
                    this.applyed = true;

                    tipComp.TWCurrentDayAirstrikeCount++;
                    return;
                }
            }

            // ===== 修复2：重构特殊 Hediff 应用逻辑 =====
            HediffDef selectedDef = null;
            bool trySpecialHediff = false;

            // 决定是否尝试应用特殊 Hediff
            if (tipComp != null &&
                tipComp.TWCurrentDaySpecialHediffCountA < TotalWarfareSettings.MaxSpecialHediffsPerDayA)
            {
                trySpecialHediff = Rand.Chance(0.25f);
            }

            if (trySpecialHediff)
            {
                // 尝试应用特殊 Hediff
                selectedDef = TWDefOf.TW_TacticalArtilleryCoordinationModule_AutoMortar;

                // 检查 Hediff 是否可应用
                if (pawn.health.hediffSet.GetFirstHediffOfDef(selectedDef) == null)
                {
                    // 应用特殊 Hediff
                    Hediff hediff = HediffMaker.MakeHediff(selectedDef, pawn, brain);
                    pawn.health.AddHediff(hediff);
                    this.applyed = true;

                    // 更新计数器
                    tipComp.TWCurrentDaySpecialHediffCountA++;
                    return;
                }
            }

            // ===== 应用普通 Hediff =====
            // 仅当特殊 Hediff 未应用时才执行
            if (pawn.equipment.Primary != null && pawn.equipment.Primary.def.IsRangedWeapon)
            {
                selectedDef = this.Props.TWhediffsRange.RandomElement();
            }
            else
            {
                selectedDef = this.Props.TWhediffsMelee.RandomElement();
            }

            // 确保 Hediff 未应用
            if (selectedDef != null &&
                pawn.health.hediffSet.GetFirstHediffOfDef(selectedDef) == null)
            {
                Hediff hediff = HediffMaker.MakeHediff(selectedDef, pawn, brain);
                pawn.health.AddHediff(hediff);
                this.applyed = true;
            }
        }



        // Token: 0x0600028A RID: 650 RVA: 0x000156C0 File Offset: 0x000138C0
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look<bool>(ref this.applyed, "applyed", false);
        }

        // Token: 0x04000161 RID: 353
        public bool applyed = false;
    }
}


namespace NCL
{
    public class TotalWarfareSettings : ModSettings
    {
        public static float AirstrikeWealthThreshold = 500000f;
        public static float WealthTriggerThreshold = 1000000f;
        public bool InvisibilityVisibleToPlayer = true;
        public static bool EnableMechEnhancement = true;
        public static int MaxAirstrikeHediffsPerDay = 1;
        public static int MaxSpecialHediffsPerDay = 25;
        public static int MaxSpecialHediffsPerDayA = 50;
        // 是否启用自动触发(财富阈值)
        public static bool EnableAutoTrigger = true;

        public static bool EnableCoreTrigger = true; // 是否启用核心建筑触发
        // 是否显示开发者日志
        public static bool ShowDevLogs = false;

        internal static int AdvancedNodeCount = 5;
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref EnableMechEnhancement, "enableMechEnhancement", true);
            Scribe_Values.Look(ref EnableAutoTrigger, "enableAutoTrigger", true);
            Scribe_Values.Look(ref EnableCoreTrigger, "enableCoreTrigger", true);
            Scribe_Values.Look(ref ShowDevLogs, "showDevLogs", false);
            Scribe_Values.Look(ref MaxSpecialHediffsPerDay, "maxSpecialHediffsPerDay", 25);
            Scribe_Values.Look(ref MaxSpecialHediffsPerDayA, "maxSpecialHediffsPerDayA", 50);
            Scribe_Values.Look(ref MaxAirstrikeHediffsPerDay, "maxAirstrikeHediffsPerDay", 1);
            Scribe_Values.Look(ref WealthTriggerThreshold, "wealthTriggerThreshold", 1000000f);
            Scribe_Values.Look(ref AirstrikeWealthThreshold, "airstrikeWealthThreshold", 500000f);
            Scribe_Values.Look(ref InvisibilityVisibleToPlayer, "invisibilityVisibleToPlayer", true);
        }
    }
}

namespace NCL
{
    public class TotalWarfareMod : Mod
    {
        private TotalWarfareSettings settings;
        public TotalWarfareMod(ModContentPack content) : base(content)
        {
            this.settings = GetSettings<TotalWarfareSettings>();
            Instance = this; // 设置静态实例
        }

        public static TotalWarfareMod Instance { get; private set; } // 静态实例
        public TotalWarfareSettings Settings { get; private set; }    // 公有属性


        public override string SettingsCategory() => "NCL_TOTALWARFARE_SETTINGS_CATEGORY".Translate();

        public override void DoSettingsWindowContents(Rect inRect)
        {
            const float verticalSpacing = 12f;
            const float sectionSpacing = 20f;
            const float numberEntryHeight = 30f;

            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

            // === 第一部分：基础设置 ===
            Rect basicLabelRect = listing.GetRect(Text.LineHeight);
            Widgets.Label(basicLabelRect, "NCL_BASIC_SETTINGS".Translate());
            if (!string.IsNullOrEmpty("NCL_BASIC_SETTINGS_DESC".Translate()))
            {
                TooltipHandler.TipRegion(basicLabelRect, "NCL_BASIC_SETTINGS_DESC".Translate());
            }

            listing.Gap(verticalSpacing);

            // 机械增强复选框
            Rect beaconRect = listing.GetRect(Text.LineHeight);
            Widgets.CheckboxLabeled(beaconRect, "NCL_TOTALWARFARE_ENABLE_BEACON_ENHANCEMENT".Translate(),
                                   ref TotalWarfareSettings.EnableMechEnhancement);
            if (!string.IsNullOrEmpty("NCL_TOTALWARFARE_ENABLE_BEACON_ENHANCEMENT_DESC".Translate()))
            {
                TooltipHandler.TipRegion(beaconRect, "NCL_TOTALWARFARE_ENABLE_BEACON_ENHANCEMENT_DESC".Translate());
            }

            listing.Gap(verticalSpacing);

            listing.Label("NCL_AIRSTRIKE_TRIGGER_SETTINGS".Translate());

            // 空袭财富值阈值输入框
            listing.Gap(8f);
            Rect airstrikeRect = listing.GetRect(30f);
            Widgets.Label(airstrikeRect.LeftHalf(), "NCL_AIRSTRIKE_THRESHOLD".Translate());

            string airstrikeBuffer = TotalWarfareSettings.AirstrikeWealthThreshold.ToString("F0");
            Widgets.TextFieldNumeric<float>(
                airstrikeRect.RightHalf(),
                ref TotalWarfareSettings.AirstrikeWealthThreshold,
                ref airstrikeBuffer,
                0f,    // 最小值
                10000000f  // 最大值
            );

            listing.Gap(verticalSpacing);



            listing.Label("NCL_WEALTH_TRIGGER_SETTINGS".Translate());
            // 财富触发复选框
            Rect wealthRect = listing.GetRect(Text.LineHeight);
            Widgets.CheckboxLabeled(wealthRect, "NCL_TOTALWARFARE_ENABLE_WEALTH_TRIGGER".Translate(),
                                   ref TotalWarfareSettings.EnableAutoTrigger);
            if (!string.IsNullOrEmpty("NCL_TOTALWARFARE_ENABLE_WEALTH_TRIGGER_DESC".Translate()))
            {
                TooltipHandler.TipRegion(wealthRect, "NCL_TOTALWARFARE_ENABLE_WEALTH_TRIGGER_DESC".Translate());
            }
            listing.Gap(8f);
            Rect thresholdRect = listing.GetRect(30f);
            Widgets.Label(thresholdRect.LeftHalf(), "NCL_WEALTH_THRESHOLD".Translate());

            string thresholdBuffer = TotalWarfareSettings.WealthTriggerThreshold.ToString("F0");
            Widgets.TextFieldNumeric<float>(
                thresholdRect.RightHalf(),
                ref TotalWarfareSettings.WealthTriggerThreshold,
                ref thresholdBuffer,
                0f,    // 最小值
                10000000f  // 最大值
            );

            listing.Gap(verticalSpacing);

            // 核心触发复选框
            Rect coreRect = listing.GetRect(Text.LineHeight);
            Widgets.CheckboxLabeled(coreRect, "NCL_ENABLE_CORETRIGGER".Translate(),
                                   ref TotalWarfareSettings.EnableCoreTrigger);
            if (!string.IsNullOrEmpty("NCL_ENABLE_CORETRIGGER_DESC".Translate()))
            {
                TooltipHandler.TipRegion(coreRect, "NCL_ENABLE_CORETRIGGER_DESC".Translate());
            }

            listing.Gap(verticalSpacing);

            // ===== 新增：每日特殊 Hediff 上限设置 =====
            try
            {
                Rect dailyLimitRect = listing.GetRect(Text.LineHeight);
                Widgets.Label(dailyLimitRect, "NCL_DAILY_SPECIAL_HEDIFF_LIMIT".Translate());
                if (!string.IsNullOrEmpty("NCL_DAILY_SPECIAL_HEDIFF_LIMIT_DESC".Translate()))
                {
                    TooltipHandler.TipRegion(dailyLimitRect, "NCL_DAILY_SPECIAL_HEDIFF_LIMIT_DESC".Translate());
                }

                listing.Gap(verticalSpacing);

                // 数字输入框
                Rect numberEntryRect = listing.GetRect(numberEntryHeight);

                // 确保设置值已初始化
                if (TotalWarfareSettings.MaxSpecialHediffsPerDay <= 0)
                    TotalWarfareSettings.MaxSpecialHediffsPerDay = 50;

                // 使用局部变量避免直接操作静态字段
                int currentValue = TotalWarfareSettings.MaxSpecialHediffsPerDay;
                string buffer = currentValue.ToString();

                Widgets.TextFieldNumeric<int>(
                    numberEntryRect,
                    ref currentValue,
                    ref buffer,
                    1,   // 最小值
                    1000 // 最大值
                );

                // 更新设置值
                TotalWarfareSettings.MaxSpecialHediffsPerDay = currentValue;
            }
            catch (Exception ex)
            {
                // 记录错误但继续执行其他UI绘制
                Log.Error($"Error in daily counter UI: {ex}");
            }

            listing.Gap(sectionSpacing);

            // ===== 新增：每日特殊 Hediff 上限设置 =====
            try
            {
                Rect dailyLimitRect = listing.GetRect(Text.LineHeight);
                Widgets.Label(dailyLimitRect, "NCL_DAILY_SPECIAL_HEDIFF_LIMIT_A".Translate());
                if (!string.IsNullOrEmpty("NCL_DAILY_SPECIAL_HEDIFF_LIMIT_DESC_A".Translate()))
                {
                    TooltipHandler.TipRegion(dailyLimitRect, "NCL_DAILY_SPECIAL_HEDIFF_LIMIT_DESC_A".Translate());
                }

                listing.Gap(verticalSpacing);

                // 数字输入框
                Rect numberEntryRect = listing.GetRect(numberEntryHeight);

                // 确保设置值已初始化
                if (TotalWarfareSettings.MaxSpecialHediffsPerDayA <= 0)
                    TotalWarfareSettings.MaxSpecialHediffsPerDayA = 50;

                // 使用局部变量避免直接操作静态字段
                int currentValue = TotalWarfareSettings.MaxSpecialHediffsPerDayA;
                string buffer = currentValue.ToString();

                Widgets.TextFieldNumeric<int>(
                    numberEntryRect,
                    ref currentValue,
                    ref buffer,
                    1,   // 最小值
                    1000 // 最大值
                );

                // 更新设置值
                TotalWarfareSettings.MaxSpecialHediffsPerDayA = currentValue;
            }
            catch (Exception ex)
            {
                // 记录错误但继续执行其他UI绘制
                Log.Error($"Error in daily counter UI: {ex}");
            }

            listing.Gap(sectionSpacing);

            // === 第三部分：特殊操作按钮 ===
            Rect operationsLabelRect = listing.GetRect(Text.LineHeight);
            Widgets.Label(operationsLabelRect, "NCL_OPERATIONS".Translate());
            if (!string.IsNullOrEmpty("NCL_OPERATIONS_DESC".Translate()))
            {
                TooltipHandler.TipRegion(operationsLabelRect, "NCL_OPERATIONS_DESC".Translate());
            }

            listing.Gap(verticalSpacing);

            // 生成古代战争信标按钮
            Rect buttonRect = listing.GetRect(30f);
            if (Widgets.ButtonText(buttonRect, "NCL_GENERATE_ANCIENT_WAR_BEACON_REMAINS".Translate()))
            {
                ExecuteAncientWarBeaconRemains();
            }
            if (!string.IsNullOrEmpty("NCL_GENERATE_ANCIENT_WAR_BEACON_REMAINS_DESC".Translate()))
            {
                TooltipHandler.TipRegion(buttonRect, "NCL_GENERATE_ANCIENT_WAR_BEACON_REMAINS_DESC".Translate());
            }



            listing.End();
        }





        private void ExecuteAncientWarBeaconRemains()
        {
            Map targetMap = GetPlayerCurrentMap();

            if (targetMap == null)
            {
                Messages.Message("NCL_NO_CURRENT_MAP_FOUND".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }

            try
            {
                // 特殊处理飞船地图
                bool isGravShipMap = targetMap.wasSpawnedViaGravShipLanding;

                if (isGravShipMap)
                {
                    // 确保地图已完全初始化
                    if (targetMap.mapDrawer == null || targetMap.terrainGrid == null)
                    {
                        Messages.Message("NCL_MAP_NOT_READY_FOR_GENERATION".Translate(), MessageTypeDefOf.RejectInput);
                        return;
                    }
                }

                // 查找合适的边缘位置（离边缘至少15格）
                IntVec3 centerPos = FindSuitablePosition(targetMap);

                if (!centerPos.IsValid)
                {
                    Messages.Message("NCL_NO_VALID_SPOT_FOUND".Translate(), MessageTypeDefOf.RejectInput);
                    return;
                }

                // 生成整个机甲遗迹
                GenerateExostriderRemains(targetMap, centerPos);

                // 添加视觉效果
                FleckMaker.ThrowDustPuffThick(centerPos.ToVector3Shifted(), targetMap, 8f, Color.gray);

                Messages.Message("NCL_SUCCESS_GENERATE_ANCIENT_WAR_BEACON".Translate(
                    centerPos.x, centerPos.z
                ), MessageTypeDefOf.PositiveEvent);
            }
            catch (Exception ex)
            {
                Log.Error($"Error executing AncientWarBeaconRemains: {ex}");
                Messages.Message("NCL_FAILED_GENERATE".Translate(ex.Message), MessageTypeDefOf.ThreatBig);
            }
        }

        private IntVec3 FindSuitablePosition(Map map)
        {
            // 确保位置离边缘至少15格
            int minEdgeDist = 15;
            CellRect safeArea = new CellRect(
                minEdgeDist,
                minEdgeDist,
                map.Size.x - minEdgeDist * 2,
                map.Size.z - minEdgeDist * 2
            );

            // 尝试100次找到合适位置
            for (int attempt = 0; attempt < 100; attempt++)
            {
                IntVec3 candidatePos = safeArea.RandomCell;

                // 确保位置有效
                if (!candidatePos.InBounds(map) || !candidatePos.Standable(map))
                    continue;

                // 检查是否靠近地图中心（地图尺寸33.3%范围内）
                float minDist = Mathf.Min(map.Size.x, map.Size.z) * 0.333f;
                if (candidatePos.DistanceTo(map.Center) < minDist)
                    continue;

                // 检查是否有建筑
                if (candidatePos.GetEdifice(map) != null)
                    continue;

                // 检查是否在玩家基地区域
                if (map.areaManager.Home[candidatePos])
                    continue;

                // 检查附近15格是否有玩家建筑
                bool nearPlayerBuilding = GenRadial.RadialDistinctThingsAround(candidatePos, map, 15f, true)
                    .Any(t => t.Faction == Faction.OfPlayer && t.def.category == ThingCategory.Building);

                if (!nearPlayerBuilding)
                {
                    return candidatePos;
                }
            }

            return IntVec3.Invalid;
        }

        // 生成完整的机甲遗迹
        private void GenerateExostriderRemains(Map map, IntVec3 center)
        {
            // 清除中心区域（5格半径）
            ClearArea(map, center, 5);

            // 生成所有机甲部件
            PlacePart(map, center, "AncientExostriderHead", new IntVec3(2, 0, 0));
            PlacePart(map, center, "AncientExostriderRemains", IntVec3.Zero);
            PlacePart(map, center, "Building_Ancient_WarBeacon", new IntVec3(-2, 0, -2));
            PlacePart(map, center, "AncientExostriderCannon", new IntVec3(-1, 0, 2));
            PlacePart(map, center, "AncientExostriderLeg", new IntVec3(-3, 0, 1));
            PlacePart(map, center, "AncientExostriderLeg", new IntVec3(-4, 0, 0));
            PlacePart(map, center, "AncientExostriderLeg", new IntVec3(1, 0, -2), Rot4.West);
            PlacePart(map, center, "Ancient_WarBeacon_PartsB", new IntVec3(0, 0, -3), Rot4.West);
            PlacePart(map, center, "Ancient_WarBeacon_PartsA", new IntVec3(-2, 0, -4));
            PlacePart(map, center, "Broken_War_Scorpion_Rust", new IntVec3(2, 0, 3));

            // 在所有部件周围生成机器碎片污垢
            GenerateFilthAroundParts(map, center, 5);
        }

        // 清除指定区域
        private void ClearArea(Map map, IntVec3 center, int radius)
        {
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, radius, true))
            {
                if (cell.InBounds(map) && cell.Standable(map))
                {
                    // 清除现有建筑和物品
                    List<Thing> things = cell.GetThingList(map);
                    for (int i = things.Count - 1; i >= 0; i--)
                    {
                        if (things[i].def.category == ThingCategory.Building ||
                            things[i].def.category == ThingCategory.Item)
                        {
                            things[i].Destroy(DestroyMode.Vanish);
                        }
                    }

                    // 清除植物
                    Plant plant = cell.GetPlant(map);
                    if (plant != null)
                    {
                        plant.Destroy(DestroyMode.Vanish);
                    }
                }
            }
        }

        // 放置单个部件
        private void PlacePart(Map map, IntVec3 center, string thingDefName, IntVec3 offset, Rot4? rotation = null)
        {
            IntVec3 position = center + offset;
            if (!position.InBounds(map)) return;

            ThingDef thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(thingDefName);
            if (thingDef == null)
            {
                Log.Warning($"Missing thing def: {thingDefName}");
                return;
            }

            Thing part = ThingMaker.MakeThing(thingDef);
            part.SetFaction(null); // 设置为古代遗迹

            // 设置旋转
            if (rotation.HasValue)
            {
                part.Rotation = rotation.Value;
            }

            // 放置部件
            GenPlace.TryPlaceThing(part, position, map, ThingPlaceMode.Direct);
        }

        // 在部件周围生成机器碎片污垢
        private void GenerateFilthAroundParts(Map map, IntVec3 center, int radius)
        {
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, radius, true))
            {
                if (!cell.InBounds(map) || !cell.Standable(map)) continue;

                // 检查是否有部件
                bool hasPart = cell.GetThingList(map).Any(t =>
                    t.def.defName.Contains("Ancient") ||
                    t.def.defName.Contains("Broken_War") ||
                    t.def.defName.Contains("Building_Ancient"));

                if (hasPart)
                {
                    // 在部件周围1格范围内生成污垢
                    foreach (IntVec3 filthCell in GenRadial.RadialCellsAround(cell, 1, true))
                    {
                        if (filthCell.InBounds(map) && filthCell.Standable(map) && Rand.Chance(0.6f))
                        {
                            FilthMaker.TryMakeFilth(
                                filthCell,
                                map,
                                DefDatabase<ThingDef>.GetNamed("Filth_MachineBits")
                            );
                        }
                    }
                }
            }
        }






        private Map GetPlayerCurrentMap()
        {
            var freeColonists = new List<Pawn>();
            foreach (Map map in Find.Maps)
            {
                freeColonists.AddRange(map.mapPawns.FreeColonists);
            }

            var validColonists = freeColonists
                .Where(pawn => pawn.Spawned && !pawn.Dead)
                .ToList();

            if (validColonists.Count > 0)
            {
                Pawn randomColonist = validColonists.RandomElement();
                return randomColonist.Map;
            }

            return null;
        }



    }
}


namespace NCL
{
    // Token: 0x020000E3 RID: 227
    public class Hediff_MechNode : Hediff
    {
        // Token: 0x170000B9 RID: 185
        // (get) Token: 0x06000473 RID: 1139 RVA: 0x000250E0 File Offset: 0x000232E0
        public int AdditionalBandwidth
        {
            get
            {
                return this.cachedTunedBandNodesCount;
            }
        }

        // Token: 0x170000BA RID: 186
        // (get) Token: 0x06000474 RID: 1140 RVA: 0x000250F8 File Offset: 0x000232F8
        public override bool ShouldRemove
        {
            get
            {
                return this.cachedTunedBandNodesCount == 0;
            }
        }

        // Token: 0x170000BB RID: 187
        // (get) Token: 0x06000475 RID: 1141 RVA: 0x00025114 File Offset: 0x00023314
        public override HediffStage CurStage
        {
            get
            {
                bool flag = this.curStage == null && this.cachedTunedBandNodesCount > 0;
                if (flag)
                {
                    StatModifier statModifier = new StatModifier();
                    statModifier.stat = StatDefOf.MechBandwidth;
                    statModifier.value = (float)this.cachedTunedBandNodesCount;
                    this.curStage = new HediffStage();
                    this.curStage.statOffsets = new List<StatModifier>
                    {
                        statModifier
                    };
                }
                return this.curStage;
            }
        }

        // Token: 0x06000476 RID: 1142 RVA: 0x00025188 File Offset: 0x00023388
        public override void PostTick()
        {
            base.PostTick();
            bool flag = this.pawn.IsHashIntervalTick(60);
            if (flag)
            {
                this.RecacheBandNodes();
            }
        }

        // Token: 0x06000477 RID: 1143 RVA: 0x000251B7 File Offset: 0x000233B7
        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
            this.RecacheBandNodes();
        }

        public void RecacheBandNodes()
        {
            int num = this.cachedTunedBandNodesCount;
            this.cachedTunedBandNodesCount = 0;
            bool foundNode = false;
            List<Map> maps = Find.Maps;

            for (int i = 0; i < maps.Count; i++)
            {
                // 使用正确的属性名 allBuildingsColonist（首字母小写）
                List<Building> allBuildings = maps[i].listerBuildings.allBuildingsColonist;

                foreach (Building thing in allBuildings)
                {
                    // 检查是否是机械节点
                    CompBandNode bandComp = thing.TryGetComp<CompBandNode>();
                    if (bandComp == null) continue;

                    // 检查是否绑定到当前角色且通电
                    CompPowerTrader powerComp = thing.TryGetComp<CompPowerTrader>();
                    bool flag = bandComp.tunedTo == this.pawn && powerComp != null && powerComp.PowerOn;

                    if (flag)
                    {
                        foundNode = true;

                        // 检查是否是升级节点
                        var upgradableComp = thing.TryGetComp<CompUpgradableBandNode>();
                        if (upgradableComp != null)
                        {
                            // 使用升级节点的带宽点数
                            this.cachedTunedBandNodesCount += upgradableComp.GetBandwidthPoints();
                        }
                        else
                        {
                            // 普通节点提供固定带宽
                            this.cachedTunedBandNodesCount += TotalWarfareSettings.AdvancedNodeCount;
                        }
                    }
                }
            }

            bool flag2 = num != this.cachedTunedBandNodesCount;
            if (flag2 || !foundNode)
            {
                this.curStage = null;
                Pawn_MechanitorTracker mechanitor = this.pawn.mechanitor;
                if (mechanitor != null)
                {
                    mechanitor.Notify_BandwidthChanged();
                }
            }
        }




        // Token: 0x06000479 RID: 1145 RVA: 0x000252DC File Offset: 0x000234DC
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look<int>(ref this.cachedTunedBandNodesCount, "cachedTunedBandNodesCount", 0, false);
        }

        // Token: 0x04000261 RID: 609
        private const int BandNodeCheckInterval = 60;

        // Token: 0x04000262 RID: 610
        private int cachedTunedBandNodesCount;

        // Token: 0x04000263 RID: 611
        private HediffStage curStage;
    }
}




namespace NCL
{
    public class CompProperties_UpgradableBandNode : CompProperties_BandNode
    {
        public int baseBandwidth = 1;
        public int bandwidthPerGeneration = 1;
        public int maxGenerations = 5;
        public float generationCooldownDays = 1f;

        // 每次升级增加的电力消耗
        public float extraPowerConsumptionPerGeneration = 50f;

        public CompProperties_UpgradableBandNode()
        {
            compClass = typeof(CompUpgradableBandNode);
        }
    }

    public class CompUpgradableBandNode : CompBandNode
    {
        private int generations = 0;
        private int cooldownTicks = 0;
        private int prevGenerations = -1;

        // 缓存电力组件
        private CompPowerTrader powerComp;
        private float originalBasePowerConsumption;

        public new CompProperties_UpgradableBandNode Props
            => (CompProperties_UpgradableBandNode)props;

        public int GenerationCount => generations;
        public bool CanGenerate => generations < Props.maxGenerations && cooldownTicks <= 0;

        // 计算总额外消耗 = 升级次数 × 每次消耗值
        public float TotalExtraPowerConsumption => generations * Props.extraPowerConsumptionPerGeneration;



        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref generations, "generations", 0);
            Scribe_Values.Look(ref cooldownTicks, "cooldownTicks", 0);
            Scribe_Values.Look(ref prevGenerations, "prevGenerations", -1);
            Scribe_Values.Look(ref originalBasePowerConsumption, "originalBasePowerConsumption", 0f);
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);

            // 确保获取电力组件
            powerComp = parent.GetComp<CompPowerTrader>();

            if (powerComp == null)
            {
                Log.Error($"[{parent.Label}] has no CompPowerTrader component");
                return;
            }

            // 保存原始基础电力消耗
            originalBasePowerConsumption = powerComp.Props.PowerConsumption;

            if (!respawningAfterLoad)
            {
                prevGenerations = generations;
            }

            // 初始应用额外电力消耗
            ApplyExtraPowerConsumption();
        }

        public override void CompTick()
        {
            // 安全调用父类逻辑
            SafeBaseCompTick();

            // 冷却倒计时
            if (cooldownTicks > 0)
            {
                cooldownTicks--;
            }

            // 应用额外电力消耗
            ApplyExtraPowerConsumption();

            // 当生成次数变化时强制重新计算带宽
            if (prevGenerations != generations)
            {
                prevGenerations = generations;

                if (tunedTo != null)
                {
                    Hediff_MechNode mechNodeHediff = GetMechNodeHediff(tunedTo);
                    if (mechNodeHediff != null)
                    {
                        mechNodeHediff.RecacheBandNodes();
                    }
                }
            }
        }

        // 安全调用父类逻辑
        private void SafeBaseCompTick()
        {
            if (powerComp == null) return;

            try
            {
                // 1. 设置电力消耗
                powerComp.PowerOutput = (tunedTo == null && tuningTo == null)
                    ? -Props.powerConsumptionIdle
                    : -powerComp.Props.PowerConsumption;

                // 2. 检查机械师是否死亡
                if (tunedTo != null && tunedTo.Dead)
                {
                    tunedTo = null;
                }
                if (tuningTo != null && tuningTo.Dead)
                {
                    tuningTo = null;
                }

                // 3. 处理调谐逻辑
                if (tuningTo != null)
                {
                    tuningTimeLeft--;
                    if (tuningTimeLeft <= 0)
                    {
                        tunedTo = tuningTo;
                        tuningTo = null;
                        if (Props.tuningCompleteSound != null)
                        {
                            Props.tuningCompleteSound.PlayOneShot(parent);
                        }
                    }
                }

                // 4. 添加 Hediff
                if (tuningTo == null && tunedTo != null &&
                    !tunedTo.health.hediffSet.HasHediff(Props.hediff, false))
                {
                    tunedTo.health.AddHediff(Props.hediff, tunedTo.health.hediffSet.GetBrain());
                }

                // 5. 处理特效
                // [复制父类的特效处理代码]
                if (powerComp != null && !powerComp.PowerOn)
                {
                    // ... [清理特效] ...
                    return;
                }

                // ... [根据状态设置特效] ...
            }
            catch (NullReferenceException ex)
            {
                Log.Error($"SafeBaseCompTick error for {parent.Label}: {ex}");
            }
        }

        // 应用额外电力消耗
        private void ApplyExtraPowerConsumption()
        {
            if (powerComp == null) return;

            // 获取当前状态
            BandNodeState currentState = GetCurrentState();

            // 计算基础消耗
            float baseConsumption = currentState == BandNodeState.Untuned
                ? Props.powerConsumptionIdle
                : originalBasePowerConsumption;

            // 计算总电力消耗 = 基础消耗 + 额外消耗
            float totalConsumption = baseConsumption + TotalExtraPowerConsumption;

            // 应用总电力消耗
            powerComp.PowerOutput = -totalConsumption;
        }

        // 复制父类状态判断逻辑
        private BandNodeState GetCurrentState()
        {
            if (tunedTo != null && tuningTo != null) return BandNodeState.Retuning;
            if (tuningTo != null) return BandNodeState.Tuning;
            if (tunedTo != null) return BandNodeState.Tuned;
            return BandNodeState.Untuned;
        }

        // 获取当前带宽点数
        public int GetBandwidthPoints()
        {
            return Props.baseBandwidth + (Props.bandwidthPerGeneration * generations);
        }

        // 获取机械师的带宽hediff
        private Hediff_MechNode GetMechNodeHediff(Pawn mechanitor)
        {
            if (mechanitor == null || mechanitor.health == null || mechanitor.health.hediffSet == null)
                return null;

            return mechanitor.health.hediffSet.GetFirstHediffOfDef(Props.hediff) as Hediff_MechNode;
        }

        // 执行一次带宽生成
        public void GenerateBandwidth()
        {
            if (!CanGenerate) return;

            generations++;
            cooldownTicks = (int)(Props.generationCooldownDays * 60000);

            // 使用父类的调谐完成音效
            if (Props.tuningCompleteSound != null)
            {
                Props.tuningCompleteSound.PlayOneShot(parent);
            }

            // 应用额外电力消耗
            ApplyExtraPowerConsumption();

            // 强制重新计算带宽
            if (tunedTo != null)
            {
                Hediff_MechNode mechNodeHediff = GetMechNodeHediff(tunedTo);
                if (mechNodeHediff != null)
                {
                    mechNodeHediff.RecacheBandNodes();
                }

                if (tunedTo.mechanitor != null)
                {
                    tunedTo.mechanitor.Notify_BandwidthChanged();
                }
            }

            Messages.Message("BandwidthGenerated".Translate(parent.Label, generations, Props.maxGenerations),
                parent, MessageTypeDefOf.PositiveEvent);
        }

        // 重置等级
        public void ResetGenerations()
        {
            if (generations == 0) return;

            generations = 0;
            cooldownTicks = 0;
            prevGenerations = -1;

            // 应用电力消耗重置
            ApplyExtraPowerConsumption();

            // 强制重新计算带宽
            if (tunedTo != null)
            {
                Hediff_MechNode mechNodeHediff = GetMechNodeHediff(tunedTo);
                if (mechNodeHediff != null)
                {
                    mechNodeHediff.RecacheBandNodes();
                }

                if (tunedTo.mechanitor != null)
                {
                    tunedTo.mechanitor.Notify_BandwidthChanged();
                }
            }

            Messages.Message("BandwidthReset".Translate(parent.Label),
                parent, MessageTypeDefOf.NeutralEvent);
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }

            // 添加生成带宽按钮
            if (tunedTo != null)
            {
                yield return new Command_Action
                {
                    icon = ContentFinder<Texture2D>.Get("ModIcon/GenerateBandwidth"),
                    defaultLabel = "GenerateBandwidth".Translate(),
                    defaultDesc = "GenerateBandwidthDesc".Translate(
                        Props.bandwidthPerGeneration,
                        Props.extraPowerConsumptionPerGeneration,
                        generations,
                        Props.maxGenerations),
                    action = GenerateBandwidth,

                    disabledReason = !CanGenerate
                        ? (generations >= Props.maxGenerations
                            ? "MaxGenerationsReached".Translate()
                            : "CooldownActive".Translate(cooldownTicks.ToStringTicksToPeriod(true, false, true, true, false)))
                        : null
                };

                // 添加重置等级按钮
                if (generations > 0)
                {
                    yield return new Command_Action
                    {
                        icon = ContentFinder<Texture2D>.Get("ModIcon/ResetBandwidth"),
                        defaultLabel = "ResetBandwidth".Translate(),
                        defaultDesc = "ResetBandwidthDesc".Translate(),
                        action = ResetGenerations
                    };
                }
            }
        }

        public override string CompInspectStringExtra()
        {
            string baseStr = base.CompInspectStringExtra();

            string bandwidthInfo = "BandwidthPoints".Translate() + ": " +
                GetBandwidthPoints() + " (" +
                Props.baseBandwidth + " + " +
                Props.bandwidthPerGeneration + " × " +
                generations + ")";

            // 显示总额外消耗 = 升级次数 × 每次消耗值
            string powerInfo = "TotalExtraPower".Translate() + ": " +
                TotalExtraPowerConsumption + "W";

            string generationInfo = "Generations".Translate() + ": " +
                generations + "/" + Props.maxGenerations;

            string cooldownInfo = cooldownTicks > 0
                ? "Cooldown".Translate() + ": " + cooldownTicks.ToStringTicksToPeriod(true, false, true, true, false)
                : "ReadyToGenerate".Translate();

            return $"{baseStr}\n{bandwidthInfo}\n{powerInfo}\n{generationInfo}\n{cooldownInfo}";
        }
    }
}


namespace RimWorld
{
    public class CompProperties_RestartableCerebrexCore : CompProperties
    {
        public float bobFrequency = 0.02f;
        public float bobAmplitude = 0.35f;
        public float zOffset = 4f;
        public float yOffset = 0.35f;
        public int restartDurationTicks = 300;

        public CompProperties_RestartableCerebrexCore()
        {
            compClass = typeof(CompRestartableCerebrexCore);
        }
    }

    public class CompRestartableCerebrexCore : ThingComp
    {
        private Graphic brainGraphic;
        private int restartCountdown = -1;
        private bool isRestarting;
        private bool restartCompleted = false;
        private bool spawnMailSent = false; // 新增：是否已发送首次出现邮件

        private CompProperties_RestartableCerebrexCore Props => (CompProperties_RestartableCerebrexCore)props;

        private Graphic BrainGraphic
        {
            get
            {
                if (brainGraphic == null)
                {
                    brainGraphic = GraphicDatabase.Get<Graphic_Multi>(
                        "Buildings/MechCoreTop",
                        ShaderDatabase.Cutout,
                        new Vector3(7f, 7f, 7f),
                        Color.white
                    );
                }
                return brainGraphic;
            }
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);

            // 如果不是重新加载游戏且是第一次生成
            if (!respawningAfterLoad && !spawnMailSent)
            {
                SendSpawnMail(); // 发送首次出现邮件
                spawnMailSent = true;
            }
        }

        // 新增：发送首次出现邮件
        private void SendSpawnMail()
        {
            Find.LetterStack.ReceiveLetter(
                "CerebrexCore_SpawnTitle".Translate(),
                "CerebrexCore_SpawnDesc".Translate(),
                LetterDefOf.NeutralEvent,
                parent);
        }

        public override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            base.DrawAt(drawLoc, flip);
            float z = Props.zOffset + 0.5f * (1f + Mathf.Sin(6.2831855f * (float)GenTicks.TicksGame / 300f)) * Props.bobAmplitude;
            Matrix4x4 matrix = Matrix4x4.TRS(
                drawLoc + new Vector3(0f, Props.yOffset, z),
                Quaternion.AngleAxis(0f, Vector3.up),
                Vector3.one
            );
            GenDraw.DrawMeshNowOrLater(
                BrainGraphic.MeshAt(Rot4.South),
                matrix,
                BrainGraphic.MatSouth,
                false
            );
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (!isRestarting && !restartCompleted && parent.Faction == Faction.OfPlayer)
            {
                yield return new Command_Action
                {
                    icon = ContentFinder<Texture2D>.Get("ModIcon/RestartCore"),
                    defaultLabel = "RestartCore".Translate(),
                    defaultDesc = "RestartCoreDECS".Translate(),
                    activateSound = SoundDefOf.Tick_Low,
                    action = StartCoreRestart
                };
            }

            if (isRestarting && !restartCompleted)
            {
                yield return new Command_Action
                {
                    icon = ContentFinder<Texture2D>.Get("ModIcon/RestartCore"),
                    defaultLabel = "CancelRestart".Translate(),
                    defaultDesc = "CancelRestartDESC".Translate(),
                    activateSound = SoundDefOf.Click,
                    action = CancelRestart
                };
            }
        }

        private void StartCoreRestart()
        {
            isRestarting = true;
            restartCountdown = Props.restartDurationTicks;
            Find.TickManager.slower.SignalForceNormalSpeed();
            Find.MusicManagerPlay.ForceFadeoutAndSilenceFor(999f, 3f, false);
        }

        private void CancelRestart()
        {
            isRestarting = false;
            restartCountdown = -1;
        }

        public override void CompTick()
        {
            base.CompTick();

            if (restartCountdown > 0)
            {
                restartCountdown--;
                if (restartCountdown == 0)
                {
                    CompleteRestart();
                }
            }
        }

        private void CompleteRestart()
        {
            restartCompleted = true;

            if (Faction.OfMechanoids != null)
            {
                typeof(Faction).GetField("deactivated",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.SetValue(Faction.OfMechanoids, false);
            }

            CameraJumper.TryJump(parent, CameraJumper.MovementMode.Cut);
            Messages.Message("CerebrexCoreRestored".Translate(), MessageTypeDefOf.NeutralEvent);
            ActivateNCLTotalWarfare();
            isRestarting = false;
        }

        private void ActivateNCLTotalWarfare()
        {
            TipComponent tipComp = Find.World.GetComponent<TipComponent>();
            if (tipComp != null)
            {
                tipComp.TWtriggered3 = true;
                tipComp.lastActivationTick = Find.TickManager.TicksGame;

                Find.LetterStack.ReceiveLetter(
                    "NCL_COREBASED_ENHANCEMENT_TITLE".Translate(),
                    "NCL_COREBASED_ENHANCEMENT_DESC".Translate(),
                    LetterDefOf.NeutralEvent);

                if (TotalWarfareSettings.ShowDevLogs)
                {
                    Log.Message("[NCL] Total Warfare system activated by Cerebrex Core");
                }
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref restartCountdown, "restartCountdown", -1);
            Scribe_Values.Look(ref isRestarting, "isRestarting", false);
            Scribe_Values.Look(ref restartCompleted, "restartCompleted", false);
            Scribe_Values.Look(ref spawnMailSent, "spawnMailSent", false);
        }
    }
}


namespace NCL
{

    public class CompProperties_TotalWarfareAlphaHediff : CompProperties
    {

        public CompProperties_TotalWarfareAlphaHediff()
        {
            this.compClass = typeof(CompTotalWarfareAlphaHediff);
        }


        public List<HediffDef> TWhediffsRange;


        public List<HediffDef> TWhediffsMelee;
    }
}

namespace NCL
{
    public class CompTotalWarfareAlphaHediff : ThingComp
    {
        public CompProperties_TotalWarfareAlphaHediff Props
        {
            get
            {
                return (CompProperties_TotalWarfareAlphaHediff)this.props;
            }
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            bool flag = !TotalWarfareSettings.EnableMechEnhancement ||  // 新增条件
           TWSettings.ReinforceNotApply ||
           !Find.World.GetComponent<TipComponent>().TWtriggered3 ||
           this.applyed;

            if (flag)
            {
                this.applyed = true;
            }
            else
            {
                Pawn pawn = this.parent as Pawn;
                bool flag2 = pawn.def.defName == "TW_Mech_Doxa" || pawn.def.defName == "TW_Mech_Shell_Fortification" || pawn.def.defName == "Mech_BlackApocriton";
                if (!flag2)
                {
                    BodyPartRecord brain = pawn.health.hediffSet.GetBrain();
                    bool flag3 = brain != null && (pawn.Faction == null || pawn.Faction.HostileTo(Faction.OfPlayer));
                    bool flag4 = !flag3;
                    if (!flag4)
                    {
                        bool flag5 = pawn.equipment.Primary != null && pawn.equipment.Primary.def.IsRangedWeapon;
                        HediffDef def;
                        if (flag5)
                        {
                            def = this.Props.TWhediffsRange.RandomElement<HediffDef>();
                        }
                        else
                        {
                            def = this.Props.TWhediffsMelee.RandomElement<HediffDef>();
                        }
                        bool flag6 = Rand.Chance(0.10f);
                        if (flag6)
                        {
                            def = TWDefOf.TW_TacticalArtilleryCoordinationModule_AutoMortar;
                        }


                        Pawn_HealthTracker health = pawn.health;
                        object obj;
                        if (health == null)
                        {
                            obj = null;
                        }
                        else
                        {
                            HediffSet hediffSet = health.hediffSet;
                            obj = ((hediffSet != null) ? hediffSet.GetFirstHediffOfDef(def, false) : null);
                        }
                        bool flag7 = obj == null;
                        if (flag7)
                        {
                            Hediff hediff = HediffMaker.MakeHediff(def, pawn, null);
                            pawn.health.AddHediff(hediff, brain, null, null);
                            this.applyed = true;
                        }
                    }
                }
            }
        }

        // Token: 0x0600028A RID: 650 RVA: 0x000156C0 File Offset: 0x000138C0
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look<bool>(ref this.applyed, "applyed", false);
        }

        // Token: 0x04000161 RID: 353
        public bool applyed = false;
    }
}

namespace NCL
{
    public class GenStepExecutionSettings
    {
        public static GenStepDef SelectedGenStep;
        public static bool ShowExecutionButton = true;

        public static void ExposeData()
        {
            Scribe_Defs.Look(ref SelectedGenStep, "selectedGenStep");
            Scribe_Values.Look(ref ShowExecutionButton, "showExecutionButton", true);
        }
    }
}

