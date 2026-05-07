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

    public virtual bool IsFavor => false;

    public virtual bool IsCurrency => false;

    public virtual bool IsThing => false;

    public virtual Thing AnyThing => (Thing) null;

    public virtual bool TraderWillTrade => true;

    public virtual bool Interactive => true;

    public virtual AcceptanceReport UnderflowReport() => new AcceptanceReport();

    public virtual AcceptanceReport OverflowReport() => new AcceptanceReport();

    public virtual string Label
    {
      get
      {
        int transferToSource = ((Transferable) this).CountToTransferToSource;
        int num = transferToSource % 1 * 24;
        return string.Format("雇佣: {0}天{1}时", (object) transferToSource, (object) num);
      }
    }

    public virtual string TipDescription => "用零部件购买机械单位的服务时间";

    public virtual int CostToInt(float cost) => Mathf.CeilToInt(cost);

    public override int CountHeldBy(Transactor trans) => trans != Transactor.Trader ? 0 : 99999;

    public virtual int GetHashCode() => -51;

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

    public virtual void ExposeData()
    {
      base.ExposeData();
      Scribe_Values.Look<int>(ref this._countToTransfer, "_countToTransfer", 0, false);
    }
  }
}
