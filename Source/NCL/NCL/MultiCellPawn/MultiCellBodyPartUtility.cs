using Verse;

namespace NCL
{
    public static class MultiCellBodyPartUtility
    {
        public static bool IsLeafPart(BodyPartRecord part)
        {
            return part != null && (part.parts == null || part.parts.Count == 0);
        }
    }
}
