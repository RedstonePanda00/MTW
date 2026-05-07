// Decompiled with JetBrains decompiler
// Type: NCL.MechanoidTradeUtility
// Assembly: TW_Mech_Capitalistic_Militor, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 19431CE1-8FC8-4A04-970C-DC00BB350104
// Assembly location: C:\Users\15858\OneDrive\Documents\Sundry\RwModCoop\MTW\1.6\Assemblies\TW_Mech_Capitalistic_Militor.dll

using RimWorld;
using Verse;

#nullable disable
namespace NCL
{
  public class MechanoidTradeUtility
  {
    public static void HandleMechanoidTrade(
      ITrader trader,
      Thing tradedThing,
      Pawn playerNegotiator)
    {
      StuffProperties stuffProps = tradedThing.def.stuffProps;
      if (stuffProps == null || !stuffProps.categories.Contains(StuffCategoryDefOf.Metallic) || !(trader is Pawn pawn))
        return;
      Comp_MechEmployable comp = ((ThingWithComps) pawn).GetComp<Comp_MechEmployable>();
      if (comp != null)
      {
        float silverAmount = tradedThing.MarketValue * (float) tradedThing.stackCount;
        comp.Employ(silverAmount);
        Messages.Message("NCL.TRADE_EMPLOY_SUCCESS".Translate(((Entity)tradedThing).LabelCap, ((Entity)pawn).LabelShort, (silverAmount / 100f).ToString("F1")), MessageTypeDefOf.PositiveEvent, true);
      }
    }
  }
}
