using RimWorld;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Verse;
using Verse.AI;

namespace NCL
{
    public class ModExtension_AncientWarBeacon : DefModExtension
    {
        public List<DragonGraveStageReq> stages;

        [MustTranslate]
        public string toggleGizmoLabel = "NCL_WARBEACON_TOGGLE_GIZMO_LABEL"; // 移除 .Translate()

        [MustTranslate]
        public string toggleGizmoOffLabel = "NCL_WARBEACON_TOGGLE_GIZMO_OFF_LABEL"; // 移除 .Translate()

        [NoTranslate]
        public string toggleGizmoIcon = "";

        [NoTranslate]
        public string toggleGizmoOffIcon = "";

        public ThingDef finalThing;

        public PawnKindDef finalPawnKind;
    }
}

namespace NCL
{
    public class DragonGraveStageReq
    {
        public List<ThingDefCountClass> Things;

        public int timeDuration = 6000;

        public List<ThingDefCountRangeClass> Rewards;

        public List<ThingDefCountRangeClass> OutRewards;

        [MustTranslate]
        public string completeMessage;

        public GraphicData graphic;

        public List<FactionDef> raidFactions;

        public IntRange raidPointRange;

        public List<PawnKindDefCount> boss;

        public float rechargePower = 1000f;

        public bool extraCenterDrop;

        public bool extraMechCluster;

        public bool raidWhenCountdownStart;
    }
}




namespace NCL
{
    [StaticConstructorOnStartup]
    public class Building_Ancient_WarBeacon : Building
    {
        // 静态材质和尺寸
        private static readonly Material filledMat = SolidColorMaterials.SimpleSolidColorMaterial(new Color(0.5f, 0.475f, 0.1f), false);
        private static readonly Material emptyMat = SolidColorMaterials.SimpleSolidColorMaterial(new Color(0.15f, 0.15f, 0.15f), false);
        private static readonly Vector2 BarSize = new Vector2(1f, 0.14f);

        // 实例字段
        public bool allowFilling;
        public int stage;
        public List<ThingDefCountClass> requiredThings = new List<ThingDefCountClass>();
        public CompPowerTrader compPower;
        private Gizmo toggleGizmo;
        public int Progress;

        // 属性部分
        public ModExtension_AncientWarBeacon extension
        {
            get
            {
                return this.def.GetModExtension<ModExtension_AncientWarBeacon>();
            }
        }

        public Gizmo ToggleGizmo
        {
            get
            {
                if (this.toggleGizmo == null)
                {
                    this.RefreshGizmo();
                }
                return this.toggleGizmo;
            }
        }

        public bool stageValid
        {
            get
            {
                return this.stage < this.extension.stages.Count;
            }
        }

        public bool matFulfilled
        {
            get
            {
                return this.requiredThings.NullOrEmpty<ThingDefCountClass>();
            }
        }

        public DragonGraveStageReq curStage
        {
            get
            {
                return this.extension.stages[this.stage];
            }
        }

        public DragonGraveStageReq curStageii
        {
            get
            {
                return this.extension.stages[this.stage - 1];
            }
        }

        public override Graphic Graphic
        {
            get
            {
                if (this.stageValid && this.curStage.graphic != null)
                {
                    return this.curStage.graphic.Graphic;
                }
                return base.Graphic;
            }
        }

        // 方法部分
        public void RefreshGizmo()
        {
            Texture icon = (Texture)ContentFinder<Texture2D>.Get(
                this.allowFilling ? this.extension.toggleGizmoIcon : this.extension.toggleGizmoOffIcon,
                false);

            string defaultLabel = this.allowFilling
                ? this.extension.toggleGizmoLabel.Translate()
                : this.extension.toggleGizmoOffLabel.Translate();

            this.toggleGizmo = new Command_Action
            {
                action = delegate ()
                {
                    this.allowFilling = !this.allowFilling;
                    this.RefreshGizmo();
                },
                defaultLabel = defaultLabel,
                icon = icon
            };
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look<bool>(ref this.allowFilling, "allowFilling", false, false);
            Scribe_Values.Look<int>(ref this.stage, "stage", 0, false);
            Scribe_Collections.Look<ThingDefCountClass>(ref this.requiredThings, "requiredThings", LookMode.Deep, Array.Empty<object>());
        }

        public override void PostMake()
        {
            base.PostMake();
            this.CopyStageReq();
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            this.compPower = this.TryGetComp<CompPowerTrader>();
        }

