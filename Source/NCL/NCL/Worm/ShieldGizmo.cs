using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Sound;
using static UnityEngine.GraphicsBuffer;

namespace NCL.Worm
{

    [StaticConstructorOnStartup]
    internal class Gizmo_MechNCLWorm_ShieldStatus : Gizmo
    {
        public CompShieldNCLWorm shield;

        private static readonly Texture2D RedShieldBarTex = SolidColorMaterials.NewSolidColorTexture(new Color(1f,0.6f,0.07f));
        private static readonly Texture2D BlueShieldBarTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.39f,0.58f,0.93f));

        private static readonly Texture2D EmptyShieldBarTex = SolidColorMaterials.NewSolidColorTexture(Color.clear);
        public Gizmo_MechNCLWorm_ShieldStatus() => Order = -200f;
        public override float GetWidth(float maxWidth) => 240f;

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            Rect rectBase = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), 75f);//????
            Widgets.DrawWindowBackground(rectBase);//??????
            {
                Widgets.DrawTextureFitted(rectBase, NCLWormTexCommand.ShieldGzimoBase, 1f);
            }


            Rect rect = rectBase.ContractedBy(6f);//??6??


            Rect rectSH = new Rect(rect.x, rect.y, 30, rect.height / 2f);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rectSH, "S.H.".Translate().Resolve());//??????

            Rect rectSHV = new Rect(rect.x+30, rect.y, rect.width-30, rect.height / 2f);
            Rect rectSHVF = rectSHV.ContractedBy(8, 6);
            float fillPercentSH = Mathf.Min(1f, shield.Power / shield.MaxPower);
            Widgets.FillableBar(rectSHVF, fillPercentSH, BlueShieldBarTex, EmptyShieldBarTex, doBorder: false);//??????

            if (true)
            {
                DrawBarDivision(rectSHVF, 0.1f, fillPercentSH);
                DrawBarDivision(rectSHVF, 0.4f, fillPercentSH);
                DrawBarDivision(rectSHVF, 0.7f, fillPercentSH);
            }//??

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            string retime = (shield.Power).ToString("F0") + " / " + (shield.MaxPower).ToString("F0");
            if (shield.shieldState == ShieldState.Resetting)
            {
                retime = "-" + (shield.ticksToReset / 60).ToString("F1");
            }
            Widgets.Label(rectSHVF, retime);//????


            Rect rectHP = rectSH;
            rectHP.y += rectSH.height;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rectHP, "H.P.".Translate().Resolve());//??????

            Rect rectHPV = rectSHV;
            rectHPV.y += rectSHV.height;

            Rect rectHPVF = rectHPV.ContractedBy(8, 6);
            float fillPercentHP = Mathf.Min(1f, GetInyfloat(((Pawn)shield.parent))/(((Pawn)shield.parent).HealthScale*175));
            Widgets.FillableBar(rectHPVF, fillPercentHP, RedShieldBarTex, EmptyShieldBarTex, doBorder: false);//??????

            if (true)
            {
                DrawBarDivision(rectHPVF, 0.1f, fillPercentSH);
                DrawBarDivision(rectHPVF, 0.4f, fillPercentSH);
                DrawBarDivision(rectHPVF, 0.7f, fillPercentSH);
            }//??

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            string HPstring = GetInyfloat(((Pawn)shield.parent)).ToString("F0")+"/"+ (((Pawn)shield.parent).HealthScale*175).ToString("F0");
            Widgets.Label(rectHPVF, HPstring);//????


            Text.Anchor = TextAnchor.UpperLeft;

            return new GizmoResult(GizmoState.Clear);
        }
        private float GetInyfloat(Pawn pawn)
        {
            float ii = pawn.HealthScale*175;
            foreach (Hediff item in pawn.health.hediffSet.hediffs)
            {
                if (item is Hediff_Injury)
                {
                    ii -= item.Severity;
                }
            }

            return ii;
        }
        private void DrawBarDivision(Rect barRect, float threshPct, float fillPercent)
        {
            float num = 5f;
            Rect rect = new Rect(barRect.x + barRect.width * threshPct - (num - 1f), barRect.y, num, barRect.height);
            if (threshPct < fillPercent)
            {
                GUI.color = new Color(0f, 0f, 0f, 0.9f);
            }
            else
            {
                GUI.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            }
            Rect position = rect;
            position.yMax = position.yMin + 4f;
            GUI.DrawTextureWithTexCoords(position, NCLWormTexCommand.NeedUnitDividerTex, new Rect(0f, 0.5f, 1f, 0.5f));
            Rect position2 = rect;
            position2.yMin = position2.yMax - 4f;
            GUI.DrawTextureWithTexCoords(position2, NCLWormTexCommand.NeedUnitDividerTex, new Rect(0f, 0f, 1f, 0.5f));
            Rect position3 = rect;
            position3.yMin = position.yMax;
            position3.yMax = position2.yMin;
            if (position3.height > 0f)
            {
                GUI.DrawTextureWithTexCoords(position3, NCLWormTexCommand.NeedUnitDividerTex, new Rect(0f, 0.4f, 1f, 0.2f));
            }
            GUI.color = Color.white;
        }
    }




}
