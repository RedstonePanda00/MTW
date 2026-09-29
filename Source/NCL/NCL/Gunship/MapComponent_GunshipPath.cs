using Unity.Collections;
using Verse;

namespace NCL
{
    public class MapComponent_GunshipPath : MapComponent
    {
        private NativeArray<int> zeroCostGrid;
        private NativeArray<ushort> multiCellBorderGrid;

        public NativeArray<int> ZeroCostGrid
        {
            get
            {
                EnsureCreated();
                return zeroCostGrid;
            }
        }

        public NativeArray<ushort> MultiCellBorderGrid
        {
            get
            {
                EnsureCreated();
                return multiCellBorderGrid;
            }
        }

        public MapComponent_GunshipPath(Map map) : base(map)
        {
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            EnsureCreated();
        }

        public override void MapRemoved()
        {
            DisposeGrid();
            base.MapRemoved();
        }

        private void EnsureCreated()
        {
            int cells = map.cellIndices.NumGridCells;
            if (zeroCostGrid.IsCreated
                && zeroCostGrid.Length == cells
                && multiCellBorderGrid.IsCreated
                && multiCellBorderGrid.Length == cells)
            {
                return;
            }

            DisposeGrid();
            zeroCostGrid = new NativeArray<int>(cells, Allocator.Persistent);
            multiCellBorderGrid = new NativeArray<ushort>(cells, Allocator.Persistent);

            int margin = GunshipDefCache.MaxMultiCellMargin;
            if (margin <= 0)
            {
                return;
            }

            for (int z = 0; z < map.Size.z; z++)
            {
                for (int x = 0; x < map.Size.x; x++)
                {
                    if (x < margin
                        || z < margin
                        || x >= map.Size.x - margin
                        || z >= map.Size.z - margin)
                    {
                        int index = map.cellIndices.CellToIndex(x, z);
                        multiCellBorderGrid[index] = 10000;
                    }
                }
            }
        }

        private void DisposeGrid()
        {
            if (zeroCostGrid.IsCreated)
            {
                zeroCostGrid.Dispose();
            }

            if (multiCellBorderGrid.IsCreated)
            {
                multiCellBorderGrid.Dispose();
            }
        }
    }
}
