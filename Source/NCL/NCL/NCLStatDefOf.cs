using RimWorld;
using Verse;

namespace NCL
{
    [DefOf]
    public static class NCLStatDefOf
    {
        public static StatDef NCL_VerbRangeFactor;

        static NCLStatDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(NCLStatDefOf));
        }
    }
}
