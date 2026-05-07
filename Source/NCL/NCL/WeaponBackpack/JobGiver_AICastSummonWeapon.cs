// Decompiled with JetBrains decompiler
// Type: Edited_BM_WeaponSummon.JobGiver_AICastSummonWeapon
// Assembly: WeaponBackpack, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 3D18544D-2643-4DE5-A8F8-3E0F3B074B2E
// Assembly location: C:\Users\15858\OneDrive\Documents\Sundry\RwModCoop\MTW\1.6\Assemblies\WeaponBackpack.dll

using RimWorld;
using Verse;

#nullable disable
namespace Edited_BM_WeaponSummon
{
  public class JobGiver_AICastSummonWeapon : JobGiver_AICastAbility
  {
    protected override LocalTargetInfo GetTarget(Pawn caster, Ability ability)
    {
      ThingWithComps primary = caster.equipment?.Primary;
      return primary != null && primary.GetComp<CompSummonedWeapon>() == null ? new LocalTargetInfo(caster) : LocalTargetInfo.Invalid;
    }
  }
}
