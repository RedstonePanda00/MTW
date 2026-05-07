// Decompiled with JetBrains decompiler
// Type: NCLWorm.CompWormPointDefense
// Assembly: NCL_WormBoss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B627DDDD-3AC9-4D6A-89F6-EA72F95FB570
// Assembly location: C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3555799437\1.6\Assemblies\NCL_WormBoss (3).dll

using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.Sound;

#nullable disable
namespace NCLWorm
{
  public class CompWormPointDefense : ThingComp
  {
    private int _interceptCounter = 0;
    private int _cooldownTimer = 0;
    private CellRect _wormAABB;

    private CompProperties_WormPointDefense Props => (CompProperties_WormPointDefense) this.props;

    private WormHead Head => this.parent as WormHead;

    public virtual void CompTick()
    {
      if (this.Head == null || !((Thing) this.Head).Spawned)
        return;
      if (this._cooldownTimer > 0)
      {
        --this._cooldownTimer;
      }
      else
      {
        if (!Gen.IsHashIntervalTick((Thing) this.parent, this.Props.scanInterval))
          return;
        this.ScanAndIntercept();
      }
    }

    private void ScanAndIntercept()
    {
      this.UpdateBoundingBox();
      List<Thing> thingList = ((Thing) this.parent).Map.listerThings.ThingsInGroup((ThingRequestGroup) 60);
      int num = 0;
      float radiusSq = this.Props.radius * this.Props.radius;
      for (int index = 0; index < thingList.Count; ++index)
      {
        Thing target = thingList[index];
        if (!target.Destroyed && target is Projectile proj && (proj.Launcher == null || GenHostility.HostileTo(proj.Launcher, (Thing) this.parent)) && this._wormAABB.Contains(target.Position) && this.IsCloseToAnySegment(target, radiusSq))
        {
          this.Intercept(proj);
          ++this._interceptCounter;
          ++num;
          if (this._interceptCounter >= this.Props.shotsBeforeCooldown)
          {
            this.TriggerCooldown();
            break;
          }
          if (num >= this.Props.interceptCountPerBurst)
            break;
        }
      }
    }

    private void UpdateBoundingBox()
    {
      int x = ((Thing) this.Head).Position.x;
      int z = ((Thing) this.Head).Position.z;
      int num1 = x;
      int num2 = z;
      List<WormBody> bodySegments = this.Head.BodySegments;
      if (bodySegments != null)
      {
        for (int index = 0; index < bodySegments.Count; ++index)
        {
          IntVec3 position = ((Thing) bodySegments[index]).Position;
          if (position.x < x)
            x = position.x;
          if (position.x > num1)
            num1 = position.x;
          if (position.z < z)
            z = position.z;
          if (position.z > num2)
            num2 = position.z;
        }
      }
      int num3 = Mathf.CeilToInt(this.Props.radius);
      this._wormAABB = new CellRect(x - num3, z - num3, num1 - x + 1 + num3 * 2, num2 - z + 1 + num3 * 2);
    }

    private bool IsCloseToAnySegment(Thing target, float radiusSq)
    {
      if (this.CheckDist(target, (Thing) this.Head, radiusSq))
        return true;
      List<WormBody> bodySegments = this.Head.BodySegments;
      if (bodySegments != null)
      {
        for (int index = 0; index < bodySegments.Count; ++index)
        {
          if (this.CheckDist(target, (Thing) bodySegments[index], radiusSq))
            return true;
        }
      }
      return false;
    }

    private bool CheckDist(Thing a, Thing b, float radiusSq)
    {
      Vector3 vector3 = (a.DrawPos - b.DrawPos);
      vector3.y = 0.0f;
      return (double) vector3.sqrMagnitude <= (double) radiusSq;
    }

    private void Intercept(Projectile proj)
    {
      if (!string.IsNullOrEmpty(this.Props.interceptEffect))
      {
        FleckDef namedSilentFail = DefDatabase<FleckDef>.GetNamedSilentFail(this.Props.interceptEffect);
        if (namedSilentFail != null)
          FleckMaker.Static(((Thing) proj).DrawPos, ((Thing) this.parent).Map, namedSilentFail, 1f);
      }
      SoundStarter.PlayOneShot(SoundDefOf.Psycast_Skip_Exit, (new TargetInfo(((Thing) proj).Position, ((Thing) this.parent).Map, false)));
      ((Thing) proj).Destroy((DestroyMode) 0);
    }

    private void TriggerCooldown()
    {
      this._cooldownTimer = this.Props.cooldownTicks;
      this._interceptCounter = 0;
      MoteMaker.ThrowText(((Thing) this.parent).DrawPos, ((Thing) this.parent).Map, "Shield Overload!", Color.red, -1f);
    }

    public virtual void PostExposeData()
    {
      base.PostExposeData();
      Scribe_Values.Look<int>(ref this._cooldownTimer, "cooldownTimer", 0, false);
      Scribe_Values.Look<int>(ref this._interceptCounter, "interceptCounter", 0, false);
    }
  }
}
