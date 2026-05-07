using System;
using System.Net.NetworkInformation;
using Verse;

namespace NCL
{
    // Token: 0x0200000A RID: 10
    public class PlaceWorker_MustBeRoofed : PlaceWorker
    {
        // Token: 0x06000023 RID: 35 RVA: 0x000031E8 File Offset: 0x000013E8
        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            Room room = loc.GetRoom(map);
            bool flag = room != null;
            if (flag)
            {
                bool flag2 = room.OutdoorsForWork || !map.roofGrid.Roofed(loc);
                if (flag2)
                {
                    return new AcceptanceReport("NCL_MustPlaceRoofed".Translate());
                }
            }
            return true;
        }
    }
}
