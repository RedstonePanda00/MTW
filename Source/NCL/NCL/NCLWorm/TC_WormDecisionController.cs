// Decompiled with JetBrains decompiler
// Type: NCLWorm.TC_WormDecisionController
// Assembly: NCL_WormBoss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B627DDDD-3AC9-4D6A-89F6-EA72F95FB570
// Assembly location: C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3555799437\1.6\Assemblies\NCL_WormBoss (3).dll

using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

#nullable disable
namespace NCLWorm
{
  public class TC_WormDecisionController : ThingComp
  {
    private TC_WormMovingController _mover;
    private TC_WormWeaponController _weapon;
    private CompWormTargeter _targeter;
    private CompWormCoordinator _coordinator;
    private CompSwarmController _swarm;
    private WormPhase _currentPhase;
    private BossAIMemory _aiMemory;
    private Thing _currentVisualTarget;
    private Mote _activeTargetMote;
    private int _visualTicks = 0;
    private int _ticksUntilDespawn = -2;
    private bool _isDeparting = false;
    private readonly List<PhaseEntry> _phaseCandidateScratch = new List<PhaseEntry>();

    public CompSwarmController Swarm
    {
      get => this._swarm ?? (this._swarm = this.parent.GetComp<CompSwarmController>());
    }

    public TC_WormMovingController Mover
    {
      get => this._mover ?? (this._mover = this.parent.GetComp<TC_WormMovingController>());
    }

    public TC_WormWeaponController Weapon
    {
      get => this._weapon ?? (this._weapon = this.parent.GetComp<TC_WormWeaponController>());
    }

    public CompWormTargeter Targeter
    {
      get => this._targeter ?? (this._targeter = this.parent.GetComp<CompWormTargeter>());
    }

    public CompWormCoordinator Coordinator
    {
      get => this._coordinator ?? (this._coordinator = this.parent.GetComp<CompWormCoordinator>());
    }

    public WormHead Head => this.parent as WormHead;

    public WormPhase CurrentPhase => this._currentPhase;

    public BossAIMemory AIMemory => this._aiMemory ?? (this._aiMemory = new BossAIMemory());

    public int TicksUntilDespawn
    {
      get => this._ticksUntilDespawn;
      set => this._ticksUntilDespawn = value;
    }

    public float SegmentReorientationStrength { get; set; } = 1f;

    public TCP_WormDecisionController Props => (TCP_WormDecisionController) this.props;

    public virtual void PostSpawnSetup(bool respawningAfterLoad)
    {
      base.PostSpawnSetup(respawningAfterLoad);
      if (!respawningAfterLoad && this._ticksUntilDespawn == -2)
      {
        int num = this.Props != null ? this.Props.initialLifespanTicks : 300000;
        if (this.Props != null && (double) this.Props.initialLifespanDays > 0.0)
          num = Mathf.RoundToInt(this.Props.initialLifespanDays * 60000f);
        this._ticksUntilDespawn = num;
      }
      if (this._currentPhase == null)
        this.DecideNextPhase();
      else
        this._currentPhase.OnEnter(this);
    }

    public virtual void CompTick()
    {
      base.CompTick();
      if (this._currentPhase == null)
        return;
      if (this._ticksUntilDespawn > 0)
      {
        --this._ticksUntilDespawn;
        int num;
        if (this._ticksUntilDespawn <= 0 && !this._isDeparting)
        {
          WormHead head = this.Head;
          num = (head != null ? (head.IsDying ? 1 : 0) : 0) == 0 ? 1 : 0;
        }
        else
          num = 0;
        if (num != 0)
        {
          this._isDeparting = true;
          this.SetPhase((WormPhase) new Phase_Departure());
        }
      }
      if (this.Head != null && this.Head.IsDying)
      {
        this.UpdateSegmentStiffness();
        this._currentPhase.Update();
        this.DispatchIntents();
      }
      else if (this._isDeparting)
      {
        this.UpdateSegmentStiffness();
        this._currentPhase.Update();
        this.DispatchIntents();
      }
      else
      {
        bool flag = this.Targeter.LockedTarget != null;
        if (flag && ((Thing) this.parent).Map != null && ((Thing) this.parent).Faction != null)
        {
          HashSet<IAttackTarget> faction = ((Thing) this.parent).Map.attackTargetsCache.TargetsHostileToFaction(((Thing) this.parent).Faction);
          flag = faction != null && faction.Count > 0;
        }
        int num;
        if (!flag)
        {
          WormHead head = this.Head;
          num = (head != null ? (head.IsDying ? 1 : 0) : 0) == 0 ? 1 : 0;
        }
        else
          num = 0;
        if (num != 0)
        {
          if (!(this._currentPhase is Phase_Idle))
            this.SetPhase((WormPhase) new Phase_Idle());
        }
        else if (this._currentPhase is Phase_Idle)
          this.DecideNextPhase();
        this.UpdateSegmentStiffness();
        this._currentPhase.Update();
        this.DispatchIntents();
        this.UpdateTargetVisuals();
        if (!flag)
          return;
        this.TryDecideNextPhase();
      }
    }

