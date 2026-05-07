// Decompiled with JetBrains decompiler
// Type: NCLWorm.TC_WormWeaponController
// Assembly: NCL_WormBoss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B627DDDD-3AC9-4D6A-89F6-EA72F95FB570
// Assembly location: C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3555799437\1.6\Assemblies\NCL_WormBoss (3).dll

using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

#nullable disable
namespace NCLWorm
{
  public class TC_WormWeaponController : ThingComp, IVerbOwner
  {
    private VerbTracker _verbTracker;
    private int _cooldownTicksLeft = 0;
    private bool _hasCompletedBurst = true;
    private WeaponIntent _currentIntent = WeaponIntent.Stop;
    private Verb _lastFiredVerb;
    private Dictionary<WormVerbTag, Verb> _verbCache = new Dictionary<WormVerbTag, Verb>();
    private bool _cacheDirty = true;

    public TCP_WormWeaponController Props => (TCP_WormWeaponController) this.props;

    public VerbTracker VerbTracker
    {
      get => this._verbTracker ?? (this._verbTracker = new VerbTracker((IVerbOwner) this));
    }

    public List<Verse.VerbProperties> VerbProperties
    {
      get => this.Props.verbs ?? ((Thing) this.parent).def.Verbs;
    }

    public List<Tool> Tools => (List<Tool>) null;

    public Thing ConstantCaster => (Thing) this.parent;

    public string UniqueVerbOwnerID() => ((Thing) this.parent).ThingID + "_WormWeapon";

    public bool VerbsStillUsableBy(Pawn p) => false;

    public ImplementOwnerTypeDef ImplementOwnerTypeDef => ImplementOwnerTypeDefOf.NativeVerb;

    public bool IsOverheating { get; private set; } = false;

    public float OverheatProgress { get; private set; } = 0.0f;

    public bool IsBurstCompleted => this._hasCompletedBurst;

    public void SetIntent(WeaponIntent intent) => this._currentIntent = intent;

    public void ForceStop()
    {
      this._currentIntent = WeaponIntent.Stop;
      Verb primaryVerb = this.VerbTracker.PrimaryVerb;
      if (primaryVerb != null && primaryVerb.state > 0)
        primaryVerb.Reset();
      this.IsOverheating = false;
      this.OverheatProgress = 0.0f;
      this._hasCompletedBurst = true;
      this._cooldownTicksLeft = 0;
    }

    public void ResetCombatStatus()
    {
      this._hasCompletedBurst = false;
      if (this._currentIntent.Mode == 0)
        return;
      this.GetVerbByTag(this._currentIntent.Tag)?.Reset();
    }

    public void SyncOverheatData(float progress, bool isOverheating)
    {
      this.OverheatProgress = progress;
      this.IsOverheating = isOverheating;
    }

    public virtual void CompTick()
    {
      base.CompTick();
      if (!this.IsCasterInBoundsForWeaponLogic())
      {
        this.ForceStop();
      }
      else
      {
        int num;
        if (this._currentIntent.Mode == FireMode.CeaseFire && this._cooldownTicksLeft <= 0 && (double) this.OverheatProgress <= 1.0 / 1000.0)
        {
          Verb primaryVerb = this.VerbTracker.PrimaryVerb;
          num = primaryVerb != null ? (primaryVerb.state != VerbState.Bursting ? 1 : 0) : 1;
        }
        else
          num = 0;
        if (num != 0)
          return;
        this.VerbTracker.VerbsTick();
        if (this.HandleCooldown())
          return;
        this.HandleIntent();
        this.UpdatePassiveCooling();
      }
    }

    private bool HandleCooldown()
    {
      if (this._cooldownTicksLeft <= 0)
        return false;
      --this._cooldownTicksLeft;
      this.UpdatePassiveCooling();
      return true;
    }

    private void HandleIntent()
    {
      if (this._currentIntent.Mode == FireMode.CeaseFire)
      {
        Verb primaryVerb = this.VerbTracker.PrimaryVerb;
        if (primaryVerb == null || primaryVerb.state != VerbState.Bursting)
          ;
      }
      else
      {
        if (!this._currentIntent.Target.IsValid)
          return;
        Map map = ((Thing) this.parent).Map;
        if (map == null || (this._currentIntent.Target.HasThing ? !TC_WormWeaponController.IsThingTargetValidInMap(this._currentIntent.Target.Thing, map) : !GenGrid.InBounds(this._currentIntent.Target.Cell, map)))
          return;
        Verb verbByTag = this.GetVerbByTag(this._currentIntent.Tag);
        if (verbByTag != null && verbByTag.state != VerbState.Bursting && verbByTag.Available())
        {
          this._hasCompletedBurst = false;
          if (verbByTag.TryStartCastOn(this._currentIntent.Target, false, true, false, false))
            this._lastFiredVerb = verbByTag;
        }
      }
    }

