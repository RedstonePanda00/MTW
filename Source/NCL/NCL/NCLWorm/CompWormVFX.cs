// Decompiled with JetBrains decompiler
// Type: NCLWorm.CompWormVFX
// Assembly: NCL_WormBoss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B627DDDD-3AC9-4D6A-89F6-EA72F95FB570
// Assembly location: C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3555799437\1.6\Assemblies\NCL_WormBoss (3).dll

using System;
using System.Collections.Generic;
using Verse;

#nullable disable
namespace NCLWorm
{
  public class CompWormVFX : ThingComp
  {
    private List<WormVFXEmitter> _activeEmitters;
    private List<VFXRuntimeState> _runtimeStates;

    private WormBody Body => this.parent as WormBody;

    public virtual void PostSpawnSetup(bool respawningAfterLoad)
    {
      base.PostSpawnSetup(respawningAfterLoad);
      this.Initialize();
    }

    private void Initialize()
    {
      WormSegmentVisuals modExtension = ((Def) ((Thing) this.parent).def).GetModExtension<WormSegmentVisuals>();
      if (modExtension == null || modExtension.emitters == null)
        return;
      if (this._activeEmitters == null)
      {
        this._activeEmitters = new List<WormVFXEmitter>();
        foreach (WormVFXEmitter emitter in modExtension.emitters)
        {
          this._activeEmitters.Add(emitter);
          if (emitter.mirror)
            this._activeEmitters.Add(emitter.CreateMirroredCopy());
        }
      }
      if (this._runtimeStates == null)
      {
        this._runtimeStates = new List<VFXRuntimeState>();
        foreach (WormVFXEmitter activeEmitter in this._activeEmitters)
          this._runtimeStates.Add(activeEmitter.CreateState());
      }
      if (this._runtimeStates.Count == this._activeEmitters.Count)
        return;
      this._runtimeStates.Clear();
      foreach (WormVFXEmitter activeEmitter in this._activeEmitters)
        this._runtimeStates.Add(activeEmitter.CreateState());
    }

    public virtual void CompTick()
    {
      base.CompTick();
      if (this.parent is WormThingBase parent && parent.IsVisualHidden || ((Thing) this.parent).Map != Find.CurrentMap || this._activeEmitters == null || this._runtimeStates == null)
        return;
      float currentFactor = this.Body != null ? this.Body.CurrentAnimateFactor : 0.0f;
      for (int index = 0; index < this._activeEmitters.Count; ++index)
        this._activeEmitters[index].Tick(this.Body, this._runtimeStates[index], currentFactor);
    }

    public virtual void PostExposeData()
    {
      base.PostExposeData();
      Scribe_Collections.Look<VFXRuntimeState>(ref this._runtimeStates, "runtimeStates", (LookMode) 2, Array.Empty<object>());
    }
  }
}
