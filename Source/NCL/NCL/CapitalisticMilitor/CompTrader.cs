// Decompiled with JetBrains decompiler
// Type: NCL.CompTrader
// Assembly: TW_Mech_Capitalistic_Militor, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 19431CE1-8FC8-4A04-970C-DC00BB350104
// Assembly location: C:\Users\15858\OneDrive\Documents\Sundry\RwModCoop\MTW\1.6\Assemblies\TW_Mech_Capitalistic_Militor.dll

using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;

#nullable disable
namespace NCL
{
  public class CompTrader : ThingComp, ITrader
  {
    public Faction Faction
    {
      get => !(this.parent is Pawn parent) ? (Faction) null : ((Thing) parent).Faction;
    }

    public virtual void PostSpawnSetup(bool respawningAfterLoad)
    {
      base.PostSpawnSetup(respawningAfterLoad);
      if (this.TraderPawn == null)
        return;
      this.TraderPawn.playerSettings = new Pawn_PlayerSettings(this.TraderPawn);
    }

    public void StartTradeWithEmploy()
    {
      Pawn pawn = Find.CurrentMap.mapPawns.FreeColonists.FirstOrDefault<Pawn>();
      if (pawn == null)
      {
        Log.Error("没有可用的殖民者作为谈判代表");
      }
      else
      {
        TradeSession.SetupWith((ITrader) this, pawn, false);
        TradeSession.deal = new TradeDeal();
        Tradeable employTradeable = this.Props.CreateEmployTradeable();
        typeof (TradeDeal).GetMethod("AddTradeable", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke((object) TradeSession.deal, new object[1]
        {
          (object) employTradeable
        });
        Find.WindowStack.Add((Window) new Dialog_Trade(pawn, (ITrader) this, false));
      }
    }

    public float TradePriceImprovementOffsetForPlayer => 0.0f;

    public virtual void PostExposeData() => base.PostExposeData();

    public CompProperties_Trader Props => (CompProperties_Trader) this.props;

    public Pawn TraderPawn => this.parent as Pawn;

    public TraderKindDef TraderKind
    {
      get
      {
        if (string.IsNullOrEmpty(this.Props.traderDefName))
        {
          Log.Error("TraderDefName is null or empty in CompTrader!");
          return (TraderKindDef) null;
        }
        TraderKindDef namedSilentFail = DefDatabase<TraderKindDef>.GetNamedSilentFail(this.Props.traderDefName);
        if (namedSilentFail == null)
          Log.Error("TraderKindDef '" + this.Props.traderDefName + "' not found!");
        return namedSilentFail;
      }
    }

    public IEnumerable<Thing> Goods
    {
      get
      {
        yield return new Thing()
        {
          def = ThingDefOf.Silver,
          stackCount = 10000
        };
      }
    }

    public int RandomPriceFactorSeed
    {
      get => Gen.HashCombineInt(((Thing) this.TraderPawn).thingIDNumber, 1149275593);
    }

    public string TraderName => ((Entity) this.TraderPawn)?.LabelShortCap ?? "机械单位";

    public bool CanTradeNow => this.TraderPawn != null && ((Thing) this.TraderPawn).Spawned;

    public TradeCurrency TradeCurrency => (TradeCurrency) 0;

    public IEnumerable<Thing> ColonyThingsWillingToBuy(Pawn playerNegotiator)
    {
      if (((Thing) playerNegotiator)?.Map != null)
      {
        foreach (Thing thing in ((Thing) playerNegotiator).Map.listerThings.AllThings)
        {
          StuffProperties stuffProps = thing.def.stuffProps;
          if (stuffProps != null && stuffProps.categories.Contains(StuffCategoryDefOf.Metallic))
          {
            if (StoreUtility.IsInAnyStorage(thing) || ((Area) ((Thing) playerNegotiator).Map.areaManager.Home)[thing.Position])
              yield return thing;
          }
          else if (thing.def == ThingDefOf.Silver && (StoreUtility.IsInAnyStorage(thing) || ((Area) ((Thing) playerNegotiator).Map.areaManager.Home)[thing.Position]))
            yield return thing;
        }
      }
    }

    public void GiveSoldThingToTrader(Thing toGive, int countToGive, Pawn playerNegotiator)
    {
      StuffProperties stuffProps = toGive.def.stuffProps;
      if (stuffProps != null && stuffProps.categories.Contains(StuffCategoryDefOf.Metallic))
      {
        Comp_MechEmployable comp = this.parent is Pawn parent ? ((ThingWithComps) parent).GetComp<Comp_MechEmployable>() : (Comp_MechEmployable) null;
        if (comp != null)
        {
          float silverAmount = toGive.MarketValue * (float) countToGive;
          comp.Employ(silverAmount);
          Messages.Message(string.Format("已用 {0} 雇佣 {1} {2:F1}天", (object) ((Entity) toGive).LabelCap, (object) ((Entity) this.parent).LabelShort, (object) (float) ((double) silverAmount / (double) comp.Props.silverPerDay)), MessageTypeDefOf.PositiveEvent, true);
        }
      }
      toGive.SplitOff(countToGive).Destroy((DestroyMode) 0);
    }

    public void GiveSoldThingToPlayer(Thing toGive, int countToGive, Pawn playerNegotiator)
    {
    }
  }
}
