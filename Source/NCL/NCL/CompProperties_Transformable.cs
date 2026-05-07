using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace NCL
{
    [DefOf]
    public static class NCLContainerDefOf
    {
        public static ThingDef Building_PawnContainer;
        public static HediffDef PawnContainedHediff;
        public static JobDef TurnIntoBuilding;
        public static AbilityDef TurnIntoBuildingAbility;
        public static ThingDef Building_PawnContainerForged;
        public static AbilityDef TurnIntoBuildingAbilityForged;
        public static JobDef TurnIntoBuildingForged;
        static NCLContainerDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(NCLContainerDefOf));
        }
    }

    // 变身建筑能力
    public class Ability_TurnIntoBuilding : Ability
    {
        public Ability_TurnIntoBuilding(Pawn pawn) : base(pawn) { }

        public Ability_TurnIntoBuilding(Pawn pawn, AbilityDef def) : base(pawn, def) { }

        public override bool Activate(LocalTargetInfo target, LocalTargetInfo dest)
        {
            if (!base.Activate(target, dest))
                return false;

            if (pawn.Map == null)
            {
                Log.Error($"Tried to turn {pawn.Label} into building but pawn is not on any map");
                return false;
            }

            // Get position before despawning
            IntVec3 position = pawn.Position;
            Map map = pawn.Map;

            // Create the building first
            Building_PawnContainer building = Building_PawnContainer.MakeSleepingBuilding(pawn);
            if (building == null)
            {
                Log.Error("Failed to create building container");
                return false;
            }

            // Despawn the pawn if it's still spawned
            if (pawn.Spawned)
            {
                pawn.DeSpawn(DestroyMode.Vanish);
            }

            // Spawn the building
            GenSpawn.Spawn(building, position, map, WipeMode.Vanish);
            FleckMaker.ThrowDustPuff(position, map, 2f);

            return true;
        }

        public override bool CanApplyOn(LocalTargetInfo target)
        {
            return target.Pawn == pawn && base.CanApplyOn(target);
        }
    }

    // 建筑容器类
    public class Building_PawnContainer : Building, IThingHolder
    {
        private ThingOwner<Pawn> innerContainer;
        private Hediff sleepingHediff;

        public Pawn ContainedPawn => innerContainer.Count > 0 ? innerContainer[0] : null;

        public Building_PawnContainer()
        {
            innerContainer = new ThingOwner<Pawn>(this);
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            // 先尝试释放Pawn
            bool released = ReleasePawn();

            // 无论是否成功释放，都要销毁建筑
            base.Destroy(mode);

            if (!released)
            {
                Log.Warning("Pawn release may have failed during building destruction");
            }
        }


        private bool ReleasePawn()
        {
            // 1. 安全检查
            if (ContainedPawn == null)
            {
                Log.Warning("No contained pawn to release");
                return false;
            }

            // 2. 保存引用，因为后续操作可能会改变状态
            Pawn pawn = ContainedPawn;
            Map map = this.Map;
            IntVec3 position = this.Position;

            try
            {
                // 3. 移除睡眠状态效果
                if (sleepingHediff != null && pawn.health != null)
                {
                    pawn.health.RemoveHediff(sleepingHediff);
                }

                // 4. 从容器中移除Pawn
                if (innerContainer.Contains(pawn))
                {
                    innerContainer.Remove(pawn);
                }
                else
                {
                    Log.Warning("Pawn was not in container as expected");
                }

                // 5. 验证地图和位置
                if (map == null || !position.IsValid || (map != null && !position.InBounds(map)))
                {
                    Log.Error($"Invalid spawn conditions - Map: {map}, Position: {position}");
                    return false;
                }

                // 6. 生成Pawn回地图
                if (!pawn.Spawned)
                {
                    GenSpawn.Spawn(pawn, position, map, Rot4.Random, WipeMode.Vanish, false);
                }
                else
                {
                    Log.Warning("Pawn was already spawned when releasing");
                }

                // 7. 恢复工作状态
                pawn.jobs?.EndCurrentJob(JobCondition.InterruptForced);

                // 8. 播放效果
                if (map != null)
                {
                    FleckMaker.ThrowDustPuff(position, map, 1f);
                }

                return true;
            }
            catch (System.Exception ex)
            {
                Log.Error($"Exception releasing pawn {pawn}: {ex}");
                return false;
            }
        }



        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            if (ContainedPawn != null && (ContainedPawn.Faction == Faction.OfPlayer || DebugSettings.ShowDevGizmos))
            {
                // 原有的释放命令
                Command_Action releaseCommand = new Command_Action
                {
                    defaultLabel = "Release " + ContainedPawn.LabelShortCap,
                    defaultDesc = "Release the contained pawn back into the world.",
                    icon = ContentFinder<Texture2D>.Get("Ability/ReleaseFromBuilding", false) ?? BaseContent.BadTex,
                    action = () => this.Destroy(DestroyMode.Vanish)
                };

                if (ContainedPawn.Downed)
                {
                    releaseCommand.Disable("Incapacitated".Translate());
                }

                yield return releaseCommand;

                // 新增的传送命令（完全兼容的写法）
               

                }
            }
      






        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
        }

        public ThingOwner GetDirectlyHeldThings()
        {
            return innerContainer;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref innerContainer, "innerContainer", this);
            Scribe_References.Look(ref sleepingHediff, "sleepingHediff");
        }

        public static Building_PawnContainer MakeSleepingBuilding(Pawn pawn)
        {
            // 1. 验证输入
            if (pawn == null)
            {
                Log.Error("Tried to create container building for null pawn");
                return null;
            }

            // 2. 记录原始信息
            Map map = pawn.Map;
            IntVec3 position = pawn.Position;
            Faction faction = pawn.Faction;

            // 3. 创建建筑实例（未生成状态）
            Building_PawnContainer building = ThingMaker.MakeThing(NCLContainerDefOf.Building_PawnContainer) as Building_PawnContainer;
            if (building == null)
            {
                Log.Error("Failed to create Building_PawnContainer instance");
                return null;
            }

            // 4. 设置建筑属性
            building.SetFaction(faction);

            // 5. 处理Pawn转移
            if (pawn.Spawned)
            {
                pawn.DeSpawn(DestroyMode.Vanish);
            }

            // 6. 添加睡眠状态效果
            building.sleepingHediff = pawn.health.AddHediff(NCLContainerDefOf.PawnContainedHediff);

            // 7. 将Pawn添加到建筑容器
            if (!building.innerContainer.TryAdd(pawn))
            {
                Log.Error($"Failed to add {pawn.Label} to container");
                return null;
            }

            // 8. 返回未生成的建筑实例（由调用者决定何时生成）
            return building;
        }



        protected override void Tick()
        {
            base.Tick();
            innerContainer.DoTick();
        }
    }

    public class JobDriver_TurnIntoBuilding : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            if (pawn.Map == null || !pawn.Spawned)
            {
                Log.Error($"Pawn {pawn.Label} is not in a valid state for transformation");
                yield break;
            }

            // Go to target position
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);

            // Transformation effect
            yield return new Toil
            {
                initAction = () => FleckMaker.ThrowDustPuff(pawn.Position, pawn.Map, 2f),
                defaultCompleteMode = ToilCompleteMode.Delay,
                defaultDuration = 60
            };

            // Transform into building
            yield return new Toil
            {
                initAction = () =>
                {
                    Ability_TurnIntoBuilding ability = pawn.abilities.GetAbility(NCLContainerDefOf.TurnIntoBuildingAbility) as Ability_TurnIntoBuilding;
                    if (ability != null)
                    {
                        ability.Activate(new LocalTargetInfo(pawn), LocalTargetInfo.Invalid);
                    }
                },
                defaultCompleteMode = ToilCompleteMode.Instant
            };
        }
    }
}

