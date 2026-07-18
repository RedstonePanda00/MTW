using Unity.Collections;
using Verse;

namespace NCL
{
    public class MapComponent_GunshipPath : MapComponent
    {
        private NativeArray<int> zeroCostGrid;

        public NativeArray<int> ZeroCostGrid
        {
            get
            {
                EnsureCreated();
                return zeroCostGrid;
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
            if (zeroCostGrid.IsCreated && zeroCostGrid.Length == cells)
            {
                return;
            }

            DisposeGrid();
            zeroCostGrid = new NativeArray<int>(cells, Allocator.Persistent);
        }

        private void DisposeGrid()
        {
            if (zeroCostGrid.IsCreated)
            {
                zeroCostGrid.Dispose();
            }
        }
    }
}
