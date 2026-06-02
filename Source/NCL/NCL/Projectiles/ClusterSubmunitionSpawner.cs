using RimWorld;
using UnityEngine;
using Verse;

namespace NCL.Projectiles
{
    public static class ClusterSubmunitionSpawner
    {
        public static void Spawn(
            Projectile parent,
            Map map,
            Vector3 launchOrigin,
            IntVec3 radialCenter,
            ModExtension_ProjectileCluster ext,
            Thing launcher,
            Thing equipment)
        {
            if (parent == null || map == null || ext?.subProjectileDef == null || ext.submunitionCount <= 0)
            {
                return;
            }

            if (!radialCenter.IsValid)
            {
                radialCenter = launchOrigin.ToIntVec3();
            }

            launcher ??= parent;
            Vector3 origin = launchOrigin;
            if (origin.y <= 0.01f)
            {
                origin.y = AltitudeLayer.Projectile.AltitudeFor();
            }

            int count = ext.submunitionCount;
            float step = 360f / count;

            for (int i = 0; i < count; i++)
            {
                float angle = i * step + Rand.Range(-ext.angleJitterDegrees, ext.angleJitterDegrees);
                float dist = ext.spreadRadiusCells + Rand.Range(-ext.radiusJitterCells, ext.radiusJitterCells);
                dist = Mathf.Max(2f, dist);

                IntVec3 targetCell = ResolveTargetCell(map, radialCenter, angle, dist);
                if (!targetCell.IsValid)
                {
                    continue;
                }

                Projectile sub = SpawnAndLaunchSub(ext.subProjectileDef, map, origin, targetCell, launcher, equipment);
                if (sub == null)
                {
                    Log.Warning($"[NCL Cluster] Failed to launch submunition {i} for {parent.def.defName}");
                }
            }
        }

        private static Projectile SpawnAndLaunchSub(
            ThingDef projectileDef,
            Map map,
            Vector3 origin,
            IntVec3 targetCell,
            Thing launcher,
            Thing equipment)
        {
            Projectile sub = ThingMaker.MakeThing(projectileDef) as Projectile;
            if (sub == null)
            {
                return null;
            }

            GenSpawn.Spawn(sub, origin.ToIntVec3(), map);

            LocalTargetInfo target = new LocalTargetInfo(targetCell);
            sub.Launch(
                launcher,
                origin,
                target,
                target,
                ProjectileHitFlags.IntendedTarget,
                preventFriendlyFire: false,
                equipment,
                targetCoverDef: null);

            return sub;
        }

        private static IntVec3 ResolveTargetCell(Map map, IntVec3 center, float angleDegrees, float distanceCells)
        {
            float rad = angleDegrees * Mathf.Deg2Rad;
            int dx = Mathf.RoundToInt(Mathf.Cos(rad) * distanceCells);
            int dz = Mathf.RoundToInt(Mathf.Sin(rad) * distanceCells);
            IntVec3 candidate = new IntVec3(center.x + dx, 0, center.z + dz);

            if (candidate.InBounds(map))
            {
                return candidate;
            }

            for (int shrink = 1; shrink <= Mathf.CeilToInt(distanceCells); shrink++)
            {
                float d = distanceCells - shrink;
                if (d < 1f)
                {
                    break;
                }

                dx = Mathf.RoundToInt(Mathf.Cos(rad) * d);
                dz = Mathf.RoundToInt(Mathf.Sin(rad) * d);
                candidate = new IntVec3(center.x + dx, 0, center.z + dz);
                if (candidate.InBounds(map))
                {
                    return candidate;
                }
            }

            return center;
        }
    }
}
