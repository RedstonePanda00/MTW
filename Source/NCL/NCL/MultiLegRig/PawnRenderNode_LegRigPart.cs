using UnityEngine;
using Verse;

namespace NCL
{
    public class PawnRenderNodeProperties_LegRigPart : PawnRenderNodeProperties
    {
        public string rigPartKey;
        public float layerSouth;
        public float layerEast;
        public float layerNorth;

        public float LayerFor(Rot4 rot)
        {
            switch (rot.AsInt)
            {
                case 0:
                    return layerNorth;
                case 2:
                    return layerSouth;
                default:
                    return layerEast;
            }
        }
    }

    public class PawnRenderNode_LegRigPart : PawnRenderNode
    {
        public CompMultiLegRig Rig;

        public PawnRenderNode_LegRigPart(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree)
            : base(pawn, props, tree)
        {
        }

        public PawnRenderNodeProperties_LegRigPart RigProps => Props as PawnRenderNodeProperties_LegRigPart;

        public override GraphicMeshSet MeshSetFor(Pawn pawn)
        {
            return MeshPool.GetMeshSetForSize(1f, 1f);
        }
    }

    public class PawnRenderNodeWorker_LegRigPart : PawnRenderNodeWorker
    {
        public override bool CanDrawNow(PawnRenderNode node, PawnDrawParms parms)
        {
            if (parms.flags.FlagSet(PawnRenderFlags.Portrait))
            {
                return false;
            }

            return base.CanDrawNow(node, parms);
        }

        public override Vector3 OffsetFor(PawnRenderNode node, PawnDrawParms parms, out Vector3 pivot)
        {
            Vector3 offset = base.OffsetFor(node, parms, out pivot);
            if (node is PawnRenderNode_LegRigPart rigNode && rigNode.Rig != null)
            {
                offset += rigNode.Rig.GetPartOffset(rigNode.RigProps?.rigPartKey, parms.facing);
            }

            return offset;
        }

        public override Quaternion RotationFor(PawnRenderNode node, PawnDrawParms parms)
        {
            Quaternion rotation = base.RotationFor(node, parms);
            if (TryGetBodyRig(node, out CompMultiLegRig rig))
            {
                rotation *= Quaternion.AngleAxis(rig.BodyTiltAngle(), Vector3.up);
            }

            return rotation;
        }

        public override Vector3 ScaleFor(PawnRenderNode node, PawnDrawParms parms)
        {
            Vector3 scale = base.ScaleFor(node, parms);
            if (TryGetBodyRig(node, out CompMultiLegRig rig))
            {
                scale = scale.ScaledBy(rig.BodyScaleFactor());
            }

            return scale;
        }

        private static bool TryGetBodyRig(PawnRenderNode node, out CompMultiLegRig rig)
        {
            rig = null;
            if (node is PawnRenderNode_LegRigPart rigNode
                && rigNode.Rig != null
                && rigNode.RigProps?.rigPartKey == CompMultiLegRig.BodyPartKey)
            {
                rig = rigNode.Rig;
            }

            return rig != null;
        }

        public override float LayerFor(PawnRenderNode node, PawnDrawParms parms)
        {
            if (node is PawnRenderNode_LegRigPart rigNode && rigNode.RigProps != null)
            {
                return node.Props.baseLayer + rigNode.RigProps.LayerFor(parms.facing) + node.debugLayerOffset;
            }

            return base.LayerFor(node, parms);
        }
    }

    // Body node of the display graphic: only used for portraits, in-map drawing is handled by the leg rig.
    public class PawnRenderNodeWorker_PortraitOnlyBody : PawnRenderNodeWorker
    {
        public override bool CanDrawNow(PawnRenderNode node, PawnDrawParms parms)
        {
            if (!parms.flags.FlagSet(PawnRenderFlags.Portrait) && parms.pawn?.GetComp<CompMultiLegRig>() != null)
            {
                return false;
            }

            return base.CanDrawNow(node, parms);
        }
    }
}
