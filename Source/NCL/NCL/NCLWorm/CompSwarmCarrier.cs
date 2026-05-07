// Decompiled with JetBrains decompiler
// Type: NCLWorm.CompSwarmCarrier
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
  public class CompSwarmCarrier : CompSwarmController
  {
    private const int TICKS_ANIMATION_STARTUP = 30;
    private const int TICKS_POST_SPAWN = 30;
    private const int TICKS_TIMEOUT = 300;
    private const int TICKS_FULL_COOLDOWN = 600;
    private List<WormBody> _tmpAvailableSegments = new List<WormBody>();
    private readonly Dictionary<ThingDef, int> _probeCountScratch = new Dictionary<ThingDef, int>();
    private readonly List<ProbeSpawnOption> _validProbeOptionsScratch = new List<ProbeSpawnOption>();
    private List<CompSwarmCarrier.SpawnTracker> _activeSpawners = new List<CompSwarmCarrier.SpawnTracker>();
    private int _cooldownTicks = 0;

    private CompProperties_SwarmCarrier Props => (CompProperties_SwarmCarrier) this.props;

    public virtual void CompTick()
    {
      base.CompTick();
      if (((Thing) this.parent).Map == null)
        return;
      this.UpdateActiveSpawners();
      this.TryStartNewSpawn();
    }

    private void UpdateActiveSpawners()
    {
      for (int index = this._activeSpawners.Count - 1; index >= 0; --index)
      {
        CompSwarmCarrier.SpawnTracker activeSpawner = this._activeSpawners[index];
        if (activeSpawner.segment == null || ((Thing) activeSpawner.segment).Destroyed || ((Thing) this.parent).Destroyed)
          this._activeSpawners.RemoveAt(index);
        else if (this.ProcessSingleTracker(activeSpawner))
          this._activeSpawners.RemoveAt(index);
      }
    }

    private bool ProcessSingleTracker(CompSwarmCarrier.SpawnTracker tracker)
    {
      ++tracker.timer;
      WormBody segment = tracker.segment;
      segment.SetVentState(1f);
      if (tracker.timer > 30 && (double) segment.VentOpenFactor < 0.10000000149011612)
        return true;
      if (!tracker.hasSpawned)
      {
        if ((double) segment.VentOpenFactor >= (double) this.Props.spawnOpenThreshold)
        {
          this.DoSpawnUnit(segment, tracker.targetDef);
          tracker.hasSpawned = true;
          tracker.timer = 0;
        }
        else if (tracker.timer > 300)
          return true;
      }
      else if (tracker.timer > 30)
        return true;
      return false;
    }

    private void TryStartNewSpawn()
    {
      if (this._cooldownTicks > 0)
        --this._cooldownTicks;
      else if (this.Minions.Count >= this.Props.maxTotalProbes)
      {
        this._cooldownTicks = 600;
      }
      else
      {
        ThingDef spawn = this.SelectProbeToSpawn();
        if (spawn == null)
        {
          this._cooldownTicks = 200;
        }
        else
        {
          if (!(this.parent is WormHead parent) || parent.BodySegments == null)
            return;
          this._tmpAvailableSegments.Clear();
          List<WormBody> bodySegments = parent.BodySegments;
          int count = bodySegments.Count;
          for (int index = 0; index < count; ++index)
          {
            WormBody seg = bodySegments[index];
            if (this.IsSegmentAvailable(seg, count))
              this._tmpAvailableSegments.Add(seg);
          }
          if (this._tmpAvailableSegments.Count == 0)
            return;
          int num = Mathf.Min(this.Props.batchSize, this._tmpAvailableSegments.Count);
          for (int index = 0; index < num; ++index)
          {
            WormBody wormBody = GenCollection.RandomElement<WormBody>((IEnumerable<WormBody>) this._tmpAvailableSegments);
            this._tmpAvailableSegments.Remove(wormBody);
            this._activeSpawners.Add(new CompSwarmCarrier.SpawnTracker()
            {
              segment = wormBody,
              timer = 0,
              hasSpawned = false,
              targetDef = spawn
            });
          }
          this._tmpAvailableSegments.Clear();
          this._cooldownTicks = this.Props.spawnInterval;
        }
      }
    }

    private ThingDef SelectProbeToSpawn()
    {
      if (this.Props.spawnOptions == null || this.Props.spawnOptions.Count == 0)
        return (ThingDef) null;
      this._probeCountScratch.Clear();
      foreach (CompProbeBrain minion in this.Minions)
      {
        if (minion != null && minion.parent != null)
        {
          ThingDef def = ((Thing) minion.parent).def;
          int num;
          this._probeCountScratch[def] = !this._probeCountScratch.TryGetValue(def, out num) ? 1 : num + 1;
        }
      }
      this._validProbeOptionsScratch.Clear();
      for (int index = 0; index < this.Props.spawnOptions.Count; ++index)
      {
        ProbeSpawnOption spawnOption = this.Props.spawnOptions[index];
        int num;
        if ((this._probeCountScratch.TryGetValue(spawnOption.probeDef, out num) ? num : 0) < spawnOption.maxCount)
          this._validProbeOptionsScratch.Add(spawnOption);
      }
      if (this._validProbeOptionsScratch.Count == 0)
        return null;
      if (!GenCollection.TryRandomElementByWeight(
            this._validProbeOptionsScratch,
            opt => (float)opt.spawnWeight,
            out ProbeSpawnOption probeSpawnOption))
        return null;
      return probeSpawnOption.probeDef;
    }

    private bool IsSegmentAvailable(WormBody seg, int totalCount)
    {
      if (seg.SegmentIndex <= 0 || seg.SegmentIndex >= totalCount - 1 || ((Thing) seg).Destroyed)
        return false;
      for (int index = 0; index < this._activeSpawners.Count; ++index)
      {
        if (this._activeSpawners[index].segment == seg)
          return false;
      }
      return true;
    }

    private void DoSpawnUnit(WormBody seg, ThingDef defToSpawn)
    {
      if (defToSpawn == null)
        return;
      WormProbe wormProbe = (WormProbe) ThingMaker.MakeThing(defToSpawn, (ThingDef) null);
      if (((Thing) this.parent).Faction != null)
        ((Thing) wormProbe).SetFaction(((Thing) this.parent).Faction, (Pawn) null);
      Vector3 exactPosition = seg.ExactPosition;
      Vector3 pos = (exactPosition + (Vector3.up * 1f));
      wormProbe.SetPhysicsState(pos, Vector3.zero);
      GenSpawn.Spawn((Thing) wormProbe, ((Thing) seg).Position, ((Thing) seg).Map, (WipeMode) 0);
      Vector3 vector3_1 = Vector3.Cross(seg.BodyFacing, Vector3.up);
      Vector3 normalized = vector3_1.normalized;
      Vector3 vector3_2 = (Rand.Bool ? normalized : (-normalized) + (seg.BodyFacing * 0.5f));
      vector3_2.Normalize();
      wormProbe.GetComp<TC_WormMovingController>()?.ForceVelocity((vector3_2 * 0.2f));
      wormProbe.GetComp<CompProbeBrain>()?.SetCommander((CompSwarmController) this);
      FleckMaker.ThrowSmoke(exactPosition, ((Thing) seg).Map, 1.5f);
    }

    public override void PostExposeData()
    {
      base.PostExposeData();
      Scribe_Collections.Look<CompSwarmCarrier.SpawnTracker>(ref this._activeSpawners, "activeSpawners", (LookMode) 2, Array.Empty<object>());
      Scribe_Values.Look<int>(ref this._cooldownTicks, "cooldownTicks", 0, false);
    }

    private class SpawnTracker : IExposable
    {
      public WormBody segment;
      public int timer;
      public bool hasSpawned;
      public ThingDef targetDef;

      public void ExposeData()
      {
        Scribe_References.Look<WormBody>(ref this.segment, "segment", false);
        Scribe_Values.Look<int>(ref this.timer, "timer", 0, false);
        Scribe_Values.Look<bool>(ref this.hasSpawned, "hasSpawned", false, false);
        Scribe_Defs.Look<ThingDef>(ref this.targetDef, "targetDef");
      }
    }
  }
}
