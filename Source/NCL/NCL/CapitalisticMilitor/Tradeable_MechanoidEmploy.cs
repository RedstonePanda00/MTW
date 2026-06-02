// Decompiled with JetBrains decompiler
// Type: NCL.Tradeable_MechanoidEmploy
// Assembly: TW_Mech_Capitalistic_Militor, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 19431CE1-8FC8-4A04-970C-DC00BB350104
// Assembly location: C:\Users\15858\OneDrive\Documents\Sundry\RwModCoop\MTW\1.6\Assemblies\TW_Mech_Capitalistic_Militor.dll

using RimWorld;
using UnityEngine;
using Verse;

#nullable disable
namespace NCL
{
  public class Tradeable_MechanoidEmploy : Tradeable
  {
    private int _countToTransfer;

    public override bool IsFavor => false;

    public override bool IsCurrency => false;

    public override bool IsThing => false;

    public override Thing AnyThing => (Thing) null;

    public override bool TraderWillTrade => true;

    public override bool Interactive => true;

    public override AcceptanceReport UnderflowReport() => new AcceptanceReport();

    public override AcceptanceReport OverflowReport() => new AcceptanceReport();

    public override string Label
    {
      get
      {
        int transferToSource = ((Transferable) this).CountToTransferToSource;
        int num = transferToSource % 1 * 24;
        return string.Format("雇佣: {0}天{1}时", (object) transferToSource, (object) num);
      }
    }

    public override string TipDescription => "用零部件购买机械单位的服务时间";

    public override int CostToInt(float cost) => Mathf.CeilToInt(cost);

    public override int CountHeldBy(Transactor trans) => trans != Transactor.Trader ? 0 : 99999;

    public override int GetHashCode() => -51;

    public override void ResolveTrade()
    {
      if (this.ActionToDo != TradeAction.PlayerBuys)
        return;
      Pawn trader = TradeSession.trader as Pawn;
      if (trader == null)
        return;
      Log.Message("[NCL] 处理机械雇佣交易...");
      Comp_MechEmployable comp = ((ThingWithComps) trader).GetComp<Comp_MechEmployable>();
      if (comp == null)
      {
        Log.Error("[NCL] 错误：交易对象缺少雇佣组件");
      }
      else
      {
        float transferToSource = (float) ((Transferable) this).CountToTransferToSource;
        float silverAmount = transferToSource * comp.Props.silverPerDay;
        Log.Message(string.Format("[NCL] 雇佣详情 | 天数: {0} | 价值: {1}银", (object) transferToSource, (object) silverAmount));
        comp.Employ(silverAmount);
        if (((Thing) trader).Faction == Faction.OfPlayer)
        {
          Log.Message("[NCL] 验证通过：机械单位已加入玩家阵营");
          Find.Selector.SelectedObjects.Add((object) trader);
        }
        else
          Log.Warning("[NCL] 警告：阵营未变更！");
      }
    }

    public override void ExposeData()
    {
      base.ExposeData();
      Scribe_Values.Look<int>(ref this._countToTransfer, "_countToTransfer", 0, false);
    }
  }
}
