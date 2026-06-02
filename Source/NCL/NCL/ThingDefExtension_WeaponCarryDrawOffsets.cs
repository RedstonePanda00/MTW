using UnityEngine;
using Verse;

namespace NCL
{
    // Replaces VEF ThingDefExtension weaponCarryDrawOffsets for carried / aimed weapon draw positions.
    public class ThingDefExtension_WeaponCarryDrawOffsets : DefModExtension
    {
        public WeaponCarryDrawOffsets weaponCarryDrawOffsets;

        public Vector3? OffsetFor(Rot4 facing)
        {
            return weaponCarryDrawOffsets?.OffsetFor(facing);
        }
    }

    public class WeaponCarryDrawOffsets
    {
        public DirDraw north;
        public DirDraw east;
        public DirDraw south;
        public DirDraw west;

        public Vector3? OffsetFor(Rot4 facing)
        {
            DirDraw d = facing.AsInt switch
            {
                0 => north,
                1 => east,
                2 => south,
                3 => west,
                _ => null
            };
            return d == null ? (Vector3?)null : d.drawOffset;
        }
    }

    public class DirDraw
    {
        public Vector3 drawOffset;
    }
}
