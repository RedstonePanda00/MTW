using System.Collections.Generic;
using NCL;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace NCL
{
    // 主组件类
    public class CompAntiInvisibilityField : ThingComp
    {
        // 配置属性
        public CompProperties_AntiInvisibilityField Props =>
            (CompProperties_AntiInvisibilityField)props;

        // 运行时数据
        private HashSet<Pawn> affectedPawns = new HashSet<Pawn>();
        private int nextCheckTick;
        private Effecter activeEffecter;
        private bool isActivated;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            isActivated = Props.startActivated;
            nextCheckTick = Find.TickManager.TicksGame + Props.checkIntervalTicks;
        }

        public void ToggleActivation()
        {
            isActivated = !isActivated;
            if (!isActivated)
            {
                affectedPawns.Clear();
                activeEffecter?.Cleanup();
                activeEffecter = null;
            }
        }

        private bool HasPower()
        {
            if (!Props.requiresPower) return true;

            if (parent.GetComp<CompPowerTrader>() is CompPowerTrader powerComp)
            {
                return powerComp.PowerOn;
            }
            return false; // 需要电力但没有电力组件时返回false
        }

        public override void CompTick()
        {
            if (!isActivated || !HasPower())
            {
                affectedPawns.Clear();
                activeEffecter?.Cleanup();
                activeEffecter = null;
                return;
            }

            // 新增电力检查
            if (!HasPower())
            {
                affectedPawns.Clear();
                activeEffecter?.Cleanup();
                activeEffecter = null;
                return;
            }

            if (!parent.Spawned || Find.TickManager.TicksGame < nextCheckTick)
                return;

            CheckArea();
            nextCheckTick = Find.TickManager.TicksGame + Props.checkIntervalTicks;
        }

        // 区域检测逻辑
        private void CheckArea()
        {
            if (!isActivated || !parent.Spawned || !HasPower()) return;

            if (!parent.Spawned || !HasPower()) return;

            if (!parent.Spawned) return;

            // 清理已离开区域的单位
            affectedPawns.RemoveWhere(p =>
                p == null ||
                !p.Spawned ||
                p.Position.DistanceTo(parent.Position) > Props.effectiveRadius);

            // 检测新进入区域的单位
            foreach (Thing thing in GenRadial.RadialDistinctThingsAround(
                parent.Position,
                parent.Map,
                Props.effectiveRadius,
                Props.affectsThroughWalls))
            {
                if (thing is Pawn pawn && ShouldAffect(pawn))
                {
                    HandleInvisiblePawn(pawn);
                }
            }

            // 更新持续效果
            UpdateOngoingEffects();
        }

        // 判断是否应该影响该单位
        private bool ShouldAffect(Pawn pawn)
        {
            // 基础检查
            if (pawn == null || !pawn.Spawned || pawn.health?.hediffSet == null)
                return false;

            // 阵营过滤
            if (!Props.affectsAllFactions && pawn.Faction == parent.Faction)
                return false;

            // 检查是否处于有效隐身状态
            return IsEffectivelyInvisible(pawn);
        }

        // 改进的隐身状态检测（不依赖特定HediffDef）
        private bool IsEffectivelyInvisible(Pawn pawn)
        {
            // 1. 检查所有hediff的隐身组件
            foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
            {
                var invisComp = hediff.TryGetComp<HediffComp_Invisibility>();
                if (invisComp != null && !invisComp.PsychologicallyVisible)
                {
                    return true;
                }
            }

            // 2. 检查mindState的隐身标记（兼容非标准实现）
            if (pawn.mindState != null)
            {
                if (pawn.mindState.lastBecameInvisibleTick > pawn.mindState.lastBecameVisibleTick)
                {
                    return true;
                }
            }

            // 3. 检查绘制状态（最终回退方案）
            if (pawn.Drawer?.renderer?.GetType().Name.Contains("Invisible") ?? false)
            {
                return true;
            }

            return false;
        }

        // 处理隐身单位
        private void HandleInvisiblePawn(Pawn pawn)
        {
            // 强制显形
            bool wasDisrupted = false;

            // 方法1：通过标准隐身组件
            foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
            {
                var invisComp = hediff.TryGetComp<HediffComp_Invisibility>();
                if (invisComp != null)
                {
                    invisComp.DisruptInvisibility();
                    wasDisrupted = true;
                }
            }

            // 方法2：直接修改mindState（兼容非组件实现）
            if (!wasDisrupted && pawn.mindState != null)
            {
                pawn.mindState.lastBecameVisibleTick = Find.TickManager.TicksGame;
                pawn.Notify_BecameVisible();
                wasDisrupted = true;
            }

            // 应用效果
            if (wasDisrupted)
            {
                affectedPawns.Add(pawn);
                PlayEffects(pawn);
                ApplyAdditionalEffects(pawn);
            }
        }

        // 播放视觉效果
        private void PlayEffects(Pawn pawn)
        {
            // 单次特效
            Props.instantEffecterDef?.Spawn(pawn, parent.Map)?.Cleanup();

            // 持续特效
            if (Props.continuousEffecterDef != null)
            {
                activeEffecter ??= Props.continuousEffecterDef.Spawn();
                activeEffecter.EffectTick(pawn, parent);
            }
        }

        // 应用附加效果
        private void ApplyAdditionalEffects(Pawn pawn)
        {
            if (Props.applyHediff != null)
            {
                pawn.health.AddHediff(Props.applyHediff);
            }

            if (Props.soundOnReveal != null)
            {
                Props.soundOnReveal.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
            }
        }

        // 更新持续效果
        private void UpdateOngoingEffects()
        {
            if (activeEffecter != null && affectedPawns.Count == 0)
            {
                activeEffecter.Cleanup();
                activeEffecter = null;
            }
        }

        // 清理
        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            affectedPawns.Clear();
            activeEffecter?.Cleanup();
            activeEffecter = null;
            base.PostDestroy(mode, previousMap);
        }

        // 修改后的CompGetGizmosExtra方法 - 只在玩家阵营时显示
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            // 首先返回基类的gizmos
            foreach (var g in base.CompGetGizmosExtra())
                yield return g;

            // 只有在玩家的建筑/物品上才显示开关按钮
            if (parent.Faction == Faction.OfPlayer)
            {
                yield return new Command_Toggle
                {
                    defaultLabel = Props.toggleCommandLabel,
                    defaultDesc = Props.toggleCommandDesc,
                    icon = ContentFinder<Texture2D>.Get("ModIcon/BanInvisibility"),
                    isActive = () => isActivated,
                    toggleAction = ToggleActivation
                };
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref isActivated, "isActivated", Props.startActivated);
        }

        public override void PostDraw()
        {
            base.PostDraw();
            if (!isActivated || !HasPower()) return;

            // 只有当被选中且设置了drawRadius时才显示
            if (Props.drawRadius && Find.Selector.SelectedObjects.Contains(parent))
            {
                // 绘制作用范围圆环
                GenDraw.DrawRadiusRing(parent.Position, Props.effectiveRadius, Props.radiusColor);

                // 绘制连接到受影响单位的线
                if (Props.drawLines)
                {
                    foreach (Pawn pawn in affectedPawns)
                    {
                        if (pawn.Spawned && pawn.Map == parent.Map)
                        {
                            GenDraw.DrawLineBetween(pawn.DrawPos, parent.DrawPos);
                        }
                    }
                }
            }
        }
    }
}

