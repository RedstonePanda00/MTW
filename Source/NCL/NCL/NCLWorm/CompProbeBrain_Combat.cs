// Decompiled with JetBrains decompiler
// Type: NCLWorm.CompProbeBrain_Combat
// Assembly: NCL_WormBoss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B627DDDD-3AC9-4D6A-89F6-EA72F95FB570
// Assembly location: C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3555799437\1.6\Assemblies\NCL_WormBoss (3).dll

using System;
using UnityEngine;
using Verse;

#nullable disable
namespace NCLWorm
{
  public class CompProbeBrain_Combat : CompProbeBrain
  {
    private ProbeState _state = ProbeState.Idle;

    private CompProperties_ProbeCombat Props => (CompProperties_ProbeCombat) this.props;

    protected override void UpdateBehavior()
    {
      MovementIntent intent1 = MovementIntent.Invalid;
      WeaponIntent intent2 = WeaponIntent.Stop;
      IntVec3 position = ((Thing) this.parent).Position;
      Vector3 vector3_1 = position.ToVector3();
      Vector3 dest = this._commander == null || ((Thing) this._commander.parent).Destroyed ? vector3_1 : ((Thing) this._commander.parent).DrawPos;
      Vector3 vector3_2 = (vector3_1 - dest);
      float sqrMagnitude = vector3_2.sqrMagnitude;
      float num1 = this.Props.returnDist * this.Props.returnDist;
      float num2 = this.Props.followRadius * this.Props.followRadius;
      this._state = this._commander == null || (double) sqrMagnitude <= (double) num1 ? (this._state != ProbeState.Return || (double) sqrMagnitude >= (double) num2 ? (this._targeter == null || this._targeter.LockedTarget == null ? ProbeState.Idle : ProbeState.Combat) : ProbeState.Idle) : ProbeState.Return;
      switch (this._state)
      {
        case ProbeState.Idle:
          float num3 = (float) (((double) Find.TickManager.TicksGame * 0.5 + (double) this._idOffset) * (Math.PI / 180.0));
          Vector3 vector3_3 = (new Vector3(Mathf.Cos(num3), 0.0f, Mathf.Sin(num3)) * this.Props.followRadius);
          intent1 = MovementIntent.Presets.Cruising((dest + vector3_3)) with
          {
            SpeedFactor = 0.6f
          };
          break;
        case ProbeState.Combat:
          Thing lockedTarget = this._targeter.LockedTarget;
          if (lockedTarget != null)
          {
            intent2 = WeaponIntent.UseVerb(WormVerbTag.Default, new LocalTargetInfo(lockedTarget));
            Vector3 drawPos = lockedTarget.DrawPos;
            Vector3 vector3_4 = (drawPos - vector3_1);
            float magnitude = vector3_4.magnitude;
            float combatRange = this.Props.combatRange;
            float num4 = (float) ((double) Find.TickManager.TicksGame * 0.019999999552965164 + (double) this._idOffset * 0.10000000149011612) + 0.8f;
            float num5 = combatRange;
            if ((double) magnitude > (double) combatRange + 2.0)
              num5 = combatRange * 0.8f;
            else if ((double) magnitude < (double) combatRange - 2.0)
              num5 = combatRange * 1.2f;
            Vector3 vector3_5 = (new Vector3(Mathf.Cos(num4), 0.0f, Mathf.Sin(num4)) * num5);
            Vector3 vector3_6 = (drawPos + vector3_5);
            float num6 = 2.5f;
            float num7 = (float) Find.TickManager.TicksGame * 0.05f;
            float num8 = Mathf.PerlinNoise(num7, this._idOffset) - 0.5f;
            float num9 = Mathf.PerlinNoise(num7 + 100f, this._idOffset) - 0.5f;
            intent1 = MovementIntent.Presets.Precise((vector3_6 + (new Vector3(num8, 0.0f, num9) * num6))) with
            {
              SpeedFactor = 1.3f,
              TurnFactor = 3f
            };
            ref MovementIntent local = ref intent1;
            Vector3 vector3_7 = (drawPos - vector3_1);
            Vector3? nullable = new Vector3?(vector3_7.normalized);
            local.ForceFacing = nullable;
            break;
          }
          break;
        case ProbeState.Return:
          intent1 = MovementIntent.Presets.Cruising(dest) with
          {
            SpeedFactor = 1.2f
          };
          break;
      }
      this._mover.SetIntent(intent1);
      if (this._weapon == null)
        return;
      this._weapon.SetIntent(intent2);
    }

    public override void PostExposeData()
    {
      base.PostExposeData();
      Scribe_Values.Look<ProbeState>(ref this._state, "state", ProbeState.Idle, false);
    }
  }
}
