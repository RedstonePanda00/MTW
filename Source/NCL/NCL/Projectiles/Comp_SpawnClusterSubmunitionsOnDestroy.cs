using RimWorld;
using UnityEngine;
using Verse;

namespace NCL.Projectiles
{
    public class CompProperties_SpawnClusterSubmunitionsOnDestroy : CompProperties
    {
        public CompProperties_SpawnClusterSubmunitionsOnDestroy()
        {
            compClass = typeof(Comp_SpawnClusterSubmunitionsOnDestroy);
        }
    }

    /// <summary>Spawns cluster submunitions when the warhead is destroyed (impact, intercept, etc.).</summary>
    public class Comp_SpawnClusterSubmunitionsOnDestroy : ThingComp
    {
        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            Projectile_DoxaClusterWarhead warhead = parent as Projectile_DoxaClusterWarhead;
            Vector3 launchOrigin = Vector3.zero;
            IntVec3 radialCenter = IntVec3.Invalid;

            if (warhead != null)
            {
                warhead.GetClusterSpawnPositions(out launchOrigin, out radialCenter);
            }
            else if (parent != null)
            {
                radialCenter = parent.Position;
                launchOrigin = parent.DrawPos;
            }

            base.PostDestroy(mode, previousMap);

            if (previousMap == null || warhead == null || !radialCenter.IsValid)
            {
                return;
            }

            ModExtension_ProjectileCluster ext = parent.def.GetModExtension<ModExtension_ProjectileCluster>();
            if (ext?.subProjectileDef == null)
            {
                return;
            }

            ClusterSubmunitionSpawner.Spawn(
                warhead,
                previousMap,
                launchOrigin,
                radialCenter,
                ext,
                warhead.InstigatorForCluster,
                warhead.EquipmentForCluster);
        }
    }
}