namespace NCL
{
    public class JobGiver_TurnIntoBuildingWhenAllied : ThinkNode_JobGiver
    {
        public float allySearchRadius = 15f; // 可配置的检测范围

        public JobGiver_TurnIntoBuildingWhenAllied() { } // 必须有无参构造

        protected override Job TryGiveJob(Pawn pawn)
        {
            // 1. 安全检查
            if (pawn == null || !pawn.Spawned || pawn.Map == null || pawn.Faction == null)
                return null;

            // 2. 检查是否有变身能力
            if (pawn.abilities?.GetAbility(NCLContainerDefOf.TurnIntoBuildingAbility) == null)
                return null;

            // 3. 查找附近友军（避免访问自己或无效派系）
            bool hasAlly = false;

            foreach (Pawn other in pawn.Map.mapPawns.SpawnedPawnsInFaction(pawn.Faction))
            {
                if (other == null || other == pawn || other.Dead)
                    continue;

                // 只检查距离，不进行 Hostile 判断（避免派系关系错误）
                if (other.Position.DistanceTo(pawn.Position) <= allySearchRadius)
                {
                    hasAlly = true;
                    break;
                }
            }

            if (!hasAlly)
                return null;

            // 4. 给予变身工作
            return JobMaker.MakeJob(NCLContainerDefOf.TurnIntoBuilding, pawn);
        }
    }
}

namespace NCL
{
    // 变身建筑能力
    public class Ability_TurnIntoBuildingForged : Ability
    {
        public Ability_TurnIntoBuildingForged(Pawn pawn) : base(pawn) { }

