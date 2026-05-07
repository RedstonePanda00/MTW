// Decompiled with JetBrains decompiler
// Type: NCLWorm.WormHead
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
  public class WormHead : WormThingBase
  {
    private const float HEAD_SERVO_SPEED = 0.05f;
    public int bodySegmentCount = 10;
    public float segmentSpacing = 1.8f;
    private List<WormBody> segments = new List<WormBody>();
    private readonly List<WormBody> _destroySegmentsScratch = new List<WormBody>();
    private bool _segmentsSpawned = false;
    private bool _isDyingSequence = false;
    private float _headAnimateFactor = 0.0f;
    private float _targetHeadAnimateFactor = 0.0f;

    public List<WormBody> BodySegments => this.segments;

    public bool IsDying => this._isDyingSequence;

    public override float CurrentAnimateFactor => this._headAnimateFactor;

    public CompSwarmController Swarm => this.GetComp<CompSwarmController>();

    public TC_WormDecisionController Brain => this.GetComp<TC_WormDecisionController>();

    public virtual void SpawnSetup(Map map, bool respawningAfterLoad)
    {
      base.SpawnSetup(map, respawningAfterLoad);
      this.ApplyXmlSettings();
      if (!respawningAfterLoad)
      {
        this.InitializeFaction();
        this.InitializePhysics();
        this.SpawnBodySegments(map);
      }
      else
        this.NotifyCoordinator();
      this.RegisterToMapComponent(map);
    }

    public virtual void DeSpawn(DestroyMode mode = 0)
    {
      ((Thing) this).Map?.GetComponent<WormBossMapComponent>()?.DeregisterBoss(this);
      base.DeSpawn(mode);
    }

    public virtual void Destroy(DestroyMode mode = 0)
    {
      if (((Thing) this).Destroyed)
        return;
      this.DestroyAllSegments();
      base.Destroy(mode);
    }

    protected override void Tick()
    {
      base.Tick();
      this.UpdateHeadAnimation();
    }

    private void ApplyXmlSettings()
    {
      WormBossDefExtension modExtension = ((Def) ((Thing) this).def).GetModExtension<WormBossDefExtension>();
      if (modExtension == null)
        return;
      this.bodySegmentCount = modExtension.bodySegmentCount;
      this.segmentSpacing = modExtension.segmentSpacing;
    }

    public virtual void SetFaction(Faction newFaction, Pawn recruiter = null)
    {
      ((Thing) this).SetFaction(newFaction, recruiter);
      if (this.segments == null)
        return;
      foreach (WormBody segment in this.segments)
      {
        if (segment != null && !((Thing) segment).Destroyed && ((Thing) segment).Faction != newFaction)
          ((Thing) segment).SetFaction(newFaction, (Pawn) null);
      }
    }

    private void InitializeFaction()
    {
      if (((Thing) this).Faction != null)
        return;
      ((Thing) this).SetFaction(Faction.OfMechanoids, (Pawn) null);
    }

    private void InitializePhysics()
    {
      IntVec3 position = ((Thing) this).Position;
      this.exactPosition = (position.ToVector3Shifted() + new Vector3(0.0f, 5f, 0.0f));
      this.exactVelocity = (Vector3.forward * 0.1f);
      this.bodyFacing = Vector3.forward;
    }

    private void RegisterToMapComponent(Map map)
    {
      map.GetComponent<WormBossMapComponent>()?.RegisterBoss(this);
    }

    private void UpdateHeadAnimation()
    {
      if ((double) Mathf.Abs(this._headAnimateFactor - this._targetHeadAnimateFactor) <= 1.0 / 1000.0)
        return;
      this._headAnimateFactor = Mathf.MoveTowards(this._headAnimateFactor, this._targetHeadAnimateFactor, 0.05f);
    }

    private void SpawnBodySegments(Map map)
    {
      if (this.segments == null)
        this.segments = new List<WormBody>();
      this.segments.Clear();
      WormBossDefExtension modExtension = ((Def) ((Thing) this).def).GetModExtension<WormBossDefExtension>();
      ThingDef defDefault = WormDefOf.Mst_Worm_Body_Normal;
      ThingDef defTail = WormDefOf.Mst_Worm_Tail;
      if (modExtension != null)
      {
        if (!GenText.NullOrEmpty(modExtension.defaultBodyDef))
        {
          ThingDef named = DefDatabase<ThingDef>.GetNamed(modExtension.defaultBodyDef, false);
          if (named != null)
            defDefault = named;
        }
        if (!GenText.NullOrEmpty(modExtension.tailDef))
        {
          ThingDef named = DefDatabase<ThingDef>.GetNamed(modExtension.tailDef, false);
          if (named != null)
            defTail = named;
        }
      }
      if (defDefault == null)
      {
        Log.Error("[WormHead] Critical Error: Default body def is null! Check WormDefOf or XML.");
      }
      else
      {
        WormThingBase leader = (WormThingBase) this;
        int bodyCount = this.bodySegmentCount - 1;
        for (int index = 0; index < bodyCount; ++index)
        {
          WormBody singleSegment = this.CreateSingleSegment(this.GetSegmentDef(index, bodyCount, defDefault, defTail, modExtension), index, leader, map);
          this.segments.Add(singleSegment);
          leader = (WormThingBase) singleSegment;
        }
        this._segmentsSpawned = true;
        this.NotifyCoordinator();
      }
    }

    private ThingDef GetSegmentDef(
      int index,
      int bodyCount,
      ThingDef defDefault,
      ThingDef defTail,
      WormBossDefExtension ext)
    {
      int num = index + 2;
      if (ext?.specialSegments != null)
      {
        foreach (BodyPartRule specialSegment in ext.specialSegments)
        {
          if (specialSegment.specificIndex == num | (specialSegment.everyNth > 0 && index % specialSegment.everyNth == 0))
          {
            ThingDef named = DefDatabase<ThingDef>.GetNamed(specialSegment.defName, false);
            if (named != null)
              return named;
          }
        }
      }
      return index == bodyCount - 1 && defTail != null ? defTail : defDefault;
    }

    private WormBody CreateSingleSegment(ThingDef def, int index, WormThingBase leader, Map map)
    {
      WormBody singleSegment = (WormBody) ThingMaker.MakeThing(def, (ThingDef) null);
      if (((Thing) this).Faction != null)
        ((Thing) singleSegment).SetFaction(((Thing) this).Faction, (Pawn) null);
      singleSegment.Head = this;
      singleSegment.Leader = leader;
      singleSegment.SegmentIndex = index;
      Vector3 pos = (leader.ExactPosition + ((-leader.BodyFacing) * this.segmentSpacing));
      singleSegment.SetPhysicsState(pos, Vector3.zero);
      singleSegment.SetBodyFacing(leader.BodyFacing);
      GenSpawn.Spawn((Thing) singleSegment, ((Thing) this).Position, map, (WipeMode) 0);
      return singleSegment;
    }

    private void NotifyCoordinator()
    {
      CompWormCoordinator comp = this.GetComp<CompWormCoordinator>();
      if (comp == null)
        return;
      comp.Clear();
      foreach (WormBody segment in this.segments)
        comp.RegisterSegment(segment);
    }

    private void DestroyAllSegments()
    {
      if (this.segments == null)
        return;
      this._destroySegmentsScratch.Clear();
      for (int index = 0; index < this.segments.Count; ++index)
        this._destroySegmentsScratch.Add(this.segments[index]);
      this.segments.Clear();
      for (int index = 0; index < this._destroySegmentsScratch.Count; ++index)
      {
        WormBody wormBody = this._destroySegmentsScratch[index];
        if (wormBody != null && !((Thing) wormBody).Destroyed)
        {
          wormBody.Leader = (WormThingBase) null;
          ((Thing) wormBody).Destroy((DestroyMode) 2);
        }
      }
    }

    public virtual void Kill(DamageInfo? dinfo = null, Hediff exactCulprit = null)
    {
      if (((Thing) this).Destroyed || this._isDyingSequence)
        return;
      this.StartDeathSequence();
    }

    private void StartDeathSequence()
    {
      this._isDyingSequence = true;
      ((Thing) this).HitPoints = 1;
      Type type = this.Brain?.Props?.deathPhaseClass;
      if ((object) type == null)
        type = typeof (Phase_DeathSequence);
      this.Brain?.SetPhase((WormPhase) Activator.CreateInstance(type));
      this.Swarm?.BroadcastOrder(ProbeCommand.Suicide((Thing) this));
    }

    public void ForceKillAndDropLoot()
    {
      if (((Thing) this).Destroyed)
        return;
      this.DoMassiveExplosions();
      this.Swarm?.ReleaseAll();
      this.GenerateLoot();
      this._isDyingSequence = false;
      base.Kill(new DamageInfo?(), (Hediff) null);
    }

    private void DoMassiveExplosions()
    {
      GenExplosion.DoExplosion(((Thing) this).Position, ((Thing) this).Map, 10.9f, DamageDefOf.Bomb, (Thing) this, 500, -1f, (SoundDef) null, (ThingDef) null, (ThingDef) null, (Thing) null, (ThingDef) null, 0.0f, 1, new GasType?(), new float?(), (int) byte.MaxValue, false, (ThingDef) null, 0.0f, 1, 0.0f, false, new float?(), (List<Thing>) null, new FloatRange?(), true, 1f, 0.0f, true, (ThingDef) null, 1f, (SimpleCurve) null, (List<IntVec3>) null, (ThingDef) null, (ThingDef) null);
      if (this.segments == null)
        return;
      for (int index = this.segments.Count - 1; index >= 0; --index)
      {
        WormBody segment = this.segments[index];
        if (segment != null && !((Thing) segment).Destroyed)
        {
          GenExplosion.DoExplosion(((Thing) segment).Position, ((Thing) segment).Map, 2.9f, DamageDefOf.Bomb, (Thing) this, 50, -1f, (SoundDef) null, (ThingDef) null, (ThingDef) null, (Thing) null, (ThingDef) null, 0.0f, 1, new GasType?(), new float?(), (int) byte.MaxValue, false, (ThingDef) null, 0.0f, 1, 0.0f, false, new float?(), (List<Thing>) null, new FloatRange?(), true, 1f, 0.0f, true, (ThingDef) null, 1f, (SimpleCurve) null, (List<IntVec3>) null, (ThingDef) null, (ThingDef) null);
          ((Thing) segment).Destroy((DestroyMode) 2);
        }
      }
      this.segments.Clear();
    }

    private void GenerateLoot() => Log.Message("[WormBoss] Boss Defeated! Loot generated.");

    public void InstantRelocate(
      Vector3 newPos,
      Vector3? newVel = null,
      Vector3? newFacing = null,
      bool stackSegments = true)
    {
      Vector3? nullable = newVel;
      Vector3 newVel1 = nullable ?? this.ExactVelocity;
      nullable = newFacing;
      Vector3 newFacing1 = nullable ?? this.BodyFacing;
      this.ForceSetPosition(newPos, newVel1, newFacing1);
      this.GetComp<TC_WormMovingController>()?.ForceVelocity(newVel1);
      if (this.BodySegments == null)
        return;
      foreach (WormBody bodySegment in this.BodySegments)
      {
        if (bodySegment != null && !((Thing) bodySegment).Destroyed)
        {
          bodySegment.ForceSetPosition(newPos, newVel1, newFacing1);
          bodySegment.SetBodyFacing(newFacing1);
        }
      }
    }

    public void InstantAlignBody(Vector3 headPos, Vector3 direction, Vector3 velocity)
    {
      this.ForceSetPosition(headPos, velocity, direction);
      this.GetComp<TC_WormMovingController>()?.ForceVelocity(velocity);
      if (this.BodySegments == null)
        return;
      Vector3 vector3 = (-direction.normalized);
      float num = 0.0f;
      for (int index = 0; index < this.BodySegments.Count; ++index)
      {
        WormBody bodySegment = this.BodySegments[index];
        num += this.segmentSpacing;
        bodySegment.ForceSetPosition((headPos + (vector3 * num)), velocity, direction);
        bodySegment.SetBodyFacing(direction);
      }
    }

    public void SetHeadAnimation(float target01)
    {
      this._targetHeadAnimateFactor = Mathf.Clamp01(target01);
    }

    public override void ExposeData()
    {
      base.ExposeData();
      Scribe_Values.Look<int>(ref this.bodySegmentCount, "bodySegmentCount", 10, false);
      Scribe_Values.Look<float>(ref this.segmentSpacing, "segmentSpacing", 1.8f, false);
      Scribe_Values.Look<bool>(ref this._segmentsSpawned, "segmentsSpawned", false, false);
      Scribe_Values.Look<bool>(ref this._isDyingSequence, "isDyingSequence", false, false);
      Scribe_Collections.Look<WormBody>(ref this.segments, "segments", (LookMode) 3, Array.Empty<object>());
    }
  }
}
