// Decompiled with JetBrains decompiler
// Type: NCLWorm.Phase_ContinuousLaserBarrage
// Assembly: NCL_WormBoss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B627DDDD-3AC9-4D6A-89F6-EA72F95FB570
// Assembly location: C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3555799437\1.6\Assemblies\NCL_WormBoss (3).dll

using System.Collections.Generic;
using UnityEngine;
using Verse;

#nullable disable
namespace NCLWorm
{
  public class Phase_ContinuousLaserBarrage : WormPhase
  {
    private const int TICKS_LIGHT_UP_WAVE = 120;
    private const int TICKS_HOLD = 60;
    private const int TICKS_TOTAL_PREPARE = 180;
    private const int TICKS_FIRING_WAVE = 240;
    private const float TURRET_TURN_RATE_DEG = 25f;
    private const int BARRAGE_CYCLES = 3;
    private const float ORBIT_RADIUS = 22f;
    private const float ORBIT_SPEED = 0.045f;
    private Phase_ContinuousLaserBarrage.State _state = Phase_ContinuousLaserBarrage.State.Positioning;
    private int _timer = 0;
    private int _completedCycles = 0;
    private Vector3 _orbitCenter;
    private float _currentAngleRad;
    private bool _isClockwise;
    private LocalTargetInfo _cachedFrameTarget;
    private Dictionary<WormBody, ExoTelegraphCone> _activeSights = new Dictionary<WormBody, ExoTelegraphCone>();

    public override void OnEnter(TC_WormDecisionController _brain)
    {
      base.OnEnter(_brain);
      this._completedCycles = 0;
      this._activeSights.Clear();
      this._isClockwise = Rand.Bool;
      LocalTargetInfo bestTarget = this.brain.GetBestTarget();
      Vector3 vector3_1 = bestTarget.IsValid ? bestTarget.CenterVector3 : this.brain.Head.ExactPosition;
      this._orbitCenter = vector3_1;
      Vector3 vector3_2 = (this.brain.Head.ExactPosition - vector3_1);
      this._currentAngleRad = Mathf.Atan2(vector3_2.z, vector3_2.x);
      this.SwitchState(Phase_ContinuousLaserBarrage.State.Positioning);
    }

    public override void OnExit()
    {
      base.OnExit();
      this.CleanupAllSights();
    }

    public override void Update()
    {
      this._cachedFrameTarget = this.brain.GetBestTarget();
      ++this._timer;
      this.UpdateOrbitCenter();
      this.UpdateOrbitAngle();
      switch (this._state)
      {
        case Phase_ContinuousLaserBarrage.State.Positioning:
          float num = WormMath.Distance2D(this.brain.Head.ExactPosition, this._orbitCenter);
          if (this._timer <= 60 && (double) Mathf.Abs(num - 22f) >= 5.0)
            break;
          this.SwitchState(Phase_ContinuousLaserBarrage.State.Telegraphing);
          break;
        case Phase_ContinuousLaserBarrage.State.Telegraphing:
          if (this._timer < 180)
            break;
          this.SwitchState(Phase_ContinuousLaserBarrage.State.Firing);
          break;
        case Phase_ContinuousLaserBarrage.State.Firing:
          if (this._timer < 240)
            break;
          ++this._completedCycles;
          if (this._completedCycles >= 3)
            this.SwitchState(Phase_ContinuousLaserBarrage.State.Recovering);
          else
            this.SwitchState(Phase_ContinuousLaserBarrage.State.Telegraphing);
          break;
        case Phase_ContinuousLaserBarrage.State.Recovering:
          if (this._timer <= 60)
            break;
          this.Finish();
          break;
      }
    }

    public override void UpdateSegmentBehavior(WormBody seg, int index, int totalCount)
    {
      if (this._state == Phase_ContinuousLaserBarrage.State.Recovering || this._state == Phase_ContinuousLaserBarrage.State.Positioning)
      {
        WormActions.SetVentState(seg, 0.0f);
        WormActions.OrderCeaseFire(seg);
        this.CleanupSingleSight(seg);
      }
      else
      {
        LocalTargetInfo cachedFrameTarget = this._cachedFrameTarget;
        if (!cachedFrameTarget.IsValid)
          return;
        float num1 = (float) index / (float) totalCount;
        if (this._state == Phase_ContinuousLaserBarrage.State.Telegraphing)
        {
          float num2 = this._timer < 120 ? (float) this._timer / 120f : 1f;
          if ((double) num1 <= (double) num2 + 0.0099999997764825821)
          {
            WormActions.SetVentState(seg, 1f);
            ExoTelegraphCone sight = this.GetOrCreateSight(seg, cachedFrameTarget.CenterVector3);
            if (sight == null)
            {
              WormActions.OrderCeaseFire(seg);
              return;
            }
            sight.TurnRate = 25f;
            sight.UpdateLock(cachedFrameTarget.CenterVector3, false);
          }
          else
            WormActions.SetVentState(seg, 0.0f);
          WormActions.OrderCeaseFire(seg);
        }
        else
        {
          if (this._state != Phase_ContinuousLaserBarrage.State.Firing)
            return;
          float num3 = (float) this._timer / 240f;
          float num4 = num1 - num3;
          if ((double) num4 > 0.0)
          {
            WormActions.SetVentState(seg, 1f);
            ExoTelegraphCone sight = this.GetOrCreateSight(seg, cachedFrameTarget.CenterVector3);
            if (sight == null)
              WormActions.OrderCeaseFire(seg);
            else if ((double) num4 < 0.079999998211860657)
            {
              sight.UpdateLock(sight.CurrentAimPosition, true);
            }
            else
            {
              sight.TurnRate = 25f;
              sight.UpdateLock(cachedFrameTarget.CenterVector3, false);
            }
          }
          else if ((double) Mathf.Abs(num4) < 1.5 / (double) totalCount)
          {
            ExoTelegraphCone exoTelegraphCone;
            if (this._activeSights.TryGetValue(seg, out exoTelegraphCone) && exoTelegraphCone != null)
            {
              Vector3 currentAimPosition = exoTelegraphCone.CurrentAimPosition;
              TC_WormWeaponController comp = seg.GetComp<TC_WormWeaponController>();
              if (comp != null)
                comp.SetIntent(new WeaponIntent()
                {
                  Mode = FireMode.FireAtLocation,
                  Tag = WormVerbTag.Heavy,
                  Target = new LocalTargetInfo(WormUtility.ClampWorldPosToMapCell(((Thing) seg).Map, currentAimPosition))
                });
            }
            this.CleanupSingleSight(seg);
            WormActions.SetVentState(seg, 1f);
          }
          else
          {
            WormActions.SetVentState(seg, 0.0f);
            WormActions.OrderCeaseFire(seg);
            this.CleanupSingleSight(seg);
          }
        }
      }
    }

