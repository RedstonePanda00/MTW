// Decompiled with JetBrains decompiler
// Type: NCLWorm.CompSwarmController
// Assembly: NCL_WormBoss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B627DDDD-3AC9-4D6A-89F6-EA72F95FB570
// Assembly location: C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3555799437\1.6\Assemblies\NCL_WormBoss (3).dll

using System;
using System.Collections.Generic;
using Verse;

#nullable disable
namespace NCLWorm
{
  public class CompSwarmController : ThingComp
  {
    private List<CompProbeBrain> _minions = new List<CompProbeBrain>();
    private List<Thing> _minionThings;

    public List<CompProbeBrain> Minions => this._minions;

    public void Register(CompProbeBrain minion)
    {
      if (minion == null || this._minions.Contains(minion))
        return;
      this._minions.Add(minion);
    }

    public void Deregister(CompProbeBrain minion) => this._minions.Remove(minion);

    public void BroadcastOrder(ProbeCommand cmd)
    {
      for (int index = this._minions.Count - 1; index >= 0; --index)
      {
        CompProbeBrain minion = this._minions[index];
        if (minion == null || ((Thing) minion.parent).Destroyed)
          this._minions.RemoveAt(index);
        else
          minion.SetOrder(cmd);
      }
    }

    public void ReleaseAll()
    {
      this.BroadcastOrder(new ProbeCommand()
      {
        Type = ProbeCommandType.None
      });
    }

    public virtual void PostExposeData()
    {
      base.PostExposeData();
      if (Scribe.mode == LoadSaveMode.LoadingVars)
      {
        this._minionThings = new List<Thing>();
        for (int index = 0; index < this._minions.Count; ++index)
        {
          CompProbeBrain minion = this._minions[index];
          if (minion?.parent != null)
            this._minionThings.Add((Thing) minion.parent);
        }
      }
      Scribe_Collections.Look<Thing>(ref this._minionThings, "minions", (LookMode) 3, Array.Empty<object>());
      if (Scribe.mode != LoadSaveMode.PostLoadInit)
        return;
      this._minions = new List<CompProbeBrain>();
      if (this._minionThings != null)
      {
        for (int index = 0; index < this._minionThings.Count; ++index)
        {
          Thing minionThing = this._minionThings[index];
          CompProbeBrain comp = minionThing != null ? ThingCompUtility.TryGetComp<CompProbeBrain>(minionThing) : (CompProbeBrain) null;
          if (comp != null)
            this._minions.Add(comp);
        }
      }
    }
  }
}
