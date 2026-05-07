using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using NCL;
using System.Linq;
using System;

namespace NCL
{
    // 属性定义（XML可配置）
    public class CompProperties_AbilitySummonDropPawns : CompProperties_AbilityEffect
    {
        // 第一组Pawn配置
        public List<PawnKindDef> pawnKinds;
        public int pawnCount = 1;

        // 第二组Pawn配置
        public List<PawnKindDef> secondaryPawnKinds;
        public int secondaryPawnCount = 0;  // 默认为0表示不生成

        // 通用配置
        public int spawnRadius = 5;
        public int minSpawnDistance = 2;
        public bool leaveSlag = false;
        public bool canRoofPunch = true;

        public CompProperties_AbilitySummonDropPawns()
        {
            compClass = typeof(CompAbilityEffect_SummonDropPawns);
        }
    }

    // 延迟生成信息结构
    public struct DelayedSpawnInfo
    {
        public Pawn caster;
        public List<PawnKindDef> pawnKinds;
        public int count;
        public int spawnRadius;
        public int minDistance;
        public bool leaveSlag;
        public bool canRoofPunch;
        public int spawnTick;

        public DelayedSpawnInfo(
            Pawn caster,
            List<PawnKindDef> pawnKinds,
            int count,
            int spawnRadius,
            int minDistance,
            bool leaveSlag,
            bool canRoofPunch,
            int spawnTick)
        {
            this.caster = caster;
            this.pawnKinds = pawnKinds;
            this.count = count;
            this.spawnRadius = spawnRadius;
            this.minDistance = minDistance;
            this.leaveSlag = leaveSlag;
            this.canRoofPunch = canRoofPunch;
            this.spawnTick = spawnTick;
        }
    }

    // 延迟生成控制器
    public class DelayedPawnSpawner : MapComponent
    {
        private List<DelayedSpawnInfo> delayedSpawns = new List<DelayedSpawnInfo>();

        public DelayedPawnSpawner(Map map) : base(map) { }

        public void RegisterDelayedSpawn(DelayedSpawnInfo info)
        {
            delayedSpawns.Add(info);
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();

            for (int i = delayedSpawns.Count - 1; i >= 0; i--)
            {
                var info = delayedSpawns[i];
                if (Find.TickManager.TicksGame >= info.spawnTick)
                {
                    for (int j = 0; j < info.count; j++)
                    {
                        SpawnDelayedPawn(info);
                    }
                    delayedSpawns.RemoveAt(i);
                }
            }
        }

        private void SpawnDelayedPawn(DelayedSpawnInfo info)
        {
            if (info.caster == null || info.caster.Map == null || info.pawnKinds == null || info.pawnKinds.Count == 0)
                return;

            PawnKindDef pawnKind = info.pawnKinds.RandomElement();
            PawnGenerationRequest request = new PawnGenerationRequest(
                pawnKind,
                info.caster.Faction ?? Faction.OfPlayer,
                PawnGenerationContext.NonPlayer,
                -1,
                forceGenerateNewPawn: true,
                allowDead: false,
                allowDowned: false,
                canGeneratePawnRelations: false,
                mustBeCapableOfViolence: true
            );

            Pawn newPawn = PawnGenerator.GeneratePawn(request);

            // 设置年龄为0天
            if (newPawn.ageTracker != null)
            {
                newPawn.ageTracker.AgeBiologicalTicks = 0;
                newPawn.ageTracker.AgeChronologicalTicks = 0;
            }

            // 设置机械能量为满
            var mechPowerComp = newPawn.GetComp<CompMechPowerCell>();
            if (mechPowerComp != null)
            {
                FieldInfo powerTicksLeftField = typeof(CompMechPowerCell).GetField("powerTicksLeft",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (powerTicksLeftField != null)
                {
                    powerTicksLeftField.SetValue(mechPowerComp, mechPowerComp.Props.totalPowerTicks);
                }
                mechPowerComp.depleted = false;
            }

            if (CellFinder.TryFindRandomCellNear(
                info.caster.Position,
                info.caster.Map,
                info.spawnRadius,
                c => c.Walkable(info.caster.Map) &&
                     c.DistanceTo(info.caster.Position) >= info.minDistance &&
                     c.GetFirstBuilding(info.caster.Map) == null,
                out IntVec3 spawnPos))
            {
                DropPodUtility.DropThingsNear(
                    spawnPos,
                    info.caster.Map,
                    new List<Thing> { newPawn },
                    faction: info.caster.Faction,
                    leaveSlag: info.leaveSlag,
                    canRoofPunch: info.canRoofPunch,
                    forbid: false
                );
            }
        }
    }

