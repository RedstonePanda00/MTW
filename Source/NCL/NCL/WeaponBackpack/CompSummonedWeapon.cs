// Decompiled with JetBrains decompiler
// Type: Edited_BM_WeaponSummon.CompSummonedWeapon
// Assembly: WeaponBackpack, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 3D18544D-2643-4DE5-A8F8-3E0F3B074B2E
// Assembly location: C:\Users\15858\OneDrive\Documents\Sundry\RwModCoop\MTW\1.6\Assemblies\WeaponBackpack.dll

using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

#nullable disable
namespace Edited_BM_WeaponSummon
{
  public class CompSummonedWeapon : ThingComp
  {
    public int ticksSummoned;
    private int lastTicked;

    public CompProperties_SummonedWeapon Props => this.props as CompProperties_SummonedWeapon;

    public virtual void CompTick()
    {
      base.CompTick();
      if (this.lastTicked == Find.TickManager.TicksGame)
        return;
      this.lastTicked = Find.TickManager.TicksGame;
      Pawn_EquipmentTracker parentHolder = ((Thing) this.parent).ParentHolder as Pawn_EquipmentTracker;
      if (Find.TickManager.TicksGame - this.ticksSummoned < this.Props.lifetimeDuration && parentHolder != null)
        return;
      Map mapHeld = ((Thing) this.parent).MapHeld;
      if (mapHeld != null && this.Props.fleckWhenExpired != null)
        FleckMaker.Static(((Thing) this.parent).PositionHeld, mapHeld, this.Props.fleckWhenExpired, 1f);
      ((Thing) this.parent).Destroy((DestroyMode) 0);
      if (parentHolder == null || !(((IEnumerable<Thing>) parentHolder.pawn.inventory.innerContainer).Where<Thing>((Func<Thing, bool>) (x => x.def.IsWeapon)).FirstOrDefault<Thing>() is ThingWithComps thingWithComps))
        return;
      ((Thing) thingWithComps).holdingOwner?.Remove((Thing) thingWithComps);
      parentHolder.AddEquipment(thingWithComps);
    }

    public virtual void PostExposeData()
    {
      base.PostExposeData();
      Scribe_Values.Look<int>(ref this.ticksSummoned, "ticksSummoned", 0, false);
    }
  }
}