        public bool TryAcceptThing(Thing thing)
        {
            if (thing == null || !this.allowFilling || !this.stageValid)
            {
                return false;
            }

            int index = this.requiredThings.FirstIndexOf((ThingDefCountClass t) => t.thingDef == thing.def);
            if (index < 0)
            {
                return false;
            }

            this.requiredThings[index].count -= thing.stackCount;
            thing.Destroy(DestroyMode.Vanish);

            if (this.requiredThings[index].count <= 0)
            {
                this.requiredThings.RemoveAt(index);
            }

            if (this.matFulfilled && this.curStage.raidWhenCountdownStart)
            {
                this.DoRaid();
            }

            return true;
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            base.DrawAt(drawLoc, flip);
            if (this.Progress > 0)
            {
                GenDraw.DrawFillableBar(new GenDraw.FillableBarRequest
                {
                    center = this.DrawPos + Vector3.up * 0.1f + Vector3.forward * 0.1f,
                    size = Building_Ancient_WarBeacon.BarSize,
                    fillPercent = (float)this.Progress / (float)this.curStage.timeDuration,
                    filledMat = Building_Ancient_WarBeacon.filledMat,
                    unfilledMat = Building_Ancient_WarBeacon.emptyMat,
                    margin = 0.15f
                });
            }
        }

        protected override void Tick()
        {
            base.Tick();
            this.DoProgress(1);
        }

        public override void TickRare()
        {
            base.TickRare();
            this.DoProgress(250);
        }

        public override void TickLong()
        {
            base.TickLong();
            this.DoProgress(2000);
        }

        public void DoProgress(int i = 1)
        {
            if (this.compPower != null)
            {
                if (this.matFulfilled)
                {
                    this.compPower.PowerOutput = 0f - this.curStage.rechargePower;
                }
                else
                {
                    this.compPower.PowerOutput = 0f - base.PowerComp.Props.idlePowerDraw;
                }
            }

            if (this.matFulfilled && (this.compPower == null || this.compPower.PowerOn))
            {
                this.Progress += i;
                if (this.Progress >= this.curStage.timeDuration)
                {
                    this.Upgrade();
                }
            }
        }

        public int RequiredCountFor(ThingDef def)
        {
            if (this.requiredThings.NullOrEmpty<ThingDefCountClass>())
            {
                return 0;
            }

            int index = this.requiredThings.FirstIndexOf((ThingDefCountClass t) => t.thingDef == def);
            return index < 0 ? 0 : this.requiredThings[index].count;
        }

        public void CopyStageReq()
        {
            foreach (ThingDefCountClass thingDefCountClass in this.curStage.Things)
            {
                ThingDefCountClass item = new ThingDefCountClass(thingDefCountClass.thingDef, thingDefCountClass.count);
                this.requiredThings.Add(item);
            }
            this.Progress = 0;
        }

