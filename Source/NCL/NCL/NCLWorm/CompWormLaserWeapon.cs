// Decompiled with JetBrains decompiler
// Type: NCLWorm.CompWormLaserWeapon
// Assembly: NCL_WormBoss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B627DDDD-3AC9-4D6A-89F6-EA72F95FB570
// Assembly location: C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3555799437\1.6\Assemblies\NCL_WormBoss (3).dll

using RimWorld;
using UnityEngine;
using Verse;

#nullable disable
namespace NCLWorm
{
  public class CompWormLaserWeapon : ThingComp
  {
    private float _runtimeDamageMultiplier = 1f;

    public CompProperties_WormLaser Props => (CompProperties_WormLaser) this.props;

    public DamageDef DamageType => this.Props.damageDef ?? DamageDefOf.Burn;

    public void SetRuntimeDamageFactor(float factor) => this._runtimeDamageMultiplier = factor;

    public float GetCurrentLimbBreakFactor(float overheatProgress)
    {
      return Mathf.Min(this.Props.baseLimbBreakFactor * (1f + overheatProgress * this.Props.overheatScaleMultiplier) * this._runtimeDamageMultiplier, this.Props.maxLimbBreakFactor);
    }

    public float GetArmorPenetration() => this.Props.armorPenetration;

    public virtual void PostExposeData()
    {
      base.PostExposeData();
      Scribe_Values.Look<float>(ref this._runtimeDamageMultiplier, "runtimeDamageMultiplier", 1f, false);
    }
  }
}
