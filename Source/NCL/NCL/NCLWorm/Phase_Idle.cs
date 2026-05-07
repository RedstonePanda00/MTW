// Decompiled with JetBrains decompiler
// Type: NCLWorm.Phase_Idle
// Assembly: NCL_WormBoss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B627DDDD-3AC9-4D6A-89F6-EA72F95FB570
// Assembly location: C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3555799437\1.6\Assemblies\NCL_WormBoss (3).dll

using UnityEngine;
using Verse;

#nullable disable
namespace NCLWorm
{
  public class Phase_Idle : WormPhase
  {
    private const float IDLE_SPEED_FACTOR = 0.3f;
    private const int MAP_MARGIN = 20;
    private const int MIN_INTERVAL_TICKS = 240;
    private const int MAX_INTERVAL_TICKS = 480;
    private const float WAVE_SPEED = 0.04f;
    private Vector3 _currentWaypoint;
    private int _ticksUntilNewWaypoint;
    private float _wavePhase = 0.0f;

    public override string GetStateString() => "Idle(Patrol)";

    public override void OnEnter(TC_WormDecisionController _brain)
    {
      base.OnEnter(_brain);
      this.IsFinished = false;
      this._wavePhase = 0.0f;
      this.PickNewWaypoint();
    }

    public override void OnExit()
    {
    }

    public override void Update()
    {
      this._wavePhase += 0.04f;
      --this._ticksUntilNewWaypoint;
      if (this._ticksUntilNewWaypoint > 0)
        return;
      this.PickNewWaypoint();
    }

    private void PickNewWaypoint()
    {
      this._ticksUntilNewWaypoint = Rand.Range(240, 480);
      if (((Thing) this.brain?.Head)?.Map == null)
      {
        this._currentWaypoint = Vector3.zero;
      }
      else
      {
        Map map = ((Thing) this.brain.Head).Map;
        int x = map.Size.x;
        int z = map.Size.z;
        int num = Mathf.Clamp(20, 5, Mathf.Min(x, z) / 4);
        this._currentWaypoint = new Vector3((float) Rand.Range(num, x - num), 0.0f, (float) Rand.Range(num, z - num));
      }
    }

    public override void UpdateSegmentBehavior(WormBody seg, int index, int totalCount)
    {
      float openFactor = (float) (((double) Mathf.Sin(this._wavePhase - (float) index * (6.28318548f / (float) Mathf.Max(totalCount, 1))) + 1.0) * 0.5);
      WormActions.SetVentState(seg, openFactor);
    }

    public override MovementIntent GetMovementIntent()
    {
      if (this.brain?.Head == null)
        return MovementIntent.Invalid;
      return new MovementIntent()
      {
        TargetPosition = this._currentWaypoint,
        OverrideAltitude = 1.5f,
        SpeedFactor = 0.3f,
        TurnFactor = 1.2f,
        AccelFactor = 0.4f,
        IsValid = true
      };
    }

    public override WeaponIntent GetWeaponIntent() => WeaponIntent.Stop;

    public override void ExposeData()
    {
      base.ExposeData();
      Scribe_Values.Look<Vector3>(ref this._currentWaypoint, "idleWaypoint", Vector3.zero, false);
      Scribe_Values.Look<int>(ref this._ticksUntilNewWaypoint, "idleWaypointTimer", 0, false);
      Scribe_Values.Look<float>(ref this._wavePhase, "idleWavePhase", 0.0f, false);
    }
  }
}