        public void Upgrade()
        {
            try
            {
                // 基础检查
                if (this.extension == null)
                {
                    Log.Error("Extension is null in Rebuilding()");
                    return;
                }

                if (!this.stageValid)
                {
                    Log.Warning($"Invalid stage {this.stage} during Rebuilding");
                    return;
                }

                var currentStage = this.curStage;
                if (currentStage == null)
                {
                    Log.Error("Current stage is null");
                    return;
                }

                // 检查最终产物是否为TW_Complete_Shellcore_Bastion
                bool isShellcoreBastion = this.extension.finalThing?.defName == "TW_Complete_Shellcore_Bastion";

                // 更新状态
                this.allowFilling = false;
                this.RefreshGizmo();

                // 安全处理电力
                if (this.compPower != null)
                {
                    this.compPower.PowerOutput = 0f - (base.PowerComp?.Props.idlePowerDraw ?? 0f);
                }

                // 发放奖励（如果是Shellcore Bastion则跳过NewSelect）
                if (currentStage.Rewards != null && !isShellcoreBastion)
                {
                    this.NewSelect(base.Position, base.Map);
                }

                // 触发袭击
                if (!currentStage.raidWhenCountdownStart)
                {
                    this.DoRaid();
                }

                this.stage++;

                // 最终转化
                if (!this.stageValid)
                {
                    // 生成最终单位或建筑
                    if (this.extension.finalPawnKind != null)
                    {
                        Pawn newThing = PawnGenerator.GeneratePawn(this.extension.finalPawnKind, Faction.OfPlayer);
                        GenSpawn.Spawn(newThing, base.Position, base.Map, Rot4.Random, WipeMode.Vanish);
                    }

                    if (this.extension.finalThing != null)
                    {
                        this.ConvertToFinalBuilding();
                    }
                }
                else
                {
                    this.CopyStageReq();
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Rebuild failed: {ex}");
            }
        }




        private void ConvertToFinalBuilding()
        {
            try
            {
                bool wasSelected = Find.Selector.IsSelected(this);
                IntVec3 pos = base.Position;
                Map map = base.Map;
                Rot4 rot = base.Rotation;

                this.DeSpawn(DestroyMode.Vanish);

                Thing finalThing = ThingMaker.MakeThing(this.extension.finalThing);
                finalThing.SetFaction(Faction.OfPlayer);
                GenSpawn.Spawn(finalThing, pos, map, rot);

                if (wasSelected)
                {
                    Find.Selector.Select(finalThing, true, true);
                }

                this.Destroy(DestroyMode.Vanish);
            }
            catch (Exception ex)
            {
                Log.Error($"WarBeacon Final conversion failed: {ex}");
            }
        }


        private void NewSelect(IntVec3 intVec, Map map)
        {
            string title = "NCL_WARBEACON_REACTIVATE_TITLE".Translate();
            string title2 = "SecondWarBeacon".Translate();
            string title3 = "ThirdWarBeacon".Translate();
            string str = "NCL_WARBEACON_REACTIVATE_DESCRIPTION".Translate();
            string str2 = "SecondWarBeaconDec".Translate();
            string str3 = "ThirdWarBeaconDec".Translate();
            string text = "NCL_WARBEACON_ELLIPSES".Translate();
            string FirstLog = "NCL_WARBEACON_FIRST_LOG".Translate();
            string SecondLog = "SecondWarBeaconLog".Translate();
            DiaNode diaNode = new DiaNode(str);
            DiaNode diaNode2 = new DiaNode(str2);
            DiaNode diaNode3 = new DiaNode(str3);
            DiaOption item = new DiaOption(text)
            {
                action = delegate ()
                {
                    Messages.Message(FirstLog, MessageTypeDefOf.PositiveEvent, true);
                    foreach (ThingDefCountRangeClass thingDefCountRangeClass in this.curStageii.Rewards)
                    {
                        int i = thingDefCountRangeClass.countRange.RandomInRange;
                        bool flag = i > 0;
                        if (flag)
                        {
                            while (i > 0)
                            {
                                Thing thing = ThingMaker.MakeThing(thingDefCountRangeClass.thingDef, null);
                                thing.stackCount = ((i > thingDefCountRangeClass.thingDef.stackLimit) ? thingDefCountRangeClass.thingDef.stackLimit : i);
                                i -= thingDefCountRangeClass.thingDef.stackLimit;
                                Thing thing2;
                                GenDrop.TryDropSpawn(thing, intVec, map, ThingPlaceMode.Near, out thing2, null, null, true);
                            }
                        }
                    }
                },
                resolveTree = true
            };
            diaNode.options.Add(item);
            diaNode2.options.Add(item);
            diaNode3.options.Add(item);
           
            int num = this.stage;
            int num2 = num;
            if (num2 != 0)
            {
                if (num2 != 1)
                {
                    Find.WindowStack.Add(new Dialog_NodeTree(diaNode3, true, true, title3));
                }
                else
                {
                    Find.WindowStack.Add(new Dialog_NodeTree(diaNode2, true, true, title2));
                }
            }
            else
            {
                Find.WindowStack.Add(new Dialog_NodeTree(diaNode, true, true, title));
            }
        }

        public void DoRaid()
        {
            // ...保持原始DoRaid方法实现不变...
            // 此处应完整复制原始DoRaid方法内容
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            bool flag = mode != DestroyMode.Vanish && this.stageValid;
            if (flag)
            {
                if (this.extension.finalPawnKind != null)
                {
                    Pawn pawn = PawnGenerator.GeneratePawn(this.extension.finalPawnKind, Faction.OfAncientsHostile);
                    GenSpawn.Spawn(pawn, base.Position, base.Map, Rot4.Random, WipeMode.Vanish, false, false);
                    pawn.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.Manhunter, null, false, false, false, null, false, false, false);
                }
                else
                {
                    // log
                    Log.Error($"[NCL] Building {this.def.defName} tried to spawn a pawn on destroy, but its finalPawnKind is null in the ModExtension XML.");
                }
                
                foreach (ThingDefCountClass thingDefCountClass in this.curStage.Things)
                {
                    int i = thingDefCountClass.count;
                    i -= this.RequiredCountFor(thingDefCountClass.thingDef);
                    bool flag2 = i > 0;
                    if (flag2)
                    {
                        while (i > 0)
                        {
                            Thing thing = ThingMaker.MakeThing(thingDefCountClass.thingDef, null);
                            thing.stackCount = ((i > thingDefCountClass.thingDef.stackLimit) ? thingDefCountClass.thingDef.stackLimit : i);
                            i -= thingDefCountClass.thingDef.stackLimit;
                            Thing thing2;
                            GenDrop.TryDropSpawn(thing, base.Position, base.Map, ThingPlaceMode.Near, out thing2, null, null, true);
                        }
                    }
                }
            }
            base.Destroy(mode);
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }
            yield return this.ToggleGizmo;
            bool flag = !DebugSettings.ShowDevGizmos;
            if (flag)
            {
                yield break;
            }
            bool flag2 = !this.matFulfilled;
            if (flag2)
            {
                yield return new Command_Action
                {
                    defaultLabel = "DEV: Finish requirement",
                    action = delegate ()
                    {
                        this.requiredThings.Clear();
                        bool raidWhenCountdownStart = this.curStage.raidWhenCountdownStart;
                        if (raidWhenCountdownStart)
                        {
                            this.DoRaid();
                        }
                    }
                };
            }
            yield return new Command_Action
            {
                defaultLabel = "DEV: upgrade",
                action = delegate ()
                {
                    this.Upgrade();
                }
            };
            yield break;
        }

