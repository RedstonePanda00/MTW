using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace NCL
{
    public class ModExtension_MechLanding : DefModExtension
    {
        public int shadowTicks = 150;
        public int fallTicks = 45;
        public float shadowStartScale = 0.25f;
        public float shadowMaxAlpha = 0.6f;
        public float fallStartScale = 3f;
        public float fallFadeInFraction = 0.35f;
        public SoundDef fallSound;
        public SoundDef impactSound;
        public float impactShake = 4f;
        public int impactDustCount = 40;
        public float impactDustRadius = 4.5f;
        public FloatRange impactDustScale = new FloatRange(2.5f, 4.5f);
        public int impactSmokeCount = 12;
        public FloatRange impactSmokeScale = new FloatRange(3f, 5f);
        public Color impactDustColor = new Color(0.55f, 0.55f, 0.55f, 4f);
    }

    public class Thing_MechLandingFaller : ThingWithComps, IThingHolder
    {
        private static readonly ModExtension_MechLanding DefaultExtension = new ModExtension_MechLanding();
        private static readonly MaterialPropertyBlock PropertyBlock = new MaterialPropertyBlock();

        private ThingOwner<Thing> innerContainer;
        private int ageTicks;
        private Rot4 pawnRotation = Rot4.South;
        private bool fallSoundPlayed;
        private Sustainer fallSustainer;
        private Graphic cachedGraphic;
        private Graphic cachedShadowGraphic;

        public Thing_MechLandingFaller()
        {
            innerContainer = new ThingOwner<Thing>(this);
        }

        private ModExtension_MechLanding Ext => def.GetModExtension<ModExtension_MechLanding>() ?? DefaultExtension;

        private Pawn InnerPawn => innerContainer.Count > 0 ? innerContainer[0] as Pawn : null;

        private int TotalTicks => Ext.shadowTicks + Ext.fallTicks;

        public ThingOwner GetDirectlyHeldThings()
        {
            return innerContainer;
        }

        public void GetChildHolders(System.Collections.Generic.List<IThingHolder> outChildren)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
        }

        public bool TryLoad(Pawn pawn, Rot4 rot)
        {
            pawnRotation = rot;
            return innerContainer.TryAddOrTransfer(pawn, canMergeWithExistingStacks: false);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref innerContainer, "innerContainer", this);
            Scribe_Values.Look(ref ageTicks, "ageTicks", 0);
            Scribe_Values.Look(ref pawnRotation, "pawnRotation", Rot4.South);
            Scribe_Values.Look(ref fallSoundPlayed, "fallSoundPlayed", false);
        }

        protected override void Tick()
        {
            base.Tick();
            if (InnerPawn == null)
            {
                Destroy();
                return;
            }

            ageTicks++;
            if (ageTicks >= Ext.shadowTicks)
            {
                TickFallSound();
            }

            if (ageTicks >= TotalTicks)
            {
                Impact();
            }
        }

        private void TickFallSound()
        {
            SoundDef sound = Ext.fallSound;
            if (sound == null)
            {
                return;
            }

            if (!sound.sustain)
            {
                if (!fallSoundPlayed)
                {
                    fallSoundPlayed = true;
                    sound.PlayOneShot(new TargetInfo(Position, Map));
                }

                return;
            }

            if (fallSustainer == null || fallSustainer.Ended)
            {
                fallSustainer = sound.TrySpawnSustainer(SoundInfo.InMap(new TargetInfo(this), MaintenanceType.PerTick));
            }

            fallSustainer?.Maintain();
        }

        private void Impact()
        {
            Pawn pawn = InnerPawn;
            Map map = Map;
            IntVec3 cell = Position;
            innerContainer.Remove(pawn);
            JXGTLandingUtility.SpawnImmediately(pawn, cell, map, pawnRotation);

            ModExtension_MechLanding ext = Ext;
            Vector3 center = pawn.Spawned ? pawn.DrawPos : cell.ToVector3Shifted();
            for (int i = 0; i < ext.impactDustCount; i++)
            {
                Vector3 loc = center + (Rand.InsideUnitCircleVec3 * ext.impactDustRadius);
                if (loc.ShouldSpawnMotesAt(map))
                {
                    FleckMaker.ThrowDustPuffThick(loc, map, ext.impactDustScale.RandomInRange, ext.impactDustColor);
                }
            }

            for (int i = 0; i < ext.impactSmokeCount; i++)
            {
                Vector3 loc = center + (Rand.InsideUnitCircleVec3 * ext.impactDustRadius * 0.8f);
                if (loc.ShouldSpawnMotesAt(map))
                {
                    FleckMaker.ThrowSmoke(loc, map, ext.impactSmokeScale.RandomInRange);
                }
            }

            ext.impactSound?.PlayOneShot(new TargetInfo(cell, map));
            if (ext.impactShake > 0f)
            {
                Find.CameraDriver.shaker.DoShake(ext.impactShake);
            }

            pawn.GetComp<CompMultiLegRig>()?.PlayImpactRecoil();
            Destroy();
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            Pawn pawn = InnerPawn;
            CompProperties_MultiLegRig rigProps = pawn?.GetComp<CompMultiLegRig>()?.Props;
            if (rigProps == null || rigProps.landingTexPath.NullOrEmpty())
            {
                return;
            }

            ModExtension_MechLanding ext = Ext;
            Vector3 center = Position.ToVector3Shifted();
            float size = rigProps.landingDrawSize;

            float shadowT = Mathf.Clamp01(ageTicks / (float)Mathf.Max(1, ext.shadowTicks));
            float shadowScale = Mathf.Lerp(ext.shadowStartScale, 1f, EaseOut(shadowT));
            float shadowAlpha = ext.shadowMaxAlpha * EaseOut(shadowT);
            cachedShadowGraphic ??= GraphicDatabase.Get<Graphic_Multi>(rigProps.landingTexPath, ShaderDatabase.Transparent, Vector2.one, Color.white);
            Vector3 shadowPos = center;
            shadowPos.y = AltitudeLayer.Shadows.AltitudeFor();
            DrawLayer(cachedShadowGraphic, shadowPos, size * shadowScale, new Color(0f, 0f, 0f, shadowAlpha));

            if (ageTicks < ext.shadowTicks)
            {
                return;
            }

            float fallT = Mathf.Clamp01((ageTicks - ext.shadowTicks) / (float)Mathf.Max(1, ext.fallTicks));
            float scale = Mathf.Lerp(ext.fallStartScale, 1f, fallT * fallT);
            float alpha = ext.fallFadeInFraction > 0f ? Mathf.Clamp01(fallT / ext.fallFadeInFraction) : 1f;
            cachedGraphic ??= GraphicDatabase.Get<Graphic_Multi>(rigProps.landingTexPath, ShaderDatabase.Transparent, Vector2.one, Color.white);
            Vector3 bodyPos = center;
            bodyPos.y = AltitudeLayer.Skyfaller.AltitudeFor();
            DrawLayer(cachedGraphic, bodyPos, size * scale, new Color(1f, 1f, 1f, alpha));
        }

        private void DrawLayer(Graphic graphic, Vector3 pos, float size, Color color)
        {
            Material mat = graphic.MatAt(pawnRotation);
            if (mat == null)
            {
                return;
            }

            bool flipMesh = pawnRotation == Rot4.West && graphic.WestFlipped;
            Mesh mesh = flipMesh ? MeshPool.GridPlaneFlip(Vector2.one) : MeshPool.GridPlane(Vector2.one);
            Matrix4x4 matrix = Matrix4x4.TRS(pos, Quaternion.identity, new Vector3(size, 1f, size));
            PropertyBlock.Clear();
            PropertyBlock.SetColor(ShaderPropertyIDs.Color, color);
            Graphics.DrawMesh(mesh, matrix, mat, 0, null, 0, PropertyBlock);
        }

        private static float EaseOut(float t)
        {
            return 1f - (1f - t) * (1f - t);
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            base.Destroy(mode);
            innerContainer.ClearAndDestroyContents();
            if (fallSustainer != null)
            {
                fallSustainer.End();
                fallSustainer = null;
            }
        }
    }
}
