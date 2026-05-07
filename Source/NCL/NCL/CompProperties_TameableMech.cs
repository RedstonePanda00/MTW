using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace NCL
{
    public class CompProperties_TameableMech : CompProperties
    {
        public float baseTameChance = 0.5f;
        public int componentsPerTame = 1;

        public CompProperties_TameableMech()
        {
            compClass = typeof(CompTameableMech);
        }
    }

    public class CompTameableMech : ThingComp
    {
        public CompProperties_TameableMech Props => (CompProperties_TameableMech)props;

        public bool CanBeTamed =>
            parent is Pawn mech &&
            mech.Faction == null &&
            mech.RaceProps.IsMechanoid &&
            mech.Spawned;

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (var g in base.CompGetGizmosExtra())
            {
                yield return g;
            }

            if (!CanBeTamed) yield break;

            Command_Action tameCommand = new Command_Action
            {
                icon = ContentFinder<Texture2D>.Get("ModIcon/TameMech"),
                defaultLabel = "TameMech".Translate(),
                defaultDesc = "TameMechDesc".Translate(Props.componentsPerTame),
                action = StartTamingProcess,
                hotKey = KeyBindingDefOf.Misc1
            };

            // 新版设置禁用状态的方式
            if (!HasEnoughComponentsInMap())
            {
                tameCommand.Disable("NotEnoughComponents".Translate(Props.componentsPerTame));
            }
            else if (FindBestTamer(parent as Pawn) == null)
            {
                tameCommand.Disable("NoAvailableTamer".Translate());
            }

            yield return tameCommand;
        }

        private void StartTamingProcess()
        {
            if (parent is not Pawn mech) return;

            if (FindBestTamer(mech) is Pawn tamer)
            {
                AddTameDesignation(mech);
                CreateTameJob(tamer, mech);
            }
        }

        private void CreateTameJob(Pawn tamer, Pawn mech)
        {
            Job job = JobMaker.MakeJob(TameMechefOf.TW_TameMech, mech);
            job.count = Props.componentsPerTame;
            job.targetB = FindClosestComponents(tamer);
            tamer.jobs.TryTakeOrderedJob(job, JobTag.Misc);
        }

        private bool HasEnoughComponentsInMap()
        {
            return parent.Map.listerThings.ThingsOfDef(ThingDefOf.ComponentIndustrial)
                .Sum(t => t.stackCount) >= Props.componentsPerTame;
        }

        private Pawn FindBestTamer(Pawn mech)
        {
            return mech.Map.mapPawns.FreeColonists
                .Where(p => !p.WorkTypeIsDisabled(WorkTypeDefOf.Handling))
                .OrderBy(p => p.Position.DistanceTo(mech.Position))
                .FirstOrDefault();
        }

        private Thing FindClosestComponents(Pawn pawn)
        {
            return GenClosest.ClosestThingReachable(
                pawn.Position,
                pawn.Map,
                ThingRequest.ForDef(ThingDefOf.ComponentIndustrial),
                PathEndMode.ClosestTouch,
                TraverseParms.For(pawn),
                maxDistance: 50f,
                validator: t => !t.IsForbidden(pawn) && pawn.CanReserve(t)
            );
        }

        private void AddTameDesignation(Pawn mech)
        {
            if (mech.Map.designationManager.DesignationOn(mech, TameMechefOf.TW_TameMechDesignation) == null)
            {
                mech.Map.designationManager.AddDesignation(
                    new Designation(mech, TameMechefOf.TW_TameMechDesignation));
            }
        }

        public static void OnTameSuccess(Pawn mech)
        {
            mech.SetFaction(Faction.OfPlayer);
            mech.Map.designationManager.RemoveAllDesignationsOn(mech);
            Messages.Message("MessageMechTamed".Translate(mech.LabelShort), mech, MessageTypeDefOf.PositiveEvent);
        }
    }
}


namespace NCL
{
    public class JobDriver_TameMech : JobDriver
    {
        private const TargetIndex MechIndex = TargetIndex.A;
        private const TargetIndex ComponentIndex = TargetIndex.B;

