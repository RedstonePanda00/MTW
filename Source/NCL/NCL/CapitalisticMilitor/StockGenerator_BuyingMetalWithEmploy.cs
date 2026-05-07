// Decompiled with JetBrains decompiler
// Type: NCL.StockGenerator_BuyingMetalWithEmploy
// Assembly: TW_Mech_Capitalistic_Militor, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 19431CE1-8FC8-4A04-970C-DC00BB350104
// Assembly location: C:\Users\15858\OneDrive\Documents\Sundry\RwModCoop\MTW\1.6\Assemblies\TW_Mech_Capitalistic_Militor.dll

using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using Verse;

#nullable disable
namespace NCL
{
  public class StockGenerator_BuyingMetalWithEmploy : StockGenerator
  {
    public override IEnumerable<Thing> GenerateThings(PlanetTile forTile, Faction faction = null)
    {
      yield break;
    }

    public override bool HandlesThingDef(ThingDef t)
    {
      return (t?.stuffProps?.categories?.Contains(StuffCategoryDefOf.Metallic) ?? false) || t == ThingDefOf.Silver;
    }

    public override Tradeability TradeabilityFor(ThingDef thingDef)
    {
      return HandlesThingDef(thingDef) ? Tradeability.All : Tradeability.None;
    }
  }
}
