// Decompiled with JetBrains decompiler
// Type: NCLWorm.CompProbeBrain_Repair
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
  public class CompProbeBrain_Repair : CompProbeBrain
  {
    private WormBody _targetSegment;
    private int _switchTargetTimer = 0;
    private int _repairActionTimer = 0;

    private CompProperties_ProbeRepair Props => (CompProperties_ProbeRepair) this.props;

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
      base.PostSpawnSetup(respawningAfterLoad);
    }

    protected override void UpdateBehavior()
    {
      if (this.Commander == null || ((Thing) this.Commander.parent).Destroyed || !(this.Commander.parent is WormHead parent))
        return;
      if (this._targetSegment == null || ((Thing) this._targetSegment).Destroyed || this._switchTargetTimer <= 0)
      {
        this.PickNewSegment(parent);
        this._switchTargetTimer = Rand.Range(300, 600);
      }
      --this._switchTargetTimer;
      WormThingBase wormThingBase = this._targetSegment != null ? (WormThingBase) this._targetSegment : (WormThingBase) parent;
      this.PerformOrbitMovement(wormThingBase);
      this.DoRepairLogic(parent, wormThingBase);
    }

    private void PickNewSegment(WormHead head)
    {
      if (head.BodySegments != null && head.BodySegments.Count > 0)
      {
        if (Rand.Chance(0.7f))
          this._targetSegment = GenCollection.RandomElement<WormBody>((IEnumerable<WormBody>) head.BodySegments);
        else
          this._targetSegment = (WormBody) null;
      }
      else
        this._targetSegment = (WormBody) null;
    }

    private void PerformOrbitMovement(WormThingBase anchor)
    {
      if (anchor == null || ((Thing) anchor).Destroyed || !(this.parent is WormThingBase parent))
        return;
      float num = (float) (((double) Find.TickManager.TicksGame * 2.0 + (double) this._idOffset) * (Math.PI / 180.0));
      Vector3 vector3_1 = (new Vector3(Mathf.Cos(num), 0.0f, Mathf.Sin(num)) * this.Props.orbitRadius);
      Vector3 vector3_2 = (anchor.ExactPosition + vector3_1);
      Vector3 vector3_3 = (parent.ExactPosition - vector3_2);
      if ((double) vector3_3.sqrMagnitude > 900.0)
      {
        parent.ForceSetPosition(vector3_2, anchor.ExactVelocity, anchor.BodyFacing);
        this._mover.ForceVelocity(anchor.ExactVelocity);
      }
      else
      {
        MovementIntent intent = MovementIntent.Presets.Cruising(vector3_2) with
        {
          RelativeTo = (Thing) anchor,
          SpeedFactor = 1f,
          AccelFactor = 4f,
          TurnFactor = 4f
        };
        ref MovementIntent local = ref intent;
        vector3_3 = (anchor.ExactPosition - ((Thing) parent).DrawPos);
        Vector3? nullable = new Vector3?(vector3_3.normalized);
        local.ForceFacing = nullable;
        this._mover.SetIntent(intent);
      }
    }

    private void DoRepairLogic(WormHead head, WormThingBase visualAnchor)
    {
      if (((Thing) head).HitPoints >= ((Thing) head).MaxHitPoints)
        return;
      if (this._repairActionTimer > 0)
      {
        --this._repairActionTimer;
      }
      else
      {
        Vector3 vector3 = (((Thing) this.parent).DrawPos - ((Thing) visualAnchor).DrawPos);
        float sqrMagnitude = vector3.sqrMagnitude;
        float num = this.Props.orbitRadius + 3f;
        if ((double) sqrMagnitude > (double) num * (double) num)
          return;
        this._repairActionTimer = this.Props.repairInterval;
        int hitPoints = ((Thing) head).HitPoints;
        ((Thing) head).HitPoints = Mathf.Min(((Thing) head).HitPoints + this.Props.repairAmount, ((Thing) head).MaxHitPoints);
        if (((Thing) head).HitPoints <= hitPoints)
          return;
        MoteMaker.ThrowText(((Thing) visualAnchor).DrawPos, ((Thing) visualAnchor).Map, "+" + (((Thing) head).HitPoints - hitPoints).ToString(), Color.green, -1f);
      }
    }

    public override void PostExposeData()
    {
      base.PostExposeData();
      Scribe_References.Look<WormBody>(ref this._targetSegment, "targetSegment", false);
      Scribe_Values.Look<int>(ref this._switchTargetTimer, "switchTargetTimer", 0, false);
      Scribe_Values.Look<int>(ref this._repairActionTimer, "repairActionTimer", 0, false);
    }
  }
}