        protected Pawn Mech => (Pawn)job.GetTarget(MechIndex).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(Mech, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            // 检查机械族是否仍可被驯服
            this.FailOn(() => Mech.Faction != null || !Mech.RaceProps.IsMechanoid);

            // 0. 检查库存是否已有足够零部件
            Toil checkInventory = new Toil();
            checkInventory.initAction = () =>
            {
                int compInInventory = pawn.inventory.innerContainer.TotalStackCountOfDef(ThingDefOf.ComponentIndustrial);
                if (compInInventory >= job.count)
                {
                    ReadyForNextToil();
                }
            };
            yield return checkInventory;

            // 1. 如果库存不足，去拿取零部件
            Toil getComponents = new Toil();
            getComponents.initAction = () =>
            {
                Thing components = FindClosestComponents(pawn, job.count - pawn.inventory.innerContainer.TotalStackCountOfDef(ThingDefOf.ComponentIndustrial));
                if (components == null)
                {
                    pawn.jobs.EndCurrentJob(JobCondition.Incompletable);
                }
                else
                {
                    job.SetTarget(TargetIndex.B, components);
                }
            };
            yield return getComponents;

            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch)
                .FailOnDespawnedNullOrForbidden(TargetIndex.B);

            yield return Toils_Haul.TakeToInventory(TargetIndex.B, job.count - pawn.inventory.innerContainer.TotalStackCountOfDef(ThingDefOf.ComponentIndustrial));

            // 2. 前往机械族
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            // 3. 等待驯服过程
            Toil waitToil = new Toil();
            waitToil.initAction = () =>
            {
                pawn.pather.StopDead();
                pawn.rotationTracker.FaceTarget(Mech);
            };
            waitToil.defaultCompleteMode = ToilCompleteMode.Delay;
            waitToil.defaultDuration = 250;
            yield return waitToil;

            // 4. 执行驯服效果
            // 只需要修改 JobDriver_TameMech 类中的最后一个 Toil（执行驯服效果的部分）
            yield return new Toil
            {
                initAction = () =>
                {
                    // 消耗零部件（保持不变）
                    int compNeeded = Mech.GetComp<CompTameableMech>().Props.componentsPerTame;
                    List<Thing> comps = pawn.inventory.innerContainer.Where(t => t.def == ThingDefOf.ComponentIndustrial).ToList();

                    int remaining = compNeeded;
                    foreach (Thing comp in comps)
                    {
                        int take = Mathf.Min(remaining, comp.stackCount);
                        pawn.inventory.innerContainer.Take(comp, take).Destroy();
                        remaining -= take;
                        if (remaining <= 0) break;
                    }

                    // 驯服结果
                    if (Rand.Value < 0.5f)
                    {
                        Mech.SetFaction(Faction.OfPlayer);
                        Messages.Message("MessageMechTamed".Translate(Mech.LabelShort), Mech, MessageTypeDefOf.PositiveEvent);
                    }
                    else
                    {
                        Messages.Message("MessageFailedToTameMech".Translate(Mech.LabelShort), Mech, MessageTypeDefOf.NegativeEvent);

                        // 修改点：失败后直接重新创建相同工作
                        Job retryJob = JobMaker.MakeJob(TameMechefOf.TW_TameMech, Mech);
                        retryJob.count = compNeeded;
                        pawn.jobs.jobQueue.EnqueueFirst(retryJob);

                        // 保持原有的设计标记逻辑
                        if (pawn.Map.designationManager.DesignationOn(Mech, TameMechefOf.TW_TameMechDesignation) == null)
                        {
                            pawn.Map.designationManager.AddDesignation(
                                new Designation(Mech, TameMechefOf.TW_TameMechDesignation));
                        }
                    }
                },
                defaultCompleteMode = ToilCompleteMode.Instant
            };
        }

        private Thing FindClosestComponents(Pawn pawn, int needed)
        {
            return GenClosest.ClosestThingReachable(
                pawn.Position,
                pawn.Map,
                ThingRequest.ForDef(ThingDefOf.ComponentIndustrial),
                PathEndMode.ClosestTouch,
                TraverseParms.For(pawn),
                maxDistance: 50f,
                validator: t => !t.IsForbidden(pawn) && pawn.CanReserve(t) && t.stackCount >= needed
            );
        }


    }



    [DefOf]
        public static class TameMechefOf
    {
            public static JobDef TW_TameMech;
            public static DesignationDef TW_TameMechDesignation;

            static TameMechefOf()
            {
                DefOfHelper.EnsureInitializedInCtor(typeof(TameMechefOf));
            }
    }


    }
        