// Decompiled with JetBrains decompiler
// Type: NCLWorm.Phase_WormholeDash
// Assembly: NCL_WormBoss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B627DDDD-3AC9-4D6A-89F6-EA72F95FB570
// Assembly location: C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3555799437\1.6\Assemblies\NCL_WormBoss (3).dll

using System.Collections.Generic;
using UnityEngine;
using Verse;

#nullable disable
namespace NCLWorm
{
  public class Phase_WormholeDash : WormPhase
  {
    private const int MAX_DASHES = 3;
    private const int TICKS_ENTRY_TIMEOUT = 300;
    private const int TICKS_LURK = 60;
    private const int TICKS_TELEGRAPH = 70;
    private const int TICKS_EXHAUSTION = 240;
    private const float FLY_ALTITUDE = 0.5f;
    private const float DASH_SPEED_MULTIPLIER = 4.5f;
    private const float ENTRY_OFFSET = 30f;
    private const float REENTRY_OFFSET = 15f;
    private Phase_WormholeDash.DashState _state = Phase_WormholeDash.DashState.InitialSetup;
    private int _dashCount = 0;
    private int _stateTimer = 0;
    private Vector3 _portalPos;
    private Vector3 _moveDir;
    private Vector3 _predictionEndPos;

    public override float? DesiredStiffness
    {
      get => new float?(this._state == Phase_WormholeDash.DashState.FinalExhaustion ? 0.4f : 0.0f);
    }

    public override void OnEnter(TC_WormDecisionController _brain)
    {
      base.OnEnter(_brain);
      this._dashCount = 0;
      this.SwitchState(Phase_WormholeDash.DashState.InitialSetup);
    }

    public override void OnExit()
    {
      base.OnExit();
      this.SetAllSegmentsVisual(true);
      this.brain.Head.GetComp<TC_WormMovingController>()?.ForceVelocity(Vector3.zero);
    }

    public override void Update()
    {
      ++this._stateTimer;
      switch (this._state)
      {
        case Phase_WormholeDash.DashState.InitialSetup:
          this.Update_InitialSetup();
          break;
        case Phase_WormholeDash.DashState.InitialEntering:
          this.Update_EnteringLogic(Phase_WormholeDash.DashState.Lurking);
          break;
        case Phase_WormholeDash.DashState.Lurking:
          this.Update_Lurking();
          break;
        case Phase_WormholeDash.DashState.Telegraphing:
          this.Update_Telegraphing();
          break;
        case Phase_WormholeDash.DashState.Exiting:
          this.Update_Exiting();
          break;
        case Phase_WormholeDash.DashState.ReEntrySetup:
          this.Update_ReEntrySetup();
          break;
        case Phase_WormholeDash.DashState.ReEntering:
          this.Update_EnteringLogic(Phase_WormholeDash.DashState.Lurking);
          break;
        case Phase_WormholeDash.DashState.FinalExhaustion:
          this.Update_FinalExhaustion();
          break;
      }
    }

    private void Update_InitialSetup()
    {
      this._moveDir = this.brain.Head.BodyFacing;
      this._portalPos = WormUtility.ClampToMap((this.brain.Head.ExactPosition + (this._moveDir * 30f)), ((Thing) this.brain.Head).Map);
      this._portalPos.y = 0.5f;
      this.TrySpawnPortal(this._portalPos);
      this.SwitchState(Phase_WormholeDash.DashState.InitialEntering);
    }

    private void Update_EnteringLogic(Phase_WormholeDash.DashState nextState)
    {
      this.UpdateVisibilityByPlane(this._portalPos, this._moveDir, true);
      WormBody tailSegment = Phase_WormholeDash.GetTailSegment(this.brain.Head.BodySegments);
      if ((tailSegment == null || !tailSegment.IsVisualHidden) && this._stateTimer <= 300)
        return;
      this.SwitchState(nextState);
    }

    private void Update_Lurking()
    {
      if (this._stateTimer == 1)
        this.SetAllSegmentsVisual(false);
      if (this._stateTimer < 60)
        return;
      this.SwitchState(Phase_WormholeDash.DashState.Telegraphing);
    }

    private void Update_Telegraphing()
    {
      if (this._stateTimer == 1)
      {
        Vector3 start;
        Vector3 dir;
        Vector3 predictionEnd;
        this.PrepareDashPath(out start, out dir, out predictionEnd);
        this._portalPos = start;
        this._moveDir = dir;
        this._predictionEndPos = predictionEnd;
        this.TrySpawnPortal(this._portalPos);
        this.SpawnWarningLine(this._portalPos, this._predictionEndPos);
        Vector3 vector3 = (this._moveDir * WormMath.SpeedToCellsPerTick(27f));
        vector3.y = 0.0f;
        this.brain.Head.InstantAlignBody(this._portalPos, this._moveDir, vector3);
        this.brain.Head.GetComp<TC_WormMovingController>()?.ForceVelocity(vector3);
        this.SetAllSegmentsVisual(false);
      }
      if (this._stateTimer < 70)
        return;
      this.SwitchState(Phase_WormholeDash.DashState.Exiting);
    }

