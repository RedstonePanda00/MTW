using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace NCL
{
    public class LegRigLegEntry
    {
        public string key;
        // Tripod default: group 0 swings in the first half of the cycle, group 1 in the second half.
        public int gaitGroup;
        // Overrides gaitGroup when >= 0; fraction of the full cycle.
        public float phaseOffset = -1f;
        // Foot pad position relative to the pawn draw center, in world units (x, z). West mirrors east.
        public Vector2 footSouth;
        public Vector2 footEast;
        public Vector2 footNorth;

        public float PhaseOffset => phaseOffset >= 0f ? phaseOffset : gaitGroup * 0.5f;

        public Vector2 FootFor(Rot4 rot)
        {
            switch (rot.AsInt)
            {
                case 0:
                    return footNorth;
                case 1:
                    return footEast;
                case 3:
                    return new Vector2(-footEast.x, footEast.y);
                default:
                    return footSouth;
            }
        }
    }

    public class CompProperties_MultiLegRig : CompProperties
    {
        public List<LegRigLegEntry> legs = new List<LegRigLegEntry>();
        public List<PawnRenderNodeProperties> renderNodeProperties;

        // Gait: phase advances by distance travelled.
        // Planted feet stay ground-locked only when strideVisual == cycleDistance * (1 - swingFraction) / 2.
        public float cycleDistance = 2f;
        public float swingFraction = 0.5f;
        public float liftHeight = 0.35f;
        public float strideVisual = 0.5f;
        public int moveBlendTicks = 30;

        public float bodyBobDepth = 0.1f;
        public int bodyBobTicks = 50;
        public float bodySwayAmount = 0.04f;
        // Hover float applied to the body regardless of movement.
        public float idleFloatAmplitude = 0.06f;
        public int idleFloatPeriodTicks = 240;

        public int dustPuffsPerFoot = 3;
        public FloatRange dustScale = new FloatRange(1.4f, 2.2f);
        public float dustSpread = 0.35f;
        public Color dustColor = new Color(0.55f, 0.55f, 0.55f, 4f);
        public SoundDef footfallSound;

        public float recoilDepth = 0.35f;
        public int recoilTicks = 70;

        // Field-gun style recoil from the primary weapon: a sharp kick over fireRecoilAttackTicks, a slow
        // ease back over fireRecoilReturnTicks, then a small damped settle wobble. Legs stay planted.
        public int fireRecoilAttackTicks = 4;
        public int fireRecoilReturnTicks = 60;
        // Settle wobble as a fraction of the full kick.
        public float fireRecoilTailAmplitude = 0.12f;
        public int fireRecoilTailPeriodTicks = 12;
        public float fireRecoilTailDecayTicks = 14f;
        // Full-kick magnitudes. Kickback pushes the body away from the shot direction, in world units.
        public float fireRecoilKickback = 0.3f;
        public float fireRecoilSink = 0.08f;
        public float fireRecoilTiltAngle = 4f;
        // Compression along the shot axis.
        public float fireRecoilSquash = 0.05f;

        public bool landOnFirstSpawn = true;
        public ThingDef landingFallerDef;
        public string landingTexPath;
        public float landingDrawSize = 10f;

        public CompProperties_MultiLegRig()
        {
            compClass = typeof(CompMultiLegRig);
        }

        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (string error in base.ConfigErrors(parentDef))
            {
                yield return error;
            }

            if (cycleDistance <= 0f)
            {
                yield return "cycleDistance must be positive";
            }

            if (swingFraction <= 0f || swingFraction >= 1f)
            {
                yield return "swingFraction must be in (0, 1)";
            }

            if (landOnFirstSpawn && landingFallerDef == null)
            {
                yield return "landOnFirstSpawn requires landingFallerDef";
            }
        }
    }
}
