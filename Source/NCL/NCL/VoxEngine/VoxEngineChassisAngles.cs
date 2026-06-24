using UnityEngine;

namespace NCL
{
    // User frame: South = 0deg, clockwise positive (West=90, North=180, East=270).
    // RimWorld AngleFlat: North=0, East=90, South=180, West=270.
    public static class VoxEngineChassisAngles
    {
        public const float SideWestMin = 45f;
        public const float SideWestMax = 135f;
        public const float SideEastMin = 225f;
        public const float SideEastMax = 315f;
        public const float SideDisplayRimAngle = 90f;

        // Map any signed angle (CW/CCW, multi-turn) into [0, 360).
        public static float NormalizeDegrees360(float degrees)
        {
            return (degrees % 360f + 360f) % 360f;
        }

        public static float RimWorldToUserAngle(float rimAngleDegrees)
        {
            return NormalizeDegrees360(rimAngleDegrees - 180f);
        }

        public static float UserToRimWorldAngle(float userAngleDegrees)
        {
            return NormalizeDegrees360(userAngleDegrees + 180f);
        }

        // RimWorld AngleFlat convention: 0 = North (+Z), 90 = East (+X).
        public static float ExtractMatrixYawDegrees(Matrix4x4 matrix)
        {
            Vector3 forward = matrix.MultiplyVector(Vector3.forward);
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
            {
                return 0f;
            }

            return NormalizeDegrees360(Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg);
        }

        // Convert a world-space yaw into node-local yaw after parms.matrix is applied first.
        public static float WorldYawToNodeLocalYaw(float worldYawDegrees, Matrix4x4 parentMatrix)
        {
            return NormalizeDegrees360(worldYawDegrees - ExtractMatrixYawDegrees(parentMatrix));
        }

        public static bool IsInSideWestZone(float userAngleDegrees, float marginDegrees)
        {
            userAngleDegrees = NormalizeDegrees360(userAngleDegrees);
            return userAngleDegrees >= SideWestMin + marginDegrees
                && userAngleDegrees <= SideWestMax - marginDegrees;
        }

        public static bool IsInSideEastZone(float userAngleDegrees, float marginDegrees)
        {
            userAngleDegrees = NormalizeDegrees360(userAngleDegrees);
            return userAngleDegrees >= SideEastMin + marginDegrees
                && userAngleDegrees <= SideEastMax - marginDegrees;
        }

        public static VoxBaseVisualMode ClassifyWithoutHysteresis(float userAngleDegrees)
        {
            userAngleDegrees = NormalizeDegrees360(userAngleDegrees);
            if (userAngleDegrees >= SideWestMin && userAngleDegrees <= SideWestMax)
            {
                return VoxBaseVisualMode.SideWest;
            }

            if (userAngleDegrees >= SideEastMin && userAngleDegrees <= SideEastMax)
            {
                return VoxBaseVisualMode.SideEast;
            }

            return VoxBaseVisualMode.TopDown;
        }

        public static VoxBaseVisualMode UpdateVisualMode(
            float userAngleDegrees,
            VoxBaseVisualMode currentMode,
            float hysteresisDegrees)
        {
            userAngleDegrees = NormalizeDegrees360(userAngleDegrees);
            VoxBaseVisualMode targetMode = ClassifyWithoutHysteresis(userAngleDegrees);
            if (targetMode != VoxBaseVisualMode.TopDown)
            {
                return targetMode;
            }

            float h = Mathf.Max(hysteresisDegrees, 0f);

            switch (currentMode)
            {
                case VoxBaseVisualMode.SideWest:
                    if (IsInSideWestZone(userAngleDegrees, -h))
                    {
                        return VoxBaseVisualMode.SideWest;
                    }

                    if (IsInSideEastZone(userAngleDegrees, h))
                    {
                        return VoxBaseVisualMode.SideEast;
                    }

                    return VoxBaseVisualMode.TopDown;

                case VoxBaseVisualMode.SideEast:
                    if (IsInSideEastZone(userAngleDegrees, -h))
                    {
                        return VoxBaseVisualMode.SideEast;
                    }

                    if (IsInSideWestZone(userAngleDegrees, h))
                    {
                        return VoxBaseVisualMode.SideWest;
                    }

                    return VoxBaseVisualMode.TopDown;

                default:
                    return VoxBaseVisualMode.TopDown;
            }
        }
    }
}
