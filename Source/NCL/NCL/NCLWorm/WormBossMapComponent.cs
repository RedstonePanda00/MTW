// Decompiled with JetBrains decompiler
// Type: NCLWorm.WormBossMapComponent
// Assembly: NCL_WormBoss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B627DDDD-3AC9-4D6A-89F6-EA72F95FB570
// Assembly location: C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3555799437\1.6\Assemblies\NCL_WormBoss (3).dll

using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

#nullable disable
namespace NCLWorm
{
  [StaticConstructorOnStartup]
  public class WormBossMapComponent : MapComponent
  {
    public List<WormHead> activeBosses = new List<WormHead>();
    private List<WormProbe> _activeProbes = new List<WormProbe>();
    public const int MAX_PROBE_COUNT = 30;
    private static Texture2D _barLeftTex;
    private static Texture2D _barMidTex;
    private static Texture2D _barRightTex;
    private static Texture2D _barBgTex;
    private static Texture2D _barFillTex;
    private const float UI_BASE_HEIGHT = 64f;
    private const float UI_DESIRED_TOTAL_WIDTH = 600f;
    private const float UI_Y_POS = 80f;
    private const float FILL_OFFSET_LEFT = 54f;
    private const float FILL_OFFSET_RIGHT = 18f;
    private const float FILL_OFFSET_TOP = 25f;
    private const float FILL_OFFSET_BOTTOM = 18f;

    private static Texture2D BarLeftTex
    {
      get
      {
        return WormBossMapComponent._barLeftTex ?? (WormBossMapComponent._barLeftTex = ContentFinder<Texture2D>.Get("WormBoss/BarLeft", false));
      }
    }

    private static Texture2D BarMidTex
    {
      get
      {
        return WormBossMapComponent._barMidTex ?? (WormBossMapComponent._barMidTex = ContentFinder<Texture2D>.Get("WormBoss/BarMiddle", false));
      }
    }

    private static Texture2D BarRightTex
    {
      get
      {
        return WormBossMapComponent._barRightTex ?? (WormBossMapComponent._barRightTex = ContentFinder<Texture2D>.Get("WormBoss/BarRight", false));
      }
    }