        public Ability_TurnIntoBuildingForged(Pawn pawn, AbilityDef def) : base(pawn, def) { }

        public override bool Activate(LocalTargetInfo target, LocalTargetInfo dest)
        {
            if (!base.Activate(target, dest))
                return false;

            if (pawn.Map == null)
            {
                Log.Error($"Tried to turn {pawn.Label} into building but pawn is not on any map");
                return false;
            }

            // Get position before despawning
            IntVec3 position = pawn.Position;
            Map map = pawn.Map;

            // Create the building first
            Building_PawnContainerForged building = Building_PawnContainerForged.MakeSleepingBuilding(pawn);
            if (building == null)
            {
                Log.Error("Failed to create building container");
                return false;
            }

            // Despawn the pawn if it's still spawned
            if (pawn.Spawned)
            {
                pawn.DeSpawn(DestroyMode.Vanish);
            }

            // Spawn the building
            GenSpawn.Spawn(building, position, map, WipeMode.Vanish);
            FleckMaker.ThrowDustPuff(position, map, 2f);

            return true;
        }

        public override bool CanApplyOn(LocalTargetInfo target)
        {
            return target.Pawn == pawn && base.CanApplyOn(target);
        }
    }

    // 建筑容器类
    public class Building_PawnContainerForged : Building, IThingHolder
    {
        private ThingOwner<Pawn> innerContainer;
        private Hediff sleepingHediff;

        public Pawn ContainedPawn => innerContainer.Count > 0 ? innerContainer[0] : null;

        public Building_PawnContainerForged()
        {
            innerContainer = new ThingOwner<Pawn>(this);
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            // 先尝试释放Pawn
            bool released = ReleasePawn();

            // 无论是否成功释放，都要销毁建筑
            base.Destroy(mode);

            if (!released)
            {
                Log.Warning("Pawn release may have failed during building destruction");
            }
        }


        private bool ReleasePawn()
        {
            // 1. 安全检查
            if (ContainedPawn == null)
            {
                Log.Warning("No contained pawn to release");
                return false;
            }

            // 2. 保存引用，因为后续操作可能会改变状态
            Pawn pawn = ContainedPawn;
            Map map = this.Map;
            IntVec3 position = this.Position;

            try
            {
                // 3. 移除睡眠状态效果
                if (sleepingHediff != null && pawn.health != null)
                {
                    pawn.health.RemoveHediff(sleepingHediff);
                }

                // 4. 从容器中移除Pawn
                if (innerContainer.Contains(pawn))
                {
                    innerContainer.Remove(pawn);
                }
                else
                {
                    Log.Warning("Pawn was not in container as expected");
                }

                // 5. 验证地图和位置
                if (map == null || !position.IsValid || (map != null && !position.InBounds(map)))
                {
                    Log.Error($"Invalid spawn conditions - Map: {map}, Position: {position}");
                    return false;
                }

                // 6. 生成Pawn回地图
                if (!pawn.Spawned)
                {
                    GenSpawn.Spawn(pawn, position, map, Rot4.Random, WipeMode.Vanish, false);
                }
                else
                {
                    Log.Warning("Pawn was already spawned when releasing");
                }

                // 7. 恢复工作状态
                pawn.jobs?.EndCurrentJob(JobCondition.InterruptForced);

                // 8. 播放效果
                if (map != null)
                {
                    FleckMaker.ThrowDustPuff(position, map, 1f);
                }

                return true;
            }
            catch (System.Exception ex)
            {
                Log.Error($"Exception releasing pawn {pawn}: {ex}");
                return false;
            }
        }



        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            if (ContainedPawn != null && (ContainedPawn.Faction == Faction.OfPlayer || DebugSettings.ShowDevGizmos))
            {
                // 原有的释放命令
                Command_Action releaseCommand = new Command_Action
                {
                    defaultLabel = "Release " + ContainedPawn.LabelShortCap,
                    defaultDesc = "Release the contained pawn back into the world.",
                    icon = ContentFinder<Texture2D>.Get("Ability/ReleaseFromBuilding", false) ?? BaseContent.BadTex,
                    action = () => this.Destroy(DestroyMode.Vanish)
                };

                if (ContainedPawn.Downed)
                {
                    releaseCommand.Disable("Incapacitated".Translate());
                }

                yield return releaseCommand;




            }
        }



        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
        }

        public ThingOwner GetDirectlyHeldThings()
        {
            return innerContainer;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref innerContainer, "innerContainer", this);
            Scribe_References.Look(ref sleepingHediff, "sleepingHediff");
        }

        public static Building_PawnContainerForged MakeSleepingBuilding(Pawn pawn)
        {
            // 1. 验证输入
            if (pawn == null)
            {
                Log.Error("Tried to create container building for null pawn");
                return null;
            }

            // 2. 记录原始信息
            Map map = pawn.Map;
            IntVec3 position = pawn.Position;
            Faction faction = pawn.Faction;

            // 3. 创建建筑实例（未生成状态）
            Building_PawnContainerForged building = ThingMaker.MakeThing(NCLContainerDefOf.Building_PawnContainerForged) as Building_PawnContainerForged;
            if (building == null)
            {
                Log.Error("Failed to create Building_PawnContainer instance");
                return null;
            }

            // 4. 设置建筑属性
            building.SetFaction(faction);

            // 5. 处理Pawn转移
            if (pawn.Spawned)
            {
                pawn.DeSpawn(DestroyMode.Vanish);
            }

            // 6. 添加睡眠状态效果
            building.sleepingHediff = pawn.health.AddHediff(NCLContainerDefOf.PawnContainedHediff);

            // 7. 将Pawn添加到建筑容器
            if (!building.innerContainer.TryAdd(pawn))
            {
                Log.Error($"Failed to add {pawn.Label} to container");
                return null;
            }

            return building;
        }



        protected override void Tick()
        {
            base.Tick();
            innerContainer.DoTick();
        }
    }

    public class JobDriver_TurnIntoBuildingForged : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            if (pawn.Map == null || !pawn.Spawned)
            {
                Log.Error($"Pawn {pawn.Label} is not in a valid state for transformation");
                yield break;
            }

            // Go to target position
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);

            // Transformation effect
            yield return new Toil
            {
                initAction = () => FleckMaker.ThrowDustPuff(pawn.Position, pawn.Map, 2f),
                defaultCompleteMode = ToilCompleteMode.Delay,
                defaultDuration = 60
            };

            // Transform into building
            yield return new Toil
            {
                initAction = () =>
                {
                    Ability_TurnIntoBuildingForged ability = pawn.abilities.GetAbility(NCLContainerDefOf.TurnIntoBuildingAbilityForged) as Ability_TurnIntoBuildingForged;
                    if (ability != null)
                    {
                        ability.Activate(new LocalTargetInfo(pawn), LocalTargetInfo.Invalid);
                    }
                },
                defaultCompleteMode = ToilCompleteMode.Instant
            };
        }
    }
}