        public override string GetInspectStringLowPriority()
        {
            StringBuilder stringBuilder = new StringBuilder();

            // 修复：添加翻译键调用
            stringBuilder.AppendLine("NCL_WARBEACON_INSPECT_DESCRIPTION".Translate());

            bool matFulfilled = this.matFulfilled;
            if (matFulfilled)
            {
                // 修复：添加翻译键调用
                stringBuilder.AppendLine("NCL_WARBEACON_REPAIR_TIME".Translate() +
                    (this.curStage.timeDuration - this.Progress).ToStringTicksToDays("F1"));
            }
            else
            {
                // 修复：添加翻译键调用
                stringBuilder.AppendLine("NCL_WARBEACON_REQUIRED_RESOURCES".Translate());
                foreach (ThingDefCountClass thingDefCountClass in this.requiredThings)
                {
                    stringBuilder.AppendLine(thingDefCountClass.thingDef.label.ToString() + ": " + thingDefCountClass.count.ToString());
                }
            }
            string text = stringBuilder.ToString();
            return text.TrimEnd(Array.Empty<char>());
        }
    }
}


namespace NCL
{
    // Token: 0x02000016 RID: 22
    internal class JobDriver_RebuildTheWarBeacon : JobDriver
    {
        // Token: 0x17000012 RID: 18
        // (get) Token: 0x06000053 RID: 83 RVA: 0x00004054 File Offset: 0x00002254
        protected Building_Ancient_WarBeacon Grave
        {
            get
            {
                return (Building_Ancient_WarBeacon)this.job.GetTarget(TargetIndex.A).Thing;
            }
        }

        // Token: 0x17000013 RID: 19
        // (get) Token: 0x06000054 RID: 84 RVA: 0x0000407C File Offset: 0x0000227C
        protected Thing Thing
        {
            get
            {
                return this.job.GetTarget(TargetIndex.B).Thing;
            }
        }

        // Token: 0x06000055 RID: 85 RVA: 0x000040A0 File Offset: 0x000022A0
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            bool flag = this.pawn.Reserve(this.Grave, this.job, 1, -1, null, errorOnFailed, false);
            return flag && this.pawn.Reserve(this.Thing, this.job, 1, -1, null, errorOnFailed, false);
        }

