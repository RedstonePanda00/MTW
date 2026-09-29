using Verse;
using Verse.AI;

namespace NCL
{
    public class CompProperties_EvasiveReposition : CompProperties
    {
        // How often the comp re-evaluates whether to break off and reposition.
        public int checkIntervalTicks = 60;

        // Ranged/explosive hits taken inside the window that force an evasive move.
        public int pressureWindowTicks = 300;
        public int hitsToEvade = 5;

        // Standing still this long while engaged also triggers a move, even without incoming fire.
        public int maxStationaryTicks = 1500;

        // Minimum gap between two repositions; multiplied by the stuck backoff.
        public IntRange cooldownTicks = new IntRange(900, 1500);
        // Retry delay after a search that found no usable cell.
        public int failRetryTicks = 300;
        // A reposition that moved less than this counts as stuck and grows the backoff.
        public float stuckDistance = 2f;
        public int maxBackoffLevel = 3;

        public FloatRange repositionDistance = new FloatRange(5f, 10f);
        public int candidateCount = 16;
        public float minThreatDistance = 8f;
        // Destinations this close to the previous reposition origin are rejected to avoid ping-ponging.
        public float avoidPreviousRadius = 4f;
        // Destination must still let the primary verb hit the current target.
        public bool keepFiringSolution = true;

        public int jobExpiryTicks = 900;
        public LocomotionUrgency locomotionUrgency = LocomotionUrgency.Walk;

        public CompProperties_EvasiveReposition()
        {
            compClass = typeof(CompEvasiveReposition);
        }
    }
}
