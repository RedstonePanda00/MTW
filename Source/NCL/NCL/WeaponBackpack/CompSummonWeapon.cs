// Decompiled with JetBrains decompiler
// Type: Edited_BM_WeaponSummon.CompSummonWeapon
// Assembly: WeaponBackpack, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 3D18544D-2643-4DE5-A8F8-3E0F3B074B2E
// Assembly location: C:\Users\15858\OneDrive\Documents\Sundry\RwModCoop\MTW\1.6\Assemblies\WeaponBackpack.dll

using RimWorld;
using Verse;

#nullable disable
namespace Edited_BM_WeaponSummon
{
  public class CompSummonWeapon : CompAbilityEffect
  {
    public CompProperties_SummonWeapon Props
    {
      get => ((AbilityComp) this).props as CompProperties_SummonWeapon;
    }

    public virtual void Apply(LocalTargetInfo target, LocalTargetInfo dest)
    {
      base.Apply(target, dest);
      ThingWithComps primary = ((AbilityComp) this).parent.pawn.equipment.Primary;
      if (primary != null)
        ((AbilityComp) this).parent.pawn.equipment.TryTransferEquipmentToContainer(primary, (ThingOwner) ((AbilityComp) this).parent.pawn.inventory.innerContainer);
      ThingWithComps thingWithComps = ThingMaker.MakeThing(this.Props.weapon, (ThingDef) null) as ThingWithComps;
      thingWithComps.GetComp<CompSummonedWeapon>().ticksSummoned = Find.TickManager.TicksGame;
      ((AbilityComp) this).parent.pawn.equipment.AddEquipment(thingWithComps);
    }
  }
}