    private ExoTelegraphCone GetOrCreateSight(WormBody seg, Vector3 initialTarget)
    {
      ExoTelegraphCone sight;
      if (!this._activeSights.TryGetValue(seg, out sight) || sight == null || sight.Destroyed)
      {
        sight = TelegraphConePool.Get(((Thing) seg).Map, ((Thing) seg).DrawPos);
        if (sight == null)
          return (ExoTelegraphCone) null;
        sight.Anchor = (Thing) seg;
        sight.ConeLength = 55f;
        sight.BaseWidth = 1.8f;
        this._activeSights[seg] = sight;
        sight.UpdateLock((initialTarget + (Rand.UnitVector3 * 3f)), false, true);
      }
      return sight;
    }

    private void CleanupSingleSight(WormBody seg)
    {
      ExoTelegraphCone cone;
      if (!this._activeSights.TryGetValue(seg, out cone))
        return;
      if (cone != null && !cone.Destroyed)
        TelegraphConePool.Return(cone);
      this._activeSights.Remove(seg);
    }

    private void CleanupAllSights()
    {
      foreach (KeyValuePair<WormBody, ExoTelegraphCone> activeSight in this._activeSights)
      {
        if (activeSight.Value != null && !activeSight.Value.Destroyed)
          TelegraphConePool.Return(activeSight.Value);
      }
      this._activeSights.Clear();
    }

    private void UpdateOrbitCenter()
    {
      if (!this._cachedFrameTarget.IsValid)
        return;
      this._orbitCenter = Vector3.Lerp(this._orbitCenter, this._cachedFrameTarget.CenterVector3, 0.1f);
    }

    private void UpdateOrbitAngle()
    {
      if (this._isClockwise)
        this._currentAngleRad -= 0.045f;
      else
        this._currentAngleRad += 0.045f;
    }

    private void SwitchState(Phase_ContinuousLaserBarrage.State newState)
    {
      this._state = newState;
      this._timer = 0;
      if (newState != Phase_ContinuousLaserBarrage.State.Recovering && newState != Phase_ContinuousLaserBarrage.State.Positioning)
        return;
      this.CleanupAllSights();
    }

    public override MovementIntent GetMovementIntent()
    {
      if (this._state == Phase_ContinuousLaserBarrage.State.Recovering)
        return MovementIntent.Presets.Cruising((this.brain.Head.ExactPosition + (this.brain.Head.BodyFacing * 40f)), 3f);
      return MovementIntent.Presets.Cruising(new Vector3(this._orbitCenter.x + Mathf.Cos(this._currentAngleRad) * 22f, 0.0f, this._orbitCenter.z + Mathf.Sin(this._currentAngleRad) * 22f), 2f) with
      {
        SpeedFactor = 1.5f,
        TurnFactor = 1.5f,
        ForceFacing = new Vector3?()
      };
    }

    public override WeaponIntent GetWeaponIntent() => WeaponIntent.Stop;

    public override void ExposeData()
    {
      base.ExposeData();
      Scribe_Values.Look<Phase_ContinuousLaserBarrage.State>(ref this._state, "state", Phase_ContinuousLaserBarrage.State.Positioning, false);
      Scribe_Values.Look<int>(ref this._timer, "timer", 0, false);
      Scribe_Values.Look<int>(ref this._completedCycles, "completedCycles", 0, false);
      Scribe_Values.Look<float>(ref this._currentAngleRad, "angle", 0.0f, false);
      Scribe_Values.Look<Vector3>(ref this._orbitCenter, "center", new Vector3(), false);
      Scribe_Values.Look<bool>(ref this._isClockwise, "clockwise", false, false);
      Scribe_Collections.Look<WormBody, ExoTelegraphCone>(ref this._activeSights, "activeSights", (LookMode) 3, (LookMode) 3);
    }

    private enum State
    {
      Positioning,
      Telegraphing,
      Firing,
      Recovering,
    }
  }
}
