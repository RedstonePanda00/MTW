using RimWorld;
using UnityEngine;
using Verse;

namespace NCLWorm
{
  // Interrupted by an external counter: knocked back, then fully paralysed (no move, no fire, no point defense).
  public class Phase_Staggered : WormPhase
  {
    private const int KNOCKBACK_TICKS = 30;
    private const float KNOCKBACK_DISTANCE = 6f;
    private int _durationTicks = 300;
    private int _ticks;
    private Vector3 _knockDir = Vector3.back;
    private Vector3 _facing = Vector3.forward;

    public Phase_Staggered()
    {
    }

    public Phase_Staggered(Vector3 knockDir, int durationTicks)
    {
      knockDir.y = 0.0f;
      this._knockDir = knockDir.sqrMagnitude > 0.001f ? knockDir.normalized : Vector3.back;
      this._durationTicks = durationTicks;
    }

    public override float? DesiredStiffness => new float?(0.4f);

    public override string GetStateString() => string.Format("Staggered ({0}/{1})", this._ticks, this._durationTicks);

    public override void OnEnter(TC_WormDecisionController _brain)
    {
      base.OnEnter(_brain);
      WormHead head = this.brain.Head;
      if (head == null)
        return;
      this._facing = head.BodyFacing;
      this._facing.y = 0.0f;
      if (this._ticks == 0 && head.Spawned)
      {
        FleckMaker.Static(head.Position, head.Map, FleckDefOf.ExplosionFlash, 8f);
        if (Find.CurrentMap == head.Map)
          Find.CameraDriver.shaker.DoShake(1.5f);
      }
    }

    public override void Update()
    {
      ++this._ticks;
      if (this._ticks <= KNOCKBACK_TICKS)
      {
        // Linear decay so the total travelled distance equals KNOCKBACK_DISTANCE.
        float peakPerTick = 2f * KNOCKBACK_DISTANCE / KNOCKBACK_TICKS;
        float speed = peakPerTick * (1f - (float) (this._ticks - 1) / KNOCKBACK_TICKS);
        this.brain.Mover?.ForceVelocity(this._knockDir * speed);
      }
      else if (this._ticks == KNOCKBACK_TICKS + 1)
      {
        this.brain.Mover?.ForceVelocity(Vector3.zero);
      }
      if (this._ticks < this._durationTicks)
        return;
      this.Finish();
    }

    public override void UpdateSegmentBehavior(WormBody seg, int index, int totalCount)
    {
      if (Rand.Chance(0.05f))
        WormActions.GlitchVentState(seg, 0.6f);
      else
        WormActions.SetVentState(seg, 0.0f);
      WormActions.OrderCeaseFire(seg);
    }

    public override MovementIntent GetMovementIntent()
    {
      MovementIntent intent = MovementIntent.Presets.Braking(this._facing);
      intent.OverrideAltitude = 0.5f;
      intent.TurnFactor = 0.0f;
      return intent;
    }

    public override WeaponIntent GetWeaponIntent() => WeaponIntent.Stop;

    public override void ExposeData()
    {
      base.ExposeData();
      Scribe_Values.Look<int>(ref this._durationTicks, "stag_duration", 300, false);
      Scribe_Values.Look<int>(ref this._ticks, "stag_ticks", 0, false);
      Scribe_Values.Look<Vector3>(ref this._knockDir, "stag_knockDir", Vector3.back, false);
      Scribe_Values.Look<Vector3>(ref this._facing, "stag_facing", Vector3.forward, false);
    }
  }
}