    private bool IsCasterInBoundsForWeaponLogic()
    {
      Map map = ((Thing) this.parent).Map;
      if (map == null || !((Thing) this.parent).Spawned)
        return false;
      return this.parent is WormThingBase parent ? GenGrid.InBounds(IntVec3Utility.ToIntVec3(parent.ExactPosition), map) : GenGrid.InBounds(((Thing) this.parent).Position, map);
    }

    private static bool IsThingTargetValidInMap(Thing target, Map casterMap)
    {
      return target != null && !target.Destroyed && target.Spawned && target.Map == casterMap && GenGrid.InBounds(target.Position, casterMap);
    }

    private void UpdatePassiveCooling()
    {
      Verb primaryVerb = this.VerbTracker.PrimaryVerb;
      if (primaryVerb != null && primaryVerb.state == VerbState.Bursting)
        return;
      this.OverheatProgress = Mathf.MoveTowards(this.OverheatProgress, 0.0f, 0.01f);
      this.IsOverheating = (double) this.OverheatProgress > 0.5;
    }

    private Verb GetVerbByTag(WormVerbTag tag)
    {
      if (this._cacheDirty || this._verbCache.Count == 0)
        this.RebuildVerbCache();
      Verb verbByTag;
      if (this._verbCache.TryGetValue(tag, out verbByTag))
        return verbByTag;
      return tag == WormVerbTag.Default ? this.VerbTracker.PrimaryVerb : (Verb) null;
    }

    private void RebuildVerbCache()
    {
      this._verbCache.Clear();
      List<Verb> allVerbs = this.VerbTracker.AllVerbs;
      if (allVerbs == null)
        return;
      for (int index = 0; index < allVerbs.Count; ++index)
      {
        Verb verb = allVerbs[index];
        if (verb.verbProps is WormVerbProperties verbProps)
          this._verbCache[verbProps.verbTag] = verb;
        else if (!this._verbCache.ContainsKey(WormVerbTag.Default))
          this._verbCache[WormVerbTag.Default] = verb;
        verb.caster = (Thing) this.parent;
        verb.castCompleteCallback = new Action(this.OnBurstComplete);
      }
      this._cacheDirty = false;
    }

    private void OnBurstComplete()
    {
      this._hasCompletedBurst = true;
      Verb verb = this._lastFiredVerb ?? this.VerbTracker.PrimaryVerb;
      if (verb == null)
        return;
      this._cooldownTicksLeft = (int) ((double) verb.verbProps.defaultCooldownTime * 60.0);
    }

    public virtual void PostSpawnSetup(bool respawningAfterLoad)
    {
      base.PostSpawnSetup(respawningAfterLoad);
      if (this._verbTracker == null)
        this._verbTracker = new VerbTracker((IVerbOwner) this);
      this._cacheDirty = true;
    }

    public virtual void PostPostMake()
    {
      base.PostPostMake();
      this._cacheDirty = true;
    }

    public virtual void PostDeSpawn(Map map, DestroyMode mode = 0)
    {
      base.PostDeSpawn(map, (DestroyMode) 0);
      this.ForceStop();
    }

    public virtual void PostExposeData()
    {
      base.PostExposeData();
      Scribe_Deep.Look<VerbTracker>(ref this._verbTracker, "verbTracker", new object[1]
      {
        (object) this
      });
      Scribe_Values.Look<int>(ref this._cooldownTicksLeft, "cooldownTicksLeft", 0, false);
      float overheatProgress = this.OverheatProgress;
      Scribe_Values.Look<float>(ref overheatProgress, "overheatProgress", 0.0f, false);
      this.OverheatProgress = overheatProgress;
      Scribe_Values.Look<bool>(ref this._hasCompletedBurst, "hasCompletedBurst", true, false);
      if (Scribe.mode != LoadSaveMode.PostLoadInit)
        return;
      this._cacheDirty = true;
    }
  }
}
