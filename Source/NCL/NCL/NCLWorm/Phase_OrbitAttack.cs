// Decompiled with JetBrains decompiler
// Type: NCLWorm.Phase_OrbitAttack
// Assembly: NCL_WormBoss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B627DDDD-3AC9-4D6A-89F6-EA72F95FB570
// Assembly location: C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3555799437\1.6\Assemblies\NCL_WormBoss (3).dll

using System.Collections.Generic;
using UnityEngine;
using Verse;

#nullable disable
namespace NCLWorm
{
  public class Phase_OrbitAttack : WormPhase
  {
    private const float PHASE_DURATION_SEC = 15f;
    private const float FIRE_INTERVAL_SEC = 0.25f;
    private static readonly int TICKS_DURATION = WormBossConstants.SecondsToTicks(15f);
    private static readonly int TICKS_FIRE_INTERVAL = WormBossConstants.SecondsToTicks(0.25f);
    private const float ORBIT_RADIUS = 25f;
    private const float CENTER_FOLLOW_SMOOTHING = 0.05f;
    private const float ANGULAR_SPEED_RAD_PER_TICK = 0.026f;
    private int _timer = 0;
    private int _fireTimer = 0;
    private Vector3 _orbitCenter;
    private bool _hasCenter = false;
    private bool _isClockwise = true;
    private float _currentAngleRad = 0.0f;

    public override float? DesiredStiffness => new float?(0.25f);

    public override void OnEnter(TC_WormDecisionController _brain)
    {
      base.OnEnter(_brain);
      this._timer = 0;
      this._fireTimer = 0;
      this._isClockwise = Rand.Bool;
      this.InitializeOrbitCenter();
    }

    public override void Update()
    {
      ++this._timer;
      ++this._fireTimer;
      this.UpdateOrbitCenter();
      this.UpdateOrbitAngle();
      this.UpdateFireLogic();
      if (this._timer < Phase_OrbitAttack.TICKS_DURATION)
        return;
      this.Finish();
    }

    private void InitializeOrbitCenter()
    {
      LocalTargetInfo bestTarget = this.brain.GetBestTarget();
      this._orbitCenter = bestTarget.IsValid ? bestTarget.CenterVector3 : (this.brain.Head.ExactPosition + (this.brain.Head.BodyFacing * 20f));
      this._hasCenter = true;
      Vector3 vector3 = (this.brain.Head.ExactPosition - this._orbitCenter);
      this._currentAngleRad = Mathf.Atan2(vector3.z, vector3.x);
    }

    private void UpdateOrbitCenter()
    {
      LocalTargetInfo bestTarget = this.brain.GetBestTarget();
      if (!bestTarget.IsValid)
        return;
      this._orbitCenter = Vector3.Lerp(this._orbitCenter, bestTarget.CenterVector3, 0.05f);
    }

    private void UpdateOrbitAngle()
    {
      if (this._isClockwise)
        this._currentAngleRad -= 0.026f;
      else
        this._currentAngleRad += 0.026f;
    }

    private void UpdateFireLogic()
    {
      if (this._fireTimer < Phase_OrbitAttack.TICKS_FIRE_INTERVAL)
        return;
      this._fireTimer = 0;
      List<WormBody> bodySegments = this.brain.Head.BodySegments;
      if (bodySegments != null && bodySegments.Count > 0)
      {
        WormBody seg = GenCollection.RandomElement<WormBody>((IEnumerable<WormBody>) bodySegments);
        if (seg != null && !((Thing) seg).Destroyed)
          WormActions.OrderFireAtMainTarget(seg, WormVerbTag.Default);
      }
    }

    public override void UpdateSegmentBehavior(WormBody seg, int index, int totalCount)
    {
      TC_WormWeaponController weapon = seg.Weapon;
      if (weapon != null && weapon.VerbTracker.PrimaryVerb.state > 0)
        WormActions.SetVentState(seg, 1f);
      else
        WormActions.SetVentState(seg, 0.0f);
    }

    public override MovementIntent GetMovementIntent()
    {
      if (!this._hasCenter)
        return MovementIntent.Invalid;
      return MovementIntent.Presets.Cruising(new Vector3(this._orbitCenter.x + Mathf.Cos(this._currentAngleRad) * 25f, 0.0f, this._orbitCenter.z + Mathf.Sin(this._currentAngleRad) * 25f), 2f) with
      {
        SpeedFactor = 2f,
        TurnFactor = 2.2f,
        AccelFactor = 2f,
        ForceFacing = new Vector3?()
      };
    }

    public override WeaponIntent GetWeaponIntent() => WeaponIntent.Stop;

    public override void ExposeData()
    {
      base.ExposeData();
      Scribe_Values.Look<int>(ref this._timer, "timer", 0, false);
      Scribe_Values.Look<int>(ref this._fireTimer, "fireTimer", 0, false);
      Scribe_Values.Look<Vector3>(ref this._orbitCenter, "center", new Vector3(), false);
      Scribe_Values.Look<bool>(ref this._hasCenter, "hasCenter", false, false);
      Scribe_Values.Look<bool>(ref this._isClockwise, "clockwise", true, false);
      Scribe_Values.Look<float>(ref this._currentAngleRad, "currentAngle", 0.0f, false);
    }
  }
}
