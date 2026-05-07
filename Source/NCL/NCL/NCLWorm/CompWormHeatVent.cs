// Decompiled with JetBrains decompiler
// Type: NCLWorm.CompWormHeatVent
// Assembly: NCL_WormBoss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B627DDDD-3AC9-4D6A-89F6-EA72F95FB570
// Assembly location: C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3555799437\1.6\Assemblies\NCL_WormBoss (3).dll

using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

#nullable disable
namespace NCLWorm
{
  public class CompWormHeatVent : ThingComp
  {
    private DamageDef _damageDef;

    public CompProperties_WormHeatVent Props => (CompProperties_WormHeatVent) this.props;

    private WormBody Body => this.parent as WormBody;

    public virtual void PostSpawnSetup(bool respawningAfterLoad)
    {
      base.PostSpawnSetup(respawningAfterLoad);
      this._damageDef = this.Props.damageDef ?? DamageDefOf.Burn;
    }

    public virtual void CompTick()
    {
      if (this.Body == null || (double) this.Body.VentOpenFactor < (double) this.Props.activationThreshold || (Find.TickManager.TicksGame + ((Thing) this.parent).thingIDNumber) % this.Props.checkInterval != 0)
        return;
      this.DoHeatVentEffect();
    }

    private void DoHeatVentEffect()
    {
      Map map = ((Thing) this.parent).Map;
      if (map == null)
        return;
      int num = GenRadial.NumCellsInRadius(this.Props.radius);
      IntVec3 position = ((Thing) this.parent).Position;
      for (int index1 = 0; index1 < num; ++index1)
      {
        IntVec3 intVec3 = (position + GenRadial.RadialPattern[index1]);
        if (GenGrid.InBounds(intVec3, map))
        {
          List<Thing> thingList = GridsUtility.GetThingList(intVec3, map);
          for (int index2 = thingList.Count - 1; index2 >= 0; --index2)
          {
            if (thingList[index2] is Pawn victim && !victim.Dead && !victim.Downed)
            {
              if (((Thing) this.parent).Faction == Faction.OfPlayer)
              {
                if (((Thing) victim).Faction != null && FactionUtility.HostileTo(((Thing) victim).Faction, Faction.OfPlayer))
                  this.ApplyEffects(victim);
              }
              else if (GenHostility.HostileTo((Thing) victim, (Thing) this.parent))
                this.ApplyEffects(victim);
            }
          }
        }
      }
    }

    private void ApplyEffects(Pawn victim)
    {
      DamageInfo damageInfo;
      // ISSUE: explicit constructor call
      damageInfo = new DamageInfo(this._damageDef, this.Props.baseDamage, this.Props.armorPenetration, -1f, (Thing) this.parent, (BodyPartRecord) null, (ThingDef) null, (DamageInfo.SourceCategory) 0, (Thing) null, true, true, (QualityCategory) 2, true, false);
      IntVec3 position = ((Thing) victim).Position;
      Vector3 vector3 = position.ToVector3();
      ((Thing) victim).TakeDamage(damageInfo);
      if (victim.Dead || ((Thing) victim).Map == null)
        return;
      this.PushPawn(victim, vector3);
    }

    private void PushPawn(Pawn p, Vector3 pPos)
    {
      IntVec3 position1 = ((Thing) this.parent).Position;
      Vector3 vector3_1 = position1.ToVector3();
      Vector3 vector3_2 = (pPos - vector3_1);
      Vector3 vector3_3 = vector3_2.normalized;
      if ((vector3_3 == Vector3.zero))
        vector3_3 = Vector3.right;
      IntVec3 position2 = ((Thing) p).Position;
      IntVec3 map = WormUtility.ClampCellToMap(((Thing) p).Map, IntVec3Utility.ToIntVec3((pPos + (vector3_3 * this.Props.knockbackDistance))));
      IntVec3 intVec3_1 = position2;
      foreach (IntVec3 intVec3_2 in GenSight.PointsOnLineOfSight(position2, map))
      {
        if (!(intVec3_2 == position2))
        {
          if (GenGrid.InBounds(intVec3_2, ((Thing) p).Map) && GenGrid.Walkable(intVec3_2, ((Thing) p).Map))
            intVec3_1 = intVec3_2;
          else
            break;
        }
      }
      if ((intVec3_1 != position2))
      {
        ((Thing) p).Position = intVec3_1;
        p.Notify_Teleported(true, false);
        p.pather?.StopDead();
      }
      if (this.Props.stunTicks <= 0)
        return;
      p.stances?.stunner?.StunFor(this.Props.stunTicks, (Thing) this.parent, true, true, false);
    }
  }
}
