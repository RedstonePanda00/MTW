using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace NCL
{
    public class CompProperties_GunshipFlight : CompProperties
    {
        public HediffDef landedHediff;
        public int takeoffTicks = 50;
        public int landingTicks = 50;
        public float hoverOffsetZ = 1.8f;
        public float hoverAltitudeY = 0.08f;
        public int thrusterFleckInterval = 2;
        public float thrusterMinSpeed = 0.05f;
        public bool defaultAirborneOnSpawn = false;
        public List<BodyPartDef> lethalEngineParts = new List<BodyPartDef>();
        public List<IntVec3> engineCellsNorth = new List<IntVec3>();

        public ThingDef crashFallerDef;
        public ThingDef wreckageDef;
        public int crashTicks = 150;
        public float crashLandingSearchRadius = 8f;
        public float crashExplosionRadius = 8.9f;
        public int crashExplosionDamage = 55;
        public DamageDef crashExplosionDamageDef;

        public CompProperties_GunshipFlight()
        {
            compClass = typeof(CompGunshipFlight);
        }

        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (string error in base.ConfigErrors(parentDef))
            {
                yield return error;
            }

            if (landedHediff == null)
            {
                yield return $"{parentDef.defName}: CompProperties_GunshipFlight.landedHediff is null.";
            }

            if (lethalEngineParts == null || lethalEngineParts.Count == 0)
            {
                yield return $"{parentDef.defName}: CompProperties_GunshipFlight.lethalEngineParts is empty.";
            }

            if (crashFallerDef == null)
            {
                yield return $"{parentDef.defName}: CompProperties_GunshipFlight.crashFallerDef is null.";
            }

            if (wreckageDef == null)
            {
                yield return $"{parentDef.defName}: CompProperties_GunshipFlight.wreckageDef is null.";
            }
        }
    }
}
