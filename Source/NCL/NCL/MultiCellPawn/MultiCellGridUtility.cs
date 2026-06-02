using System.Collections.Generic;
using Verse;

namespace NCL
{
    public static class MultiCellGridUtility
    {
        public static readonly IntVec3 CoreCell = IntVec3.Zero;

        private static readonly IntVec3[] FourNeighbors =
        {
            new IntVec3(1, 0, 0),
            new IntVec3(-1, 0, 0),
            new IntVec3(0, 0, 1),
            new IntVec3(0, 0, -1)
        };

        public static IEnumerable<IntVec3> GetFourNeighbors(IntVec3 cell)
        {
            for (int i = 0; i < FourNeighbors.Length; i++)
            {
                yield return cell + FourNeighbors[i];
            }
        }

        public static bool IsConnected(HashSet<IntVec3> cells, IntVec3 required = default)
        {
            if (cells == null || cells.Count == 0)
            {
                return false;
            }

            if (required == default)
            {
                required = CoreCell;
            }

            if (!cells.Contains(required))
            {
                return false;
            }

            HashSet<IntVec3> visited = new HashSet<IntVec3>();
            Queue<IntVec3> queue = new Queue<IntVec3>();
            queue.Enqueue(required);
            visited.Add(required);

            while (queue.Count > 0)
            {
                IntVec3 current = queue.Dequeue();
                foreach (IntVec3 neighbor in GetFourNeighbors(current))
                {
                    if (!cells.Contains(neighbor) || visited.Contains(neighbor))
                    {
                        continue;
                    }

                    visited.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }

            return visited.Count == cells.Count;
        }

        public static bool CanRemove(HashSet<IntVec3> cells, IntVec3 cell)
        {
            if (cells == null || !cells.Contains(cell))
            {
                return false;
            }

            if (cell == CoreCell)
            {
                return false;
            }

            HashSet<IntVec3> trial = new HashSet<IntVec3>(cells);
            trial.Remove(cell);
            return IsConnected(trial, CoreCell);
        }

        public static IEnumerable<IntVec3> GetExpansionCandidates(HashSet<IntVec3> cells)
        {
            if (cells == null || cells.Count == 0)
            {
                yield return CoreCell;
                yield break;
            }

            HashSet<IntVec3> candidates = new HashSet<IntVec3>();
            foreach (IntVec3 cell in cells)
            {
                foreach (IntVec3 neighbor in GetFourNeighbors(cell))
                {
                    if (!cells.Contains(neighbor))
                    {
                        candidates.Add(neighbor);
                    }
                }
            }

            foreach (IntVec3 candidate in candidates)
            {
                yield return candidate;
            }
        }

        public static void GetBounds(HashSet<IntVec3> cells, out int minX, out int maxX, out int minZ, out int maxZ)
        {
            minX = maxX = minZ = maxZ = 0;
            if (cells == null || cells.Count == 0)
            {
                return;
            }

            bool first = true;
            foreach (IntVec3 cell in cells)
            {
                if (first)
                {
                    minX = maxX = cell.x;
                    minZ = maxZ = cell.z;
                    first = false;
                    continue;
                }

                if (cell.x < minX) minX = cell.x;
                if (cell.x > maxX) maxX = cell.x;
                if (cell.z < minZ) minZ = cell.z;
                if (cell.z > maxZ) maxZ = cell.z;
            }
        }
    }
}
