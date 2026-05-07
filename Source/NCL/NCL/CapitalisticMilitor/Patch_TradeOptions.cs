// Decompiled with JetBrains decompiler
// Type: Patch_TradeOptions
// Assembly: TW_Mech_Capitalistic_Militor, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 19431CE1-8FC8-4A04-970C-DC00BB350104
// Assembly location: C:\Users\15858\OneDrive\Documents\Sundry\RwModCoop\MTW\1.6\Assemblies\TW_Mech_Capitalistic_Militor.dll

using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;

#nullable disable
namespace NCL
{
  [HarmonyPatch(typeof(Pawn), "GetFloatMenuOptions")]
  public static class Patch_TradeOptions
  {
    public static void Postfix(Pawn __instance, ref IEnumerable<FloatMenuOption> __result)
    {
      CompTrader trader = ThingCompUtility.TryGetComp<CompTrader>((Thing)__instance);
      if (trader == null || !trader.CanTradeNow)
        return;
      __result = __result.Concat<FloatMenuOption>((IEnumerable<FloatMenuOption>)new FloatMenuOption[1]
      {
        new FloatMenuOption("与机械单位交易", (Action)(() => Patch_TradeOptions.StartCustomTrade(trader)), (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0.0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0)
      });
    }

    private static void StartCustomTrade(CompTrader trader)
    {
      if (trader.TraderKind == null)
      {
        Log.Error("[NCL] Aborting trade: TraderKind is null.");
      }
      else
      {
        Pawn pawn = Find.CurrentMap.mapPawns.FreeColonists.FirstOrDefault<Pawn>();
        if (pawn == null)
        {
          Log.Error("[NCL] No available colonist to negotiate");
        }
        else
        {
          TradeSession.SetupWith((ITrader)trader, pawn, false);
          TradeSession.deal = new TradeDeal();
          Tradeable_MechanoidEmploy tradeableMechanoidEmploy = new Tradeable_MechanoidEmploy();
          typeof(TradeDeal).GetMethod("AddTradeable", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke((object)TradeSession.deal, new object[1]
          {
            (object)tradeableMechanoidEmploy
          });
          Find.WindowStack.Add((Window)new Dialog_Trade(pawn, (ITrader)trader, false));
        }
      }
    }

    private static void InitTradeDeal(CompTrader trader)
    {
      if (TradeSession.deal == null)
        TradeSession.deal = new TradeDeal();
      try
      {
        Tradeable_MechanoidEmploy tradeableMechanoidEmploy = new Tradeable_MechanoidEmploy();
        typeof(TradeDeal).GetMethod("AddTradeable", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke((object)TradeSession.deal, new object[1]
        {
          (object)tradeableMechanoidEmploy
        });
        typeof(TradeDeal).GetMethod("UpdateTradeable", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke((object)TradeSession.deal, (object[])null);
      }
      catch (Exception ex)
      {
        Log.Error(string.Format("Failed to initialize trade deal: {0}", (object)ex));
      }
    }
  }
}
