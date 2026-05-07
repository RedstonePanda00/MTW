// Decompiled with JetBrains decompiler
// Type: NCL.Comp_MechEmployable
// Assembly: TW_Mech_Capitalistic_Militor, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 19431CE1-8FC8-4A04-970C-DC00BB350104
// Assembly location: C:\Users\15858\OneDrive\Documents\Sundry\RwModCoop\MTW\1.6\Assemblies\TW_Mech_Capitalistic_Militor.dll

using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

#nullable disable
namespace NCL
{
  public class Comp_MechEmployable : ThingComp
  {
    public int employmentTicks = -1;
    private Dictionary<Thing, float> enemyRecords = new Dictionary<Thing, float>();

    public CompProperties_MechEmployable Props => (CompProperties_MechEmployable) this.props;

    public void Employ(float silverAmount)
    {
      try
      {
        if (!(this.parent is Pawn parent))
          return;
        int num = (int) ((double) silverAmount / (double) this.Props.silverPerDay * 60000.0);
        Log.Message("[NCL] 开始雇佣流程 | 当前阵营: " + (((Thing) parent).Faction?.Name ?? "null"));
        if (this.employmentTicks <= 0)
        {
          ((Thing) parent).SetFactionDirect(Faction.OfPlayer);
          parent.playerSettings = new Pawn_PlayerSettings(parent);
          Log.Message("[NCL] 阵营已设置为: " + (((Thing) parent).Faction?.Name ?? "null"));
        }
        this.employmentTicks += num;
        parent.jobs.EndCurrentJob((JobCondition) 16, true, true);
        parent.mindState.Reset(true, true);
        Log.Message(string.Format("[NCL] 雇佣成功 | 剩余ticks: {0}", (object) this.employmentTicks));
      }
      catch (Exception ex)
      {
        Log.Error(string.Format("[NCL] 雇佣错误: {0}", (object) ex));
      }
    }

    public override void CompTick()
    {
      base.CompTick();
      if (this.employmentTicks <= 0)
        return;
      this.employmentTicks--;
      if (this.employmentTicks != 0)
        return;
      if (!(this.parent is Pawn pawn))
        return;
      pawn.SetFaction(null, null);
      Messages.Message("NCL.EMPLOYMENT_EXPIRED".Translate(pawn.LabelShortCap), MessageTypeDefOf.NeutralEvent, true);
    }

    public void RecordDamage(DamageInfo dinfo)
    {
      Dictionary<Thing, float> safeEnemyRecords = this.SafeEnemyRecords;
      if (dinfo.Instigator == null)
        return;
      if (safeEnemyRecords.ContainsKey(dinfo.Instigator))
        safeEnemyRecords[dinfo.Instigator] += dinfo.Amount;
      else
        safeEnemyRecords.Add(dinfo.Instigator, dinfo.Amount);
    }

    public override string CompInspectStringExtra()
    {
      return this.employmentTicks > 0
        ? "NCL.EMPLOYMENT_REMAINING".Translate(GenDate.ToStringTicksToPeriod(this.employmentTicks, true, false, true, true, false), this.SafeEnemyRecords.Count.ToString())
        : null;
    }

    private Dictionary<Thing, float> SafeEnemyRecords
    {
      get
      {
        if (this.enemyRecords == null)
          this.enemyRecords = new Dictionary<Thing, float>();
        return this.enemyRecords;
      }
    }

    public override void PostPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
    {
      if (dinfo.Def.harmsHealth)
        this.RecordDamage(dinfo);
      base.PostPostApplyDamage(dinfo, totalDamageDealt);
    }

    public override void PostExposeData()
    {
      base.PostExposeData();
      Scribe_Values.Look<int>(ref this.employmentTicks, "employmentTicks", -1, false);
      List<Thing> thingList = new List<Thing>();
      List<float> floatList = new List<float>();
      if (Scribe.mode == LoadSaveMode.Saving)
      {
        thingList = this.enemyRecords.Keys.ToList<Thing>();
        floatList = this.enemyRecords.Values.ToList<float>();
      }
      Scribe_Collections.Look(ref this.enemyRecords, "enemyRecords", LookMode.Reference, LookMode.Value, ref thingList, ref floatList, true, false, false);
      if (Scribe.mode != LoadSaveMode.LoadingVars || this.enemyRecords != null)
        return;
      this.enemyRecords = new Dictionary<Thing, float>();
    }
  }
}
