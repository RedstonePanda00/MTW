using Verse;

namespace NCL.Projectiles
{
    public class ModExtension_ProjectileHighArc : DefModExtension
    {
        /// <summary>Extra height at apex above the launch–target chord midpoint (world +Z).</summary>
        public float arcPeakHeight;

        /// <summary>Optional draw Y at apex; if zero, midpoint Y + small factor from arcHeightFactor.</summary>
        public float apexDrawAltitude;

        public bool activeTracking;
    }
}
