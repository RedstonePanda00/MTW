using RimWorld;
using Verse;

namespace NoBody
{
    [DefOf]
    public static class TransparentBodyDefOf
    {
        public static BodyTypeDef MaleTransparent;
        public static BodyTypeDef FemaleTransparent;
        public static BodyTypeDef ThinTransparent;
        public static BodyTypeDef HulkTransparent;
        public static BodyTypeDef FatTransparent;
        public static ThingDef Apparel_NoBody;

        static TransparentBodyDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(TransparentBodyDefOf));
        }
    }
}