    private static Texture2D BarBgTex
    {
      get
      {
        return WormBossMapComponent._barBgTex ?? (WormBossMapComponent._barBgTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.2f, 0.2f, 0.2f, 0.8f)));
      }
    }

    private static Texture2D BarFillTex
    {
      get
      {
        return WormBossMapComponent._barFillTex ?? (WormBossMapComponent._barFillTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.8f, 0.1f, 0.1f, 1f)));
      }
    }

    public WormBossMapComponent(Map map)
      : base(map)
    {
    }

    public virtual void MapComponentTick()
    {
      base.MapComponentTick();
      if (Find.TickManager.TicksGame % 120 != 0)
        return;
      this.ValidateLists();
    }

    private void ValidateLists()
    {
      this.activeBosses.RemoveAll((Predicate<WormHead>) (x => x == null || ((Thing) x).Destroyed));
      this._activeProbes.RemoveAll((Predicate<WormProbe>) (x => x == null || ((Thing) x).Destroyed || !((Thing) x).Spawned));
    }

    public bool CanSpawnMoreProbes() => this._activeProbes.Count < 30;

    public void RegisterProbe(WormProbe probe)
    {
      if (this._activeProbes.Contains(probe))
        return;
      this._activeProbes.Add(probe);
    }

    public void DeregisterProbe(WormProbe probe) => this._activeProbes.Remove(probe);

    public WormHead GetClosestBoss(Vector3 pos)
    {
      if (this.activeBosses.Count == 0)
        return (WormHead) null;
      WormHead closestBoss = (WormHead) null;
      float num = float.MaxValue;
      foreach (WormHead activeBoss in this.activeBosses)
      {
        if (activeBoss != null && !((Thing) activeBoss).Destroyed)
        {
          Vector3 vector3 = (activeBoss.ExactPosition - pos);
          float sqrMagnitude = vector3.sqrMagnitude;
          if ((double) sqrMagnitude < (double) num)
          {
            num = sqrMagnitude;
            closestBoss = activeBoss;
          }
        }
      }
      return closestBoss;
    }

    public void RegisterBoss(WormHead boss)
    {
      if (this.activeBosses.Contains(boss))
        return;
      this.activeBosses.Add(boss);
    }

    public void DeregisterBoss(WormHead boss) => this.activeBosses.Remove(boss);

    public virtual void MapComponentOnGUI()
    {
      base.MapComponentOnGUI();
      if (this.activeBosses.Count == 0)
        return;
      this.ValidateLists();
      float yPos = 80f;
      foreach (WormHead activeBoss in this.activeBosses)
      {
        if (activeBoss != null && !((Thing) activeBoss).Destroyed && ((Thing) activeBoss).Spawned)
        {
          this.DrawBossHealthBar(activeBoss, yPos);
          yPos += 68f;
        }
      }
    }

    private void DrawBossHealthBar(WormHead boss, float yPos)
    {
      float screenWidth = (float) UI.screenWidth;
      float hpPct = Mathf.Clamp01((float) ((Thing) boss).HitPoints / (float) ((Thing) boss).MaxHitPoints);
      string label = ((Entity) boss).LabelCap + " - " + GenText.ToStringPercent(hpPct);
      if (WormBossMapComponent.BarLeftTex == null || WormBossMapComponent.BarMidTex == null || WormBossMapComponent.BarRightTex == null)
      {
        this.DrawFallbackHealthBar(screenWidth, hpPct, label, yPos);
      }
      else
      {
        float num1 = 64f / (float) ((Texture) WormBossMapComponent.BarLeftTex).height;
        float num2 = (float) ((Texture) WormBossMapComponent.BarLeftTex).width * num1;
        float num3 = (float) ((Texture) WormBossMapComponent.BarRightTex).width * num1;
        float num4 = Mathf.Max(600f - num2 - num3, num2);
        float num5 = num2 + num4 + num3;
        float num6 = (float) (((double) screenWidth - (double) num5) / 2.0);
        Rect rect1;
        // ISSUE: explicit constructor call
        rect1 = new Rect(num6, yPos, num2, 64f);
        Rect rect2;
        // ISSUE: explicit constructor call
        rect2 = new Rect(num6 + num2, yPos, num4, 64f);
        Rect rect3;
        // ISSUE: explicit constructor call
        rect3 = new Rect(num6 + num2 + num4, yPos, num3, 64f);
        GUI.DrawTexture(rect1, (Texture) WormBossMapComponent.BarLeftTex);
        GUI.DrawTexture(rect2, (Texture) WormBossMapComponent.BarMidTex);
        GUI.DrawTexture(rect3, (Texture) WormBossMapComponent.BarRightTex);
        float num7 = (float) ((double) num5 - 54.0 - 18.0) * hpPct;
        Rect rect4;
        // ISSUE: explicit constructor call
        rect4 = new Rect(num6 + 54f, yPos + 25f, num7, 21f);
        GUI.DrawTexture(rect4, (Texture) WormBossMapComponent.BarFillTex);
        this.DrawTextWithShadow(rect2, label);
      }
    }

    private void DrawFallbackHealthBar(float screenWidth, float hpPct, string label, float yPos)
    {
      float num1 = 600f;
      float num2 = 24f;
      Rect rect1 = new Rect((float)((screenWidth - num1) / 2.0), yPos, num1, num2);
      Rect rect2 = rect1;
      rect2.width *= hpPct;
      GUI.DrawTexture(rect1, BarBgTex);
      GUI.DrawTexture(rect2, BarFillTex);
      DrawTextWithShadow(rect1, label);
    }

    private void DrawTextWithShadow(Rect rect, string label)
    {
      Text.Font = GameFont.Medium;
      Text.Anchor = TextAnchor.MiddleCenter;
      Rect shadowRect = rect;
      shadowRect.x += 1f;
      shadowRect.y += 1f;
      GUI.color = Color.black;
      Widgets.Label(shadowRect, label);
      GUI.color = Color.white;
      Widgets.Label(rect, label);
      Text.Anchor = TextAnchor.UpperLeft;
      Text.Font = GameFont.Small;
    }
  }
}
