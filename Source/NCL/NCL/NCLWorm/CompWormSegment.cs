// Decompiled with JetBrains decompiler
// Type: NCLWorm.CompWormSegment
// Assembly: NCL_WormBoss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B627DDDD-3AC9-4D6A-89F6-EA72F95FB570
// Assembly location: C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3555799437\1.6\Assemblies\NCL_WormBoss (3).dll

using RimWorld;
using System;
using UnityEngine;
using Verse;

#nullable disable
namespace NCLWorm
{
  public class CompWormSegment : ThingComp
  {
    private int _lastWindowStartTick = -999;
    private float _maxDamageInWindow = 0.0f;
    private float _accumulatedDamage = 0.0f;
    private int _lastMoteTick = -999;
    private const int MOTE_INTERVAL = 30;

    private WormBody Body => this.parent as WormBody;

    private CompProperties_WormSegment Props => (CompProperties_WormSegment) this.props;

    public virtual void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
    {
      absorbed = true;
      if (!this.IsBodyValid())
        return;
      float hardCappedAmount = this.GetHardCappedAmount(dinfo);
      float armorMultiplier = this.GetArmorMultiplier();
      float gatedDamageDelta = this.GetGatedDamageDelta(hardCappedAmount * armorMultiplier);
      if ((double) gatedDamageDelta <= 0.0099999997764825821)
        return;
      this.TransferDamageToHead(gatedDamageDelta, dinfo, armorMultiplier);
    }

    private bool IsBodyValid()
    {
      return this.Body != null && this.Body.Head != null && !((Thing) this.Body.Head).Destroyed;
    }

    private float GetHardCappedAmount(DamageInfo dinfo)
    {
      return this.Props.excludedDamageDefs != null && this.Props.excludedDamageDefs.Contains(dinfo.Def) ? dinfo.Amount : Mathf.Min(dinfo.Amount, this.Props.maxDamagePerHit);
    }

    private float GetArmorMultiplier()
    {
      return (double) this.Body.VentOpenFactor >= 0.800000011920929 ? 1f : 0.05f;
    }

    private float GetGatedDamageDelta(float currentDamage)
    {
      int ticksGame = Find.TickManager.TicksGame;
      if (ticksGame > this._lastWindowStartTick + this.Props.damageGateWindow)
      {
        this._lastWindowStartTick = ticksGame;
        this._maxDamageInWindow = 0.0f;
      }
      if ((double) currentDamage <= (double) this._maxDamageInWindow)
        return 0.0f;
      float gatedDamageDelta = currentDamage - this._maxDamageInWindow;
      this._maxDamageInWindow = currentDamage;
      return gatedDamageDelta;
    }

    private void TransferDamageToHead(
      float amountToApply,
      DamageInfo originalDinfo,
      float multiplier)
    {
      float attemptedDealt = Mathf.Min(amountToApply, this.Props.maxDamagePerHit);
      if ((double) attemptedDealt <= 0.0099999997764825821)
        return;
      DamageInfo damageInfo;
      // ISSUE: explicit constructor call
      damageInfo = new DamageInfo(originalDinfo.Def, attemptedDealt, 999f, originalDinfo.Angle, originalDinfo.Instigator, (BodyPartRecord) null, originalDinfo.Weapon, (DamageInfo.SourceCategory) 0, originalDinfo.IntendedTarget, true, true, (QualityCategory) 2, true, false);
      try
      {
        if (this.Body.Head == null || ((Thing) this.Body.Head).Destroyed || this.Body.Head.IsDying)
          return;
        int actualDealt = GenMath.RoundRandom(attemptedDealt);
        if (actualDealt < 1 && (double) attemptedDealt > 0.0099999997764825821)
          actualDealt = 1;
        if (actualDealt <= 0)
          return;
        WormHead head = this.Body.Head;
        ((Thing) head).HitPoints = ((Thing) head).HitPoints - actualDealt;
        if (((Thing) this.Body.Head).HitPoints <= 0)
        {
          ((Thing) this.Body.Head).HitPoints = 0;
          ((Thing) this.Body.Head).Kill(new DamageInfo?(damageInfo), (Hediff) null);
        }
        this.ShowDamageFeedback((float) actualDealt, attemptedDealt, multiplier);
      }
      catch (Exception ex)
      {
        Log.ErrorOnce(string.Format("[WormBoss] Critical error transferring damage from segment {0} to head: {1}", (object) this.parent, (object) ex), ((Thing) this.parent).thingIDNumber ^ 19229);
      }
    }

    private void ShowDamageFeedback(float actualDealt, float attemptedDealt, float multiplier)
    {
      this._accumulatedDamage += actualDealt;
      if ((double) this._accumulatedDamage < 1.0)
        return;
      int ticksGame = Find.TickManager.TicksGame;
      if (ticksGame < this._lastMoteTick + 30)
        return;
      Color color = (double) multiplier < 0.10000000149011612 ? Color.gray : Color.Lerp(Color.red, Color.yellow, 0.5f);
      MoteMaker.ThrowText(((Thing) this.parent).DrawPos, ((Thing) this.parent).Map, Mathf.CeilToInt(this._accumulatedDamage).ToString(), color, -1f);
      this._accumulatedDamage = 0.0f;
      this._lastMoteTick = ticksGame;
    }

    public virtual void PostExposeData()
    {
      base.PostExposeData();
      Scribe_Values.Look<int>(ref this._lastWindowStartTick, "lastWindowStartTick", -999, false);
      Scribe_Values.Look<float>(ref this._maxDamageInWindow, "maxDamageInWindow", 0.0f, false);
      Scribe_Values.Look<float>(ref this._accumulatedDamage, "accDamage", 0.0f, false);
    }
  }
}
