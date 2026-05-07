// Decompiled with JetBrains decompiler
// Type: Edited_BM_WeaponSummon.CompProperties_SummonedWeapon
// Assembly: WeaponBackpack, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 3D18544D-2643-4DE5-A8F8-3E0F3B074B2E
// Assembly location: C:\Users\15858\OneDrive\Documents\Sundry\RwModCoop\MTW\1.6\Assemblies\WeaponBackpack.dll

using Verse;

#nullable disable
namespace Edited_BM_WeaponSummon
{
  public class CompProperties_SummonedWeapon : CompProperties
  {
    public int lifetimeDuration;
    public FleckDef fleckWhenExpired;

    public CompProperties_SummonedWeapon() => this.compClass = typeof (CompSummonedWeapon);
  }
}