    public class CompAbilityEffect_SummonDropPawns : CompAbilityEffect
    {
        public new CompProperties_AbilitySummonDropPawns Props =>
            (CompProperties_AbilitySummonDropPawns)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            // 确保地图组件存在
            if (parent.pawn.Map.GetComponent<DelayedPawnSpawner>() == null)
            {
                parent.pawn.Map.components.Add(new DelayedPawnSpawner(parent.pawn.Map));
            }

            // 生成第一组Pawn（立即生成）
            for (int i = 0; i < Props.pawnCount; i++)
            {
                SpawnPawnAnywhere(parent.pawn, Props.pawnKinds);
            }

            // 生成第二组Pawn（延迟5秒=300ticks生成）
            if (Props.secondaryPawnCount > 0 &&
                Props.secondaryPawnKinds != null &&
                Props.secondaryPawnKinds.Count > 0)
            {
                parent.pawn.Map.GetComponent<DelayedPawnSpawner>()
                    .RegisterDelayedSpawn(new DelayedSpawnInfo(
                        caster: parent.pawn,
                        pawnKinds: Props.secondaryPawnKinds,
                        count: Props.secondaryPawnCount,
                        spawnRadius: Props.spawnRadius,
                        minDistance: Props.minSpawnDistance,
                        leaveSlag: Props.leaveSlag,
                        canRoofPunch: Props.canRoofPunch,
                        spawnTick: Find.TickManager.TicksGame + 300 // 5秒延迟
                    ));
            }
        }

        // 修改方法名和逻辑：在无限距离内寻找最近的有效位置
        private void SpawnPawnAnywhere(Pawn caster, List<PawnKindDef> possibleKinds)
        {
            if (caster == null || caster.Map == null || possibleKinds == null || possibleKinds.Count == 0)
            {
                Log.Error("CompAbilityEffect_SummonDropPawns: 无效的生成参数!");
                return;
            }

            PawnKindDef pawnKind = possibleKinds.RandomElement();
            PawnGenerationRequest request = new PawnGenerationRequest(
                pawnKind,
                caster.Faction ?? Faction.OfPlayer,
                PawnGenerationContext.NonPlayer,
                -1,
                forceGenerateNewPawn: true,
                allowDead: false,
                allowDowned: false,
                canGeneratePawnRelations: false,
                mustBeCapableOfViolence: true
            );

            Pawn newPawn = PawnGenerator.GeneratePawn(request);

            // 强制设置年龄为0天
            if (newPawn.ageTracker != null)
            {
                newPawn.ageTracker.AgeBiologicalTicks = 0;
                newPawn.ageTracker.AgeChronologicalTicks = 0;
            }

            // 检查并设置机械能量组件为满状态
            var mechPowerComp = newPawn.GetComp<CompMechPowerCell>();
            if (mechPowerComp != null)
            {
                FieldInfo powerTicksLeftField = typeof(CompMechPowerCell).GetField("powerTicksLeft",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (powerTicksLeftField != null)
                {
                    powerTicksLeftField.SetValue(mechPowerComp, mechPowerComp.Props.totalPowerTicks);
                }
                mechPowerComp.depleted = false;
            }

            // 核心修改：在整个地图上搜索最近的有效位置
            IntVec3 spawnPos = FindNearestValidDropPosition(caster);

            // 执行空投
            DropPodUtility.DropThingsNear(
                spawnPos,
                caster.Map,
                new List<Thing> { newPawn },
                faction: caster.Faction,
                leaveSlag: Props.leaveSlag,
                canRoofPunch: Props.canRoofPunch,
                forbid: false
            );
        }

        // 在整个地图上搜索最近的有效位置
        private IntVec3 FindNearestValidDropPosition(Pawn caster)
        {
            Map map = caster.Map;
            IntVec3 casterPos = caster.Position;
            IntVec3 bestPos = IntVec3.Invalid;
            float bestDistance = float.MaxValue;

            // 使用地图的单元格索引快速遍历所有单元格
            for (int i = 0; i < map.cellIndices.NumGridCells; i++)
            {
                IntVec3 cell = map.cellIndices.IndexToCell(i);

                // 跳过无效位置
                if (!cell.IsValid || !cell.InBounds(map)) continue;

                // 检查位置是否符合要求
                if (!IsValidDropPosition(map, cell)) continue;

                // 计算距离并比较
                float distance = cell.DistanceTo(casterPos);
                if (distance < bestDistance)
                {
                    bestPos = cell;
                    bestDistance = distance;
                }
            }

            // 如果找到有效位置则返回
            if (bestPos.IsValid)
            {
                return bestPos;
            }

            // 找不到有效位置时使用备用方案
            Log.Warning("未找到有效的无屋顶区域，降落在使用者附近");

            // 尝试在附近寻找可降落位置
            if (CellFinder.TryFindRandomCellNear(
                casterPos,
                map,
                Props.minSpawnDistance * 2, // 搜索范围加倍
                c => c.Standable(map) && c.GetFirstBuilding(map) == null,
                out IntVec3 fallbackPos))
            {
                return fallbackPos;
            }

            // 最终回退到使用者位置
            return casterPos;
        }

        // 检查位置是否适合空投
        private bool IsValidDropPosition(Map map, IntVec3 cell)
        {
            // 必须满足的基本条件
            return cell.Standable(map) &&           // 地面可站立
                   !cell.Roofed(map) &&             // 无任何屋顶（包括厚岩顶）
                   cell.GetFirstBuilding(map) == null && // 无建筑阻挡
                   cell.DistanceTo(parent.pawn.Position) >= Props.minSpawnDistance; // 满足最小距离要求
        }
    }
}

namespace NCL
{
    public class ThinkNode_ConditionalPlayerFaction : ThinkNode_Conditional
    {
        protected override bool Satisfied(Pawn pawn)
        {
            // 唯一条件：属于玩家派系（无论是否机械族、殖民者、奴隶等）
            return pawn.Faction?.IsPlayer == true;
        }
    }
}

namespace NCL
{
    // 定义Comp属性（可选配置）
    public class CompProperties_AlwaysWork : CompProperties
    {
        public CompProperties_AlwaysWork()
        {
            this.compClass = typeof(Comp_AlwaysWork);
        }
    }

