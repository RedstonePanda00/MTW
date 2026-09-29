using System.Collections.Generic;
using Verse;

namespace NCL
{
    // WalkableBy / Pawn.Flying / DrawPos run for every pawn many times per tick. TryGetComp walks the
    // whole comps list, so every patch gates on this def set first.
    [StaticConstructorOnStartup]
    public static class GunshipDefCache
    {
        private static readonly HashSet<ThingDef> FlightDefs = new HashSet<ThingDef>();
        private static readonly HashSet<ThingDef> CargoDefs = new HashSet<ThingDef>();
        private static readonly HashSet<ThingDef> MultiCellDefs = new HashSet<ThingDef>();

        public static int MaxMultiCellMargin { get; private set; }

        static GunshipDefCache()
        {
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (def.comps == null)
                {
                    continue;
                }

                for (int i = 0; i < def.comps.Count; i++)
                {
                    CompProperties props = def.comps[i];
                    if (props is CompProperties_GunshipFlight)
                    {
                        FlightDefs.Add(def);
                    }
                    else if (props is CompProperties_GunshipCargo)
                    {
                        CargoDefs.Add(def);
                    }
                    else if (props is CompProperties_MultiCellPawn)
                    {
                        MultiCellDefs.Add(def);
                        int margin = System.Math.Max(def.size.x, def.size.z) / 2;
                        if (margin > MaxMultiCellMargin)
                        {
                            MaxMultiCellMargin = margin;
                        }
                    }
                }
            }
        }

        public static bool HasFlight(ThingDef def)
        {
            return def != null && FlightDefs.Contains(def);
        }

        public static bool HasCargo(ThingDef def)
        {
            return def != null && CargoDefs.Contains(def);
        }

        public static bool HasMultiCell(ThingDef def)
        {
            return def != null && MultiCellDefs.Contains(def);
        }

        public static CompGunshipFlight GetFlight(Pawn pawn)
        {
            if (pawn == null || !FlightDefs.Contains(pawn.def))
            {
                return null;
            }

            return pawn.TryGetComp<CompGunshipFlight>();
        }

        public static CompGunshipCargo GetCargo(Pawn pawn)
        {
            if (pawn == null || !CargoDefs.Contains(pawn.def))
            {
                return null;
            }

            return pawn.TryGetComp<CompGunshipCargo>();
        }
    }
}