        // Token: 0x06000056 RID: 86 RVA: 0x000040FD File Offset: 0x000022FD
        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOnBurningImmobile(TargetIndex.A);
            base.AddEndCondition(() => (this.Grave.requiredThings.Any<ThingDefCountClass>() && this.Grave.allowFilling) ? JobCondition.Ongoing : JobCondition.Succeeded);
            yield return Toils_General.DoAtomic(delegate
            {
                this.job.count = this.Grave.RequiredCountFor(this.Thing.def);
            });
            Toil reserveWort = Toils_Reserve.Reserve(TargetIndex.B, 1, -1, null, false);
            yield return reserveWort;
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch, false).FailOnDespawnedNullOrForbidden(TargetIndex.B).FailOnSomeonePhysicallyInteracting(TargetIndex.B);
            yield return Toils_Haul.StartCarryThing(TargetIndex.B, false, true, false, true, false).FailOnDestroyedNullOrForbidden(TargetIndex.B);
            yield return Toils_Haul.CheckForGetOpportunityDuplicate(reserveWort, TargetIndex.B, TargetIndex.None, true, null);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch, false);
            yield return Toils_General.Wait(200, TargetIndex.None).FailOnDestroyedNullOrForbidden(TargetIndex.B).FailOnDestroyedNullOrForbidden(TargetIndex.A).FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch).WithProgressBarToilDelay(TargetIndex.A, false, -0.5f);
            Toil toil = ToilMaker.MakeToil("MakeNewToils");
            toil.initAction = delegate ()
            {
                this.Grave.TryAcceptThing(this.Thing);
            };
            toil.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return toil;
            yield break;
        }

        // Token: 0x0400002D RID: 45
        private const TargetIndex GraveIndex = TargetIndex.A;

        // Token: 0x0400002E RID: 46
        private const TargetIndex ThingIndex = TargetIndex.B;

        // Token: 0x0400002F RID: 47
        private const int Duration = 200;
    }
}

namespace NCL
{
    // Token: 0x02000015 RID: 21
    internal class WorkGiver_RebuildTheWarBeacon : WorkGiver_Scanner
    {
        // Token: 0x17000010 RID: 16
        // (get) Token: 0x0600004C RID: 76 RVA: 0x00003E4B File Offset: 0x0000204B
        public override ThingRequest PotentialWorkThingRequest
        {
            get
            {
                return ThingRequest.ForDef(DefDatabase<ThingDef>.GetNamed("Building_Ancient_WarBeacon", true));
            }
        }

        // Token: 0x17000011 RID: 17
        // (get) Token: 0x0600004D RID: 77 RVA: 0x00003E5D File Offset: 0x0000205D
        public override PathEndMode PathEndMode
        {
            get
            {
                return PathEndMode.Touch;
            }
        }

        // Token: 0x0600004E RID: 78 RVA: 0x00003E60 File Offset: 0x00002060
        public static void ResetStaticData()
        {
            WorkGiver_RebuildTheWarBeacon.NoWortTrans = "NCL_WARBEACON_NO_MATERIAL".Translate();
        }

        // Token: 0x0600004F RID: 79 RVA: 0x00003E78 File Offset: 0x00002078
        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            Building_Ancient_WarBeacon Building_Ancient_WarBeacon = t as Building_Ancient_WarBeacon;
            bool flag = Building_Ancient_WarBeacon == null || Building_Ancient_WarBeacon.requiredThings.NullOrEmpty<ThingDefCountClass>() || !Building_Ancient_WarBeacon.allowFilling;
            bool result;
            if (flag)
            {
                result = false;
            }
            else
            {
                bool flag2 = t.IsForbidden(pawn) || !pawn.CanReserve(t, 1, -1, null, forced);
                if (flag2)
                {
                    result = false;
                }
                else
                {
                    bool flag3 = pawn.Map.designationManager.DesignationOn(t, DesignationDefOf.Deconstruct) != null;
                    if (flag3)
                    {
                        result = false;
                    }
                    else
                    {
                        bool flag4 = this.FindThingToFill(pawn, Building_Ancient_WarBeacon) == null;
                        if (flag4)
                        {
                            JobFailReason.Is(WorkGiver_RebuildTheWarBeacon.NoWortTrans, null);
                            result = false;
                        }
                        else
                        {
                            bool flag5 = t.IsBurning();
                            result = !flag5;
                        }
                    }
                }
            }
            return result;
        }

        // Token: 0x06000050 RID: 80 RVA: 0x00003F3C File Offset: 0x0000213C
        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            Building_Ancient_WarBeacon buinding = (Building_Ancient_WarBeacon)t;
            Thing t2 = this.FindThingToFill(pawn, buinding);
            return JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("RebuildTheWarBeacon", true), t, t2);
        }

        // Token: 0x06000051 RID: 81 RVA: 0x00003F7C File Offset: 0x0000217C
        private Thing FindThingToFill(Pawn pawn, Building_Ancient_WarBeacon buinding)
        {
            Predicate<Thing> validator = (Thing x) => !x.IsForbidden(pawn) && pawn.CanReserve(x, 1, -1, null, false);
            Thing thing = null;
            foreach (ThingDefCountClass thingDefCountClass in buinding.requiredThings)
            {
                thing = GenClosest.ClosestThingReachable(pawn.Position, pawn.Map, ThingRequest.ForDef(thingDefCountClass.thingDef), PathEndMode.ClosestTouch, TraverseParms.For(pawn, Danger.Deadly, TraverseMode.ByPawn, false, false, false), 9999f, validator, null, 0, -1, false, RegionType.Set_Passable, false);
                bool flag = thing != null;
                if (flag)
                {
                    return thing;
                }
            }
            return thing;
        }

        // Token: 0x0400002C RID: 44
        private static string NoWortTrans;
    }
}