    // 核心Comp实现
    public class Comp_AlwaysWork : ThingComp
    {
        private Pawn Pawn => this.parent as Pawn;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            EnsureWorkSettingsInitialized();
        }

        public override void CompTick()
        {
            base.CompTick();
            // 每60ticks检查一次（避免性能开销）
            if (Pawn.IsHashIntervalTick(600))
            {
                EnsureWorkSettingsInitialized();
                ForceAllWorkTypes();
            }
        }

        private void EnsureWorkSettingsInitialized()
        {
            if (Pawn?.workSettings == null && Pawn != null)
            {
                // 强制初始化工作设置
                Pawn.workSettings = new Pawn_WorkSettings(Pawn);
                Pawn.workSettings.EnableAndInitialize();
                Log.Message($"[AlwaysWork] Initialized work settings for {Pawn.LabelShort}");
            }
        }

        private void ForceAllWorkTypes()
        {
            if (Pawn?.workSettings == null) return;

            // 定义允许的工作类型列表
            List<string> allowedWorkTypes = new List<string>
    {
        "Construction",
        "Firefighter",
        "Hauling",
        "Cleaning",
        "Mining",
        "PlantCutting",
        "Art",
        "Crafting",
    };

            // 遍历所有工作类型
            foreach (WorkTypeDef workType in DefDatabase<WorkTypeDef>.AllDefs)
            {
                if (allowedWorkTypes.Contains(workType.defName))
                {
                    // 启用允许的工作类型（优先级设为3）
                    if (!Pawn.workSettings.WorkIsActive(workType))
                    {
                        Pawn.workSettings.SetPriority(workType, 3);
                    }
                }
                else
                {
                    // 禁用其他工作类型（优先级设为0）
                    if (Pawn.workSettings.GetPriority(workType) > 0)
                    {
                        Pawn.workSettings.SetPriority(workType, 0);
                    }
                }
            }
        }



        public override void PostExposeData()
        {
            base.PostExposeData();
            // 存档时无需特殊处理
        }
    }
}


namespace NCL
{
    // 定义Comp属性（XML可配置）
    public class CompProperties_SkillRefresh : CompProperties
    {
        public List<ThingDef> targetPawnDefs;  // 需要检测的生物类型
        public int checkIntervalTicks = 60;    // 检测间隔（默认1秒）

        public CompProperties_SkillRefresh()
        {
            compClass = typeof(Comp_SkillRefresh);
        }
    }

