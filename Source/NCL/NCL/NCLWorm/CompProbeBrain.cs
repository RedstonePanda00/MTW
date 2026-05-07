// Decompiled with JetBrains decompiler
// Type: NCLWorm.CompProbeBrain
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
  public class CompProbeBrain : ThingComp
  {
    protected TC_WormMovingController _mover;
    protected TC_WormWeaponController _weapon;
    protected CompWormTargeter _targeter;
    protected CompSwarmController _commander;
    protected ProbeCommand _overrideOrder = new ProbeCommand()
    {
      Type = ProbeCommandType.None
    };
    protected float _idOffset;

    public CompSwarmController Commander => this._commander;

    public virtual void PostSpawnSetup(bool respawningAfterLoad)
    {
      base.PostSpawnSetup(respawningAfterLoad);
      this._mover = this.parent.GetComp<TC_WormMovingController>();
      this._weapon = this.parent.GetComp<TC_WormWeaponController>();
      this._targeter = this.parent.GetComp<CompWormTargeter>();
      this._idOffset = (float) ((Thing) this.parent).thingIDNumber % 360f;
      if (respawningAfterLoad)
        return;
      this.FindAndRegisterCommander();
    }

    public virtual void PostDeSpawn(Map map, DestroyMode mode = 0)
    {
      this._commander?.Deregister(this);
      base.PostDeSpawn(map, (DestroyMode) 0);
    }

    public void FindAndRegisterCommander()
    {
      WormBossMapComponent component = ((Thing) this.parent).Map.GetComponent<WormBossMapComponent>();
      if (component == null)
        return;
      WormBossMapComponent bossMapComponent = component;
      IntVec3 position = ((Thing) this.parent).Position;
      Vector3 vector3 = position.ToVector3();
      WormHead closestBoss = bossMapComponent.GetClosestBoss(vector3);
      if (closestBoss != null)
        this.SetCommander(closestBoss.GetComp<CompSwarmController>());
    }

    public void SetCommander(CompSwarmController newCommander)
    {
      if (this._commander != null && this._commander != newCommander)
        this._commander.Deregister(this);
      this._commander = newCommander;
      this._commander?.Register(this);
    }

    public void SetOrder(ProbeCommand cmd) => this._overrideOrder = cmd;

    public void ClearOrder()
    {
      this._overrideOrder = new ProbeCommand()
      {
        Type = ProbeCommandType.None
      };
    }

    public virtual void CompTick()
    {
      base.CompTick();
      if (this._mover == null)
        return;
      if (this._commander == null || ((Thing) this._commander.parent).Destroyed)
      {
        this._commander = (CompSwarmController) null;
        if (Gen.IsHashIntervalTick((Thing) this.parent, 60))
          this.FindAndRegisterCommander();
      }
      if (this._overrideOrder.IsValid)
        this.ExecuteOverrideOrder();
      else
        this.UpdateBehavior();
    }

    protected virtual void UpdateBehavior()
    {
    }

    protected virtual void ExecuteOverrideOrder()
    {
      MovementIntent intent1 = MovementIntent.Invalid;
      WeaponIntent intent2 = WeaponIntent.Stop;
      switch (this._overrideOrder.Type)
      {
        case ProbeCommandType.MoveToPosition:
          intent1 = MovementIntent.Presets.Precise(this._overrideOrder.TargetPos);
          ref MovementIntent local = ref intent1;
          Vector3 vector3_1 = (this._overrideOrder.TargetPos - ((Thing) this.parent).DrawPos);
          Vector3? nullable = new Vector3?(vector3_1.normalized);
          local.ForceFacing = nullable;
          break;
        case ProbeCommandType.FocusFire:
          Thing targetThing = this._overrideOrder.TargetThing;
          if (targetThing != null && !targetThing.Destroyed)
          {
            Vector3 drawPos1 = targetThing.DrawPos;
            IntVec3 position1 = ((Thing) this.parent).Position;
            Vector3 vector3_2 = position1.ToVector3();
            Vector3 vector3_3 = (drawPos1 - vector3_2);
            MovementIntent movementIntent;
            if ((double) vector3_3.magnitude <= 15.0)
            {
              Vector3 drawPos2 = targetThing.DrawPos;
              IntVec3 position2 = ((Thing) this.parent).Position;
              Vector3 vector3_4 = position2.ToVector3();
              Vector3 vector3_5 = (drawPos2 - vector3_4);
              movementIntent = MovementIntent.Presets.Braking(vector3_5.normalized);
            }
            else
              movementIntent = MovementIntent.Presets.Cruising(targetThing.DrawPos);
            intent1 = movementIntent;
            intent2 = WeaponIntent.UseVerb(WormVerbTag.Default, new LocalTargetInfo(targetThing));
            break;
          }
          this.ClearOrder();
          break;
        case ProbeCommandType.SelfDestruct:
          if (this._overrideOrder.TargetThing != null)
          {
            intent1 = MovementIntent.Presets.Ramming(this._overrideOrder.TargetThing.DrawPos);
            if ((double) IntVec3Utility.DistanceTo(((Thing) this.parent).Position, this._overrideOrder.TargetThing.Position) < 2.9000000953674316)
              this.DoSelfDestruct();
            break;
          }
          break;
      }
      this._mover.SetIntent(intent1);
      this._weapon?.SetIntent(intent2);
    }

    protected void DoSelfDestruct()
    {
      GenExplosion.DoExplosion(((Thing) this.parent).Position, ((Thing) this.parent).Map, 3.9f, DamageDefOf.Bomb, (Thing) this.parent, -1, -1f, (SoundDef) null, (ThingDef) null, (ThingDef) null, (Thing) null, (ThingDef) null, 0.0f, 1, new GasType?(), new float?(), (int) byte.MaxValue, false, (ThingDef) null, 0.0f, 1, 0.0f, false, new float?(), (List<Thing>) null, new FloatRange?(), true, 1f, 0.0f, true, (ThingDef) null, 1f, (SimpleCurve) null, (List<IntVec3>) null, (ThingDef) null, (ThingDef) null);
      ((Thing) this.parent).Destroy((DestroyMode) 0);
    }

    public virtual void PostExposeData()
    {
      base.PostExposeData();
      Scribe_Deep.Look<ProbeCommand>(ref this._overrideOrder, "overrideOrder", Array.Empty<object>());
      Scribe_Values.Look<float>(ref this._idOffset, "idOffset", 0.0f, false);
      Thing parent = (Thing) this._commander?.parent;
      Scribe_References.Look<Thing>(ref parent, "commanderThing", false);
      if (Scribe.mode != LoadSaveMode.PostLoadInit || parent == null)
        return;
      this._commander = ThingCompUtility.TryGetComp<CompSwarmController>(parent);
    }
  }
}