namespace NCL
{
    public class JobGiver_TurnIntoBuildingWhenAlliedForged : ThinkNode_JobGiver
    {
        public float allySearchRadius = 15f; // 可配置的检测范围

        public JobGiver_TurnIntoBuildingWhenAlliedForged() { } // 必须有无参构造

        protected override Job TryGiveJob(Pawn pawn)
        {
            // 1. 安全检查
            if (pawn == null || !pawn.Spawned || pawn.Map == null || pawn.Faction == null)
                return null;

            // 2. 检查是否有变身能力
            if (pawn.abilities?.GetAbility(NCLContainerDefOf.TurnIntoBuildingAbilityForged) == null)
                return null;

            // 3. 查找附近友军（避免访问自己或无效派系）
            bool hasAlly = false;

            foreach (Pawn other in pawn.Map.mapPawns.SpawnedPawnsInFaction(pawn.Faction))
            {
                if (other == null || other == pawn || other.Dead)
                    continue;

                // 只检查距离，不进行 Hostile 判断（避免派系关系错误）
                if (other.Position.DistanceTo(pawn.Position) <= allySearchRadius)
                {
                    hasAlly = true;
                    break;
                }
            }

            if (!hasAlly)
                return null;

            // 4. 给予变身工作
            return JobMaker.MakeJob(NCLContainerDefOf.TurnIntoBuildingForged, pawn);
        }
    }
}


namespace NCL
{
    // 定义 CompProperties（必须）
    public class CompProperties_MechanicalBuilding : CompProperties
    {
        public CompProperties_MechanicalBuilding()
        {
            this.compClass = typeof(CompMechanicalBuilding);
        }
    }

    // 实际的 Comp 逻辑
    public class CompMechanicalBuilding : ThingComp
    {
        private int lastCheckTick = 0;

        // 每 60 帧（约1秒）检测一次
        public override void CompTick()
        {
            base.CompTick();

            if (Find.TickManager.TicksGame > lastCheckTick + 60)
            {
                lastCheckTick = Find.TickManager.TicksGame;
                CheckAndFixFaction();
            }
        }

        // 强制修正为机械族阵营
        private void CheckAndFixFaction()
        {
            if (parent.Faction != Faction.OfMechanoids)
            {
                parent.SetFaction(Faction.OfMechanoids);
                Log.Message($"强制修正建筑 {parent.Label} 为机械族阵营");
            }
        }
    }
}
