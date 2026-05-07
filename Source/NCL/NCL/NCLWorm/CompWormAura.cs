// Decompiled with JetBrains decompiler
// Type: NCLWorm.CompWormAura
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
  public class CompWormAura : ThingComp
  {
    private CellRect _wormAABB;
    private readonly List<IntVec3> _debugAabbEdgeCells = new List<IntVec3>();

    public CompProperties_WormAura Props => (CompProperties_WormAura) this.props;

    private WormHead Head => this.parent as WormHead;

    public virtual void CompTick()
    {
      if (this.Head == null || !((Thing) this.Head).Spawned || !Gen.IsHashIntervalTick((Thing) this.parent, this.Props.checkInterval))
        return;
      this.ApplyAuraEffect();
    }

    private void ApplyAuraEffect()
    {
      this.UpdateBoundingBox();
      float radiusSq = this.Props.range * this.Props.range;
      IReadOnlyList<Pawn> allPawnsSpawned = ((Thing) this.parent).Map.mapPawns.AllPawnsSpawned;
      for (int index = 0; index < allPawnsSpawned.Count; ++index)
      {
        Pawn target = allPawnsSpawned[index];
        if (!target.Dead && target != this.parent && GenHostility.HostileTo((Thing) target, (Thing) this.parent) && this._wormAABB.Contains(((Thing) target).Position) && this.IsCloseToAnySegment((Thing) target, radiusSq))
          this.GiveHediff(target);
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
      int num3 = Mathf.CeilToInt(this.Props.range);
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

    private void GiveHediff(Pawn target)
    {
      if (this.Props.hediff == null)
        return;
      HediffComp_Disappears comp = HediffUtility.TryGetComp<HediffComp_Disappears>(target.health.GetOrAddHediff(this.Props.hediff, (BodyPartRecord) null, new DamageInfo?(), (DamageWorker.DamageResult) null));
      if (comp == null)
        return;
      comp.ticksToDisappear = this.Props.checkInterval + 60;
    }

    public virtual void PostDraw()
    {
      if (!this.Props.drawLines || !Find.Selector.IsSelected((object) this.parent))
        return;
      this._debugAabbEdgeCells.Clear();
      foreach (IntVec3 cell in this._wormAABB.Cells)
        this._debugAabbEdgeCells.Add(cell);
      GenDraw.DrawFieldEdges(this._debugAabbEdgeCells, Color.cyan, new float?(), (HashSet<IntVec3>) null, 2900);
      GenDraw.DrawRadiusRing(((Thing) this.Head).Position, this.Props.range, Color.red, (Func<IntVec3, bool>) null);
      if (this.Head.BodySegments != null)
      {
        foreach (Thing bodySegment in this.Head.BodySegments)
          GenDraw.DrawRadiusRing(bodySegment.Position, this.Props.range, Color.red, (Func<IntVec3, bool>) null);
      }
    }
  }
}
