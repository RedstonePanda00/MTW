using System.Collections.Generic;
using NyarsModPackTwo;
using RimWorld;
using Verse;

namespace NCL
{
    // Unified Iron Rain threat cache: incremental dictionary sync (add/remove), no full replace per scan.
    public class MapComponent_IronRainTargeting : MapComponent
    {
        public const int WeakScanIntervalTicks = 240;
        public const int MissileAssignIntervalTicks = 30;

        // Block silo launch when in-flight missiles exceed this multiple of assignable candidates.
        public const int LaunchThrottleMissilesPerCandidate = 2;

        private readonly List<Bullet_TracingEnemies> activeMissiles = new List<Bullet_TracingEnemies>();
        private readonly HashSet<Building_MissileSilo> registeredSilos = new HashSet<Building_MissileSilo>();
        private readonly Dictionary<int, Bullet_TracingEnemies> lockedProjectiles = new Dictionary<int, Bullet_TracingEnemies>();

        private readonly Dictionary<int, Thing> airProjectilesById = new Dictionary<int, Thing>();
        private readonly Dictionary<int, Pawn> pawnsById = new Dictionary<int, Pawn>();
        private readonly Dictionary<Faction, IronRainThreatSnapshot> threatByFaction = new Dictionary<Faction, IronRainThreatSnapshot>();

        private readonly HashSet<int> scratchSeenIds = new HashSet<int>();
        private readonly HashSet<Faction> scratchFactions = new HashSet<Faction>();
        private readonly List<int> scratchRemoveIds = new List<int>();

        private bool cacheInitialized;

        private struct IronRainThreatSnapshot
        {
            public bool HasHostileAir;
            public bool HasHostilePawn;
        }

        public MapComponent_IronRainTargeting(Map map)
            : base(map)
        {
        }

        public static MapComponent_IronRainTargeting Get(Map map)
        {
            return map?.GetComponent<MapComponent_IronRainTargeting>();
        }

        public static MapComponent_IronRainTargeting GetOrCreate(Map map)
        {
            if (map == null)
            {
                return null;
            }

            MapComponent_IronRainTargeting component = map.GetComponent<MapComponent_IronRainTargeting>();
            if (component == null)
            {
                component = new MapComponent_IronRainTargeting(map);
                map.components.Add(component);
            }

            return component;
        }

        public void RegisterSilo(Building_MissileSilo silo)
        {
            if (silo == null || silo.Destroyed)
            {
                return;
            }

            registeredSilos.Add(silo);
        }

        public void UnregisterSilo(Building_MissileSilo silo)
        {
            if (silo == null)
            {
                return;
            }

            registeredSilos.Remove(silo);
        }

        public void Register(Bullet_TracingEnemies missile)
        {
            if (missile == null || missile.Destroyed)
            {
                return;
            }

            if (!activeMissiles.Contains(missile))
            {
                activeMissiles.Add(missile);
            }
        }

        public void Unregister(Bullet_TracingEnemies missile)
        {
            if (missile == null)
            {
                return;
            }

            activeMissiles.Remove(missile);
            ReleaseProjectileLocksHeldBy(missile);
        }

        public void ReleaseLocksFor(Bullet_TracingEnemies missile)
        {
            ReleaseProjectileLocksHeldBy(missile);
        }

        // Remove a target that failed validation so later lookups skip it (O(1) ContainsKey).
        public void NotifyCachedTargetInvalid(Thing target)
        {
            if (target == null)
            {
                return;
            }

            if (target is Pawn pawn)
            {
                RemovePawnFromCache(pawn.thingIDNumber);
            }
            else
            {
                RemoveAirProjectileFromCache(target.thingIDNumber);
            }
        }

        public bool HasEnemyTargetsFor(Building_MissileSilo silo)
        {
            if (silo == null || !silo.Spawned || silo.Map != map || silo.Faction == null)
            {
                return false;
            }

            EnsureCacheInitialized();

            if (!threatByFaction.TryGetValue(silo.Faction, out IronRainThreatSnapshot snapshot))
            {
                return false;
            }

            switch (silo.launchCondition)
            {
                case Building_MissileSilo.LaunchCondition.HighAngleProjectilesOnly:
                    return snapshot.HasHostileAir;
                case Building_MissileSilo.LaunchCondition.AnyEnemyThreats:
                    return snapshot.HasHostileAir || snapshot.HasHostilePawn;
                default:
                    return false;
            }
        }

        public int GetAssignableCandidateCount(Building_MissileSilo silo)
        {
            if (silo?.Faction == null)
            {
                return 0;
            }

            EnsureCacheInitialized();
            return CountAssignableCandidatesForFaction(silo.Faction, silo.launchCondition);
        }

