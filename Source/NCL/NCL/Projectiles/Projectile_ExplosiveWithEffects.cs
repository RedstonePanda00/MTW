using RimWorld;
using Verse;

namespace NCL.Projectiles
{
    /// <summary>High-arc explosive with ModExtension_ProjectileEffects (flecks, launch/impact effects).</summary>
    public class Projectile_ExplosiveWithEffects : Projectile_HighArcExplosiveBase
    {
        protected override void OnHighArcImpact(Thing hitThing, bool blockedByShield)
        {
            ImpactExplosiveCore(hitThing, blockedByShield);
            effects.Impact(Map, hitThing, blockedByShield);
        }
    }
}