namespace NCL
{
    public class CompProperties_AntiInvisibilityField : CompProperties
    {
        public float effectiveRadius = 12f;
        public int checkIntervalTicks = 60;
        public bool affectsThroughWalls = false;
        public bool affectsAllFactions = false;
        public HediffDef applyHediff;
        public SoundDef soundOnReveal;
        public EffecterDef instantEffecterDef;
        public EffecterDef continuousEffecterDef;
        public bool drawRadius = true;
        public bool drawLines = true;
        public Color radiusColor = new Color(0.2f, 0.8f, 0.2f, 0.3f);
        public bool requiresPower = true; // 新增是否依赖电力的选项
        public bool startActivated = true; // 初始是否激活
        public string toggleCommandLabel = "Toggle Scanner"; // 开关命令标签
        public string toggleCommandDesc = "Enable/disable invisibility detection"; // 开关命令描述

        public CompProperties_AntiInvisibilityField()
        {
            compClass = typeof(CompAntiInvisibilityField);
        }
    }
}

namespace NCL
{
    public class PlaceWorker_ShowAntiInvisibilityRadius : PlaceWorker
    {
        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            GenDraw.DrawRadiusRing(loc, (checkingDef as ThingDef).GetCompProperties<CompProperties_AntiInvisibilityField>().effectiveRadius);
            return true;
        }
    }
}



namespace NCL
{
    // Token: 0x0200074E RID: 1870
    public class Hediff_DisruptorFlash : HediffWithComps
    {
        // Token: 0x17000837 RID: 2103
        // (get) Token: 0x06002D90 RID: 11664 RVA: 0x000EACA4 File Offset: 0x000E8EA4
        public override HediffStage CurStage
        {
            get
            {
                return this.stage;
            }
        }

        // Token: 0x06002D91 RID: 11665 RVA: 0x000EACAC File Offset: 0x000E8EAC
        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
            this.SetupStage();
        }

        // Token: 0x06002D92 RID: 11666 RVA: 0x000EACBB File Offset: 0x000E8EBB
        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                this.SetupStage();
            }
        }

        // Token: 0x06002D93 RID: 11667 RVA: 0x000EACD4 File Offset: 0x000E8ED4
        public void SetupStage()
        {
            this.stage = new HediffStage
            {
                capMods = new List<PawnCapacityModifier>
                {
                    new PawnCapacityModifier
                    {
                        capacity = PawnCapacityDefOf.Consciousness,
                        postFactor = 1f - this.pawn.GetStatValue(StatDefOf.PsychicSensitivity, true, -1) * 0.1f
                    },
                    new PawnCapacityModifier
                    {
                        capacity = PawnCapacityDefOf.Moving,
                        postFactor = 1f - this.pawn.GetStatValue(StatDefOf.PsychicSensitivity, true, -1) * 0.2f
                    }
                }
            };
        }

        // Token: 0x04002444 RID: 9284
        private HediffStage stage;
    }
}
