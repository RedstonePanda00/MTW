// Decompiled with JetBrains decompiler
// Type: NCLWorm.Phase_DeathSequence
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
  public class Phase_DeathSequence : WormPhase
  {
    private const float DURATION_FAILURE_SEC = 2f;
    private const float DURATION_GLITCH_SEC = 20f;
    private const float DURATION_SILENCE_SEC = 1f;
    private static readonly int TICKS_FAILURE = WormBossConstants.SecondsToTicks(2f);
    private static readonly int TICKS_GLITCH = WormBossConstants.SecondsToTicks(20f);
    private static readonly int TICKS_SILENCE = WormBossConstants.SecondsToTicks(1f);
    private const int SHAKE_INTERVAL_HEAVY = 4;
    private const int SHAKE_INTERVAL_LIGHT = 15;
    private const int EXPLOSION_INTERVAL = 60;
    private Phase_DeathSequence.DeathStage _stage = Phase_DeathSequence.DeathStage.CriticalFailure;
    private int _timer = 0;
    private float _currentSpeed = 4f;

    public override void OnEnter(TC_WormDecisionController _brain)
    {
      base.OnEnter(_brain);
      this.brain.Weapon?.ForceStop();
      Messages.Message((Translator.Translate("Mst_Message_BossCritical")), ((Thing) this.brain.Head), MessageTypeDefOf.ThreatBig, true);
    }

    public override void Update()
    {
      ++this._timer;
      if (((Thing) this.brain.Head).HitPoints < 1)
        ((Thing) this.brain.Head).HitPoints = 1;
      switch (this._stage)
      {
        case Phase_DeathSequence.DeathStage.CriticalFailure:
          this.Update_CriticalFailure();
          break;
        case Phase_DeathSequence.DeathStage.GlitchLoop:
          this.Update_GlitchLoop();
          break;
        case Phase_DeathSequence.DeathStage.FinalSilence:
          this.Update_FinalSilence();
          break;
        case Phase_DeathSequence.DeathStage.Detonation:
          this.Update_Detonation();
          break;
      }
    }

    private void Update_CriticalFailure()
    {
      if (this._timer % 4 == 0)
        Find.CameraDriver.shaker.DoShake(1.5f);
      if (this._timer <= Phase_DeathSequence.TICKS_FAILURE)
        return;
      this.SwitchState(Phase_DeathSequence.DeathStage.GlitchLoop);
    }

    private void Update_GlitchLoop()
    {
      this.DoVisualEffects_Glitch();
      if (this._timer <= Phase_DeathSequence.TICKS_GLITCH)
        return;
      this.SwitchState(Phase_DeathSequence.DeathStage.FinalSilence);
    }

    private void Update_FinalSilence()
    {
      if (this._timer == 1)
      {
        foreach (WormBody bodySegment in this.brain.Head.BodySegments)
          bodySegment.ServoSpeedMultiplier = 1f;
      }
      if (this._timer <= Phase_DeathSequence.TICKS_SILENCE)
        return;
      this.SwitchState(Phase_DeathSequence.DeathStage.Detonation);
    }

    private void Update_Detonation()
    {
      this.NotifyNCLOnDeath();
      this.brain.Head.ForceKillAndDropLoot();
      this.Finish();
    }

    private void NotifyNCLOnDeath()
    {
      WormHead head = this.brain.Head;
      if (head == null)
        return;
      GameComponent gameComponent = (GameComponent) null;
      foreach (GameComponent component in Current.Game.components)
      {
        if (component.GetType().FullName == "NCLWorm.GameComp_NCLWorm")
        {
          gameComponent = component;
          break;
        }
      }
      if (gameComponent == null)
        return;
      Type type = gameComponent.GetType();
      if (((Thing) head).Faction != null && FactionUtility.HostileTo(((Thing) head).Faction, Faction.OfPlayer))
      {
        type.GetField("inWormWar")?.SetValue((object) gameComponent, (object) false);
        if (((Thing) head).Map?.weatherManager != null)
          ((Thing) head).Map.weatherManager.curWeather = WeatherDefOf.Clear;
        Messages.Message((Translator.Translate("NCLWormWarEnd")), MessageTypeDefOf.PositiveEvent, true);
      }
      else
      {
        Faction faction = ((Thing) head).Faction;
        if (faction == null || !faction.IsPlayer)
          return;
        type.GetField("ReLongTime")?.SetValue((object) gameComponent, (object) 0);
      }
    }

    public override void UpdateSegmentBehavior(WormBody seg, int index, int totalCount)
    {
      switch (this._stage)
      {
        case Phase_DeathSequence.DeathStage.CriticalFailure:
          WormActions.SetVentState(seg, 1f);
          break;
        case Phase_DeathSequence.DeathStage.GlitchLoop:
          WormActions.GlitchVentState(seg);
          break;
        case Phase_DeathSequence.DeathStage.FinalSilence:
          WormActions.SetVentState(seg, 1f);
          break;
      }
      WormActions.OrderCeaseFire(seg);
    }

    private void DoVisualEffects_Glitch()
    {
      if (this._timer % 15 == 0)
        Find.CameraDriver.shaker.DoShake(0.8f);
      if (this._timer % 60 != 0)
        return;
      List<WormBody> bodySegments = this.brain.Head.BodySegments;
      if (bodySegments != null && bodySegments.Count > 0)
      {
        WormBody wormBody = GenCollection.RandomElement<WormBody>((IEnumerable<WormBody>) bodySegments);
        if (wormBody != null && !((Thing) wormBody).Destroyed)
        {
          GenExplosion.DoExplosion(((Thing) wormBody).Position, ((Thing) wormBody).Map, 2.9f, DamageDefOf.Bomb, (Thing) null, 10, -1f, (SoundDef) null, (ThingDef) null, (ThingDef) null, (Thing) null, (ThingDef) null, 0.0f, 1, new GasType?(), new float?(), (int) byte.MaxValue, false, (ThingDef) null, 0.0f, 1, 0.0f, false, new float?(), (List<Thing>) null, new FloatRange?(), true, 1f, 0.0f, true, (ThingDef) null, 1f, (SimpleCurve) null, (List<IntVec3>) null, (ThingDef) null, (ThingDef) null);
          FleckMaker.ThrowMicroSparks(((Thing) wormBody).DrawPos, ((Thing) wormBody).Map);
        }
      }
    }

    private void SwitchState(Phase_DeathSequence.DeathStage newState)
    {
      this._stage = newState;
      this._timer = 0;
    }

    public override MovementIntent GetMovementIntent()
    {
      if (this._stage == Phase_DeathSequence.DeathStage.Detonation || this._stage == Phase_DeathSequence.DeathStage.FinalSilence)
        return MovementIntent.Invalid;
      Map map = ((Thing) this.brain.Head).Map;
      Vector3 vector3;
      // ISSUE: explicit constructor call
      vector3 = new Vector3((float) map.Size.x / 2f, 0.0f, (float) map.Size.z / 2f);
      this.UpdateSpeedLogic(vector3);
      MovementIntent movementIntent = MovementIntent.Presets.Cruising(vector3, (float) (0.5 + (double) Mathf.Sin((float) Find.TickManager.TicksGame * 0.1f) * 0.5));
      float valueOrDefault = this.brain.Mover?.Props != null ? this.brain.Mover.Props.baseSpeed : 6f;
      movementIntent.SpeedFactor = this._currentSpeed / valueOrDefault;
      movementIntent.TurnFactor = 0.22f;
      return movementIntent;
    }

    private void UpdateSpeedLogic(Vector3 mapCenter)
    {
      if (this._stage == Phase_DeathSequence.DeathStage.GlitchLoop)
        this._currentSpeed = Mathf.Lerp(4f, 0.5f, (float) this._timer / (float) Phase_DeathSequence.TICKS_GLITCH);
      if ((double) WormMath.Distance2D(this.brain.Head.ExactPosition, mapCenter) <= 40.0)
        return;
      this._currentSpeed = Mathf.Max(this._currentSpeed, 2f);
    }

    public override WeaponIntent GetWeaponIntent() => WeaponIntent.Stop;

    public override void ExposeData()
    {
      base.ExposeData();
      Scribe_Values.Look<Phase_DeathSequence.DeathStage>(ref this._stage, "stage", Phase_DeathSequence.DeathStage.CriticalFailure, false);
      Scribe_Values.Look<int>(ref this._timer, "timer", 0, false);
      Scribe_Values.Look<float>(ref this._currentSpeed, "currentSpeed", 0.0f, false);
    }

    private enum DeathStage
    {
      CriticalFailure,
      GlitchLoop,
      FinalSilence,
      Detonation,
    }
  }
}
