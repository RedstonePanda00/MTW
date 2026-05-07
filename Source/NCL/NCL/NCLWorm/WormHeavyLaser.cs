// Decompiled with JetBrains decompiler
// Type: NCLWorm.WormHeavyLaser
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
  public class WormHeavyLaser : Projectile
  {
    private HashSet<int> _hitThings = new HashSet<int>();
    private Vector3 _lastExactPos;

    private ProjectileExtension_WormLaser Extension
    {
      get => ((Def) ((Thing) this).def).GetModExtension<ProjectileExtension_WormLaser>();
    }

    private float HitWidth
    {
      get
      {
        ProjectileExtension_WormLaser extension = this.Extension;
        return extension == null ? 1.5f : extension.laserWidth;
      }
    }

    private float MaxRange
    {
      get
      {
        ProjectileExtension_WormLaser extension = this.Extension;
        return extension == null ? 40f : extension.maxRange;
      }
    }

    public virtual void ExposeData()
    {
      base.ExposeData();
      List<int> collection = (List<int>) null;
      if (Scribe.mode == LoadSaveMode.LoadingVars && this._hitThings != null)
        collection = new List<int>((IEnumerable<int>) this._hitThings);
      Scribe_Collections.Look<int>(ref collection, "hitThings", (LookMode) 1, Array.Empty<object>());
      if (Scribe.mode == LoadSaveMode.PostLoadInit && collection != null)
        this._hitThings = new HashSet<int>((IEnumerable<int>) collection);
      Scribe_Values.Look<Vector3>(ref this._lastExactPos, "lastExactPos", new Vector3(), false);
    }

    public virtual void Launch(
      Thing launcher,
      Vector3 origin,
      LocalTargetInfo usedTarget,
      LocalTargetInfo intendedTarget,
      ProjectileHitFlags hitFlags,
      bool preventFriendlyFire = false,
      Thing equipment = null,
      ThingDef targetCoverDef = null)
    {
      Vector3 vector3_1 = (usedTarget.CenterVector3 - origin);
      Vector3 vector3_2 = vector3_1.normalized;
      if ((vector3_2 == Vector3.zero))
        vector3_2 = Vector3.forward;
      float maxRange = this.MaxRange;
      Vector3 vector3_3 = (origin + (vector3_2 * maxRange));
      Map map = launcher.Map;
      if (map != null)
      {
        vector3_3.x = Mathf.Clamp(vector3_3.x, 1f, (float) map.Size.x - 1f);
        vector3_3.z = Mathf.Clamp(vector3_3.z, 1f, (float) map.Size.z - 1f);
      }
      base.Launch(launcher, origin, new LocalTargetInfo(IntVec3Utility.ToIntVec3(vector3_3)), intendedTarget, hitFlags, preventFriendlyFire, equipment, targetCoverDef);
      this.destination = vector3_3;
      this.origin = origin;
      this._lastExactPos = origin;
      float num = ((Thing) this).def.projectile.SpeedTilesPerTick;
      if ((double) num <= 1.0 / 1000.0)
        num = 1f;
      this.ticksToImpact = Mathf.CeilToInt(maxRange / num);
    }

    protected virtual void Tick()
    {
      base.Tick();
      if (this.landed)
        return;
      Vector3 exactPosition = this.ExactPosition;
      this.CheckCollisionAlongPath(IntVec3Utility.ToIntVec3(this._lastExactPos), IntVec3Utility.ToIntVec3(exactPosition));
      this._lastExactPos = exactPosition;
      --this.ticksToImpact;
      if (this.ticksToImpact > 0 && GenGrid.InBounds(exactPosition, ((Thing) this).Map))
        return;
      ((Thing) this).Destroy((DestroyMode) 0);
    }

    private void CheckCollisionAlongPath(IntVec3 start, IntVec3 end)
    {
      int x1 = start.x;
      int z1 = start.z;
      int x2 = end.x;
      int z2 = end.z;
      int num1 = Mathf.Abs(x2 - x1);
      int num2 = Mathf.Abs(z2 - z1);
      int num3 = x1 < x2 ? 1 : -1;
      int num4 = z1 < z2 ? 1 : -1;
      int num5 = num1 - num2;
      Map map = ((Thing) this).Map;
      float hitWidth = this.HitWidth;
      ProjectileExtension_WormLaser extension = this.Extension;
      bool flag = extension == null || extension.passThroughWalls;
      Thing launcher = this.launcher;
      Faction faction = launcher?.Faction;
      while (true)
      {
        IntVec3 intVec3;
        // ISSUE: explicit constructor call
        intVec3 = new IntVec3(x1, 0, z1);
        if (GenGrid.InBounds(intVec3, map))
        {
          List<Thing> thingList = GridsUtility.GetThingList(intVec3, map);
          for (int index = thingList.Count - 1; index >= 0; --index)
          {
            Thing t = thingList[index];
            if (t != this && t != launcher && !this._hitThings.Contains(t.thingIDNumber))
            {
              if (t.def.Fillage == FillCategory.Full)
              {
                if (t.def.building != null && (t.def.building.isNaturalRock || t.def.mineable))
                {
                  if (!flag)
                    return;
                  continue;
                }
                if (!flag)
                {
                  base.Impact(t, false);
                  return;
                }
              }
              if ((t is Pawn || t is Building) && (!(t is Building) || t.Faction != null))
              {
                if (faction == Faction.OfPlayer)
                {
                  if (t.Faction == null || !FactionUtility.HostileTo(t.Faction, Faction.OfPlayer))
                    continue;
                }
                else if (launcher != null && t.Faction != null && !FactionUtility.HostileTo(t.Faction, faction))
                  continue;
                if ((double) this.PointToLineDistance(this._lastExactPos, this.ExactPosition, t.DrawPos) <= (!(t is Pawn pawn) ? (double) Mathf.Max(t.def.size.x, t.def.size.z) * 0.5 + (double) hitWidth * 0.5 : (double) pawn.BodySize * 0.5 + (double) hitWidth * 0.5))
                  this.ImpactTarget(t);
              }
            }
          }
        }
        if (x1 != x2 || z1 != z2)
        {
          int num6 = 2 * num5;
          if (num6 > -num2)
          {
            num5 -= num2;
            x1 += num3;
          }
          if (num6 < num1)
          {
            num5 += num1;
            z1 += num4;
          }
        }
        else
          break;
      }
    }

    private void ImpactTarget(Thing t)
    {
      this._hitThings.Add(t.thingIDNumber);
      DamageDef damageDef = ((Thing) this).def.projectile.damageDef;
      DamageInfo damageInfo;
      // ISSUE: explicit constructor call
      damageInfo = new DamageInfo(damageDef, (float) this.DamageAmount, this.ArmorPenetration, -1f, this.launcher, (BodyPartRecord) null, this.equipmentDef, (DamageInfo.SourceCategory) 0, (Thing) null, true, true, (QualityCategory) 2, true, false);
      t.TakeDamage(damageInfo);
    }

    protected virtual void Impact(Thing hitThing, bool blockedByShield = false)
    {
      base.Impact(hitThing, blockedByShield);
    }

    private float PointToLineDistance(Vector3 lineStart, Vector3 lineEnd, Vector3 point)
    {
      Vector3 vector3_1 = (lineEnd - lineStart);
      float sqrMagnitude = vector3_1.sqrMagnitude;
      if ((double) sqrMagnitude < 9.9999997473787516E-05)
      {
        Vector3 vector3_2 = (point - lineStart);
        return vector3_2.magnitude;
      }
      float num = Mathf.Clamp01(Vector3.Dot((point - lineStart), vector3_1) / sqrMagnitude);
      Vector3 vector3_3 = (lineStart + (num * vector3_1));
      Vector3 vector3_4 = (point - vector3_3);
      return vector3_4.magnitude;
    }
  }
}
