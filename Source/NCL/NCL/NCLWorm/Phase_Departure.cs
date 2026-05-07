// Decompiled with JetBrains decompiler
// Type: NCLWorm.Phase_Departure
// Assembly: NCL_WormBoss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B627DDDD-3AC9-4D6A-89F6-EA72F95FB570
// Assembly location: C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3555799437\1.6\Assemblies\NCL_WormBoss (3).dll

using RimWorld;
using UnityEngine;
using Verse;

#nullable disable
namespace NCLWorm
{
  public class Phase_Departure : WormPhase
  {
    private float _speedFactor = 0.5f;
    private Vector3 _departureDirection;
    private int _ticksInThisPhase = 0;

    public override string GetStateString() => "Departing";

    public override void OnEnter(TC_WormDecisionController _brain)
    {
      base.OnEnter(_brain);
      this.IsFinished = false;
      this._departureDirection = this.brain?.Head?.BodyFacing ?? Vector3.forward;
      this.brain?.Swarm?.ReleaseAll();
      Messages.Message((Translator.Translate("Boss_Worm_Departure")), MessageTypeDefOf.NeutralEvent, false);
    }

    public override void OnExit()
    {
    }

    public override void Update()
    {
      ++this._ticksInThisPhase;
      this._speedFactor = Mathf.Lerp(this._speedFactor, 4f, 0.02f);
      if (this.brain?.Head?.BodySegments != null)
      {
        for (int index = 0; index < this.brain.Head.BodySegments.Count; ++index)
          WormActions.SetVentState(this.brain.Head.BodySegments[index], 0.0f);
      }
      if (this._ticksInThisPhase <= 180 || this.brain?.Head == null || ((Thing) this.brain.Head).Destroyed)
        return;
      ((Thing) this.brain.Head).Destroy((DestroyMode) 0);
    }

    public override void UpdateSegmentBehavior(WormBody seg, int index, int totalCount)
    {
    }

    public override MovementIntent GetMovementIntent()
    {
      if (this.brain?.Head == null)
        return MovementIntent.Invalid;
      return new MovementIntent()
      {
        TargetPosition = (this.brain.Head.ExactPosition + (this._departureDirection * 1000f)),
        OverrideAltitude = (float) (1.0 + (double) this._ticksInThisPhase * 0.10000000149011612),
        SpeedFactor = this._speedFactor,
        TurnFactor = 0.1f,
        AccelFactor = 1f,
        IsValid = true
      };
    }

    public override WeaponIntent GetWeaponIntent() => WeaponIntent.Stop;

    public override void ExposeData()
    {
      base.ExposeData();
      Scribe_Values.Look<float>(ref this._speedFactor, "dep_speedFactor", 0.5f, false);
      Scribe_Values.Look<int>(ref this._ticksInThisPhase, "dep_ticks", 0, false);
      Scribe_Values.Look<Vector3>(ref this._departureDirection, "dep_dir", Vector3.forward, false);
    }
  }
}
