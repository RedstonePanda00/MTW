using System;
using UnityEngine;
using Verse;

namespace NCL
{
    [StaticConstructorOnStartup]
    public class Command_Transform_Action : Command
    {
        public Action action;
        public CompFormChange compFormChange;
        public TransformData transformData;
        private static readonly Texture2D CooldownBarTex =
            SolidColorMaterials.NewSolidColorTexture(new Color32(9, 203, 4, 64));

        public override void ProcessInput(Event ev)
        {
            base.ProcessInput(ev);
            action?.Invoke();
        }

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            Rect rect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), 75f);
            GizmoResult result = base.GizmoOnGUI(topLeft, maxWidth, parms);
            if (compFormChange != null && compFormChange.cooldownNow > 0 && compFormChange.cooldownMax > 0)
            {
                float fill = Mathf.InverseLerp(compFormChange.cooldownMax, 0f, compFormChange.cooldownNow);
                Widgets.FillableBar(rect, Mathf.Clamp01(fill), CooldownBarTex);
            }
            return result;
        }
    }
}
