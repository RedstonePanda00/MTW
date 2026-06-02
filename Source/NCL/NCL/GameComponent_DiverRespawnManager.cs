using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using NCL.Diver;
using NCL.Stratagem;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Steam;

namespace NCL.Worm
{
    public class GameComponent_DiverRespawnManager : GameComponent
    {
        public const int TargetCount = 4;
        public const float EnergyPerDiver = 500f;

        private const int AutoCheckIntervalTicks = 250;
        private const string DiverDefName = "MTW_Diver";
        private const string DefaultPodDefName = "NCL_AtmosphericPlasmaDropPod";
        private const int ReservedSlotCount = 4;
        private const int DiverPodImpactStaggerTicks = 15;

        private bool diverSystemEnabled;
        private bool diverIntroCompleted;
        private int preferredMapUniqueId = -1;
        private int lastDeathMapUniqueId = -1;
        private IntVec3 lastDeathCell = IntVec3.Invalid;
        private int pendingDiverCount;
        private List<Pawn> trackedDivers = new List<Pawn>();
        private DiverSlotPersistentData[] slots;
        private bool needsTrackedRebuild;
        private bool needsDeferredSlotSetup;
        private bool needsPostLoadRimTalkSync;

        // Legacy save fields — migrated once after load, not written on new saves.
        private List<string> slotNicknames;
        private List<string> slotLoadoutDefNames;
        private List<DiverSlotPersistentData> legacyLoadedSlotData;

        public bool DiverSystemEnabled => diverSystemEnabled;
        public bool DiverIntroCompleted => diverIntroCompleted;
        public bool ShouldShowDiverIntroTab => !diverIntroCompleted;

        public GameComponent_DiverRespawnManager(Game game)
        {
            InitializeSlots();
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            InitializeSlots();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref diverSystemEnabled, "nclDiverSystemEnabled", defaultValue: false);
            Scribe_Values.Look(ref diverIntroCompleted, "nclDiverIntroCompleted", defaultValue: false);
            Scribe_Values.Look(ref preferredMapUniqueId, "nclDiverPreferredMapUniqueId", defaultValue: -1);
            Scribe_Values.Look(ref lastDeathMapUniqueId, "nclDiverLastDeathMapUniqueId", defaultValue: -1);
            Scribe_Values.Look(ref lastDeathCell, "nclDiverLastDeathCell", defaultValue: IntVec3.Invalid);
            Scribe_Values.Look(ref pendingDiverCount, "nclDiverPendingCount", defaultValue: 0);
            Scribe_Collections.Look(ref trackedDivers, "nclTrackedDivers", LookMode.Reference);
            Scribe_Collections.Look(ref legacyLoadedSlotData, "nclDiverSlotData", LookMode.Deep);
            Scribe_Collections.Look(ref slotNicknames, "nclDiverSlotNicknames", LookMode.Value);
            Scribe_Collections.Look(ref slotLoadoutDefNames, "nclDiverSlotLoadoutDefNames", LookMode.Value);

            InitializeSlots();
            for (int i = 0; i < ReservedSlotCount; i++)
            {
                Scribe_Deep.Look(ref slots[i], $"nclDiverSlot_{i}");
                if (slots[i] == null)
                {
                    slots[i] = new DiverSlotPersistentData();
                }
            }

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                needsDeferredSlotSetup = true;
                needsPostLoadRimTalkSync = true;
                pendingDiverCount = Mathf.Max(0, pendingDiverCount);
                if (diverSystemEnabled)
                {
                    diverIntroCompleted = true;
                }
            }
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();
            TryRunDeferredSlotSetup();
            TryRunPostLoadRimTalkSync();

            if (!diverSystemEnabled || !IsGameReadyForDiverStateOps())
            {
                return;
            }

            if (needsTrackedRebuild)
            {
                RebuildTrackedDiversFromMaps();
                needsTrackedRebuild = false;
            }

            PruneTrackedDivers();
            if (Find.TickManager == null || Find.TickManager.TicksGame % AutoCheckIntervalTicks != 0)
            {
                return;
            }

            if (GetTrackedAliveCount() > 0 || pendingDiverCount > 0)
            {
                return;
            }

            Map map = ResolveRespawnMap();
            if (map == null)
            {
                return;
            }

            ThingDef podDef = DefDatabase<ThingDef>.GetNamedSilentFail(DefaultPodDefName);
            if (podDef == null)
            {
                return;
            }