    // 核心实现
    public class Comp_SkillRefresh : ThingComp
    {
        private CompProperties_SkillRefresh Props =>
            (CompProperties_SkillRefresh)props;

        public override void CompTick()
        {
            base.CompTick();

            // 只有当拥有者存在于地图上时才检测
            if (!(parent is Pawn owner) || owner.Map == null || owner.Dead)
                return;

            // 按间隔检测（优化性能）
            if (!owner.IsHashIntervalTick(Props.checkIntervalTicks))
                return;

            // 检测地图是否存在目标生物
            if (!AreTargetPawnsPresent(owner.Map))
            {
                RefreshAllAbilities(owner);
;
            }
        }

        // 检测目标生物是否存在
        private bool AreTargetPawnsPresent(Map map)
        {
            if (Props.targetPawnDefs.NullOrEmpty() || map == null)
                return false;

            return map.mapPawns.AllPawnsSpawned
                .Any(p => !p.Dead && Props.targetPawnDefs.Contains(p.def));
        }

        // 刷新所有技能冷却
        private void RefreshAllAbilities(Pawn owner)
        {
            if (owner.abilities != null)
            {
                foreach (Ability ability in owner.abilities.abilities)
                {
                    ability.ResetCooldown(); // 官方API重置冷却

                    // 如果是充能型技能，恢复充能
                    if (ability.UsesCharges)
                        ability.RemainingCharges = ability.maxCharges;
                }
            }
        }

        public override void PostExposeData() => base.PostExposeData();
    }
}




namespace NCL
{
    public class JobDriver_AttackMinitor : JobDriver
    {
        private const int AttackIntervalTicks = 300;
        private int lastAttackTick = -9999;
        private bool attackCompleted;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {

            return pawn.Reserve(job.targetA, job, /* maxPawns */ -1, /* stackCount */ -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_General.Do(() => {
                pawn.Map.reservationManager.ReleaseAllClaimedBy(pawn); // 释放当前单位的所有锁定
                InitializeTarget();
            });
            // 初始化目标
            yield return Toils_General.Do(() => InitializeTarget());

            // 移动至目标
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch)
                .FailOn(() => attackCompleted);

            // 攻击循环
            var attackToil = new Toil();
            attackToil.initAction = () =>
            {
                if (Find.TickManager.TicksGame - lastAttackTick >= AttackIntervalTicks)
                {
                    if (TryAttackTarget())
                    {
                        attackCompleted = true;
                        ReadyForNextToil();
                    }
                    else
                    {
                        InitializeTarget();
                    }
                    lastAttackTick = Find.TickManager.TicksGame;
                }
            };
            attackToil.defaultCompleteMode = ToilCompleteMode.Delay;
            attackToil.defaultDuration = AttackIntervalTicks;
            yield return attackToil;

            // 完成攻击
            yield return Toils_General.Do(() => EndJobWith(JobCondition.Succeeded));
        }

        private void InitializeTarget()
        {
            var newTarget = FindClosestMinitor();
            if (newTarget != null)
            {
                job.SetTarget(TargetIndex.A, newTarget);
                attackCompleted = false;
            }
        }

        private bool TryAttackTarget()
        {
            if (!(TargetA.Thing is Pawn target) ||
                !pawn.CanReach(target, PathEndMode.Touch, Danger.Deadly) ||
                !CanPawnAttack(pawn, target))
            {
                return false;
            }

            // 强制攻击逻辑
            pawn.meleeVerbs.TryMeleeAttack(target, null, false);

            return true;
        }

        // 修改后的攻击条件检查（移除HostileTo检查）
        private bool CanPawnAttack(Pawn attacker, Pawn target)
        {
            return attacker != null &&
                   target != null &&
                   !attacker.Downed &&
                   !attacker.Dead &&
                   !target.Downed &&
                   !target.Dead;
        }

        private Thing FindClosestMinitor()
        {
            return GenClosest.ClosestThingReachable(
                pawn.Position,
                pawn.Map,
                ThingRequest.ForDef(DefDatabase<ThingDef>.GetNamed("TW_University_Minitor")),
                PathEndMode.Touch,
                TraverseParms.For(pawn),
                maxDistance: 50f,
                validator: t =>
                    t is Pawn p &&
                    CanPawnAttack(pawn, p) &&
                    !p.health.hediffSet.HasHediff(DefDatabase<HediffDef>.GetNamed("TW_WasEncouraged")) // 新增条件
            );
        }
    }
}


