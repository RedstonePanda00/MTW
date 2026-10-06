using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace NCL
{
    public static class AssaultTrooperBreachUtility
    {
        private const int CenterCacheTicks = 2500;
        private const float DiagonalCost = 1.41421f;
        // Breaks ties between equally short routes in favour of the one crossing fewer buildings,
        // without ever trading path length for it.
        private const float BlockerTieBreakCost = 0.001f;

        private struct CenterCache
        {
            public int Tick;
            public IntVec3 Cell;
        }

        private static readonly Dictionary<int, CenterCache> CenterByMap = new Dictionary<int, CenterCache>();

        private static float[] gScore;
        private static int[] parentIndex;
        private static int[] openStamp;
        private static int[] closedStamp;
        private static int stamp;
        private static readonly List<KeyValuePair<float, int>> Heap = new List<KeyValuePair<float, int>>();

        private static readonly IntVec3[] Directions =
        {
            new IntVec3(0, 0, 1), new IntVec3(1, 0, 0), new IntVec3(0, 0, -1), new IntVec3(-1, 0, 0),
            new IntVec3(1, 0, 1), new IntVec3(1, 0, -1), new IntVec3(-1, 0, -1), new IntVec3(-1, 0, 1)
        };

        public static bool TryGetColonyCenter(Map map, out IntVec3 center)
        {
            center = IntVec3.Invalid;
            if (map == null)
            {
                return false;
            }

            int now = Find.TickManager.TicksGame;
            if (CenterByMap.TryGetValue(map.uniqueID, out CenterCache cache) && now - cache.Tick < CenterCacheTicks)
            {
                center = cache.Cell;
                return center.IsValid;
            }

            IntVec3 result = IntVec3.Invalid;
            Area_Home home = map.areaManager?.Home;
            if (home != null && home.TrueCount > 0)
            {
                long sumX = 0;
                long sumZ = 0;
                int count = 0;
                foreach (IntVec3 cell in home.ActiveCells)
                {
                    sumX += cell.x;
                    sumZ += cell.z;
                    count++;
                }

                if (count > 0)
                {
                    IntVec3 centroid = new IntVec3((int)(sumX / count), 0, (int)(sumZ / count));
                    result = NearestTerrainPassable(map, centroid);
                }
            }

            CenterByMap[map.uniqueID] = new CenterCache { Tick = now, Cell = result };
            center = result;
            return center.IsValid;
        }

        // Shortest 8-way route that treats every building, natural rock included, as passable. Only
        // impassable terrain and the map edge constrain it.
        public static bool TryFindPathIgnoringBuildings(Map map, IntVec3 start, IntVec3 goal, List<IntVec3> path)
        {
            path.Clear();
            if (!start.InBounds(map) || !goal.InBounds(map))
            {
                return false;
            }

            if (start == goal)
            {
                path.Add(start);
                return true;
            }

            int sizeX = map.Size.x;
            int cellCount = map.cellIndices.NumGridCells;
            EnsureBuffers(cellCount);
            stamp++;
            if (stamp == int.MaxValue)
            {
                System.Array.Clear(openStamp, 0, openStamp.Length);
                System.Array.Clear(closedStamp, 0, closedStamp.Length);
                stamp = 1;
            }

            int startIdx = map.cellIndices.CellToIndex(start);
            int goalIdx = map.cellIndices.CellToIndex(goal);
            Heap.Clear();
            gScore[startIdx] = 0f;
            parentIndex[startIdx] = -1;
            openStamp[startIdx] = stamp;
            HeapPush(Heuristic(start, goal), startIdx);

            bool found = false;
            while (Heap.Count > 0)
            {
                int current = HeapPop();
                if (closedStamp[current] == stamp)
                {
                    continue;
                }

                closedStamp[current] = stamp;
                if (current == goalIdx)
                {
                    found = true;
                    break;
                }

                IntVec3 cell = map.cellIndices.IndexToCell(current);
                for (int d = 0; d < Directions.Length; d++)
                {
                    IntVec3 next = cell + Directions[d];
                    if (!TerrainPassable(map, next))
                    {
                        continue;
                    }

                    bool diagonal = d >= 4;
                    if (diagonal && (!TerrainPassable(map, new IntVec3(next.x, 0, cell.z)) || !TerrainPassable(map, new IntVec3(cell.x, 0, next.z))))
                    {
                        continue;
                    }

                    int nextIdx = next.z * sizeX + next.x;
                    if (closedStamp[nextIdx] == stamp)
                    {
                        continue;
                    }

                    float step = diagonal ? DiagonalCost : 1f;
                    Building edifice = next.GetEdifice(map);
                    if (edifice != null && edifice.def.passability == Traversability.Impassable)
                    {
                        step += BlockerTieBreakCost;
                    }

                    float tentative = gScore[current] + step;
                    if (openStamp[nextIdx] == stamp && tentative >= gScore[nextIdx])
                    {
                        continue;
                    }

                    openStamp[nextIdx] = stamp;
                    gScore[nextIdx] = tentative;
                    parentIndex[nextIdx] = current;
                    HeapPush(tentative + Heuristic(next, goal), nextIdx);
                }
            }

            Heap.Clear();
            if (!found)
            {
                return false;
            }

            for (int idx = goalIdx; idx != -1; idx = parentIndex[idx])
            {
                path.Add(map.cellIndices.IndexToCell(idx));
            }

            path.Reverse();
            return true;
        }

        // Walks the route from the pawn and returns the first building the pawn cannot physically pass.
        // A diagonal step is blocked by either of the two orthogonal cells it squeezes between, the same
        // corner-cutting rule the vanilla pather applies. standIndex is the last clear route cell, which
        // is always adjacent to the blocker.
        public static bool TryFindFirstBlocker(Pawn pawn, List<IntVec3> path, out Building blocker, out int standIndex)
        {
            blocker = null;
            standIndex = -1;
            Map map = pawn.Map;
            for (int i = 1; i < path.Count; i++)
            {
                IntVec3 prev = path[i - 1];
                IntVec3 cell = path[i];
                if (prev.x != cell.x && prev.z != cell.z)
                {
                    Building corner = BlockingBuildingAt(pawn, map, new IntVec3(cell.x, 0, prev.z))
                        ?? BlockingBuildingAt(pawn, map, new IntVec3(prev.x, 0, cell.z));
                    if (corner != null)
                    {
                        blocker = corner;
                        standIndex = i - 1;
                        return true;
                    }
                }

                Building building = BlockingBuildingAt(pawn, map, cell);
                if (building != null)
                {
                    blocker = building;
                    standIndex = i - 1;
                    return true;
                }
            }

            return false;
        }

        public static Building BlockingBuildingAt(Pawn pawn, Map map, IntVec3 cell)
        {
            if (!cell.InBounds(map))
            {
                return null;
            }

            Building edifice = cell.GetEdifice(map);
            if (edifice == null)
            {
                return null;
            }

            if (edifice is Building_Door door)
            {
                return door.CanPhysicallyPass(pawn) ? null : door;
            }

            return edifice.def.passability == Traversability.Impassable ? edifice : null;
        }

        public static bool IsValidStandCell(Pawn pawn, IntVec3 cell, Thing target)
        {
            return cell.IsValid
                && cell.InBounds(pawn.Map)
                && cell.Standable(pawn.Map)
                && cell.AdjacentTo8WayOrInside(target)
                && pawn.CanReach(cell, PathEndMode.OnCell, Danger.Deadly);
        }

        public static bool TryFindAlternateStandCell(Pawn pawn, Thing target, out IntVec3 result)
        {
            result = IntVec3.Invalid;
            float bestDist = float.MaxValue;
            foreach (IntVec3 cell in GenAdj.CellsAdjacent8Way(target))
            {
                if (!IsValidStandCell(pawn, cell, target))
                {
                    continue;
                }

                float dist = cell.DistanceToSquared(pawn.Position);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    result = cell;
                }
            }

            return result.IsValid;
        }

        // A reachable standable cell roughly retreatDistance away from the target, on the side the
        // pawn approached from.
        public static bool TryFindRetreatCell(Pawn pawn, Thing target, float retreatDistance, out IntVec3 result)
        {
            result = IntVec3.Invalid;
            Map map = pawn.Map;
            Vector3 origin = target.TrueCenter();
            Vector3 away = pawn.Position.ToVector3Shifted() - origin;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f)
            {
                away = Vector3.forward;
            }

            Vector3 ideal = origin + away.normalized * retreatDistance;
            float maxDistance = retreatDistance + 3f;
            int cellCount = GenRadial.NumCellsInRadius(maxDistance);
            IntVec3 targetCell = target.Position;
            float bestScore = float.MaxValue;
            for (int i = 0; i < cellCount; i++)
            {
                IntVec3 cell = targetCell + GenRadial.RadialPattern[i];
                if (!cell.InBounds(map))
                {
                    continue;
                }

                float dist = cell.DistanceTo(targetCell);
                if (dist < retreatDistance || !cell.Standable(map))
                {
                    continue;
                }

                float score = (cell.ToVector3Shifted() - ideal).sqrMagnitude;
                if (score >= bestScore || !pawn.CanReach(cell, PathEndMode.OnCell, Danger.Deadly))
                {
                    continue;
                }

                bestScore = score;
                result = cell;
            }

            return result.IsValid;
        }

        public static bool TryFindStandableNear(Pawn pawn, IntVec3 root, float radius, out IntVec3 result)
        {
            result = IntVec3.Invalid;
            Map map = pawn.Map;
            int cellCount = GenRadial.NumCellsInRadius(radius);
            for (int i = 0; i < cellCount; i++)
            {
                IntVec3 cell = root + GenRadial.RadialPattern[i];
                if (cell.InBounds(map) && cell.Standable(map) && pawn.CanReach(cell, PathEndMode.OnCell, Danger.Deadly))
                {
                    result = cell;
                    return true;
                }
            }

            return false;
        }

        private static bool TerrainPassable(Map map, IntVec3 cell)
        {
            if (!cell.InBounds(map))
            {
                return false;
            }

            TerrainDef terrain = map.terrainGrid.TerrainAt(cell);
            return terrain != null && terrain.passability != Traversability.Impassable;
        }

        private static IntVec3 NearestTerrainPassable(Map map, IntVec3 root)
        {
            int cellCount = GenRadial.NumCellsInRadius(30f);
            for (int i = 0; i < cellCount; i++)
            {
                IntVec3 cell = root + GenRadial.RadialPattern[i];
                if (TerrainPassable(map, cell))
                {
                    return cell;
                }
            }

            return IntVec3.Invalid;
        }

        private static float Heuristic(IntVec3 a, IntVec3 b)
        {
            int dx = Mathf.Abs(a.x - b.x);
            int dz = Mathf.Abs(a.z - b.z);
            int min = Mathf.Min(dx, dz);
            int max = Mathf.Max(dx, dz);
            return max + (DiagonalCost - 1f) * min;
        }

        private static void EnsureBuffers(int cellCount)
        {
            if (gScore != null && gScore.Length >= cellCount)
            {
                return;
            }

            gScore = new float[cellCount];
            parentIndex = new int[cellCount];
            openStamp = new int[cellCount];
            closedStamp = new int[cellCount];
            stamp = 0;
        }

        private static void HeapPush(float priority, int value)
        {
            Heap.Add(new KeyValuePair<float, int>(priority, value));
            int i = Heap.Count - 1;
            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (Heap[parent].Key <= Heap[i].Key)
                {
                    break;
                }

                KeyValuePair<float, int> tmp = Heap[parent];
                Heap[parent] = Heap[i];
                Heap[i] = tmp;
                i = parent;
            }
        }

        private static int HeapPop()
        {
            int result = Heap[0].Value;
            int last = Heap.Count - 1;
            Heap[0] = Heap[last];
            Heap.RemoveAt(last);
            int i = 0;
            int count = Heap.Count;
            while (true)
            {
                int left = i * 2 + 1;
                if (left >= count)
                {
                    break;
                }

                int right = left + 1;
                int smallest = right < count && Heap[right].Key < Heap[left].Key ? right : left;
                if (Heap[i].Key <= Heap[smallest].Key)
                {
                    break;
                }

                KeyValuePair<float, int> tmp = Heap[smallest];
                Heap[smallest] = Heap[i];
                Heap[i] = tmp;
                i = smallest;
            }

            return result;
        }
    }
}
