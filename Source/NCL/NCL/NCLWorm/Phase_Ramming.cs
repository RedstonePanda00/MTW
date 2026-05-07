// Decompiled with JetBrains decompiler
// Type: NCLWorm.Phase_Ramming
// Assembly: NCL_WormBoss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B627DDDD-3AC9-4D6A-89F6-EA72F95FB570
// Assembly location: C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3555799437\1.6\Assemblies\NCL_WormBoss (3).dll

using UnityEngine;
using Verse;

#nullable disable
namespace NCLWorm
{
  public class Phase_Ramming : WormPhase
  {
    private const int TIMEOUT_SEEKING_TICKS = 300;
    private const int DURATION_LOCKING_TICKS = 48;
    private const int DURATION_CHARGING_TICKS = 90;
    private const int DURATION_RECOVERING_TICKS = 120;
    private const float ANGLE_LOCK_THRESHOLD = 45f;
    private const float CHARGE_DISTANCE = 200f;
    private const float RECOVER_DISTANCE = 60f;
    private Phase_Ramming.RamState state = Phase_Ramming.RamState.Seeking;
    private int stateTimer = 0;
    private Vector3 cachedChargeDir;

    public override void OnEnter(TC_WormDecisionController _brain)
    {
      base.OnEnter(_brain);
      this.SwitchState(Phase_Ramming.RamState.Seeking);
    }

    public override string GetStateString()
    {
      return string.Format("{0} (T:{1})", (object) this.state, (object) this.stateTimer);
    }

    public override void Update()
    {
      ++this.stateTimer;
      switch (this.state)
      {
        case Phase_Ramming.RamState.Seeking:
          this.Update_Seeking();
          break;
        case Phase_Ramming.RamState.Locking:
          this.Update_Locking();
          break;
        case Phase_Ramming.RamState.Charging:
          this.Update_Charging();
          break;
        case Phase_Ramming.RamState.Recovering:
          this.Update_Recovering();
          break;
      }
    }

    private void Update_Seeking()
    {
      LocalTargetInfo bestTarget = this.brain.GetBestTarget();
      if (!bestTarget.IsValid)
      {
        this.SwitchState(Phase_Ramming.RamState.Recovering);
      }
      else
      {
        Vector3 exactPosition = this.brain.Head.ExactPosition;
        float num1 = WormMath.Distance2D(bestTarget.CenterVector3, exactPosition);
        float num2 = Vector3.Angle(this.brain.Head.BodyFacing, (bestTarget.CenterVector3 - exactPosition));
        if (((double) num1 >= 30.0 || (double) num2 >= 45.0) && this.stateTimer <= 300)
          return;
        this.SwitchState(Phase_Ramming.RamState.Locking);
      }
    }

    private void Update_Locking()
    {
      if (this.stateTimer <= 48)
        return;
      LocalTargetInfo bestTarget = this.brain.GetBestTarget();
      Vector3 vector3_1;
      if (!bestTarget.IsValid)
      {
        vector3_1 = this.brain.Head.BodyFacing;
      }
      else
      {
        Vector3 vector3_2 = (bestTarget.CenterVector3 - this.brain.Head.ExactPosition);
        vector3_1 = vector3_2.normalized;
      }
      this.cachedChargeDir = vector3_1;
      this.cachedChargeDir.y = 0.0f;
      this.SwitchState(Phase_Ramming.RamState.Charging);
    }

    private void Update_Charging()
    {
      if (this.stateTimer <= 90)
        return;
      this.SwitchState(Phase_Ramming.RamState.Recovering);
    }

    private void Update_Recovering()
    {
      if (this.stateTimer <= 120)
        return;
      this.Finish();
    }

    public override void UpdateSegmentBehavior(WormBody seg, int index, int totalCount)
    {
      switch (this.state)
      {
        case Phase_Ramming.RamState.Locking:
          WormActions.SetVentState(seg, WormRules.EveryNth(index, 2) ? 0.5f : 0.0f);
          break;
        case Phase_Ramming.RamState.Charging:
          WormActions.SetVentState(seg, 1f);
          break;
        default:
          WormActions.SetVentState(seg, 0.0f);
          break;
      }
      WormActions.OrderCeaseFire(seg);
    }

    public override MovementIntent GetMovementIntent()
    {
      LocalTargetInfo bestTarget = this.brain.GetBestTarget();
      Vector3 dest = bestTarget.IsValid ? bestTarget.CenterVector3 : this.brain.Head.ExactPosition;
      Vector3 exactPosition = this.brain.Head.ExactPosition;
      switch (this.state)
      {
        case Phase_Ramming.RamState.Seeking:
          return MovementIntent.Presets.Cruising(dest, 2.5f);
        case Phase_Ramming.RamState.Locking:
          Vector3 vector3_1 = (dest - exactPosition);
          Vector3 vector3_2;
          if ((double) vector3_1.sqrMagnitude <= 1.0 / 1000.0)
          {
            vector3_2 = this.brain.Head.BodyFacing;
          }
          else
          {
            vector3_1 = (dest - exactPosition);
            vector3_2 = vector3_1.normalized;
          }
          Vector3 vector3_3 = vector3_2;
          return MovementIntent.Presets.Precise(dest) with
          {
            ForceFacing = new Vector3?(vector3_3)
          };
        case Phase_Ramming.RamState.Charging:
          return MovementIntent.Presets.Ramming((exactPosition + (this.cachedChargeDir * 200f)));
        case Phase_Ramming.RamState.Recovering:
          return MovementIntent.Presets.Cruising((exactPosition + (this.brain.Head.BodyFacing * 60f)), 3.5f) with
          {
            SpeedFactor = 0.8f
          };
        default:
          return MovementIntent.Invalid;
      }
    }

    public override WeaponIntent GetWeaponIntent() => WeaponIntent.Stop;

    private void SwitchState(Phase_Ramming.RamState newState)
    {
      this.state = newState;
      this.stateTimer = 0;
    }

    public override void ExposeData()
    {
      base.ExposeData();
      Scribe_Values.Look<Phase_Ramming.RamState>(ref this.state, "state", Phase_Ramming.RamState.Seeking, false);
      Scribe_Values.Look<int>(ref this.stateTimer, "stateTimer", 0, false);
      Scribe_Values.Look<Vector3>(ref this.cachedChargeDir, "cachedChargeDir", new Vector3(), false);
    }

    public enum RamState
    {
      Seeking,
      Locking,
      Charging,
      Recovering,
    }
  }
}