namespace NCL
{
    public class JobGiver_AttackMinitor : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            var target = FindClosestMinitor(pawn);
            if (target == null) return null;

            // 创建强制攻击Job
            return JobMaker.MakeJob(
                DefDatabase<JobDef>.GetNamed("AttackUniversityMinitor"),
                target,
                expiryInterval: 600,
                checkOverrideOnExpiry: true
            );
        }

        private Thing FindClosestMinitor(Pawn pawn)
        {
            return GenClosest.ClosestThingReachable(
                pawn.Position,
                pawn.Map,
                ThingRequest.ForDef(DefDatabase<ThingDef>.GetNamed("TW_University_Minitor")),
                PathEndMode.Touch,
                TraverseParms.For(pawn),
                maxDistance: 50f,
                validator: t =>
                    t is Pawn p &&
                    !p.Downed &&
                    !p.Dead &&
                    !pawn.Downed &&
                    !pawn.Dead &&
                    !p.health.hediffSet.HasHediff(DefDatabase<HediffDef>.GetNamed("TW_WasEncouraged")) // 新增条件
            );
        }
    }
}



namespace NCL
{
    // ThingComp属性配置
    public class CompProperties_SilverSummon : CompProperties
    {
        // 白银消耗
        public int silverCost = 500;

        // 第一组Pawn配置
        public List<PawnKindDef> pawnKinds;
        public int pawnCount = 1;

        // 第二组Pawn配置
        public List<PawnKindDef> secondaryPawnKinds;
        public int secondaryPawnCount = 0;

        // 通用配置
        public int spawnRadius = 5;
        public int minSpawnDistance = 2;
        public bool leaveSlag = false;
        public bool canRoofPunch = true;

        // Gizmo配置
        public string gizmoLabelKey = "NCL_SummonGizmoLabel";
        public string gizmoDescKey = "NCL_SummonGizmoDesc"; 
        public string gizmoIcon = "UI/Commands/CallAid";

        public CompProperties_SilverSummon()
        {
            compClass = typeof(CompSilverSummon);
        }
    }

    public class CompSilverSummon : ThingComp
    {
        public CompProperties_SilverSummon Props => (CompProperties_SilverSummon)props;

        private Pawn Pawn => parent as Pawn;

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (Pawn == null || Pawn.Faction != Faction.OfPlayer)
                yield break;

            Command_Action command = new Command_Action
            {
                defaultLabel = Props.gizmoLabelKey.Translate(),
                defaultDesc = Props.gizmoDescKey.Translate() + "\n" + "NCL_SilverCost".Translate(Props.silverCost),
                icon = ContentFinder<Texture2D>.Get(Props.gizmoIcon, false) ?? BaseContent.BadTex,
                action = TrySummon
            };

            int availableSilver = GetAvailableSilver();
            if (availableSilver < Props.silverCost)
            {
                command.Disable("NCL_NotEnoughSilver".Translate(availableSilver, Props.silverCost));
            }

            yield return command;
        }

