// Decompiled with JetBrains decompiler
// Type: NCLWorm.Phase_StormCircle
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
  public class Phase_StormCircle : WormPhase
  {
    private const int TICKS_CHANNEL_TOTAL = 1200;
    private const int TICKS_DISPERSE = 120;
    private const int WAVE_COUNT_SMALL = 8;
    private const int WAVE_INTERVAL_TICKS = 90;
    private const int LIGHTNING_BASE_COUNT = 3;
    private const int LIGHTNING_INCREMENT = 5;
    private const int TARGETED_BASE_COUNT = 1;
    private const int TARGETED_INCREMENT = 1;
    private const int TARGETED_INCREMENT_INTERVAL = 1;
    private const int CLIMAX_DELAY_TICKS = 120;
    private const int CLIMAX_AMBIENT_COUNT = 25;
    private Phase_StormCircle.StormState _state = Phase_StormCircle.StormState.Calculating;
    private int _stateTimer = 0;
    private WeatherDef _previousWeather;
    private float _targetRadius;
    private float _entryRadius;
    private float _angularSpeedRad;
    private float _visualWaveSpeed;
    private float _flySpeed;
    private Vector3 _stormCenter;
    private float _currentAngleRad;
    private float _spoolingProgressRad;
    private Thing _activeTornado;
    private Queue<Phase_StormCircle.StormWave> _timeline = new Queue<Phase_StormCircle.StormWave>();

    public override float? DesiredStiffness
    {
      get
      {
        switch (this._state)
        {
          case Phase_StormCircle.StormState.Spooling:
            return new float?(1f);
          case Phase_StormCircle.StormState.Channeling:
            return new float?(0.2f);
          default:
            return new float?(0.5f);
        }
      }
    }

    public override void OnEnter(TC_WormDecisionController _brain)
    {
      base.OnEnter(_brain);
      this._state = Phase_StormCircle.StormState.Calculating;
      this._activeTornado = (Thing) null;
      this._timeline.Clear();
      if (((Thing) this.brain.Head).Map != null)
      {
        this._previousWeather = ((Thing) this.brain.Head).Map.weatherManager.curWeather;
        if (WormDefOf.RainyThunderstorm != null)
          ((Thing) this.brain.Head).Map.weatherManager.TransitionTo(WormDefOf.RainyThunderstorm);
      }
      this.InitializeDynamicStats();
      this.BuildStormTimeline();
    }

    public override void OnExit()
    {
      base.OnExit();
      if (this._activeTornado != null)
      {
        if (this._activeTornado is Mst_StaticTornado activeTornado && !((Thing) activeTornado).Destroyed)
        {
          activeTornado.StartFadeOut();
          this._activeTornado = (Thing) null;
        }
        else if (!this._activeTornado.Destroyed)
          this._activeTornado.Destroy((DestroyMode) 0);
      }
      this._timeline.Clear();
      if (((Thing) this.brain.Head).Map == null || this._previousWeather == null)
        return;
      ((Thing) this.brain.Head).Map.weatherManager.TransitionTo(this._previousWeather);
    }

    private void BuildStormTimeline()
    {
      int num1 = 60;
      for (int index = 0; index < 8; ++index)
      {
        int num2 = 3 + index * 5;
        if (Rand.Chance(0.3f))
          ++num2;
        int num3 = 1 + index / 1;
        this._timeline.Enqueue(new Phase_StormCircle.StormWave()
        {
          TriggerTick = num1,
          AmbientCount = num2,
          TargetedCount = num3
        });
        num1 += 90;
      }
      int num4 = num1 + 120;
      this._timeline.Enqueue(new Phase_StormCircle.StormWave()
      {
        TriggerTick = num4,
        AmbientCount = 25,
        TargetedCount = 50
      });
    }

    private void InitializeDynamicStats()
    {
      WormHead head = this.brain.Head;
      TCP_WormMovingController props = this.brain.Mover.Props;
      this._flySpeed = props.baseSpeed;
      float turnRate = props.turnRate;
      this._targetRadius = Mathf.Max(Mathf.Max((float) ((double) ((float) (head.bodySegmentCount - 1) * head.segmentSpacing) * 1.1499999761581421 / 6.2831854820251465), 10f), this._flySpeed / (turnRate * ((float) Math.PI / 180f)) * 1.1f);
      this._entryRadius = this._targetRadius * 1.75f;
      this._angularSpeedRad = (float) ((double) this._flySpeed / (double) this._targetRadius / 60.0);
      this._visualWaveSpeed = 0.25f;
    }

    public override void Update()
    {
      ++this._stateTimer;
      switch (this._state)
      {
        case Phase_StormCircle.StormState.Calculating:
          this.CalculateGeometry();
          break;
        case Phase_StormCircle.StormState.Relocating:
          this.CheckRelocationArrival();
          break;
        case Phase_StormCircle.StormState.Spooling:
          this.UpdateSpiralPhysics();
          break;
        case Phase_StormCircle.StormState.Channeling:
          this.UpdateHoldingPhysics();
          this.MaintainTornado();
          this.UpdateTimelineExecution();
          if (this._stateTimer <= 1200)
            break;
          this.SwitchState(Phase_StormCircle.StormState.Dispersing);
          break;
        case Phase_StormCircle.StormState.Dispersing:
          if (this._activeTornado != null)
          {
            if (this._activeTornado is Mst_StaticTornado activeTornado && !((Thing) activeTornado).Destroyed)
            {
              activeTornado.StartFadeOut();
              this._activeTornado = (Thing) null;
            }
            else if (!this._activeTornado.Destroyed)
            {
              this._activeTornado.Destroy((DestroyMode) 0);
              this._activeTornado = (Thing) null;
            }
          }
          if (this._stateTimer <= 120)
            break;
          this.Finish();
          break;
      }
    }

    private void UpdateTimelineExecution()
    {
      if (this._timeline.Count <= 0)
        return;
      Phase_StormCircle.StormWave wave = this._timeline.Peek();
      if (this._stateTimer >= wave.TriggerTick)
      {
        this.ExecuteWave(wave);
        this._timeline.Dequeue();
      }
    }

    private void ExecuteWave(Phase_StormCircle.StormWave wave)
    {
      if (wave.AmbientCount > 0)
        this.SpawnAmbientLightningBatch(wave.AmbientCount);
      if (wave.TargetedCount <= 0)
        return;
      if (wave.TargetedCount > 10)
        Find.CameraDriver.shaker.DoShake(1f);
      this.SpawnTargetedLightning(wave.TargetedCount);
    }

    private void SpawnAmbientLightningBatch(int count)
    {
      Map map = ((Thing) this.brain.Head).Map;
      IntVec3 size = map.Size;
      for (int index = 0; index < count; ++index)
      {
        IntVec3 intVec3;
        // ISSUE: explicit constructor call
        intVec3 = new IntVec3(Rand.Range(0, size.x), 0, Rand.Range(0, size.z));
        if (GenGrid.InBounds(intVec3, map))
          map.weatherManager.eventHandler.AddEvent((WeatherEvent) new WeatherEvent_LightningStrike(map, intVec3));
      }
    }

    private void SpawnTargetedLightning(int totalStrikeCount)
    {
      Map map = ((Thing) this.brain.Head).Map;
      List<Pawn> pawnList = new List<Pawn>();
      foreach (Pawn pawn in (IEnumerable<Pawn>) map.mapPawns.AllPawnsSpawned)
      {
        if (!pawn.Dead && !pawn.Downed && GenHostility.HostileTo((Thing) pawn, (Thing) this.brain.Head))
          pawnList.Add(pawn);
      }
      if (pawnList.Count == 0)
        return;
      if (pawnList.Count >= totalStrikeCount)
      {
        int num = 0;
        foreach (Pawn p in GenCollection.InRandomOrder<Pawn>((IEnumerable<Pawn>) pawnList, (IList<Pawn>) null))
        {
          if (num >= totalStrikeCount)
            break;
          this.StrikePawn(p, map);
          ++num;
        }
      }
      else
      {
        int num1 = totalStrikeCount / pawnList.Count;
        int num2 = totalStrikeCount % pawnList.Count;
        foreach (Pawn p in pawnList)
        {
          for (int index = 0; index < num1; ++index)
            this.StrikePawn(p, map);
        }
        int num3 = 0;
        foreach (Pawn p in GenCollection.InRandomOrder<Pawn>((IEnumerable<Pawn>) pawnList, (IList<Pawn>) null))
        {
          if (num3 < num2)
          {
            this.StrikePawn(p, map);
            ++num3;
          }
          else
            break;
        }
      }
    }

    private void StrikePawn(Pawn p, Map map)
    {
      map.weatherManager.eventHandler.AddEvent((WeatherEvent) new WeatherEvent_LightningStrike(map, ((Thing) p).Position));
    }

    private void MaintainTornado()
    {
      Map map = ((Thing) this.brain.Head).Map;
      if (map == null)
        return;
      IntVec3 mapCell = WormUtility.ClampWorldPosToMapCell(map, this._stormCenter);
      if ((this._activeTornado == null || this._activeTornado.Destroyed) && WormDefOf.Mst_StaticTornado != null)
        this._activeTornado = GenSpawn.Spawn(WormDefOf.Mst_StaticTornado, mapCell, map, (WipeMode) 0);
      if (this._activeTornado == null || !(this._activeTornado.Position != mapCell))
        return;
      this._activeTornado.Position = mapCell;
    }

    private void CalculateGeometry()
    {
      this._stormCenter = this.FindBestArenaCenter();
      Vector3 vector3 = (this.brain.Head.ExactPosition - this._stormCenter);
      this._currentAngleRad = Mathf.Atan2(vector3.z, vector3.x);
      this._spoolingProgressRad = 0.0f;
      this.SwitchState(Phase_StormCircle.StormState.Relocating);
    }

    private Vector3 FindBestArenaCenter()
    {
      Map map = ((Thing) this.brain.Head).Map;
      Vector3 bestArenaCenter;
      // ISSUE: explicit constructor call
      bestArenaCenter = new Vector3((float) map.Size.x / 2f, 0.0f, (float) map.Size.z / 2f);
      Vector3 vector3_1 = Vector3.zero;
      int num = 0;
      foreach (Pawn pawn in (IEnumerable<Pawn>) map.mapPawns.AllPawnsSpawned)
      {
        if (!pawn.Dead && !pawn.Downed && GenHostility.HostileTo((Thing) pawn, (Thing) this.brain.Head))
        {
          vector3_1 = (vector3_1 + ((Thing) pawn).DrawPos);
          ++num;
        }
      }
      if (num <= 0)
        return bestArenaCenter;
      Vector3 vector3_2 = (vector3_1 / (float) num);
      vector3_2.y = 0.0f;
      return WormUtility.ClampToMap(Vector3.Lerp(bestArenaCenter, vector3_2, 0.6f), map, (int) ((double) this._targetRadius + 5.0));
    }

    private void CheckRelocationArrival()
    {
      if ((double) WormMath.Distance2D(this.brain.Head.ExactPosition, this.GetOrbitPosition(this._stormCenter, this._entryRadius, this._currentAngleRad)) >= 5.0 && this._stateTimer <= 300)
        return;
      this.SwitchState(Phase_StormCircle.StormState.Spooling);
    }

    private void UpdateSpiralPhysics()
    {
      float num1 = Mathf.Clamp01(this._spoolingProgressRad / 6.28318548f);
      float num2 = (float) ((double) this._flySpeed / (double) Mathf.Lerp(this._entryRadius, this._targetRadius, num1) / 60.0);
      this._currentAngleRad += num2;
      this._spoolingProgressRad += num2;
      if ((double) num1 < 1.0)
        return;
      this.SwitchState(Phase_StormCircle.StormState.Channeling);
    }

    private void UpdateHoldingPhysics() => this._currentAngleRad += this._angularSpeedRad;

    public override MovementIntent GetMovementIntent()
    {
      if (this._state == Phase_StormCircle.StormState.Relocating)
        return MovementIntent.Presets.Ramming(this.GetOrbitPosition(this._stormCenter, this._entryRadius, this._currentAngleRad)) with
        {
          ForceFacing = new Vector3?(this.GetTangentDirection(this._currentAngleRad)),
          SpeedFactor = 1f
        };
      if (this._state == Phase_StormCircle.StormState.Spooling || this._state == Phase_StormCircle.StormState.Channeling)
        return MovementIntent.Presets.Ramming(this.GetOrbitPosition(this._stormCenter, this._state == Phase_StormCircle.StormState.Spooling ? Mathf.Lerp(this._entryRadius, this._targetRadius, Mathf.Clamp01(this._spoolingProgressRad / 6.28318548f)) : this._targetRadius, this._currentAngleRad)) with
        {
          OverrideAltitude = 1f,
          SpeedFactor = 1f,
          TurnFactor = 50f,
          AccelFactor = 50f,
          ForceFacing = new Vector3?(this.GetTangentDirection(this._currentAngleRad))
        };
      return this._state == Phase_StormCircle.StormState.Dispersing ? MovementIntent.Presets.Cruising((this.brain.Head.ExactPosition + (this.GetTangentDirection(this._currentAngleRad) * 60f)), 2f) : MovementIntent.Invalid;
    }

    public override void UpdateSegmentBehavior(WormBody seg, int index, int totalCount)
    {
      WormActions.OrderCeaseFire(seg);
      if (this._state == Phase_StormCircle.StormState.Channeling)
      {
        float num1 = (float) ((double) index * 0.800000011920929 - (double) this._stateTimer * (double) this._visualWaveSpeed * 0.800000011920929);
        float num2 = 15f;
        float num3 = Mathf.Abs(Mathf.Repeat(num1, num2) - num2 / 2f);
        float num4 = 0.0f;
        if ((double) num3 <= 1.5)
          num4 = 1f;
        else if ((double) num3 <= 3.5)
          num4 = (float) (1.0 - ((double) num3 - 1.5) / 2.0);
        WormActions.SetVentState(seg, Mathf.Clamp01(num4));
      }
      else if (this._state == Phase_StormCircle.StormState.Spooling)
        WormActions.SetVentState(seg, 0.2f);
      else
        WormActions.SetVentState(seg, 0.0f);
    }

    private Vector3 GetOrbitPosition(Vector3 center, float radius, float angleRad)
    {
      return new Vector3(center.x + Mathf.Cos(angleRad) * radius, 0.0f, center.z + Mathf.Sin(angleRad) * radius);
    }

    private Vector3 GetTangentDirection(float angleRad)
    {
      return new Vector3(-Mathf.Sin(angleRad), 0.0f, Mathf.Cos(angleRad));
    }

    private void SwitchState(Phase_StormCircle.StormState s)
    {
      this._state = s;
      this._stateTimer = 0;
    }

    public override WeaponIntent GetWeaponIntent() => WeaponIntent.Stop;

    public override void ExposeData()
    {
      base.ExposeData();
      Scribe_Values.Look<Phase_StormCircle.StormState>(ref this._state, "state", Phase_StormCircle.StormState.Calculating, false);
      Scribe_Values.Look<int>(ref this._stateTimer, "stateTimer", 0, false);
      Scribe_Values.Look<float>(ref this._targetRadius, "targetRadius", 0.0f, false);
      Scribe_Values.Look<float>(ref this._entryRadius, "entryRadius", 0.0f, false);
      Scribe_Values.Look<float>(ref this._angularSpeedRad, "angularSpeedRad", 0.0f, false);
      Scribe_Values.Look<float>(ref this._flySpeed, "flySpeed", 0.0f, false);
      Scribe_Values.Look<float>(ref this._visualWaveSpeed, "visualWaveSpeed", 0.0f, false);
      Scribe_Values.Look<Vector3>(ref this._stormCenter, "stormCenter", new Vector3(), false);
      Scribe_Values.Look<float>(ref this._currentAngleRad, "currentAngle", 0.0f, false);
      Scribe_Values.Look<float>(ref this._spoolingProgressRad, "spoolProgress", 0.0f, false);
      Scribe_Defs.Look<WeatherDef>(ref this._previousWeather, "previousWeather");
      Scribe_References.Look<Thing>(ref this._activeTornado, "tornado", false);
      List<Phase_StormCircle.StormWave> stormWaveList = (List<Phase_StormCircle.StormWave>) null;
      if (Scribe.mode == LoadSaveMode.LoadingVars && this._timeline != null)
        stormWaveList = new List<Phase_StormCircle.StormWave>((IEnumerable<Phase_StormCircle.StormWave>) this._timeline);
      Scribe_Collections.Look<Phase_StormCircle.StormWave>(ref stormWaveList, "timeline", (LookMode) 2, Array.Empty<object>());
      if (Scribe.mode != LoadSaveMode.PostLoadInit || stormWaveList == null)
        return;
      this._timeline = new Queue<Phase_StormCircle.StormWave>();
      foreach (Phase_StormCircle.StormWave stormWave in stormWaveList)
        this._timeline.Enqueue(stormWave);
    }

    private enum StormState
    {
      Calculating,
      Relocating,
      Spooling,
      Channeling,
      Dispersing,
    }

    private struct StormWave : IExposable
    {
      public int TriggerTick;
      public int AmbientCount;
      public int TargetedCount;

      public void ExposeData()
      {
        Scribe_Values.Look<int>(ref this.TriggerTick, "TriggerTick", 0, false);
        Scribe_Values.Look<int>(ref this.AmbientCount, "AmbientCount", 0, false);
        Scribe_Values.Look<int>(ref this.TargetedCount, "TargetedCount", 0, false);
      }
    }
  }
}