    private void Update_Exiting()
    {
      if (this._stateTimer == 1)
      {
        Vector3 vector3 = (this._moveDir * WormMath.SpeedToCellsPerTick(27f));
        this.brain.Head.InstantAlignBody(this._portalPos, this._moveDir, vector3);
        this.brain.Head.GetComp<TC_WormMovingController>()?.ForceVelocity(vector3);
        this.SetAllSegmentsVisual(false);
      }
      this.UpdateVisibilityByPlane(this._portalPos, this._moveDir, false);
      WormBody tailSegment = Phase_WormholeDash.GetTailSegment(this.brain.Head.BodySegments);
      if ((tailSegment == null || (double) Vector3.Dot((tailSegment.ExactPosition - this._portalPos), this._moveDir) <= 2.0) && this._stateTimer <= 120)
        return;
      if (this._dashCount >= 2)
      {
        this.SwitchState(Phase_WormholeDash.DashState.FinalExhaustion);
      }
      else
      {
        ++this._dashCount;
        this.SwitchState(Phase_WormholeDash.DashState.ReEntrySetup);
      }
    }

    private void Update_ReEntrySetup()
    {
      Vector3 vector3 = (this.brain.Head.ExactPosition + (this._moveDir * 15f));
      vector3.y = 0.5f;
      this._portalPos = vector3;
      if (this.IsPosInBoundsValid(this._portalPos, ((Thing) this.brain.Head).Map, 2))
        WormWarpVisuals.TriggerPortalOpen(this._portalPos, ((Thing) this.brain.Head).Map);
      this.SwitchState(Phase_WormholeDash.DashState.ReEntering);
    }

    private void Update_FinalExhaustion()
    {
      if (this._stateTimer == 1)
        this.SetAllSegmentsVisual(true);
      if (this._stateTimer < 240)
        return;
      this.Finish();
    }

    public override MovementIntent GetMovementIntent()
    {
      if (this._state == Phase_WormholeDash.DashState.InitialEntering || this._state == Phase_WormholeDash.DashState.Exiting || this._state == Phase_WormholeDash.DashState.ReEntrySetup || this._state == Phase_WormholeDash.DashState.ReEntering)
      {
        Vector3 dest = (this.brain.Head.ExactPosition + (this._moveDir * 200f));
        dest.y = 0.5f;
        return MovementIntent.Presets.Ramming(dest) with
        {
          SpeedFactor = 4.5f,
          TurnFactor = 0.0f,
          AccelFactor = 100f,
          OverrideAltitude = 0.5f,
          ForceFacing = new Vector3?(this._moveDir)
        };
      }
      if (this._state == Phase_WormholeDash.DashState.FinalExhaustion)
      {
        Map map = ((Thing) this.brain.Head).Map;
        Vector3 dest;
        // ISSUE: explicit constructor call
        dest = new Vector3((float) map.Size.x / 2f, 0.0f, (float) map.Size.z / 2f);
        MovementIntent movementIntent = MovementIntent.Presets.Cruising(dest, 0.5f) with
        {
          SpeedFactor = 0.05f,
          TurnFactor = 4f
        };
        ref MovementIntent local = ref movementIntent;
        Vector3 vector3 = (dest - this.brain.Head.ExactPosition);
        Vector3? nullable = new Vector3?(vector3.normalized);
        local.ForceFacing = nullable;
        return movementIntent;
      }
      return MovementIntent.Presets.Braking(Vector3.forward) with
      {
        IsValid = false
      };
    }

    public override void UpdateSegmentBehavior(WormBody seg, int index, int totalCount)
    {
      switch (this._state)
      {
        case Phase_WormholeDash.DashState.Exiting:
        case Phase_WormholeDash.DashState.ReEntrySetup:
        case Phase_WormholeDash.DashState.ReEntering:
          WormActions.SetVentState(seg, 1f);
          break;
        case Phase_WormholeDash.DashState.FinalExhaustion:
          if (Rand.Chance(0.05f))
          {
            WormActions.GlitchVentState(seg, 0.2f);
            break;
          }
          WormActions.SetVentState(seg, 1f);
          break;
        default:
          WormActions.SetVentState(seg, 0.0f);
          break;
      }
      WormActions.OrderCeaseFire(seg);
    }

