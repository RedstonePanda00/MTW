// Decompiled with JetBrains decompiler
// Type: NCLWorm.Phase_ExoEnergyBlast
// Assembly: NCL_WormBoss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B627DDDD-3AC9-4D6A-89F6-EA72F95FB570
// Assembly location: C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3555799437\1.6\Assemblies\NCL_WormBoss (3).dll

using UnityEngine;
using Verse;

#nullable disable
namespace NCLWorm
{
  public class Phase_ExoEnergyBlast : WormPhase
  {
    private const int TICKS_APPROACH_MAX = 600;
    private const int TICKS_BRAKING = 45;
    private const int TICKS_POSITIONING = 120;
    private const int TICKS_CHARGING = 180;
    private const int TICKS_FIRING_TIMEOUT_MAX = 1200;
    private const int TICKS_RECOVERY = 60;
    private const int TICKS_VENT_CYCLE_INTERVAL = 180;
    private const float DIST_APPROACH_STOP = 15f;
    private const float DIST_FLANKING = 25f;
    private Phase_ExoEnergyBlast.SubState _state = Phase_ExoEnergyBlast.SubState.Approaching;
    private int _stateTimer = 0;
    private bool _hasRequestedFire = false;
    private bool _hasFiredThisPhase = false;

    public override void OnEnter(TC_WormDecisionController _brain)
    {
      base.OnEnter(_brain);
      this._hasFiredThisPhase = false;
      this.DecideInitialState();
    }

    public override void OnExit()
    {
      base.OnExit();
      this.brain.Weapon?.ForceStop();
    }

    public override string GetStateString()
    {
      return string.Format("{0} (T:{1})", (object) this._state, (object) this._stateTimer);
    }

    private void DecideInitialState()
    {
      LocalTargetInfo bestTarget = this.brain.GetBestTarget();
      if (!bestTarget.IsValid)
        this.SwitchState(Phase_ExoEnergyBlast.SubState.Recovering);
      else
        this.SwitchState((double) WormMath.Distance2D(bestTarget.CenterVector3, this.brain.Head.ExactPosition) > 30.0 ? Phase_ExoEnergyBlast.SubState.Approaching : Phase_ExoEnergyBlast.SubState.Positioning);
    }

    public override void Update()
    {
      ++this._stateTimer;
      this.UpdateSegmentStiffness();
      switch (this._state)
      {
        case Phase_ExoEnergyBlast.SubState.Approaching:
          this.Update_Approaching();
          break;
        case Phase_ExoEnergyBlast.SubState.Braking:
          this.Update_Braking();
          break;
        case Phase_ExoEnergyBlast.SubState.Positioning:
          this.Update_Positioning();
          break;
        case Phase_ExoEnergyBlast.SubState.Charging:
          this.Update_Charging();
          break;
        case Phase_ExoEnergyBlast.SubState.Firing:
          this.Update_Firing();
          break;
        case Phase_ExoEnergyBlast.SubState.Recovering:
          this.Update_Recovering();
          break;
      }
    }

    private void UpdateSegmentStiffness()
    {
      switch (this._state)
      {
        case Phase_ExoEnergyBlast.SubState.Braking:
          this.brain.SegmentReorientationStrength = 0.15f;
          break;
        case Phase_ExoEnergyBlast.SubState.Charging:
        case Phase_ExoEnergyBlast.SubState.Firing:
          this.brain.SegmentReorientationStrength = 0.0f;
          break;
      }
    }

    private void Update_Approaching()
    {
      LocalTargetInfo bestTarget = this.brain.GetBestTarget();
      if (!bestTarget.IsValid || this._stateTimer > 600)
      {
        this.SwitchState(Phase_ExoEnergyBlast.SubState.Braking);
      }
      else
      {
        if ((double) WormMath.Distance2D(bestTarget.CenterVector3, this.brain.Head.ExactPosition) >= 15.0)
          return;
        this.SwitchState(Phase_ExoEnergyBlast.SubState.Braking);
      }
    }

    private void Update_Braking()
    {
      if (this._stateTimer < 45)
        return;
      this.SwitchState(Phase_ExoEnergyBlast.SubState.Positioning);
    }

    private void Update_Positioning()
    {
      if (this._stateTimer < 120)
        return;
      this.SwitchState(Phase_ExoEnergyBlast.SubState.Charging);
    }

    private void Update_Charging()
    {
      if (this._stateTimer < 180)
        return;
      this._hasRequestedFire = false;
      this._hasFiredThisPhase = false;
      this.SwitchState(Phase_ExoEnergyBlast.SubState.Firing);
    }

    private void Update_Firing()
    {
      if (this._hasRequestedFire && this.brain.Weapon != null && this.brain.Weapon.IsBurstCompleted)
        this.SwitchState(Phase_ExoEnergyBlast.SubState.Recovering);
      else if (this._stateTimer >= 1200)
      {
        this.SwitchState(Phase_ExoEnergyBlast.SubState.Recovering);
      }
      else
      {
        if (!this._hasRequestedFire || this._stateTimer <= 30)
          return;
        Verb primaryVerb = this.brain.Weapon.VerbTracker.PrimaryVerb;
        if (primaryVerb != null && primaryVerb.state != VerbState.Bursting)
        {
          this.brain.Weapon.ForceStop();
          this.SwitchState(Phase_ExoEnergyBlast.SubState.Recovering);
        }
      }
    }

