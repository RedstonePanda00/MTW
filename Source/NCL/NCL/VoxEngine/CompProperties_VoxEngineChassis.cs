using System.Collections.Generic;
using RimWorld;
using Verse;

namespace NCL
{
    // Index-aligned with ThingDef.race.lifeStageAges; empty entries fall back to the comp-level paths.
    public class VoxChassisLifeStageTextures
    {
        public string baseTexPath;
        public string baseSideTexPath;
    }

    public class CompProperties_VoxEngineChassis : CompProperties
    {
        public float angularVelocityDegPerSec = 45f;
        public string baseTexPath = "Things/VoxEngine/Vox_Base";
        public string baseSideTexPath = "Things/VoxEngine/Vox_Base_side";
        public List<VoxChassisLifeStageTextures> lifeStageTextures;
        public float baseDrawSize = 4.5f;
        public float baseLayer = -10f;
        public float cardinalBandDeg = 45f;
        public float sideSwitchHysteresisDeg = 3f;
        public List<PawnRenderNodeProperties> renderNodeProperties;

        public CompProperties_VoxEngineChassis()
        {
            compClass = typeof(CompVoxEngineChassis);
        }
    }
}
