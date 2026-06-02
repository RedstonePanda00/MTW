using Verse;

namespace NCL.Diver
{
    // South draws under body; north/east over body; west mirrors east via Graphic_Multi + DrawData.
    public class PawnRenderNodeWorker_DiverCloak : PawnRenderNodeWorker
    {
        public override bool CanDrawNow(PawnRenderNode node, PawnDrawParms parms)
        {
            if (node is PawnRenderNode_DiverCloak cloakNode)
            {
                CompDiverCloak comp = cloakNode.CloakComp;
                if (comp == null || !comp.CloakVisible || comp.CurrentSet == null)
                {
                    return false;
                }
            }

            return base.CanDrawNow(node, parms);
        }
    }
}