            IntVec3 center = ResolveAutoRespawnCenter(map);
            int needCount = TargetCount - GetTrackedAliveCount();
            int launched = LaunchDiverPods(map, podDef, center, needCount, useRandomizedCenter: true);
            if (launched > 0)
            {
                Messages.Message("NCL_DiverAutoReinforce_Launched".Translate(launched), MessageTypeDefOf.PositiveEvent);
            }
        }

        public bool TryBeginManualSummonFromAurora(CompAuroraCaller callerComp, out string message)
        {
            Map map = callerComp?.parent?.Map;
            if (map == null)
            {
                message = "NCL_DiverSummon_InvalidMap".Translate();
                return false;
            }

            ThingDef podDef = callerComp.Props?.dropPodDef ?? DefDatabase<ThingDef>.GetNamedSilentFail(DefaultPodDefName);
            if (podDef == null)
            {
                message = "MTW_Diver_Reinforce_PodMissing".Translate();
                return false;
            }

            preferredMapUniqueId = map.uniqueID;
            int occupancy = GetOccupiedDiverSlotCount();
            if (occupancy >= TargetCount)
            {
                message = "NCL_DiverPanel_Full".Translate();
                return false;
            }

            diverSystemEnabled = true;
            int needCount = TargetCount - occupancy;
            Find.Targeter.BeginTargeting(
                new TargetingParameters
                {
                    canTargetLocations = true,
                    canTargetPawns = false,
                    canTargetBuildings = false
                },
                _ => { },
                null,
                target =>
                {
                    if (!target.IsValid || !target.Cell.InBounds(map))
                    {
                        Messages.Message("NCL_DiverSummon_InvalidTarget".Translate(), MessageTypeDefOf.RejectInput);
                        return false;
                    }

                    int launched = LaunchDiverPods(map, podDef, target.Cell, needCount, useRandomizedCenter: false);
                    if (launched > 0)
                    {
                        Messages.Message("MTW_Diver_Reinforce_Success".Translate(launched), MessageTypeDefOf.PositiveEvent);
                    }
                    else
                    {
                        Messages.Message("MTW_Diver_Reinforce_Failed".Translate(), MessageTypeDefOf.RejectInput);
                    }

                    return false;
                },
                null,
                null
            );

            message = "NCL_DiverSummon_SelectTarget".Translate();
            return true;
        }

        /// Launch drop pods at the target cell to fill the diver roster up to <see cref="TargetCount"/>.
        public int LaunchReinforcementPodsAt(Map map, IntVec3 targetCell)
        {
            if (map == null || !targetCell.InBounds(map) || !IsGameReadyForDiverStateOps())
            {
                return 0;
            }

            ThingDef podDef = DefDatabase<ThingDef>.GetNamedSilentFail(DefaultPodDefName);
            if (podDef == null)
            {
                return 0;
            }

            diverSystemEnabled = true;
            preferredMapUniqueId = map.uniqueID;
            PruneTrackedDivers();

            int occupancy = GetOccupiedDiverSlotCount();
            if (occupancy >= TargetCount)
            {
                return 0;
            }

            int needCount = TargetCount - occupancy;
            return LaunchDiverPods(map, podDef, targetCell, needCount, useRandomizedCenter: true);
        }

        public bool TryCompleteIntroDeploy(Map map, ThingDef podDef, out string message)
        {
            message = null;
            if (map == null)
            {
                message = "NCL_DiverIntro_InvalidMap".Translate();
                return false;
            }

            if (podDef == null)
            {
                podDef = DefDatabase<ThingDef>.GetNamedSilentFail(DefaultPodDefName);
            }

            if (podDef == null)
            {
                message = "NCL_AuroraPanel_PodOrMapInvalid".Translate();
                return false;
            }

            diverSystemEnabled = true;
            diverIntroCompleted = true;
            preferredMapUniqueId = map.uniqueID;

            int needCount = TargetCount - GetOccupiedDiverSlotCount();
            IntVec3 center = ResolveAutoRespawnCenter(map);
            int launched = 0;
            if (needCount > 0 && center.IsValid)
            {
                launched = LaunchDiverPods(map, podDef, center, needCount, useRandomizedCenter: true);
            }

            if (launched > 0)
            {
                message = "NCL_DiverIntro_DeploySuccess".Translate(launched);
            }
            else if (needCount <= 0)
            {
                message = "NCL_DiverIntro_DeployAlreadyFull".Translate();
            }
            else
            {
                message = "NCL_DiverIntro_DeployFailed".Translate();
            }

            return true;
        }

        public int GetTrackedAliveCount()
        {
            EnsureSlotListsInitialized();
            if (diverSystemEnabled && IsGameReadyForDiverStateOps())
            {
                PruneTrackedDivers();
            }

            int count = 0;
            for (int i = 0; i < ReservedSlotCount; i++)
            {
                if (IsPlayerControlledAliveDiver(trackedDivers[i]))
                {
                    count++;
                }
            }

            return count;
        }

        public int GetOccupiedDiverSlotCount()
        {
            return Mathf.Min(ReservedSlotCount, GetTrackedAliveCount() + Mathf.Max(0, pendingDiverCount));
        }

        public IReadOnlyList<DiverSlotViewData> GetSlotViewData()
        {
            EnsureSlotListsInitialized();
            if (diverSystemEnabled && IsGameReadyForDiverStateOps())
            {
                PruneTrackedDivers();
            }

            List<DiverSlotViewData> result = new List<DiverSlotViewData>(ReservedSlotCount);
            for (int i = 0; i < ReservedSlotCount; i++)
            {
                Pawn pawn = trackedDivers[i];
                DiverSlotPersistentData data = Slot(i);
                string nickname = data.nickname;
                if (nickname.NullOrEmpty() && pawn?.Name != null)
                {
                    nickname = pawn.Name.ToStringShort;
                }

                result.Add(new DiverSlotViewData(i, pawn, nickname, IsPlayerControlledAliveDiver(pawn)));
            }

            return result;
        }

        public bool TrySetSlotNickname(int slotIndex, string nickname)
        {
            EnsureSlotListsInitialized();
            if (slotIndex < 0 || slotIndex >= ReservedSlotCount || string.IsNullOrWhiteSpace(nickname))
            {
                return false;
            }

            Slot(slotIndex).nickname = nickname;
            Pawn pawn = trackedDivers[slotIndex];
            if (IsPlayerControlledAliveDiver(pawn))
            {
                pawn.Name = new NameSingle(nickname);
            }

            return true;
        }

        public int GetSlotIndexForPawn(Pawn pawn)
        {
            EnsureSlotListsInitialized();
            if (pawn == null)
            {
                return -1;
            }

            return trackedDivers.IndexOf(pawn);
        }

        public DiverSlotPersistentData GetSlotData(int slotIndex)
        {
            EnsureSlotListsInitialized();
            if (slotIndex < 0 || slotIndex >= ReservedSlotCount)
            {
                return null;
            }

            return Slot(slotIndex);
        }

        public string GetSlotLoadoutDefName(int slotIndex, int loadoutIndex)
        {
            EnsureSlotListsInitialized();
            if (slotIndex < 0 || slotIndex >= ReservedSlotCount || loadoutIndex < 0 || loadoutIndex >= 4)
            {
                return null;
            }

            DiverSlotPersistentData data = Slot(slotIndex);
            if (loadoutIndex >= data.loadoutDefNames.Count)
            {
                return null;
            }

            return data.loadoutDefNames[loadoutIndex];
        }

        public bool SetSlotLoadoutDefName(int slotIndex, int loadoutIndex, string stratagemDefName)
        {
            EnsureSlotListsInitialized();
            if (slotIndex < 0 || slotIndex >= ReservedSlotCount || loadoutIndex < 0 || loadoutIndex >= 4)
            {
                return false;
            }

            if (stratagemDefName != null)
            {
                if (DefDatabase<StratagemDef>.GetNamedSilentFail(stratagemDefName) == null)
                {
                    return false;
                }

                if (IsStratagemUsedInOtherLoadoutSlot(slotIndex, loadoutIndex, stratagemDefName))
                {
                    return false;
                }
            }

            Slot(slotIndex).loadoutDefNames[loadoutIndex] = stratagemDefName;
            return true;
        }

        public bool IsStratagemUsedInOtherLoadoutSlot(int slotIndex, int exceptLoadoutIndex, string stratagemDefName)
        {
            if (stratagemDefName.NullOrEmpty())
            {
                return false;
            }

            EnsureSlotListsInitialized();
            for (int i = 0; i < DiverSlotPersistentData.LoadoutCount; i++)
            {
                if (i == exceptLoadoutIndex)
                {
                    continue;
                }

                if (GetSlotLoadoutDefName(slotIndex, i) == stratagemDefName)
                {
                    return true;
                }
            }

            return false;
        }

        public Pawn GetTrackedDiver(int slotIndex)
        {
            EnsureSlotListsInitialized();
            if (slotIndex < 0 || slotIndex >= ReservedSlotCount)
            {
                return null;
            }

            return trackedDivers[slotIndex];
        }

        public void GetSlotCloak(int slotIndex, out string setId, out Color color, out bool visible)
        {
            EnsureSlotListsInitialized();
            DiverSlotPersistentData data = Slot(slotIndex);
            setId = data.cloakSetId;
            color = data.cloakColor;
            visible = data.cloakVisible;
        }

        public void SetSlotCloak(int slotIndex, string setId, Color color, bool visible)
        {
            EnsureSlotListsInitialized();
            if (slotIndex < 0 || slotIndex >= ReservedSlotCount)
            {
                return;
            }

            DiverSlotPersistentData data = Slot(slotIndex);
            data.cloakSetId = DiverCloakCatalog.NormalizeSetId(setId);
            data.cloakColor = color;
            data.cloakVisible = visible;

            Pawn pawn = trackedDivers[slotIndex];
            CompDiverCloak comp = CompDiverCloak.Get(pawn);
            if (comp != null)
            {
                comp.SetCloakSet(data.cloakSetId);
                comp.SetCloakColor(data.cloakColor);
                comp.SetCloakVisible(data.cloakVisible);
            }
        }

        public int GetStratagemCooldownUntilTick(int slotIndex, int loadoutIndex)
        {
            EnsureSlotListsInitialized();
            if (slotIndex < 0 || slotIndex >= ReservedSlotCount || loadoutIndex < 0 || loadoutIndex >= 4)
            {
                return 0;
            }

            return Slot(slotIndex).stratagemCooldownUntilTick[loadoutIndex];
        }

        public void SetStratagemCooldownUntilTick(int slotIndex, int loadoutIndex, int tick)
        {
            EnsureSlotListsInitialized();
            if (slotIndex < 0 || slotIndex >= ReservedSlotCount || loadoutIndex < 0 || loadoutIndex >= 4)
            {
                return;
            }

            Slot(slotIndex).stratagemCooldownUntilTick[loadoutIndex] = tick;
        }

        public float GetSlotCombatRecordValue(int slotIndex, RecordDef def, bool preferLivePawn)
        {
            EnsureSlotListsInitialized();
            if (slotIndex < 0 || slotIndex >= ReservedSlotCount || def == null)
            {
                return 0f;
            }

            Pawn pawn = trackedDivers[slotIndex];
            bool useLive = preferLivePawn && IsPlayerControlledAliveDiver(pawn);
            return DiverRecordsUtility.GetDisplayRecordValue(Slot(slotIndex), pawn, useLive, def);
        }

        public static void ApplySlotCombatLedger(int slotIndex, float damageDealt, int kills, int headshots)
        {
            GameComponent_DiverRespawnManager manager = Current.Game?.GetComponent<GameComponent_DiverRespawnManager>();
            manager?.ApplySlotCombatLedgerInternal(slotIndex, damageDealt, kills, headshots);
        }

        private void ApplySlotCombatLedgerInternal(int slotIndex, float damageDealt, int kills, int headshots)
        {
            EnsureSlotListsInitialized();
            if (slotIndex < 0 || slotIndex >= ReservedSlotCount)
            {
                return;
            }

            DiverSlotPersistentData data = Slot(slotIndex);
            if (damageDealt > 0f)
            {
                data.AddToRecord(RecordDefOf.DamageDealt, damageDealt);
            }

            if (kills > 0)
            {
                data.AddToRecord(RecordDefOf.Kills, kills);
            }

            if (headshots > 0)
            {
                data.AddToRecord(RecordDefOf.Headshots, headshots);
            }
        }

        public List<string> GetSteamDisplayNamesForSelection()
        {
            List<string> names = TryGetSteamFriendDisplayNames();
            string playerName = GetPlayerDisplayName();
            if (!names.Contains(playerName))
            {
                names.Insert(0, playerName);
            }

            return names.Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().ToList();
        }

        public static int GetGlobalAliveDiverCount()
        {
            GameComponent_DiverRespawnManager manager = Current.Game?.GetComponent<GameComponent_DiverRespawnManager>();
            return manager?.GetTrackedAliveCount() ?? 0;
        }

        public static int GetGlobalOccupiedDiverSlots()
        {
            GameComponent_DiverRespawnManager manager = Current.Game?.GetComponent<GameComponent_DiverRespawnManager>();
            return manager?.GetOccupiedDiverSlotCount() ?? 0;
        }

        public static void NotifyDiverSpawned(Pawn pawn, IntVec3 spawnCell, Map map, int reservedSlotIndex = -1)
        {
            GameComponent_DiverRespawnManager manager = Current.Game?.GetComponent<GameComponent_DiverRespawnManager>();
            manager?.OnDiverSpawnedByPod(pawn, spawnCell, map, reservedSlotIndex);
        }

        public static void NotifyDiverDeath(Pawn pawn)
        {
            if (!IsGameReadyForDiverStateOps())
            {
                return;
            }

            GameComponent_DiverRespawnManager manager = Current.Game?.GetComponent<GameComponent_DiverRespawnManager>();
            manager?.OnDiverDeathBroadcast(pawn);
        }

        private static bool IsGameReadyForDiverStateOps()
        {
            return Current.ProgramState == ProgramState.Playing
                && Current.Game != null
                && Scribe.mode != LoadSaveMode.LoadingVars
                && Scribe.mode != LoadSaveMode.ResolvingCrossRefs
                && Scribe.mode != LoadSaveMode.PostLoadInit;
        }

        public static float GetPlayerMapStoredEnergy(Map map)
        {
            if (map?.powerNetManager == null)
            {
                return 0f;
            }

            float total = 0f;
            foreach (PowerNet net in map.powerNetManager.AllNetsListForReading)
            {
                foreach (CompPowerBattery battery in net.batteryComps)
                {
                    if (battery?.parent?.Faction == Faction.OfPlayer)
                    {
                        total += battery.StoredEnergy;
                    }
                }
            }

            return total;
        }

        public static bool TryConsumePlayerMapEnergy(Map map, float amount)
        {
            if (amount <= 0f)
            {
                return true;
            }

            if (map?.powerNetManager == null)
            {
                return false;
            }

            List<CompPowerBattery> batteries = new List<CompPowerBattery>();
            foreach (PowerNet net in map.powerNetManager.AllNetsListForReading)
            {
                foreach (CompPowerBattery battery in net.batteryComps)
                {
                    if (battery?.parent?.Faction == Faction.OfPlayer && battery.StoredEnergy > 0f)
                    {
                        batteries.Add(battery);
                    }
                }
            }

            float available = batteries.Sum(x => x.StoredEnergy);
            if (available + 0.001f < amount)
            {
                return false;
            }

            float remaining = amount;
            foreach (CompPowerBattery battery in batteries.OrderByDescending(x => x.StoredEnergy))
            {
                if (remaining <= 0f)
                {
                    break;
                }

                float draw = Mathf.Min(remaining, battery.StoredEnergy);
                battery.DrawPower(draw);
                remaining -= draw;
            }

            return remaining <= 0.001f;
        }

        private void OnDiverSpawnedByPod(Pawn pawn, IntVec3 spawnCell, Map map, int reservedSlotIndex)
        {
            if (!IsPlayerControlledAliveDiver(pawn))
            {
                return;
            }

            EnsureSlotListsInitialized();
            PruneTrackedDivers();
            int slotIndex = reservedSlotIndex >= 0 && reservedSlotIndex < ReservedSlotCount
                ? reservedSlotIndex
                : GetFirstAvailableSlotIndex();

            if (slotIndex < 0)
            {
                slotIndex = 0;
            }

            trackedDivers[slotIndex] = pawn;
            DiverSlotPersistentData data = Slot(slotIndex);
            if (!data.nickname.NullOrEmpty())
            {
                pawn.Name = new NameSingle(data.nickname);
            }
            else if (pawn.Name != null)
            {
                data.nickname = pawn.Name.ToStringShort;
            }

            ApplySlotToPawn(slotIndex, pawn);

            if (map != null)
            {
                preferredMapUniqueId = map.uniqueID;
            }

            pendingDiverCount = Mathf.Max(0, pendingDiverCount - 1);
        }

        private Map ResolveRespawnMap()
        {
            if (preferredMapUniqueId >= 0)
            {
                Map preferred = Find.Maps.FirstOrDefault(m => m.uniqueID == preferredMapUniqueId);
                if (preferred != null)
                {
                    return preferred;
                }
            }

            Map homeMap = Find.Maps.FirstOrDefault(m => m.IsPlayerHome);
            if (homeMap != null)
            {
                preferredMapUniqueId = homeMap.uniqueID;
                return homeMap;
            }

            Map first = Find.Maps.FirstOrDefault();
            if (first != null)
            {
                preferredMapUniqueId = first.uniqueID;
            }

            return first;
        }

        private IntVec3 ResolveAutoRespawnCenter(Map map)
        {
            if (map == null)
            {
                return IntVec3.Invalid;
            }

            if (lastDeathMapUniqueId == map.uniqueID && lastDeathCell.IsValid && lastDeathCell.InBounds(map))
            {
                if (CellFinder.TryFindRandomCellNear(lastDeathCell, map, 6, c => c.Standable(map), out IntVec3 aroundLastDeath))
                {
                    return aroundLastDeath;
                }

                return lastDeathCell;
            }

            // Fallback when no death location is recorded.
            if (CellFinder.TryFindRandomCellNear(map.Center, map, 35, c => c.Standable(map) && !c.Fogged(map), out IntVec3 randomFallback))
            {
                return randomFallback;
            }

            Building anchor = map.listerBuildings?.allBuildingsColonist?.FirstOrDefault(b => b.TryGetComp<CompAuroraCaller>() != null);
            return anchor?.Position ?? map.Center;
        }

        private int LaunchDiverPods(Map map, ThingDef podDef, IntVec3 center, int requestedCount, bool useRandomizedCenter)
        {
            if (map == null || podDef == null || requestedCount <= 0)
            {
                return 0;
            }

            EnsureSlotListsInitialized();
            int remainingSlots = ReservedSlotCount - GetOccupiedDiverSlotCount();
            int spawnTarget = Mathf.Min(requestedCount, remainingSlots);
            if (spawnTarget <= 0)
            {
                return 0;
            }

            List<int> slotIndices = GetAvailableSlotIndices().Take(spawnTarget).ToList();
            if (slotIndices.Count == 0)
            {
                return 0;
            }

            IntVec3 actualCenter = center;
            if (useRandomizedCenter && center.IsValid && center.InBounds(map))
            {
                CellFinder.TryFindRandomCellNear(center, map, 5, c => c.Standable(map), out actualCenter);
            }

            List<IntVec3> cells = FindClusterCells(map, actualCenter, slotIndices.Count);
            if (cells.Count == 0)
            {
                return 0;
            }

            List<string> randomNames = BuildDiverNames(cells.Count);
            int launched = 0;
            for (int i = 0; i < cells.Count; i++)
            {
                if (!TryConsumePlayerMapEnergy(map, EnergyPerDiver))
                {
                    break;
                }

                Thing thing = ThingMaker.MakeThing(podDef);
                if (!(thing is Skyfaller_AtmosphericPlasmaPod pod))
                {
                    break;
                }

                pod.spawnPawnDefName = DiverDefName;
                pod.spawnPawnCount = 1;
                pod.spawnAsPlayerFaction = true;
                int slotIndex = slotIndices[i];
                string slotName = Slot(slotIndex).nickname;
                pod.forcedPawnName = !slotName.NullOrEmpty() ? slotName : (i < randomNames.Count ? randomNames[i] : null);
                pod.forcedSlotIndex = slotIndex;
                pod.extraTicksToImpactStagger = i * DiverPodImpactStaggerTicks;
                GenSpawn.Spawn(pod, cells[i], map, WipeMode.Vanish);
                pendingDiverCount++;
                launched++;
            }

            return launched;
        }

        private List<IntVec3> FindClusterCells(Map map, IntVec3 center, int count)
        {
            List<IntVec3> result = new List<IntVec3>();
            if (map == null || count <= 0)
            {
                return result;
            }

            IntVec3 anchor = center.IsValid && center.InBounds(map) ? center : map.Center;
            // Prefer offsets around 2-3 cells from anchor.
            List<IntVec3> candidates = new List<IntVec3>();
            int maxCells = GenRadial.NumCellsInRadius(8.2f);
            for (int i = 0; i < maxCells; i++)
            {
                IntVec3 c = anchor + GenRadial.RadialPattern[i];
                if (!c.InBounds(map) || !c.Standable(map))
                {
                    continue;
                }

                float dist = (c - anchor).LengthHorizontal;
                if (dist >= 1.9f && dist <= 8.0f)
                {
                    candidates.Add(c);
                }
            }

            // Shuffle candidates to avoid "cluster as close as possible" behavior.
            for (int i = 0; i < candidates.Count; i++)
            {
                int pick = Rand.Range(i, candidates.Count);
                IntVec3 tmp = candidates[i];
                candidates[i] = candidates[pick];
                candidates[pick] = tmp;
            }

            for (int i = 0; i < candidates.Count && result.Count < count; i++)
            {
                IntVec3 candidate = candidates[i];
                if (HasEnoughSpacing(candidate, result))
                {
                    result.Add(candidate);
                }
            }

            while (result.Count < count)
            {
                if (!CellFinder.TryFindRandomCellNear(anchor, map, 12, c =>
                    c.Standable(map)
                    && !result.Contains(c)
                    && (c - anchor).LengthHorizontal >= 1.9f
                    && (c - anchor).LengthHorizontal <= 8.0f
                    && HasEnoughSpacing(c, result), out IntVec3 fallback))
                {
                    break;
                }

                result.Add(fallback);
            }

            return result;
        }

        private static bool HasEnoughSpacing(IntVec3 candidate, List<IntVec3> existing)
        {
            for (int i = 0; i < existing.Count; i++)
            {
                IntVec3 placed = existing[i];
                if (Mathf.Abs(candidate.x - placed.x) < 3)
                {
                    return false;
                }

                if (Mathf.Abs(candidate.z - placed.z) < 2)
                {
                    return false;
                }

                if ((candidate - placed).LengthHorizontal < 3f)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsPlayerControlledAliveDiver(Pawn pawn)
        {
            return pawn != null
                && !pawn.DestroyedOrNull()
                && !pawn.Dead
                && pawn.Spawned
                && pawn.Faction != null
                && pawn.Faction == Faction.OfPlayer
                && pawn.def?.defName == DiverDefName;
        }

        private void PruneTrackedDivers()
        {
            if (!IsGameReadyForDiverStateOps())
            {
                return;
            }

            EnsureSlotListsInitialized();

            for (int i = 0; i < ReservedSlotCount; i++)
            {
                Pawn pawn = trackedDivers[i];
                if (IsPlayerControlledAliveDiver(pawn))
                {
                    continue;
                }

                if (pawn == null || pawn.DestroyedOrNull())
                {
                    trackedDivers[i] = null;
                    continue;
                }

                if (pawn.Dead)
                {
                    UploadSlotFromPawn(i, pawn);
                    UpdateLastDeathLocationFromPawn(pawn);
                    trackedDivers[i] = null;
                }
            }
        }

        private void OnDiverDeathBroadcast(Pawn pawn)
        {
            if (!IsGameReadyForDiverStateOps() || pawn == null || pawn.DestroyedOrNull())
            {
                return;
            }

            if (pawn.def?.defName != DiverDefName || pawn.Faction != Faction.OfPlayer)
            {
                return;
            }

            UpdateLastDeathLocationFromPawn(pawn);
            if (trackedDivers == null)
            {
                EnsureSlotListsInitialized();
            }

            int slotIndex = trackedDivers.IndexOf(pawn);
            if (slotIndex >= 0)
            {
                UploadSlotFromPawn(slotIndex, pawn);
                trackedDivers[slotIndex] = null;
            }
        }

        private void UpdateLastDeathLocationFromPawn(Pawn pawn)
        {
            if (pawn == null)
            {
                return;
            }

            Map map = pawn.Corpse?.Map ?? pawn.Map;
            IntVec3 pos = pawn.Corpse?.Position ?? pawn.Position;
            if (map != null && pos.IsValid && pos.InBounds(map))
            {
                lastDeathMapUniqueId = map.uniqueID;
                lastDeathCell = pos;
            }
        }

        private void RebuildTrackedDiversFromMaps()
        {
            EnsureSlotListsInitialized();
            for (int i = 0; i < ReservedSlotCount; i++)
            {
                trackedDivers[i] = null;
            }

            if (Find.Maps == null)
            {
                return;
            }

            int fillIndex = 0;
            for (int i = 0; i < Find.Maps.Count && fillIndex < ReservedSlotCount; i++)
            {
                Map map = Find.Maps[i];
                IReadOnlyList<Pawn> pawns = map.mapPawns?.AllPawnsSpawned;
                if (pawns == null)
                {
                    continue;
                }

                for (int j = 0; j < pawns.Count && fillIndex < ReservedSlotCount; j++)
                {
                    Pawn pawn = pawns[j];
                    if (IsPlayerControlledAliveDiver(pawn))
                    {
                        trackedDivers[fillIndex] = pawn;
                        if (Slot(fillIndex).nickname.NullOrEmpty() && pawn.Name != null)
                        {
                            Slot(fillIndex).nickname = pawn.Name.ToStringShort;
                        }

                        fillIndex++;
                    }
                }
            }
        }

        private void InitializeSlots()
        {
            if (slots == null || slots.Length != ReservedSlotCount)
            {
                slots = new DiverSlotPersistentData[ReservedSlotCount];
            }

            for (int i = 0; i < ReservedSlotCount; i++)
            {
                if (slots[i] == null)
                {
                    slots[i] = new DiverSlotPersistentData();
                }

                slots[i].EnsureInitialized();
            }
        }

        private void EnsureSlotListsInitialized()
        {
            InitializeSlots();

            if (trackedDivers == null)
            {
                trackedDivers = new List<Pawn>();
            }

            while (trackedDivers.Count < ReservedSlotCount)
            {
                trackedDivers.Add(null);
            }
        }

        private void TryRunDeferredSlotSetup()
        {
            if (!needsDeferredSlotSetup || !IsGameReadyForDiverStateOps())
            {
                return;
            }

            InitializeSlots();
            MigrateLegacySlotFields();
            MigrateLegacyLoadedSlotData();
            needsTrackedRebuild = true;
            needsDeferredSlotSetup = false;
        }

        // One-shot after load: init RimTalk reflection, rebuild tracking if needed, ensure VocalLink + slot persona.
        private void TryRunPostLoadRimTalkSync()
        {
            if (!needsPostLoadRimTalkSync || !IsGameReadyForDiverStateOps() || !diverSystemEnabled)
            {
                return;
            }

            needsPostLoadRimTalkSync = false;
            DiverRimTalkUtility.EnsureInitialized();
            DiverRimTalkPersonaPersistence.TryEnsurePatchesApplied(NCL.NCL_Mod.harmony);
            if (!DiverRimTalkUtility.IsRimTalkAvailable)
            {
                if (DiverRimTalkUtility.DebugLoggingEnabled)
                {
                    Log.Message("[NCL_DiverRimTalk] [PostLoadSync] skipped: RimTalk unavailable.");
                }
                return;
            }

            if (needsTrackedRebuild)
            {
                RebuildTrackedDiversFromMaps();
                needsTrackedRebuild = false;
            }
            else
            {
                PruneTrackedDivers();
            }

            if (DiverRimTalkUtility.DebugLoggingEnabled)
            {
                Log.Message("[NCL_DiverRimTalk] [PostLoadSync] running EnsureRimTalkForAllTrackedDivers.");
            }

            EnsureRimTalkForAllTrackedDivers();
        }

        private void MigrateLegacyLoadedSlotData()
        {
            if (legacyLoadedSlotData == null || legacyLoadedSlotData.Count == 0)
            {
                legacyLoadedSlotData = null;
                return;
            }

            int count = Mathf.Min(ReservedSlotCount, legacyLoadedSlotData.Count);
            for (int i = 0; i < count; i++)
            {
                DiverSlotPersistentData legacy = legacyLoadedSlotData[i];
                if (legacy == null)
                {
                    continue;
                }

                legacy.EnsureInitialized();
                DiverSlotPersistentData target = Slot(i);
                if (target.nickname.NullOrEmpty() && !legacy.nickname.NullOrEmpty())
                {
                    target.nickname = legacy.nickname;
                }

                CopySlotFieldsIfEmpty(target, legacy);
            }

            legacyLoadedSlotData = null;
        }

        private static void CopySlotFieldsIfEmpty(DiverSlotPersistentData target, DiverSlotPersistentData source)
        {
            if (target.cloakSetId == DiverCloakCatalog.DefaultSetId && !source.cloakSetId.NullOrEmpty())
            {
                target.cloakSetId = source.cloakSetId;
            }

            target.cloakColor = source.cloakColor;
            target.cloakVisible = source.cloakVisible;

            for (int j = 0; j < DiverSlotPersistentData.LoadoutCount; j++)
            {
                if (target.loadoutDefNames[j].NullOrEmpty() && !source.loadoutDefNames[j].NullOrEmpty())
                {
                    target.loadoutDefNames[j] = source.loadoutDefNames[j];
                }

                if (target.stratagemCooldownUntilTick[j] == 0 && source.stratagemCooldownUntilTick[j] > 0)
                {
                    target.stratagemCooldownUntilTick[j] = source.stratagemCooldownUntilTick[j];
                }
            }

            if ((target.recordDefNames == null || target.recordDefNames.Count == 0)
                && source.recordDefNames != null
                && source.recordDefNames.Count > 0)
            {
                Dictionary<string, float> snapshot = new Dictionary<string, float>();
                int recordCount = Mathf.Min(source.recordDefNames.Count, source.recordValues?.Count ?? 0);
                for (int r = 0; r < recordCount; r++)
                {
                    snapshot[source.recordDefNames[r]] = source.recordValues[r];
                }

                target.SetRecordSnapshot(snapshot);
            }
        }

        private DiverSlotPersistentData Slot(int slotIndex)
        {
            EnsureSlotListsInitialized();
            return slots[slotIndex];
        }

        private void MigrateLegacySlotFields()
        {
            bool hasLegacyNicknames = slotNicknames != null && slotNicknames.Count > 0;
            bool hasLegacyLoadouts = slotLoadoutDefNames != null && slotLoadoutDefNames.Count > 0;
            if (!hasLegacyNicknames && !hasLegacyLoadouts)
            {
                return;
            }

            EnsureSlotListsInitialized();
            for (int i = 0; i < ReservedSlotCount; i++)
            {
                DiverSlotPersistentData data = Slot(i);
                if (hasLegacyNicknames && i < slotNicknames.Count && !slotNicknames[i].NullOrEmpty())
                {
                    data.nickname = slotNicknames[i];
                }

                if (hasLegacyLoadouts)
                {
                    for (int j = 0; j < 4; j++)
                    {
                        int flat = i * 4 + j;
                        if (flat < slotLoadoutDefNames.Count && !slotLoadoutDefNames[flat].NullOrEmpty())
                        {
                            data.loadoutDefNames[j] = slotLoadoutDefNames[flat];
                        }
                    }
                }
            }

            slotNicknames = null;
            slotLoadoutDefNames = null;
        }

        private void ApplySlotToPawn(int slotIndex, Pawn pawn)
        {
            if (!IsGameReadyForDiverStateOps() || pawn == null || pawn.DestroyedOrNull()
                || slotIndex < 0 || slotIndex >= ReservedSlotCount)
            {
                return;
            }

            DiverSlotPersistentData data = Slot(slotIndex);
            CompDiverCloak comp = CompDiverCloak.Get(pawn);
            if (comp != null)
            {
                comp.SetCloakSet(data.cloakSetId);
                comp.SetCloakColor(data.cloakColor);
                comp.SetCloakVisible(data.cloakVisible);
            }

            DiverRecordsUtility.ApplySlotRecordsToPawn(data, pawn);
            DiverRimTalkUtility.ApplySlotRimTalkToPawn(pawn, data, $"ApplySlot slot={slotIndex}");
        }

        private void EnsureRimTalkForAllTrackedDivers()
        {
            if (!IsGameReadyForDiverStateOps())
            {
                return;
            }

            EnsureSlotListsInitialized();
            for (int i = 0; i < ReservedSlotCount; i++)
            {
                Pawn pawn = trackedDivers[i];
                if (!IsPlayerControlledAliveDiver(pawn))
                {
                    continue;
                }

                DiverSlotPersistentData data = Slot(i);
                if (data.rimTalkPersonality.NullOrEmpty())
                {
                    DiverRimTalkUtility.CapturePersonaFromPawn(pawn, data, $"MigrateSlot slot={i}");
                }

                DiverRimTalkUtility.ApplySlotRimTalkToPawn(pawn, data, $"PostLoadEnsure slot={i}");
            }
        }

        private void UploadSlotFromPawn(int slotIndex, Pawn pawn)
        {
            if (!IsGameReadyForDiverStateOps() || pawn == null || pawn.DestroyedOrNull()
                || slotIndex < 0 || slotIndex >= ReservedSlotCount)
            {
                return;
            }

            DiverSlotPersistentData data = Slot(slotIndex);
            if (pawn.Name != null)
            {
                data.nickname = pawn.Name.ToStringShort;
            }

            CompDiverCloak comp = CompDiverCloak.Get(pawn);
            if (comp != null)
            {
                data.cloakSetId = comp.CloakSetId;
                data.cloakColor = comp.CloakColor;
                data.cloakVisible = comp.CloakVisible;
            }

            DiverRecordsUtility.SnapshotPawnRecordsToSlot(pawn, data);
            DiverRimTalkUtility.CapturePersonaFromPawn(pawn, data, $"UploadSlot slot={slotIndex}");
        }

        private IEnumerable<int> GetAvailableSlotIndices()
        {
            EnsureSlotListsInitialized();
            for (int i = 0; i < ReservedSlotCount; i++)
            {
                if (trackedDivers[i] == null)
                {
                    yield return i;
                }
            }
        }

        private int GetFirstAvailableSlotIndex()
        {
            foreach (int idx in GetAvailableSlotIndices())
            {
                return idx;
            }

            return -1;
        }

        private static List<string> BuildDiverNames(int count)
        {
            List<string> result = new List<string>();
            if (count <= 0)
            {
                return result;
            }

            string playerName = GetPlayerDisplayName();
            List<string> friendNames = TryGetSteamFriendDisplayNames();
            if (friendNames.Count == 0)
            {
                friendNames.Add(playerName);
            }

            for (int i = 0; i < count; i++)
            {
                result.Add(friendNames[Rand.Range(0, friendNames.Count)]);
            }

            int forcedIndex = Rand.Range(0, result.Count);
            result[forcedIndex] = playerName;
            return result;
        }

        private static string GetPlayerDisplayName()
        {
            string steamPersona = SteamUtility.SteamPersonaName;
            if (!steamPersona.NullOrEmpty() && steamPersona != "???")
            {
                return steamPersona;
            }

            return "Player";
        }

        private static List<string> TryGetSteamFriendDisplayNames()
        {
            List<string> names = new List<string>();
            try
            {
                Type steamManagerType = AccessTools.TypeByName("Verse.Steam.SteamManager");
                PropertyInfo initializedProp = steamManagerType?.GetProperty("Initialized", BindingFlags.Public | BindingFlags.Static);
                bool initialized = initializedProp != null && initializedProp.GetValue(null) is bool b && b;
                if (!initialized)
                {
                    return names;
                }

                Type steamFriendsType = AccessTools.TypeByName("Steamworks.SteamFriends");
                Type friendFlagsType = AccessTools.TypeByName("Steamworks.EFriendFlags");
                if (steamFriendsType == null || friendFlagsType == null)
                {
                    return names;
                }

                object friendFlag = ResolveFriendFlag(friendFlagsType);
                if (friendFlag == null)
                {
                    return names;
                }

                MethodInfo getFriendCount = FindStaticMethod(steamFriendsType, "GetFriendCount", 1);
                MethodInfo getFriendByIndex = FindStaticMethod(steamFriendsType, "GetFriendByIndex", 2);
                MethodInfo getFriendPersonaName = FindStaticMethod(steamFriendsType, "GetFriendPersonaName", 1);
                if (getFriendCount == null || getFriendByIndex == null || getFriendPersonaName == null)
                {
                    return names;
                }

                object countObj = getFriendCount.Invoke(null, new[] { friendFlag });
                int friendCount = countObj is int count ? count : 0;
                for (int i = 0; i < friendCount; i++)
                {
                    object steamId = getFriendByIndex.Invoke(null, new object[] { i, friendFlag });
                    if (steamId == null)
                    {
                        continue;
                    }

                    object nameObj = getFriendPersonaName.Invoke(null, new[] { steamId });
                    if (nameObj is string name && !name.NullOrEmpty())
                    {
                        names.Add(name);
                    }
                }
            }
            catch (Exception)
            {
                return names;
            }

            return names;
        }

        private static MethodInfo FindStaticMethod(Type type, string methodName, int parameterCount)
        {
            return type?
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == methodName && m.GetParameters().Length == parameterCount);
        }

        private static object ResolveFriendFlag(Type friendFlagsType)
        {
            string[] preferredNames =
            {
                "k_EFriendFlagImmediate",
                "Immediate",
                "k_EFriendFlagAll",
                "All"
            };

            for (int i = 0; i < preferredNames.Length; i++)
            {
                string name = preferredNames[i];
                if (Enum.GetNames(friendFlagsType).Contains(name))
                {
                    return Enum.Parse(friendFlagsType, name);
                }
            }

            Array allValues = Enum.GetValues(friendFlagsType);
            return allValues.Length > 0 ? allValues.GetValue(0) : null;
        }
    }

    public class DiverSlotViewData
    {
        public int SlotIndex { get; }
        public Pawn Pawn { get; }
        public string Nickname { get; }
        public bool IsAlive { get; }

        public DiverSlotViewData(int slotIndex, Pawn pawn, string nickname, bool isAlive)
        {
            SlotIndex = slotIndex;
            Pawn = pawn;
            Nickname = nickname;
            IsAlive = isAlive;
        }
    }

}