        public bool CanSiloLaunchMoreMissiles(Building_MissileSilo silo)
        {
            if (silo == null)
            {
                return false;
            }

            int candidateCount = GetAssignableCandidateCount(silo);
            if (candidateCount <= 0)
            {
                return false;
            }

            int maxInFlight = candidateCount * LaunchThrottleMissilesPerCandidate;
            return activeMissiles.Count < maxInFlight;
        }

        private int CountAssignableCandidatesForFaction(
            Faction siloFaction,
            Building_MissileSilo.LaunchCondition launchCondition)
        {
            int count = 0;

            foreach (Thing thing in airProjectilesById.Values)
            {
                if (!IsHostileAirProjectileForFaction(thing, siloFaction))
                {
                    continue;
                }

                count++;
            }

            if (launchCondition == Building_MissileSilo.LaunchCondition.AnyEnemyThreats)
            {
                foreach (Pawn pawn in pawnsById.Values)
                {
                    if (IsHostilePawnForFaction(pawn, siloFaction))
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private bool IsHostileAirProjectileForFaction(Thing thing, Faction siloFaction)
        {
            if (!IsValidAirProjectileCandidate(thing) || thing.Map != map)
            {
                return false;
            }

            Projectile projectile = thing as Projectile;
            return siloFaction.HostileTo(projectile.Launcher.Faction);
        }

        private bool IsHostilePawnForFaction(Pawn pawn, Faction siloFaction)
        {
            return IsValidPawnCandidate(pawn) && siloFaction.HostileTo(pawn.Faction);
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();

            if (activeMissiles.Count == 0 && registeredSilos.Count == 0)
            {
                return;
            }

            PruneInvalidMissiles();
            PruneInvalidSilos();

            int tick = Find.TickManager.TicksGame;

            if (tick % WeakScanIntervalTicks == 0)
            {
                PerformWeakScan();
            }
            else if (tick % MissileAssignIntervalTicks == 0)
            {
                PruneInvalidCacheEntries();
            }

            if (activeMissiles.Count > 0 && tick % MissileAssignIntervalTicks == 0)
            {
                EnsureCacheInitialized();
                UpdateAllMissileTargetsFromCache();
            }
        }

        private void EnsureCacheInitialized()
        {
            if (!cacheInitialized)
            {
                PerformWeakScan();
            }
        }

        // Incremental sync: add new map entities, delete ids no longer on map (dict is never Clear()).
        private void PerformWeakScan()
        {
            SyncAirProjectilesFromMap();
            SyncPawnsFromMap();
            RebuildThreatFlagsFromCache();
            cacheInitialized = true;
        }

        private void SyncAirProjectilesFromMap()
        {
            scratchSeenIds.Clear();

            List<Thing> projectiles = map.listerThings.ThingsInGroup(ThingRequestGroup.Projectile);
            for (int i = 0; i < projectiles.Count; i++)
            {
                Thing thing = projectiles[i];
                if (!IsValidAirProjectileCandidate(thing))
                {
                    continue;
                }

                int id = thing.thingIDNumber;
                scratchSeenIds.Add(id);

                if (airProjectilesById.ContainsKey(id))
                {
                    airProjectilesById[id] = thing;
                }
                else
                {
                    airProjectilesById.Add(id, thing);
                }
            }

            RemoveAirProjectileIdsNotIn(scratchSeenIds);
        }

        private void SyncPawnsFromMap()
        {
            scratchSeenIds.Clear();

            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
            {
                if (!IsValidPawnCandidate(pawn))
                {
                    continue;
                }

                int id = pawn.thingIDNumber;
                scratchSeenIds.Add(id);

                if (pawnsById.ContainsKey(id))
                {
                    pawnsById[id] = pawn;
                }
                else
                {
                    pawnsById.Add(id, pawn);
                }
            }

            RemovePawnIdsNotIn(scratchSeenIds);
        }

        private void RemoveAirProjectileIdsNotIn(HashSet<int> seenIds)
        {
            scratchRemoveIds.Clear();
            foreach (KeyValuePair<int, Thing> pair in airProjectilesById)
            {
                if (!seenIds.Contains(pair.Key))
                {
                    scratchRemoveIds.Add(pair.Key);
                }
            }

            for (int i = 0; i < scratchRemoveIds.Count; i++)
            {
                RemoveAirProjectileFromCache(scratchRemoveIds[i]);
            }
        }

        private void RemovePawnIdsNotIn(HashSet<int> seenIds)
        {
            scratchRemoveIds.Clear();
            foreach (KeyValuePair<int, Pawn> pair in pawnsById)
            {
                if (!seenIds.Contains(pair.Key))
                {
                    scratchRemoveIds.Add(pair.Key);
                }
            }

            for (int i = 0; i < scratchRemoveIds.Count; i++)
            {
                RemovePawnFromCache(scratchRemoveIds[i]);
            }
        }

        private void PruneInvalidCacheEntries()
        {
            bool cacheChanged = false;

            scratchRemoveIds.Clear();
            foreach (KeyValuePair<int, Thing> pair in airProjectilesById)
            {
                if (!IsValidAirProjectileCandidate(pair.Value))
                {
                    scratchRemoveIds.Add(pair.Key);
                }
            }

            for (int i = 0; i < scratchRemoveIds.Count; i++)
            {
                RemoveAirProjectileFromCache(scratchRemoveIds[i]);
                cacheChanged = true;
            }

            scratchRemoveIds.Clear();
            foreach (KeyValuePair<int, Pawn> pair in pawnsById)
            {
                if (!IsValidPawnCandidate(pair.Value))
                {
                    scratchRemoveIds.Add(pair.Key);
                }
            }

            for (int i = 0; i < scratchRemoveIds.Count; i++)
            {
                RemovePawnFromCache(scratchRemoveIds[i]);
                cacheChanged = true;
            }

            if (cacheChanged)
            {
                RebuildThreatFlagsFromCache();
            }
        }

        private void RemoveAirProjectileFromCache(int thingId)
        {
            airProjectilesById.Remove(thingId);
            lockedProjectiles.Remove(thingId);
        }

        private void RemovePawnFromCache(int thingId)
        {
            pawnsById.Remove(thingId);
        }

        private static bool IsValidAirProjectileCandidate(Thing thing)
        {
            if (thing == null || thing.Destroyed || !thing.Spawned)
            {
                return false;
            }

            Projectile projectile = thing as Projectile;
            if (projectile?.def?.projectile == null || !projectile.def.projectile.flyOverhead)
            {
                return false;
            }

            return projectile.Launcher?.Faction != null;
        }

        private bool IsValidPawnCandidate(Pawn pawn)
        {
            if (pawn == null || pawn.Destroyed || !pawn.Spawned || pawn.Map != map)
            {
                return false;
            }

            if (pawn.Downed || pawn.IsPrisoner || pawn.Faction == null)
            {
                return false;
            }

            return true;
        }

        private void RebuildThreatFlagsFromCache()
        {
            threatByFaction.Clear();
            scratchFactions.Clear();

            foreach (Building_MissileSilo silo in registeredSilos)
            {
                if (silo?.Faction != null)
                {
                    scratchFactions.Add(silo.Faction);
                }
            }

            foreach (Faction faction in scratchFactions)
            {
                threatByFaction[faction] = default;
            }

            foreach (Thing thing in airProjectilesById.Values)
            {
                if (!IsValidAirProjectileCandidate(thing))
                {
                    continue;
                }

                Projectile projectile = thing as Projectile;
                Thing projLauncher = projectile?.Launcher;
                if (projLauncher?.Faction == null)
                {
                    continue;
                }

                foreach (Faction faction in scratchFactions)
                {
                    if (!threatByFaction[faction].HasHostileAir && faction.HostileTo(projLauncher.Faction))
                    {
                        IronRainThreatSnapshot snapshot = threatByFaction[faction];
                        snapshot.HasHostileAir = true;
                        threatByFaction[faction] = snapshot;
                    }
                }
            }

            foreach (Pawn pawn in pawnsById.Values)
            {
                if (!IsValidPawnCandidate(pawn))
                {
                    continue;
                }

                foreach (Faction faction in scratchFactions)
                {
                    if (!threatByFaction[faction].HasHostilePawn && faction.HostileTo(pawn.Faction))
                    {
                        IronRainThreatSnapshot snapshot = threatByFaction[faction];
                        snapshot.HasHostilePawn = true;
                        threatByFaction[faction] = snapshot;
                    }
                }
            }
        }

        private void PruneInvalidSilos()
        {
            registeredSilos.RemoveWhere(silo =>
                silo == null || silo.Destroyed || !silo.Spawned || silo.Map != map);
        }

        private void PruneInvalidMissiles()
        {
            for (int i = activeMissiles.Count - 1; i >= 0; i--)
            {
                Bullet_TracingEnemies missile = activeMissiles[i];
                if (missile == null || missile.Destroyed || !missile.Spawned || missile.Map != map)
                {
                    if (missile != null)
                    {
                        ReleaseProjectileLocksHeldBy(missile);
                    }

                    activeMissiles.RemoveAt(i);
                }
            }
        }

        private void UpdateAllMissileTargetsFromCache()
        {
            bool pawnFallbackNeeded = false;

            for (int i = 0; i < activeMissiles.Count; i++)
            {
                Bullet_TracingEnemies missile = activeMissiles[i];
                if (!missile.IsReadyForCentralTargeting)
                {
                    continue;
                }

                if (missile.HasValidCentralTarget)
                {
                    continue;
                }

                ReleaseProjectileLocksHeldBy(missile);

                Thing projectileTarget = FindClosestAvailableProjectile(missile);
                if (projectileTarget != null)
                {
                    LockProjectile(projectileTarget.thingIDNumber, missile);
                    missile.ApplyCentralizedTarget(projectileTarget, Bullet_TracingEnemies.TrackingTargetKind.Projectile);
                    continue;
                }

                pawnFallbackNeeded = true;
            }

            if (!pawnFallbackNeeded)
            {
                return;
            }

            for (int i = 0; i < activeMissiles.Count; i++)
            {
                Bullet_TracingEnemies missile = activeMissiles[i];
                if (!missile.IsReadyForCentralTargeting || missile.HasValidCentralTarget)
                {
                    continue;
                }

                Pawn pawnTarget = FindClosestHostilePawn(missile);
                if (pawnTarget != null)
                {
                    missile.ApplyCentralizedTarget(pawnTarget, Bullet_TracingEnemies.TrackingTargetKind.Pawn);
                }
                else
                {
                    missile.ClearCentralizedTarget();
                }
            }
        }

        private Thing FindClosestAvailableProjectile(Bullet_TracingEnemies missile)
        {
            Thing best = null;
            int bestDist = int.MaxValue;
            Faction launcherFaction = missile.Launcher?.Faction;

            if (launcherFaction == null)
            {
                return null;
            }

            scratchRemoveIds.Clear();

            foreach (KeyValuePair<int, Thing> pair in airProjectilesById)
            {
                Thing thing = pair.Value;
                if (!IsValidAirProjectileCandidate(thing) || thing.Map != map)
                {
                    scratchRemoveIds.Add(pair.Key);
                    continue;
                }

                Projectile projectile = thing as Projectile;
                Thing projLauncher = projectile.Launcher;
                if (projLauncher?.Faction == null || !launcherFaction.HostileTo(projLauncher.Faction))
                {
                    continue;
                }

                int projectileId = pair.Key;
                if (lockedProjectiles.TryGetValue(projectileId, out Bullet_TracingEnemies owner) && owner != missile)
                {
                    continue;
                }

                int dist = (thing.Position - missile.Position).LengthHorizontalSquared;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = thing;
                }
            }

            for (int i = 0; i < scratchRemoveIds.Count; i++)
            {
                RemoveAirProjectileFromCache(scratchRemoveIds[i]);
            }

            if (scratchRemoveIds.Count > 0)
            {
                RebuildThreatFlagsFromCache();
            }

            return best;
        }

        private Pawn FindClosestHostilePawn(Bullet_TracingEnemies missile)
        {
            Pawn best = null;
            int bestDist = int.MaxValue;
            Faction launcherFaction = missile.Launcher?.Faction;

            if (launcherFaction == null)
            {
                return null;
            }

            scratchRemoveIds.Clear();

            foreach (KeyValuePair<int, Pawn> pair in pawnsById)
            {
                Pawn pawn = pair.Value;
                if (!IsValidPawnCandidate(pawn))
                {
                    scratchRemoveIds.Add(pair.Key);
                    continue;
                }

                if (!launcherFaction.HostileTo(pawn.Faction))
                {
                    continue;
                }

                int dist = (pawn.Position - missile.Position).LengthHorizontalSquared;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = pawn;
                }
            }

            for (int i = 0; i < scratchRemoveIds.Count; i++)
            {
                RemovePawnFromCache(scratchRemoveIds[i]);
            }

            if (scratchRemoveIds.Count > 0)
            {
                RebuildThreatFlagsFromCache();
            }

            return best;
        }

        private void LockProjectile(int projectileId, Bullet_TracingEnemies missile)
        {
            lockedProjectiles[projectileId] = missile;
        }

        private void ReleaseProjectileLocksHeldBy(Bullet_TracingEnemies missile)
        {
            scratchRemoveIds.Clear();
            foreach (KeyValuePair<int, Bullet_TracingEnemies> pair in lockedProjectiles)
            {
                if (pair.Value == missile)
                {
                    scratchRemoveIds.Add(pair.Key);
                }
            }

            for (int i = 0; i < scratchRemoveIds.Count; i++)
            {
                lockedProjectiles.Remove(scratchRemoveIds[i]);
            }
        }
    }
}