namespace NCL
{
    // Token: 0x02000015 RID: 21
    internal class WorkGiver_RebuildBrokenShellcoreBastion : WorkGiver_Scanner
    {
        // Token: 0x17000010 RID: 16
        // (get) Token: 0x0600004C RID: 76 RVA: 0x00003E4B File Offset: 0x0000204B
        public override ThingRequest PotentialWorkThingRequest
        {
            get
            {
                return ThingRequest.ForDef(DefDatabase<ThingDef>.GetNamed("Broken_Shellcore_Bastion", true));
            }
        }

        // Token: 0x17000011 RID: 17
        // (get) Token: 0x0600004D RID: 77 RVA: 0x00003E5D File Offset: 0x0000205D
        public override PathEndMode PathEndMode
        {
            get
            {
                return PathEndMode.Touch;
            }
        }

        // Token: 0x0600004E RID: 78 RVA: 0x00003E60 File Offset: 0x00002060
        public static void ResetStaticData()
        {
            WorkGiver_RebuildBrokenShellcoreBastion.NoWortBTrans = "NCL_SHELLCORE_NO_MATERIAL".Translate();
        }

        // Token: 0x0600004F RID: 79 RVA: 0x00003E78 File Offset: 0x00002078
        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            Building_Ancient_WarBeacon Building_Ancient_WarBeacon = t as Building_Ancient_WarBeacon;
            bool flag = Building_Ancient_WarBeacon == null || Building_Ancient_WarBeacon.requiredThings.NullOrEmpty<ThingDefCountClass>() || !Building_Ancient_WarBeacon.allowFilling;
            bool result;
            if (flag)
            {
                result = false;
            }
            else
            {
                bool flag2 = t.IsForbidden(pawn) || !pawn.CanReserve(t, 1, -1, null, forced);
                if (flag2)
                {
                    result = false;
                }
                else
                {
                    bool flag3 = pawn.Map.designationManager.DesignationOn(t, DesignationDefOf.Deconstruct) != null;
                    if (flag3)
                    {
                        result = false;
                    }
                    else
                    {
                        bool flag4 = this.FindThingToFill(pawn, Building_Ancient_WarBeacon) == null;
                        if (flag4)
                        {
                            JobFailReason.Is(WorkGiver_RebuildBrokenShellcoreBastion.NoWortBTrans, null);
                            result = false;
                        }
                        else
                        {
                            bool flag5 = t.IsBurning();
                            result = !flag5;
                        }
                    }
                }
            }
            return result;
        }

        // Token: 0x06000050 RID: 80 RVA: 0x00003F3C File Offset: 0x0000213C
        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            Building_Ancient_WarBeacon buinding = (Building_Ancient_WarBeacon)t;
            Thing t2 = this.FindThingToFill(pawn, buinding);
            return JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("RebuildBrokenShellcoreBastion", true), t, t2);
        }

        // Token: 0x06000051 RID: 81 RVA: 0x00003F7C File Offset: 0x0000217C
        private Thing FindThingToFill(Pawn pawn, Building_Ancient_WarBeacon buinding)
        {
            Predicate<Thing> validator = (Thing x) => !x.IsForbidden(pawn) && pawn.CanReserve(x, 1, -1, null, false);
            Thing thing = null;
            foreach (ThingDefCountClass thingDefCountClass in buinding.requiredThings)
            {
                thing = GenClosest.ClosestThingReachable(pawn.Position, pawn.Map, ThingRequest.ForDef(thingDefCountClass.thingDef), PathEndMode.ClosestTouch, TraverseParms.For(pawn, Danger.Deadly, TraverseMode.ByPawn, false, false, false), 9999f, validator, null, 0, -1, false, RegionType.Set_Passable, false);
                bool flag = thing != null;
                if (flag)
                {
                    return thing;
                }
            }
            return thing;
        }

        // Token: 0x0400002C RID: 44
        private static string NoWortBTrans;
    }
}
