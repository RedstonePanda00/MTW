// Decompiled with JetBrains decompiler
// Type: NCL.CompProperties_Trader
// Assembly: TW_Mech_Capitalistic_Militor, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 19431CE1-8FC8-4A04-970C-DC00BB350104
// Assembly location: C:\Users\15858\OneDrive\Documents\Sundry\RwModCoop\MTW\1.6\Assemblies\TW_Mech_Capitalistic_Militor.dll

using RimWorld;
using Verse;

#nullable disable
namespace NCL
{
  public class CompProperties_Trader : CompProperties
  {
    public string traderDefName;

    public virtual Tradeable CreateEmployTradeable() => (Tradeable) new Tradeable_MechanoidEmploy();

    public CompProperties_Trader() => this.compClass = typeof (CompTrader);
  }
}
