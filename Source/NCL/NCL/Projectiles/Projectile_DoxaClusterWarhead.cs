using RimWorld;
using UnityEngine;
using Verse;

namespace NCL.Projectiles
{
    /// <summary>
    /// Doxa tactical warhead: primary burst on impact only; cluster submunitions via
    /// <see cref="Comp_SpawnClusterSubmunitionsOnDestroy"/> when the thing is destroyed.
    /// </summary>
    public class Projectile_DoxaClusterWarhead : Projectile_ExplosiveWithEffects
    {
        public Thing InstigatorForCluster => launcher ?? this;

        public Thing EquipmentForCluster => equipment;

        /// <summary>Visual burst point (includes high-arc Z). Radial targets use ground <see cref="Position"/>.</summary>
        public void GetClusterSpawnPositions(out Vector3 launchOrigin, out IntVec3 radialCenter)
        {
            radialCenter = Position;
            launchOrigin = this is Projectile_HighArcExplosiveBase ? DrawPos : Position.ToVector3Shifted();
            if (launchOrigin.y <= 0.01f)
            {
                launchOrigin.y = AltitudeLayer.Projectile.AltitudeFor();
            }
        }

        private ModExtension_ProjectileCluster ClusterExt =>
            def.GetModExtension<ModExtension_ProjectileCluster>();

        protected override void OnHighArcImpact(Thing hitThing, bool blockedByShield)
        {
            Map map = Map;
            ModExtension_ProjectileCluster ext = ClusterExt;
            if (map == null || ext == null)
            {
                Log.Warning($"[NCL Cluster] {def.defName} missing map or ModExtension_ProjectileCluster; falling back to vanilla impact.");
                ImpactExplosiveCore(hitThing, blockedByShield);
                effects.Impact(map, hitThing, blockedByShield);
                return;
            }

            if (ext.subProjectileDef == null)
            {
                Log.Warning($"[NCL Cluster] {def.defName} has no subProjectileDef.");
            }

            Thing instigator = launcher ?? this;
            DamageDef damageDef = ext.ResolvePrimaryDamageDef(def);
            if (damageDef != null && ext.primaryRadius > 0.001f)
            {
                GenExplosion.DoExplosion(
                    Position,
                    map,
                    ext.primaryRadius,
                    damageDef,
                    instigator,
                    ext.primaryDamage,
                    armorPenetration: -1f,
                    doVisualEffects: true,
                    doSoundEffects: true);
            }

            effects.Impact(map, hitThing, blockedByShield);

            Destroy(DestroyMode.Vanish);
        }
    }
}
