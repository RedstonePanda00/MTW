using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace NCL.Diver
{
    public class PawnRenderNode_DiverCloak : PawnRenderNode
    {
        public CompDiverCloak CloakComp { get; private set; }

        public PawnRenderNode_DiverCloak(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree)
            : base(pawn, props, tree)
        {
            CloakComp = CompDiverCloak.Get(pawn);
            meshSet = MeshSetFor(pawn);
        }

        public override GraphicMeshSet MeshSetFor(Pawn pawn)
        {
            return HumanlikeMeshPoolUtility.GetHumanlikeBodySetForPawn(pawn);
        }

        protected override IEnumerable<Graphic> GraphicsFor(Pawn pawn)
        {
            if (CloakComp == null || !CloakComp.CloakVisible)
            {
                yield break;
            }

            DiverCloakSetEntry set = CloakComp.CurrentSet;
            if (set == null)
            {
                yield break;
            }

            Graphic graphic = GraphicDatabase.Get<Graphic_Multi>(
                set.FullTexPath,
                ShaderDatabase.Cutout,
                Vector2.one,
                CloakComp.CloakColor);

            if (graphic != null)
            {
                yield return graphic;
            }
        }

        public override Color ColorFor(Pawn pawn)
        {
            return CloakComp?.CloakColor ?? Color.white;
        }
    }
}
