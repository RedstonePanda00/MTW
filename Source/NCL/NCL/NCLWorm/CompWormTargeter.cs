// Decompiled with JetBrains decompiler
// Type: NCLWorm.CompWormTargeter
// Assembly: NCL_WormBoss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B627DDDD-3AC9-4D6A-89F6-EA72F95FB570
// Assembly location: C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3555799437\1.6\Assemblies\NCL_WormBoss (3).dll

using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

#nullable disable
namespace NCLWorm
{
  public class CompWormTargeter : ThingComp
  {
    private Thing _lockedTarget;
    private int _ticksSinceLastScan = 0;
    private const int SCAN_INTERVAL = 60;

    public Thing LockedTarget
    {
      get
      {
        if (!this.IsTargetValid(this._lockedTarget))
          this._lockedTarget = this.FindBestTargetGlobal();
        return this._lockedTarget;
      }
    }

    public LocalTargetInfo CurrentTargetInfo
    {
      get
      {
        return this.LockedTarget == null ? LocalTargetInfo.Invalid : new LocalTargetInfo(this.LockedTarget);
      }
    }

    public virtual void PostSpawnSetup(bool respawningAfterLoad)
    {
      base.PostSpawnSetup(respawningAfterLoad);
      if (this._lockedTarget != null)
        return;
      this._lockedTarget = this.FindBestTargetGlobal();
    }

    public virtual void CompTick()
    {
      base.CompTick();
      ++this._ticksSinceLastScan;
      if (this._ticksSinceLastScan < 60)
        return;
      this._ticksSinceLastScan = 0;
      if (!this.IsTargetValid(this._lockedTarget))
        this._lockedTarget = this.FindBestTargetGlobal();
      else
        this.CheckForBetterTarget();
    }

    private bool IsTargetValid(Thing t)
    {
      if (t == null || t.Destroyed || !t.Spawned || t.Map != ((Thing) this.parent).Map)
        return false;
      if (t is Pawn pawn)
      {
        if (pawn.Dead || pawn.Downed)
          return false;
        CompCanBeDormant comp = ((ThingWithComps) pawn).GetComp<CompCanBeDormant>();
        if (comp != null && !comp.Awake)
          return false;
      }
      return !GridsUtility.Fogged(t.Position, t.Map);
    }

    private Thing FindBestTargetGlobal()
    {
      if (((Thing) this.parent).Map == null)
        return (Thing) null;
      IntVec3 position = ((Thing) this.parent).Position;
      Vector3 vector3_1 = position.ToVector3();
      Thing bestTargetGlobal = (Thing) null;
      float num1 = float.MinValue;
      HashSet<IAttackTarget> faction = ((Thing) this.parent).Map.attackTargetsCache.TargetsHostileToFaction(((Thing) this.parent).Faction);
      if (faction == null)
        return (Thing) null;
      foreach (IAttackTarget iattackTarget in faction)
      {
        if (iattackTarget != null)
        {
          Thing thing = iattackTarget.Thing;
          if (this.IsTargetValid(thing) && thing != this.parent)
          {
            float num2 = 0.0f;
            Vector3 vector3_2 = (thing.DrawPos - vector3_1);
            float sqrMagnitude = vector3_2.sqrMagnitude;
            float num3 = num2 - sqrMagnitude * (1f / 1000f);
            float num4;
            switch (thing)
            {
              case Pawn pawn:
                num4 = num3 + (pawn.IsColonist ? 5000f : 1000f);
                break;
              case Building_Turret _:
                num4 = num3 + 500f;
                break;
              default:
                num4 = num3 + 100f;
                break;
            }
            if (thing == this._lockedTarget)
              num4 += 500f;
            if ((double) num4 > (double) num1)
            {
              num1 = num4;
              bestTargetGlobal = thing;
            }
          }
        }
      }
      return bestTargetGlobal;
    }

    private void CheckForBetterTarget()
    {
      IntVec3 intVec3 = (this._lockedTarget.Position - ((Thing) this.parent).Position);
      if ((double) intVec3.LengthHorizontal <= 50.0)
        return;
      Thing bestTargetGlobal = this.FindBestTargetGlobal();
      if (bestTargetGlobal != null && bestTargetGlobal != this._lockedTarget)
        this._lockedTarget = bestTargetGlobal;
    }

    public virtual void PostExposeData()
    {
      base.PostExposeData();
      Scribe_References.Look<Thing>(ref this._lockedTarget, "lockedTarget", false);
    }
  }
}
