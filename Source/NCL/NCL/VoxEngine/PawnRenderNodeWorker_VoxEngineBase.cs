using UnityEngine;
using Verse;

namespace NCL
{
    public class PawnRenderNodeWorker_VoxEngineBase : PawnRenderNodeWorker
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
            pivot = Vector3.zero;
            return Vector3.zero;
        }

        public override float LayerFor(PawnRenderNode node, PawnDrawParms parms)
        {
            CompVoxEngineChassis comp = CompVoxEngineChassis.Get(parms.pawn);
            if (comp != null && comp.Props.baseLayer != 0f)
            {
                return comp.Props.baseLayer + node.debugLayerOffset;
            }

            return base.LayerFor(node, parms);
        }

        protected override Graphic GetGraphic(PawnRenderNode node, PawnDrawParms parms)
        {
            if (node is PawnRenderNode_VoxEngineBase baseNode)
            {
                Graphic graphic = baseNode.GraphicFor(parms.pawn);
                if (graphic != null)
                {
                    return graphic;
                }
            }

            return base.GetGraphic(node, parms);
        }

        public override Vector3 ScaleFor(PawnRenderNode node, PawnDrawParms parms)
        {
            CompVoxEngineChassis comp = CompVoxEngineChassis.Get(parms.pawn);
            float drawSize = comp != null && comp.Props.baseDrawSize > 0f
                ? comp.Props.baseDrawSize
                : node.Props.drawSize.x;
            return new Vector3(drawSize, 1f, drawSize) * node.debugScale;
        }

        public override Quaternion RotationFor(PawnRenderNode node, PawnDrawParms parms)
        {
            CompVoxEngineChassis comp = CompVoxEngineChassis.Get(parms.pawn);
            if (comp == null)
            {
                return Quaternion.AngleAxis(parms.facing.AsAngle, Vector3.up);
            }

            float angle = comp.GetDisplayRimAngle();
            return Quaternion.AngleAxis(angle, Vector3.up);
        }
    }
}