    private void Update_Recovering()
    {
      if (this._stateTimer < 60)
        return;
      this.Finish();
    }

    public override void UpdateSegmentBehavior(WormBody seg, int index, int totalCount)
    {
      WormActions.OrderCeaseFire(seg);
      bool flag1 = this.brain.Weapon != null && this.brain.Weapon.IsOverheating;
      switch (this._state)
      {
        case Phase_ExoEnergyBlast.SubState.Firing:
          if (flag1)
          {
            bool flag2 = Find.TickManager.TicksGame / 180 % 2 == 0 ? WormRules.EveryNth(index, 2) : WormRules.EveryNth(index, 2, 1);
            WormActions.SetVentState(seg, flag2 ? 1f : 0.0f);
            break;
          }
          WormActions.SetVentState(seg, 0.0f);
          break;
        case Phase_ExoEnergyBlast.SubState.Recovering:
          WormActions.SetVentState(seg, 1f);
          break;
        default:
          WormActions.SetVentState(seg, 0.0f);
          break;
      }
    }

    public override MovementIntent GetMovementIntent()
    {
      LocalTargetInfo bestTarget = this.brain.GetBestTarget();
      Vector3 dest = bestTarget.IsValid ? bestTarget.CenterVector3 : this.brain.Head.ExactPosition;
      Vector3 exactPosition = this.brain.Head.ExactPosition;
      switch (this._state)
      {
        case Phase_ExoEnergyBlast.SubState.Approaching:
          return MovementIntent.Presets.Cruising(dest, 3.5f) with
          {
            SpeedFactor = 0.8f
          };
        case Phase_ExoEnergyBlast.SubState.Braking:
          return MovementIntent.Presets.Braking((dest - exactPosition));
        case Phase_ExoEnergyBlast.SubState.Positioning:
          Vector3 vector3_1 = dest;
          Vector3 vector3_2 = (exactPosition - dest);
          Vector3 vector3_3 = (vector3_2.normalized * 25f);
          return MovementIntent.Presets.Cruising((vector3_1 + vector3_3), 0.5f);
        case Phase_ExoEnergyBlast.SubState.Charging:
          MovementIntent movementIntent = MovementIntent.Presets.Creeping(exactPosition, 0.5f) with
          {
            SpeedFactor = 0.05f
          };
          if (bestTarget.IsValid)
          {
            ref MovementIntent local = ref movementIntent;
            Vector3 vector3_4 = (bestTarget.CenterVector3 - exactPosition);
            Vector3? nullable = new Vector3?(vector3_4.normalized);
            local.ForceFacing = nullable;
          }
          return movementIntent;
        case Phase_ExoEnergyBlast.SubState.Firing:
          return MovementIntent.Presets.Creeping(bestTarget.IsValid ? bestTarget.CenterVector3 : (exactPosition + (this.brain.Head.BodyFacing * 50f)), 0.5f) with
          {
            SpeedFactor = 0.1f,
            TurnFactor = 0.1f,
            ForceFacing = new Vector3?()
          };
        case Phase_ExoEnergyBlast.SubState.Recovering:
          return MovementIntent.Presets.Cruising((exactPosition + (this.brain.Head.BodyFacing * 50f)), 3.5f);
        default:
          return MovementIntent.Invalid;
      }
    }

    public override WeaponIntent GetWeaponIntent()
    {
      if (this._state != Phase_ExoEnergyBlast.SubState.Firing || this._hasFiredThisPhase)
        return WeaponIntent.Stop;
      this._hasFiredThisPhase = true;
      this._hasRequestedFire = true;
      Vector3 world = (this.brain.Head.ExactPosition + (this.brain.Head.BodyFacing * 25f));
      Map map = ((Thing) this.brain.Head).Map;
      return WeaponIntent.UseVerb(WormVerbTag.Default, new LocalTargetInfo(map != null ? WormUtility.ClampWorldPosToMapCell(map, world) : IntVec3Utility.ToIntVec3(world)));
    }

    private void SwitchState(Phase_ExoEnergyBlast.SubState newState)
    {
      this._state = newState;
      this._stateTimer = 0;
      if (newState == Phase_ExoEnergyBlast.SubState.Charging || newState == Phase_ExoEnergyBlast.SubState.Firing)
        this.brain.Weapon?.ResetCombatStatus();
      if (newState != Phase_ExoEnergyBlast.SubState.Charging)
        return;
      this._hasRequestedFire = false;
      this._hasFiredThisPhase = false;
    }

    public override void ExposeData()
    {
      base.ExposeData();
      Scribe_Values.Look<Phase_ExoEnergyBlast.SubState>(ref this._state, "state", Phase_ExoEnergyBlast.SubState.Approaching, false);
      Scribe_Values.Look<int>(ref this._stateTimer, "stateTimer", 0, false);
      Scribe_Values.Look<bool>(ref this._hasRequestedFire, "hasRequestedFire", false, false);
      Scribe_Values.Look<bool>(ref this._hasFiredThisPhase, "hasFiredThisPhase", false, false);
    }

    private enum SubState
    {
      Approaching,
      Braking,
      Positioning,
      Charging,
      Firing,
      Recovering,
    }
  }
}
