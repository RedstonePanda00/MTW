// Decompiled with JetBrains decompiler
// Type: NCLWorm.MoteTargetTrackerVisuals
// Assembly: NCL_WormBoss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B627DDDD-3AC9-4D6A-89F6-EA72F95FB570
// Assembly location: C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3555799437\1.6\Assemblies\NCL_WormBoss (3).dll

using UnityEngine;
using Verse;

#nullable disable
namespace NCLWorm
{
  public class MoteTargetTrackerVisuals : DefModExtension
  {
    [NoTranslate]
    public string texCrosshair = "UI/WormBoss/Targeting/Crosshair";
    public float scaleCrosshair = 1f;
    [NoTranslate]
    public string texRing = "UI/WormBoss/Targeting/Ring";
    public float scaleRing = 1.5f;
    public float rotationSpeedRing = -30f;
    [NoTranslate]
    public string texTriangles = "UI/WormBoss/Targeting/Triangles";
    public float scaleTrianglesBase = 1.8f;
    public float scaleTrianglesSwing = 0.2f;
    public float breathSpeed = 5f;
    public Color? customColor;
  }
}
