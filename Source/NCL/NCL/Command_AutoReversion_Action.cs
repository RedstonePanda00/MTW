using UnityEngine;
using Verse;

namespace NCL
{
    [StaticConstructorOnStartup]
    public class Command_AutoReversion_Action : Command
    {
        public CompFormChange compFormChange;
        public TransformData transformData;
        private static readonly Texture2D CooldownBarTex =
            SolidColorMaterials.NewSolidColorTexture(new Color32(9, 203, 4, 64));

        public override void ProcessInput(Event ev) => base.ProcessInput(ev);

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            Rect rect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), 75f);
            GizmoResult result = base.GizmoOnGUI(topLeft, maxWidth, parms);
            if (compFormChange?.Props?.revertData != null)
            {
                float num = 1f - Mathf.InverseLerp(
                    compFormChange.Props.revertData.revertAfterTicks,
                    0f,
                    compFormChange.revertTickCounter);
                Widgets.FillableBar(rect, Mathf.Clamp01(num), CooldownBarTex);
                if (compFormChange.cooldownNow > 0)
                {
                    Text.Font = GameFont.Tiny;
                    Text.Anchor = TextAnchor.MiddleCenter;
                    Widgets.Label(rect, GenText.ToStringPercent(num, "F0"));
                    Text.Anchor = TextAnchor.UpperLeft;
                }
            }
            return result;
        }
    }
}
