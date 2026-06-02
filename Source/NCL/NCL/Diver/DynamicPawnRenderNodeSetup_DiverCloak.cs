using System.Collections.Generic;
using NCL;
using RimWorld;
using UnityEngine;
using Verse;

namespace NCL.Diver
{
    public class DynamicPawnRenderNodeSetup_DiverCloak : DynamicPawnRenderNodeSetup
    {
        // Vanilla reference (DynamicPawnRenderNodeSetup_Apparel, ApparelBody baseLayer 20):
        // - Recon armor (Shell LastLayer): North drawData 88; East/South/West use baseLayer 20.
        // - Low-shield / jump pack (RenderAsPack): North 93, South -3; East/West baseLayer 20.
        // - Mod point-defense pack drone node: baseLayer 82.
        // Cloak must be: South < body; North > 88 and < 93; East > 20 and < 82.
        private const float LayerSouthBelowBody = -5f;
        private const float LayerNorthBetweenShellAndPack = 90f;
        private const float LayerEastBetweenApparelAndPackDrone = 81f;

        public override bool HumanlikeOnly => false;

        public override IEnumerable<(PawnRenderNode node, PawnRenderNode parent)> GetDynamicNodes(Pawn pawn, PawnRenderTree tree)
        {
            if (!DiverUtility.IsDiver(pawn))
            {
                yield break;
            }

            CompDiverCloak comp = CompDiverCloak.Get(pawn);
            if (comp == null || !comp.CloakVisible || comp.CurrentSet == null)
            {
                yield break;
            }

            if (!tree.TryGetNodeByTag(PawnRenderNodeTagDefOf.Body, out PawnRenderNode bodyNode) || bodyNode == null)
            {
                yield break;
            }

            PawnRenderNodeProperties props = new PawnRenderNodeProperties
            {
                debugLabel = "DiverCloak",
                nodeClass = typeof(PawnRenderNode_DiverCloak),
                workerClass = typeof(PawnRenderNodeWorker_DiverCloak),
                baseLayer = 0f,
                drawSize = new Vector2(1f, 1f),
                parentTagDef = PawnRenderNodeTagDefOf.Body,
                drawData = DrawData.NewWithData(
                    new DrawData.RotationalData(Rot4.South, LayerSouthBelowBody),
                    new DrawData.RotationalData(Rot4.North, LayerNorthBetweenShellAndPack),
                    new DrawData.RotationalData(Rot4.East, LayerEastBetweenApparelAndPackDrone))
            };

            if (!tree.ShouldAddNodeToTree(props))
            {
                yield break;
            }

            yield return (new PawnRenderNode_DiverCloak(pawn, props, tree), bodyNode);
        }
    }
}