        private void TrySummon()
        {
            if (Pawn == null || Pawn.Map == null)
                return;

            int availableSilver = GetAvailableSilver();
            if (availableSilver < Props.silverCost)
            {
                Messages.Message("NCL_CannotSummonNoSilver".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }

            if (!ConsumeSilver(Props.silverCost))
            {
                Messages.Message("NCL_ConsumeSilverFailed".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }

            // 确保延迟生成器存在
            if (Pawn.Map.GetComponent<DelayedPawnSpawner>() == null)
            {
                Pawn.Map.components.Add(new DelayedPawnSpawner(Pawn.Map));
            }

            // 生成第一组Pawn
            for (int i = 0; i < Props.pawnCount; i++)
            {
                SpawnPawnAnywhere(Pawn, Props.pawnKinds);
            }

            // 注册第二组延迟生成
            if (Props.secondaryPawnCount > 0 &&
                Props.secondaryPawnKinds != null &&
                Props.secondaryPawnKinds.Count > 0)
            {
                Pawn.Map.GetComponent<DelayedPawnSpawner>()
                    .RegisterDelayedSpawn(new DelayedSpawnInfo(
                        caster: Pawn,
                        pawnKinds: Props.secondaryPawnKinds,
                        count: Props.secondaryPawnCount,
                        spawnRadius: Props.spawnRadius,
                        minDistance: Props.minSpawnDistance,
                        leaveSlag: Props.leaveSlag,
                        canRoofPunch: Props.canRoofPunch,
                        spawnTick: Find.TickManager.TicksGame + 300
                    ));
            }

            Messages.Message("NCL_SummonSuccess".Translate(Props.silverCost), MessageTypeDefOf.PositiveEvent, false);
        }


        private int GetAvailableSilver()
        {
            if (Pawn?.Map == null)
                return 0;

            return Pawn.Map.resourceCounter.GetCount(ThingDefOf.Silver);
        }

        private bool ConsumeSilver(int amount)
        {
            if (Pawn?.Map == null)
                return false;

            List<Thing> silverList = Pawn.Map.listerThings.ThingsOfDef(ThingDefOf.Silver);
            int remaining = amount;
            List<Thing> toDestroy = new List<Thing>();

            // 先收集需要消耗的白银
            foreach (Thing silver in silverList)
            {
                if (remaining <= 0)
                    break;

                int toTake = Mathf.Min(remaining, silver.stackCount);

                if (toTake >= silver.stackCount)
                {
                    // 整堆消耗
                    toDestroy.Add(silver);
                    remaining -= silver.stackCount;
                }
                else
                {
                    // 部分消耗
                    Thing split = silver.SplitOff(toTake);
                    toDestroy.Add(split);
                    remaining -= toTake;
                }
            }

            // 统一销毁
            foreach (Thing thing in toDestroy)
            {
                thing.Destroy();
            }

            return remaining == 0;
        }


        private void SpawnPawnAnywhere(Pawn caster, List<PawnKindDef> possibleKinds)
        {
            if (caster == null || caster.Map == null || possibleKinds == null || possibleKinds.Count == 0)
                return;

            PawnKindDef pawnKind = possibleKinds.RandomElement();
            PawnGenerationRequest request = new PawnGenerationRequest(
                pawnKind,
                caster.Faction ?? Faction.OfPlayer,
                PawnGenerationContext.NonPlayer,
                -1,
                forceGenerateNewPawn: true,
                allowDead: false,
                allowDowned: false,
                canGeneratePawnRelations: false,
                mustBeCapableOfViolence: true
            );

            Pawn newPawn = PawnGenerator.GeneratePawn(request);

            if (newPawn.ageTracker != null)
            {
                newPawn.ageTracker.AgeBiologicalTicks = 0;
                newPawn.ageTracker.AgeChronologicalTicks = 0;
            }

            var mechPowerComp = newPawn.GetComp<CompMechPowerCell>();
            if (mechPowerComp != null)
            {
                FieldInfo powerTicksLeftField = typeof(CompMechPowerCell).GetField("powerTicksLeft",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (powerTicksLeftField != null)
                {
                    powerTicksLeftField.SetValue(mechPowerComp, mechPowerComp.Props.totalPowerTicks);
                }
                mechPowerComp.depleted = false;
            }

            IntVec3 spawnPos = FindNearestValidDropPosition(caster);

            DropPodUtility.DropThingsNear(
                spawnPos,
                caster.Map,
                new List<Thing> { newPawn },
                faction: caster.Faction,
                leaveSlag: Props.leaveSlag,
                canRoofPunch: Props.canRoofPunch,
                forbid: false
            );
        }

        private IntVec3 FindNearestValidDropPosition(Pawn caster)
        {
            Map map = caster.Map;
            IntVec3 casterPos = caster.Position;
            IntVec3 bestPos = IntVec3.Invalid;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < map.cellIndices.NumGridCells; i++)
            {
                IntVec3 cell = map.cellIndices.IndexToCell(i);

                if (!cell.IsValid || !cell.InBounds(map))
                    continue;

                if (!IsValidDropPosition(map, cell))
                    continue;

                float distance = cell.DistanceTo(casterPos);
                if (distance < bestDistance)
                {
                    bestPos = cell;
                    bestDistance = distance;
                }
            }

            if (bestPos.IsValid)
                return bestPos;

            if (CellFinder.TryFindRandomCellNear(
                casterPos,
                map,
                Props.minSpawnDistance * 2,
                c => c.Standable(map) && c.GetFirstBuilding(map) == null,
                out IntVec3 fallbackPos))
            {
                return fallbackPos;
            }

            return casterPos;
        }

        private bool IsValidDropPosition(Map map, IntVec3 cell)
        {
            return cell.Standable(map) &&
                   !cell.Roofed(map) &&
                   cell.GetFirstBuilding(map) == null &&
                   cell.DistanceTo(Pawn.Position) >= Props.minSpawnDistance;
        }
    }
}
