using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace NCL
{
    [StaticConstructorOnStartup]
    public class Verb_CastAbilityDragonFly : Verb_CastAbility
    {
        public static readonly Texture2D TargeterMouseAttachment = ContentFinder<Texture2D>.Get("Things/SuckerPunch", true);
        private DragonFlyExtension dragonFlyExtension;
        public int MaxLaunchDistance = 50;

        protected override bool TryCastShot()
        {
            // 仅限玩家阵营使用
            if (this.caster.Faction != Faction.OfPlayer)
                return false;

            this.dragonFlyExtension = this.ability.def.GetModExtension<DragonFlyExtension>();
            if (this.dragonFlyExtension == null)
            {
                Log.Error("Missing DragonFlyExtension on ability: " + this.ability.def.defName);
                return false;
            }

            // 必须是玩家控制的机甲且已征召
            if (this.CasterPawn.IsColonyMechPlayerControlled && this.CasterPawn.Drafted)
            {
                this.StartChoosingDestination();
            }

            // 开始冷却
            this.ability.StartCooldown(this.ability.def.cooldownTicksRange.RandomInRange);
            return true;
        }

        public void StartChoosingDestination()
        {
            CameraJumper.TryJump(CameraJumper.GetWorldTarget(this.caster));
            Find.WorldSelector.ClearSelection();
            int tile = this.caster.Map.Tile;

            // 使用原版调用方式
            Find.WorldTargeter.BeginTargeting(
                new Func<GlobalTargetInfo, bool>(this.ChoseWorldTarget),
                true,
                Verb_CastAbilityDragonFly.TargeterMouseAttachment,
                true,
                delegate ()
                {
                    GenDraw.DrawWorldRadiusRing(tile, this.MaxLaunchDistance);
                },
                (GlobalTargetInfo target) =>
                    Verb_CastAbilityDragonFly.TargetingLabelGetter(target, tile, this.MaxLaunchDistance, null, new Action<int, TransportersArrivalAction>(this.TryLaunch)),
                null
            );
        }

        public void TryLaunch(int destinationTile, TransportersArrivalAction arrivalAction)
        {
            Pawn pawn = this.caster as Pawn;
            if (pawn != null && pawn.drafter != null)
            {
                pawn.drafter.Drafted = false;
            }

            if (!this.caster.Spawned)
            {
                Log.Error("Tried to launch " + this.caster + ", but it's unspawned.");
                return;
            }

            Map map = this.caster.Map;
            int distance = Find.WorldGrid.TraversalDistanceBetween(map.Tile, destinationTile, true, int.MaxValue);
            if (distance > this.MaxLaunchDistance)
            {
                Messages.Message("TransportPodDestinationBeyondMaximumRange".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }

            // 使用 ActiveTransporter - 从扩展中获取定义
            ThingDef activeTransporterDef = this.dragonFlyExtension.activeTransporterDef ?? ThingDef.Named("TW_ActiveDropPod");
            Thing transporterThing = ThingMaker.MakeThing(activeTransporterDef);

            ActiveTransporter activeTransporter = transporterThing as ActiveTransporter;
            activeTransporter.Contents = new ActiveTransporterInfo();

            this.caster.DeSpawn(DestroyMode.Vanish);
            bool added = activeTransporter.Contents.innerContainer.TryAddOrTransfer(this.caster, true);

            if (!added)
            {
                Log.Error("Failed to add caster to transporter");
                return;
            }

            // 创建发射舱
            FlyShipLeaving flyShip = (FlyShipLeaving)SkyfallerMaker.MakeSkyfaller(this.dragonFlyExtension.dropPodLeavingDef, activeTransporter);
            flyShip.groupID = Find.UniqueIDsManager.GetNextTransporterGroupID();
            flyShip.destinationTile = destinationTile;
            flyShip.arrivalAction = arrivalAction;
            flyShip.worldObjectDef = WorldObjectDefOf.TravellingTransporters;

            GenSpawn.Spawn(flyShip, this.caster.Position, map, WipeMode.Vanish);

            CameraJumper.TryHideWorld();
        }

        public bool ChoseWorldTarget(GlobalTargetInfo target)
        {
            return this.ChoseWorldTarget(target, this.caster.Map.Tile, this.MaxLaunchDistance, new Action<int, TransportersArrivalAction>(this.TryLaunch));
        }

        public bool ChoseWorldTarget(GlobalTargetInfo target, int tile, int maxLaunchDistance, Action<int, TransportersArrivalAction> launchAction)
        {
            if (!target.IsValid)
            {
                Messages.Message("MessageTransportPodsDestinationIsInvalid".Translate(), MessageTypeDefOf.RejectInput, false);
                return false;
            }

            int distance = Find.WorldGrid.TraversalDistanceBetween(tile, target.Tile, true, int.MaxValue);
            if (maxLaunchDistance > 0 && distance > maxLaunchDistance)
            {
                Messages.Message("TransportPodDestinationBeyondMaximumRange".Translate(), MessageTypeDefOf.RejectInput, false);
                return false;
            }

            IEnumerable<FloatMenuOption> options = this.GetTransportPodsFloatMenuOptionsAt(target.Tile);
            if (!options.Any())
            {
                return false;
            }

            if (options.Count() == 1)
            {
                FloatMenuOption option = options.First();
                if (!option.Disabled)
                {
                    option.action();
                    return true;
                }
                return false;
            }

            Find.WindowStack.Add(new FloatMenu(options.ToList()));
            return false;
        }

        public IEnumerable<FloatMenuOption> GetTransportPodsFloatMenuOptionsAt(int tile)
        {
            foreach (WorldObject worldObject in Find.WorldObjects.AllWorldObjects)
            {
                if (worldObject.Tile == tile)
                {
                    MapParent mapParent = worldObject as MapParent;
                    if (mapParent != null && mapParent.HasMap)
                    {
                        yield return this.GetTransportPodsFloatMenuOptions(tile, mapParent);
                    }
                }
            }
        }

        public FloatMenuOption GetTransportPodsFloatMenuOptions(int tile, MapParent mapParent)
        {
            return new FloatMenuOption("LandInExistingMap".Translate(mapParent.Label), delegate ()
            {
                Map originalMap = this.caster.Map;
                Map targetMap = mapParent.Map;

                // 切换到目标地图
                Current.Game.CurrentMap = targetMap;

                // 选择具体位置
                Find.Targeter.BeginTargeting(
                    TargetingParameters.ForDropPodsDestination(),
                    delegate (LocalTargetInfo x)
                    {
                        // 创建到达动作，使用扩展定义的降落舱Def（如果没有则使用默认）
                        ThingDef landingPodDef = this.dragonFlyExtension.landingPodDef ?? ThingDefOf.DropPodIncoming;

                        // 获取ActiveTransporter定义
                        ThingDef activeTransporterDef = this.dragonFlyExtension.activeTransporterDef ?? ThingDef.Named("TW_ActiveDropPod");

                        var arrivalAction = new TransportersArrivalAction_SZLandInSpecificCell(mapParent, x.Cell, landingPodDef, activeTransporterDef);
                        this.TryLaunch(tile, arrivalAction);
                    },
                    null,
                    delegate ()
                    {
                        // 切换回原地图
                        if (Find.Maps.Contains(originalMap))
                        {
                            Current.Game.CurrentMap = originalMap;
                        }
                    },
                    CompLaunchable.TargeterMouseAttachment,
                    true
                );
            }, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0);
        }

        public static string TargetingLabelGetter(GlobalTargetInfo target, int tile, int maxLaunchDistance, IEnumerable<IThingHolder> pods, Action<int, TransportersArrivalAction> launchAction)
        {
            MapParent mapParent = target.WorldObject as MapParent;
            if (mapParent != null && mapParent.HasMap)
            {
                return "ClickToSeeAvailableOrders_WorldObject".Translate(mapParent.LabelCap);
            }
            return "InvalidTarget".Translate();
        }
    }

    // 自定义到达动作 - 修复抽象成员缺失问题
    public class TransportersArrivalAction_SZLandInSpecificCell : TransportersArrivalAction
    {
        private MapParent mapParent;
        private IntVec3 cell;
        private ThingDef landingPodDef; // 存储降落舱Def
        private ThingDef activeTransporterDef; // 存储ActiveTransporter Def

        public TransportersArrivalAction_SZLandInSpecificCell(MapParent mapParent, IntVec3 cell, ThingDef landingPodDef, ThingDef activeTransporterDef)
        {
            this.mapParent = mapParent;
            this.cell = cell;
            this.landingPodDef = landingPodDef;
            this.activeTransporterDef = activeTransporterDef;
        }

        // 实现抽象属性
        public override bool GeneratesMap => false;

        public override void Arrived(List<ActiveTransporterInfo> transporters, PlanetTile tile)
        {
            Map map = mapParent.Map;
            if (map == null)
            {
                Log.Error("Destination map not found for tile: " + tile.Tile);
                return;
            }

            for (int i = 0; i < transporters.Count; i++)
            {
                // 创建降落舱 - 使用配置的Def或默认
                Thing landingPodThing = ThingMaker.MakeThing(landingPodDef);
                Skyfaller landingPod = landingPodThing as Skyfaller;

                if (landingPod == null)
                {
                    Log.Error("Failed to create landing pod");
                    continue;
                }

                // 创建 ActiveTransporter - 使用配置的Def
                Thing transporterThing = ThingMaker.MakeThing(activeTransporterDef);
                ActiveTransporter activeTransporter = transporterThing as ActiveTransporter;
                activeTransporter.Contents = transporters[i];

                // 添加到降落舱
                bool added = landingPod.innerContainer.TryAdd(activeTransporter);
                if (!added)
                {
                    Log.Error("Failed to add transporter to landing pod");
                    continue;
                }

                // 在目标位置生成降落舱
                GenSpawn.Spawn(landingPod, cell, map, WipeMode.Vanish);
            }
        }

        public override void ExposeData()
        {
            Scribe_References.Look(ref mapParent, "mapParent");
            Scribe_Values.Look(ref cell, "cell");
            Scribe_Defs.Look(ref landingPodDef, "landingPodDef");
            Scribe_Defs.Look(ref activeTransporterDef, "activeTransporterDef");
        }
    }

    // 扩展类定义 - 添加降落舱Def支持
    public class DragonFlyExtension : DefModExtension
    {
        public ThingDef dropPodLeavingDef; // 发射的 Skyfaller（离开动画）
        public ThingDef landingPodDef;     // 降落的 Skyfaller（进入动画），如果没有设置则使用DropPodIncoming
        public ThingDef activeTransporterDef; // ActiveTransporter Def，如果没有设置则使用TW_ActiveDropPod
    }
}