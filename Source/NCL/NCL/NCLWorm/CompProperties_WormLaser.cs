// Decompiled with JetBrains decompiler
// Type: NCLWorm.CompProperties_WormLaser
// Assembly: NCL_WormBoss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B627DDDD-3AC9-4D6A-89F6-EA72F95FB570
// Assembly location: C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3555799437\1.6\Assemblies\NCL_WormBoss (3).dll

using UnityEngine;
using Verse;

#nullable disable
namespace NCLWorm
{
  public class CompProperties_WormLaser : CompProperties
  {
    public DamageDef damageDef;
    public float armorPenetration = 0.9f;
    public float baseLimbBreakFactor = 0.1f;
    public float overheatScaleMultiplier = 3f;
    public float maxLimbBreakFactor = 0.8f;
    public float normalWidth = 2f;
    public float overheatWidth = 6f;
    public Color normalColor = Color.cyan;
    public Color overheatColor = Color.red;
    public SimpleCurve overheatCurve;

    public CompProperties_WormLaser() => this.compClass = typeof (CompWormLaserWeapon);
  }
}
