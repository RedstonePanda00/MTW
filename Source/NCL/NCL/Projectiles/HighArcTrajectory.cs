using UnityEngine;
using Verse;

namespace NCL.Projectiles
{
    public static class HighArcTrajectory
    {
        public static float SymmetricBump(float t)
        {
            return 4f * t * (1f - t);
        }

        public static float EvaluateZ(float z0, float z1, float zApex, float t)
        {
            t = Mathf.Clamp01(t);
            float chord = Mathf.Lerp(z0, z1, t);
            float midChord = (z0 + z1) * 0.5f;
            float lift = zApex - midChord;
            return chord + lift * SymmetricBump(t);
        }

        public static float ResolveApexZ(float z0, float z1, ThingDef def)
        {
            float mid = (z0 + z1) * 0.5f;
            if (def?.projectile == null)
            {
                return mid;
            }

            ModExtension_ProjectileHighArc highArc = def.GetModExtension<ModExtension_ProjectileHighArc>();
            if (highArc != null && highArc.arcPeakHeight > 0f)
            {
                return mid + highArc.arcPeakHeight;
            }

            return mid + def.projectile.arcHeightFactor;
        }

        public static float GetArcLift(float z0, float z1, ThingDef def)
        {
            float zApex = ResolveApexZ(z0, z1, def);
            return zApex - (z0 + z1) * 0.5f;
        }

        /// <summary>World-space velocity along the visual arc (ground track + symmetric Z lift).</summary>
        public static Vector3 GetVisualVelocity(Vector3 origin, Vector3 destination, ThingDef def, float progress)
        {
            progress = Mathf.Clamp01(progress);
            Vector3 h0 = origin.Yto0();
            Vector3 h1 = destination.Yto0();
            float lift = GetArcLift(h0.z, h1.z, def);
            return h1 - h0 + new Vector3(0f, 0f, lift * 4f * (1f - 2f * progress));
        }

        public static Quaternion GetVisualRotation(Vector3 origin, Vector3 destination, ThingDef def, float progress)
        {
            Vector3 vel = GetVisualVelocity(origin, destination, def, progress);
            if (vel.sqrMagnitude < 1E-6f)
            {
                Vector3 flat = (destination - origin).Yto0();
                return flat.sqrMagnitude < 1E-6f ? Quaternion.identity : Quaternion.LookRotation(flat);
            }

            return Quaternion.LookRotation(vel.normalized);
        }

        public static float GetArcOffsetZ(Vector3 origin, Vector3 destination, ThingDef def, float progress)
        {
            if (float.IsNaN(progress) || float.IsInfinity(progress))
            {
                progress = 0f;
            }

            progress = Mathf.Clamp01(progress);
            float z0 = origin.Yto0().z;
            float z1 = destination.Yto0().z;
            float zApex = ResolveApexZ(z0, z1, def);
            float zVisual = EvaluateZ(z0, z1, zApex, progress);
            float offset = zVisual - Mathf.Lerp(z0, z1, progress);
            if (float.IsNaN(offset) || float.IsInfinity(offset))
            {
                return 0f;
            }

            return offset;
        }

        public static void Apply(
            ProjectileEffectTracker effects,
            Thing projectile,
            Vector3 origin,
            Vector3 destination,
            float progress)
        {
            float arcZ = GetArcOffsetZ(origin, destination, projectile.def, progress);
            effects.currentExactPosition = projectile is Projectile p ? p.ExactPosition : origin;
            effects.currentVisualHeight = arcZ;
            effects.currentVisualPosition = effects.currentExactPosition + new Vector3(0f, 0f, arcZ);
            effects.currentVisualRotation = GetVisualRotation(origin, destination, projectile.def, progress);
            effects.currentVisualAngle = effects.currentVisualRotation.eulerAngles.y;
        }
    }
}
