// Decompiled with JetBrains decompiler
// Type: NyarsModPackOne.DroneCarrierGizmo
// Assembly: NyearsModPackOne, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: FDB00AC3-5462-4449-9639-A371FA2E00F3
// Assembly location: C:\Users\15858\OneDrive\Documents\Sundry\RwModCoop\MTW\1.6\Assemblies\NyearsModPackOne.dll

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Verse;

#nullable disable
namespace NyarsModPackOne
{
  [StaticConstructorOnStartup]
  public class DroneCarrierGizmo : Gizmo
  {
    private CompDroneCarrier carrier;
    private float lastTargetValue;
    private float targetValue;
    private static bool draggingBar;
    private List<float> bandPercentages;
    private static readonly Texture2D BarTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.34f, 0.42f, 0.43f));
    private static readonly Texture2D BarHighlightTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.43f, 0.54f, 0.55f));
    private static readonly Texture2D EmptyBarTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.03f, 0.035f, 0.05f));
    private static readonly Texture2D DragBarTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.74f, 0.97f, 0.8f));
    private const int Increments = 24;
    private const float Width = 160f;

    public DroneCarrierGizmo(CompDroneCarrier carrier)
    {
      this.carrier = carrier;
      this.targetValue = (float) carrier.maxToFill / (float) carrier.Props.maxIngredientCount;
      this.bandPercentages = new List<float>();
      int maxIngredientCount = carrier.Props.maxIngredientCount;
      if (maxIngredientCount >= 50)
      {
        int num1 = 50;
        int num2 = maxIngredientCount / num1;
        for (int index = 0; index <= num2; ++index)
          this.bandPercentages.Add(Mathf.Clamp01((float) (index * num1) / (float) maxIngredientCount));
      }
      else
      {
        this.bandPercentages = new List<float>();
        int num = 12;
        for (int index = 0; index <= num; ++index)
          this.bandPercentages.Add((float) index / (float) num);
      }
    }

    public virtual float GetWidth(float maxWidth) => 160f;

    public virtual GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
    {
      Rect rect1;
      // ISSUE: explicit constructor call
      ((Rect) ref rect1).\u002Ector(topLeft.x, topLeft.y, 160f, 75f);
      Rect rect2 = GenUI.ContractedBy(rect1, 10f);
      Widgets.DrawWindowBackground(rect1);
      Verse.Text.Font = (GameFont) 1;
      string str1 = TaggedString.op_Implicit(((Def) this.carrier.Props.fixedIngredient).LabelCap);
      float num1 = Verse.Text.CalcHeight(str1, ((Rect) ref rect2).width);
      Rect rect3;
      // ISSUE: explicit constructor call
      ((Rect) ref rect3).\u002Ector(((Rect) ref rect2).x, ((Rect) ref rect2).y, ((Rect) ref rect2).width, num1);
      Verse.Text.Anchor = (TextAnchor) 3;
      Widgets.Label(rect3, str1);
      Verse.Text.Anchor = (TextAnchor) 0;
      this.lastTargetValue = this.targetValue;
      float num2 = ((Rect) ref rect2).height - ((Rect) ref rect3).height;
      float num3 = num2 - 4f;
      float num4 = (float) (((double) num2 - (double) num3) / 2.0);
      Rect rect4;
      // ISSUE: explicit constructor call
      ((Rect) ref rect4).\u002Ector(((Rect) ref rect2).x, ((Rect) ref rect3).yMax + num4, ((Rect) ref rect2).width, num3);
      Widgets.DraggableBar(rect4, DroneCarrierGizmo.BarTex, DroneCarrierGizmo.BarHighlightTex, DroneCarrierGizmo.EmptyBarTex, DroneCarrierGizmo.DragBarTex, ref DroneCarrierGizmo.draggingBar, this.carrier.FillPercentage, ref this.targetValue, (IEnumerable<float>) this.bandPercentages, 24, 0.0f, 1f);
      Verse.Text.Anchor = (TextAnchor) 4;
      ref Rect local = ref rect4;
      ((Rect) ref local).y = ((Rect) ref local).y - 2f;
      string str2 = string.Format("{0} / {1} ", (object) this.carrier.IngredientCount, (object) this.carrier.Props.maxIngredientCount);
      Widgets.Label(rect4, str2);
      Verse.Text.Anchor = (TextAnchor) 0;
      TooltipHandler.TipRegion(rect4, (Func<string>) (() => this.GetResourceBarTip()), Gen.HashCombineInt(((object) this.carrier).GetHashCode(), 34242369));
      if ((double) this.lastTargetValue != (double) this.targetValue)
        this.carrier.maxToFill = Mathf.RoundToInt(this.targetValue * (float) this.carrier.Props.maxIngredientCount);
      return new GizmoResult((GizmoState) 0);
    }

    private string GetResourceBarTip() => new StringBuilder().ToString();

    private void DrawTickMarks(Rect rect)
    {
      GUI.color = new Color(1f, 1f, 1f, 0.5f);
      float num = 5f;
      foreach (float bandPercentage in this.bandPercentages)
      {
        if ((double) bandPercentage > 0.0 && (double) bandPercentage < 1.0)
          Widgets.DrawLineVertical(((Rect) ref rect).x + ((Rect) ref rect).width * bandPercentage, ((Rect) ref rect).y, num);
      }
      GUI.color = Color.white;
    }
  }
}
