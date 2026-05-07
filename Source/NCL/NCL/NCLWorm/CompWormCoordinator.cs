// Decompiled with JetBrains decompiler
// Type: NCLWorm.CompWormCoordinator
// Assembly: NCL_WormBoss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B627DDDD-3AC9-4D6A-89F6-EA72F95FB570
// Assembly location: C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3555799437\1.6\Assemblies\NCL_WormBoss (3).dll

using System.Collections.Generic;
using Verse;

#nullable disable
namespace NCLWorm
{
  public class CompWormCoordinator : ThingComp
  {
    private List<WormBody> _segments = new List<WormBody>();

    public void RegisterSegment(WormBody body)
    {
      if (this._segments.Contains(body))
        return;
      this._segments.Add(body);
    }

    public void Clear() => this._segments.Clear();

    public virtual void CompTick()
    {
      base.CompTick();
      if (this._segments.Count == 0)
        return;
      TC_WormDecisionController comp = this.parent.GetComp<TC_WormDecisionController>();
      WormPhase currentPhase = comp?.CurrentPhase;
      if (comp == null || currentPhase == null)
        return;
      int count = this._segments.Count;
      for (int index = 0; index < count; ++index)
      {
        WormBody segment = this._segments[index];
        if (segment != null && !((Thing) segment).Destroyed)
          currentPhase.UpdateSegmentBehavior(segment, index, count);
      }
    }

    public virtual void PostExposeData() => base.PostExposeData();
  }
}