    private void PrepareDashPath(out Vector3 start, out Vector3 dir, out Vector3 predictionEnd)
    {
      LocalTargetInfo bestTarget = this.brain.GetBestTarget();
      Vector3 vector3_1 = bestTarget.IsValid ? bestTarget.CenterVector3 : this.brain.Head.ExactPosition;
      Map map = ((Thing) this.brain.Head).Map;
      Vector3 vector3_2 = (Rand.UnitVector3 * 25f);
      vector3_2.y = 0.0f;
      start = WormUtility.ClampToMap((vector3_1 - vector3_2), map);
      start.y = 0.5f;
      Vector3 vector3_3 = vector3_1;
      vector3_3.y = 0.5f;
      Vector3 vector3_4 = vector3_3 - start;
      dir = vector3_4.normalized;
      if (dir == Vector3.zero)
        dir = Vector3.forward;
      dir.y = 0.0f;
      dir.Normalize();
      predictionEnd = (start + (dir * 80f));
      predictionEnd.y = 0.5f;
    }

    private bool IsPosInBoundsValid(Vector3 pos, Map map, int buffer)
    {
      IntVec3 intVec3 = IntVec3Utility.ToIntVec3(pos);
      return intVec3.x >= buffer && intVec3.x < map.Size.x - buffer && intVec3.z >= buffer && intVec3.z < map.Size.z - buffer;
    }

    private void TrySpawnPortal(Vector3 pos)
    {
      if (!this.IsPosInBoundsValid(pos, ((Thing) this.brain.Head).Map, 2))
        return;
      WormWarpVisuals.TriggerPortalOpen(pos, ((Thing) this.brain.Head).Map);
    }

    private void UpdateVisibilityByPlane(
      Vector3 planePoint,
      Vector3 planeNormal,
      bool hideIfCrossed)
    {
      this.CheckVis((WormThingBase) this.brain.Head, planePoint, planeNormal, hideIfCrossed);
      foreach (WormThingBase bodySegment in this.brain.Head.BodySegments)
        this.CheckVis(bodySegment, planePoint, planeNormal, hideIfCrossed);
    }

    private void CheckVis(WormThingBase thing, Vector3 pt, Vector3 norm, bool hideIfCrossed)
    {
      bool flag = (double) Vector3.Dot((thing.ExactPosition - pt), norm) > 0.0;
      thing.IsVisualHidden = flag ? hideIfCrossed : !hideIfCrossed;
    }

    private void SetAllSegmentsVisual(bool visible)
    {
      this.brain.Head.IsVisualHidden = !visible;
      foreach (WormThingBase bodySegment in this.brain.Head.BodySegments)
        bodySegment.IsVisualHidden = !visible;
    }

    private void SpawnWarningLine(Vector3 start, Vector3 end)
    {
      ExoRiftPath exoRiftPath = (ExoRiftPath) ThingMaker.MakeThing(WormDefOf.Mst_WormRiftPath, (ThingDef) null);
      exoRiftPath.StartPos = start;
      exoRiftPath.EndPos = end;
      exoRiftPath.Duration = 100;
      exoRiftPath.Width = 3.5f;
      Map map = ((Thing) this.brain.Head).Map;
      if (map == null)
        return;
      GenSpawn.Spawn((Thing) exoRiftPath, WormUtility.ClampWorldPosToMapCell(map, start), map, (WipeMode) 0);
    }

    private void SwitchState(Phase_WormholeDash.DashState newState)
    {
      this._state = newState;
      this._stateTimer = 0;
    }

    private static WormBody GetTailSegment(List<WormBody> segments)
    {
      return segments == null || segments.Count == 0 ? (WormBody) null : segments[segments.Count - 1];
    }

    public override WeaponIntent GetWeaponIntent() => WeaponIntent.Stop;

    public override void ExposeData()
    {
      base.ExposeData();
      Scribe_Values.Look<Phase_WormholeDash.DashState>(ref this._state, "state", Phase_WormholeDash.DashState.InitialSetup, false);
      Scribe_Values.Look<int>(ref this._dashCount, "dashCount", 0, false);
      Scribe_Values.Look<Vector3>(ref this._portalPos, "portalPos", new Vector3(), false);
      Scribe_Values.Look<Vector3>(ref this._moveDir, "moveDir", new Vector3(), false);
      Scribe_Values.Look<int>(ref this._stateTimer, "stateTimer", 0, false);
    }

    private enum DashState
    {
      InitialSetup,
      InitialEntering,
      Lurking,
      Telegraphing,
      Exiting,
      ReEntrySetup,
      ReEntering,
      FinalExhaustion,
    }
  }
}