    private void TryDecideNextPhase()
    {
      if (!this._currentPhase.IsFinished)
        return;
      this.DecideNextPhase();
    }

    private void DecideNextPhase()
    {
      if (this.Head != null && this.Head.IsDying)
        return;
      this.SetPhase(BossPhaseSelector.SelectNext(this, this._currentPhase, this.Props.phases, this._phaseCandidateScratch));
    }

    public void SetPhase(WormPhase newPhase)
    {
      this._currentPhase?.OnExit();
      this._currentPhase = newPhase;
      this._currentPhase?.OnEnter(this);
      this.BroadcastCeaseFireToSegments();
    }

    private void BroadcastCeaseFireToSegments()
    {
      if (!(this.parent is WormHead parent) || parent.BodySegments == null)
        return;
      for (int index = 0; index < parent.BodySegments.Count; ++index)
      {
        WormBody bodySegment = parent.BodySegments[index];
        if (bodySegment != null && !((Thing) bodySegment).Destroyed)
          bodySegment.Weapon?.SetIntent(WeaponIntent.Stop);
      }
    }

    private void UpdateSegmentStiffness()
    {
      double num1;
      if (this.Head == null)
      {
        num1 = 0.0;
      }
      else
      {
        Vector3 exactVelocity = this.Head.ExactVelocity;
        num1 = (double) exactVelocity.magnitude;
      }
      float num2 = (float) num1;
      float num3 = Mathf.Clamp01((float) (((double) num2 - 0.05000000074505806) / 0.20000000298023224));
      WormPhase currentPhase = this._currentPhase;
      if (currentPhase != null && currentPhase.DesiredStiffness.HasValue)
        num3 = this._currentPhase.DesiredStiffness.Value;
      float num4 = Mathf.Clamp01((float) (((double) num2 - 0.019999999552965164) / 0.079999998211860657));
      this.SegmentReorientationStrength = Mathf.Lerp(this.SegmentReorientationStrength, num3 * num4, 0.1f);
    }

    private void DispatchIntents()
    {
      if (this._currentPhase == null)
        return;
      this.Mover?.SetIntent(this._currentPhase.GetMovementIntent());
      this.Weapon?.SetIntent(this._currentPhase.GetWeaponIntent());
    }

    public LocalTargetInfo GetBestTarget()
    {
      CompWormTargeter targeter = this.Targeter;
      return targeter == null ? LocalTargetInfo.Invalid : targeter.CurrentTargetInfo;
    }

    private void UpdateTargetVisuals()
    {
      ++this._visualTicks;
      LocalTargetInfo bestTarget = this.GetBestTarget();
      Thing thing = bestTarget.Thing;
      if (thing != this._currentVisualTarget)
      {
        this.ClearTargetMote();
        this._currentVisualTarget = thing;
        this._visualTicks = this.Props.targetVisualInterval;
      }
      if (this._currentVisualTarget == null || this._currentVisualTarget.Destroyed || !this._currentVisualTarget.Spawned || this._visualTicks < this.Props.targetVisualInterval)
        return;
      this._visualTicks = 0;
      if (this.Props.targetMoteDef != null && (this._activeTargetMote == null || ((Thing) this._activeTargetMote).Destroyed))
        this._activeTargetMote = MoteMaker.MakeAttachedOverlay(this._currentVisualTarget, this.Props.targetMoteDef, Vector3.zero, this.Props.targetMoteScale, -1f);
      if (this.Props.targetFleckDef != null)
        FleckMaker.AttachedOverlay(this._currentVisualTarget, this.Props.targetFleckDef, Vector3.zero, this.Props.targetFleckScale, -1f);
    }

    private void ClearTargetMote()
    {
      if (this._activeTargetMote != null && !((Thing) this._activeTargetMote).Destroyed)
        ((Thing) this._activeTargetMote).Destroy((DestroyMode) 0);
      this._activeTargetMote = (Mote) null;
    }

    public virtual void PostExposeData()
    {
      base.PostExposeData();
      Scribe_Values.Look<int>(ref this._ticksUntilDespawn, "ticksUntilDespawn", -2, false);
      Scribe_Values.Look<bool>(ref this._isDeparting, "isDeparting", false, false);
      Scribe_Deep.Look<WormPhase>(ref this._currentPhase, "currentPhase", Array.Empty<object>());
      Scribe_Deep.Look<BossAIMemory>(ref this._aiMemory, "aiMemory", Array.Empty<object>());
      if (Scribe.mode != LoadSaveMode.PostLoadInit || this._currentPhase == null)
        return;
      this._currentPhase.OnEnter(this);
    }

    public void TeleportWholeWorm(
      Vector3 newPos,
      Vector3? newVel = null,
      Vector3? newFacing = null,
      bool stackSegments = true)
    {
      this.Head?.InstantRelocate(newPos, newVel, newFacing, stackSegments);
    }
  }
}
