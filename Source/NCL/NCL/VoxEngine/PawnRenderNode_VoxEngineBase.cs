using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace NCL
{
    public class PawnRenderNode_VoxEngineBase : PawnRenderNode
    {
        private static readonly Vector2 UnitPlaneSize = Vector2.one;

        private CompVoxEngineChassis chassisComp;
        private Graphic cachedTopDownGraphic;
        private Graphic cachedSideGraphic;
        private string cachedTopDownPath;
        private string cachedSidePath;
        private float cachedTopDrawSize = -1f;
        private float cachedSideDrawSize = -1f;

        public PawnRenderNode_VoxEngineBase(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree)
            : base(pawn, props, tree)
        {
            chassisComp = CompVoxEngineChassis.Get(pawn);
        }

        public void BindChassisComp(CompVoxEngineChassis comp)
        {
            chassisComp = comp;
        }

        public override Graphic GraphicFor(Pawn pawn)
        {
            CompVoxEngineChassis comp = chassisComp ?? CompVoxEngineChassis.Get(pawn);
            float drawSize = ResolveDrawSize(comp);

            if (comp != null && comp.UseSideProfileGraphic && !comp.Props.baseSideTexPath.NullOrEmpty())
            {
                return ResolveSideGraphic(comp.Props.baseSideTexPath, drawSize);
            }

            string topPath = comp?.Props.baseTexPath;
            if (topPath.NullOrEmpty())
            {
                topPath = Props.texPath;
            }

            if (topPath.NullOrEmpty())
            {
                return null;
            }

            return ResolveTopGraphic(topPath, drawSize);
        }

        public override Mesh GetMesh(PawnDrawParms parms)
        {
            CompVoxEngineChassis comp = chassisComp ?? CompVoxEngineChassis.Get(tree?.pawn);
            if (comp != null && comp.SideMirrorX)
            {
                return MeshPool.GridPlaneFlip(UnitPlaneSize);
            }

            return MeshPool.GridPlane(UnitPlaneSize);
        }

        protected override IEnumerable<Graphic> GraphicsFor(Pawn pawn)
        {
            Graphic graphic = GraphicFor(pawn);
            if (graphic != null)
            {
                yield return graphic;
            }
        }

        private static float ResolveDrawSize(CompVoxEngineChassis comp)
        {
            if (comp != null && comp.Props.baseDrawSize > 0f)
            {
                return comp.Props.baseDrawSize;
            }

            return 1f;
        }

        private Graphic ResolveTopGraphic(string texPath, float drawSize)
        {
            if (cachedTopDownGraphic == null
                || cachedTopDownPath != texPath
                || !Mathf.Approximately(cachedTopDrawSize, drawSize))
            {
                cachedTopDownPath = texPath;
                cachedTopDrawSize = drawSize;
                cachedTopDownGraphic = GraphicDatabase.Get<Graphic_Single>(
                    texPath,
                    ShaderDatabase.Cutout,
                    new Vector2(drawSize, drawSize),
                    Color.white);
            }

            return cachedTopDownGraphic;
        }

        private Graphic ResolveSideGraphic(string texPath, float drawSize)
        {
            if (cachedSideGraphic == null
                || cachedSidePath != texPath
                || !Mathf.Approximately(cachedSideDrawSize, drawSize))
            {
                cachedSidePath = texPath;
                cachedSideDrawSize = drawSize;
                cachedSideGraphic = GraphicDatabase.Get<Graphic_Single>(
                    texPath,
                    ShaderDatabase.Cutout,
                    new Vector2(drawSize, drawSize),
                    Color.white);
            }

            return cachedSideGraphic;
        }
    }
}
