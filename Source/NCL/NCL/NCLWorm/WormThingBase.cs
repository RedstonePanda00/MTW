// Decompiled with JetBrains decompiler
// Type: NCLWorm.WormThingBase
// Assembly: NCL_WormBoss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B627DDDD-3AC9-4D6A-89F6-EA72F95FB570
// Assembly location: C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3555799437\1.6\Assemblies\NCL_WormBoss (3).dll

using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

#nullable disable
namespace NCLWorm
{
  [StaticConstructorOnStartup]
  public abstract class WormThingBase : 
    ThingWithComps,
    IAttackTarget,
    ILoadReferenceable,
    IAttackTargetSearcher
  {
    protected Vector3 exactPosition;
    protected Vector3 exactVelocity;
    protected Vector3 bodyFacing = Vector3.forward;
    protected Quaternion visualRotation = Quaternion.identity;
    private static Material _shadowMat;
    private bool _materialsInitialized = false;
    private WormSegmentVisuals _visuals;
    private static MaterialPropertyBlock _mpb;

    public bool IsVisualHidden { get; set; } = false;

    protected virtual float VisualLayerOffset => 0.0f;

    private static Material ShadowMat
    {
      get
      {
        return WormThingBase._shadowMat ?? (WormThingBase._shadowMat = MaterialPool.MatFrom("Things/Skyfaller/SkyfallerShadowCircle", ShaderDatabase.Transparent));
      }
    }

    public Vector3 ExactPosition => this.exactPosition;

    public Vector3 ExactVelocity => this.exactVelocity;

    public Vector3 BodyFacing => this.bodyFacing;

    public float Altitude => this.exactPosition.y;

    public Vector3 PreviousPosition { get; protected set; }

    public Thing Thing => (Thing) this;

    public float TargetPriorityFactor => 5f;

    public LocalTargetInfo TargetCurrentlyAimingAt => LocalTargetInfo.Invalid;

    public Pawn TargetCurrentlyAimingAtPawn => (Pawn) null;

    public bool ThreatDisabled(IAttackTargetSearcher f)
    {
      return !((Thing) this).Spawned || ((Thing) this).Destroyed;
    }

    public Verb CurrentEffectiveVerb => (Verb) null;

    public LocalTargetInfo LastAttackedTarget => LocalTargetInfo.Invalid;

    public int LastAttackTargetTick => 0;

    public virtual float CurrentAnimateFactor => 0.0f;

    protected WormSegmentVisuals CachedVisuals => this._visuals;

    public virtual void SetPhysicsState(Vector3 pos, Vector3 vel)
    {
      this.exactPosition = pos;
      this.exactVelocity = vel;
    }

    public virtual void SetBodyFacing(Vector3 newFacing)
    {
      if ((double) newFacing.sqrMagnitude < 1.0 / 1000.0)
        return;
      this.bodyFacing = newFacing.normalized;
      this.UpdateVisualRotation();
    }

    protected void UpdateVisualRotation()
    {
      Vector3 vector3;
      // ISSUE: explicit constructor call
      vector3 = new Vector3(this.bodyFacing.x, 0.0f, this.bodyFacing.z + this.bodyFacing.y * 0.5f);
      if ((double) vector3.sqrMagnitude <= 1.0 / 1000.0)
        return;
      this.visualRotation = Quaternion.LookRotation(vector3);
    }

    protected virtual void Tick()
    {
      if (((Thing) this).Destroyed)
        return;
      this.PreviousPosition = this.exactPosition;
      base.Tick();
      IntVec3 size = ((Thing) this).Map.Size;
      IntVec3 position = ((Thing) this).Position;
      IntVec3 intVec3 = IntVec3Utility.ToIntVec3(this.exactPosition);
      int num1 = Mathf.Clamp(intVec3.x, 0, size.x - 1);
      int num2 = Mathf.Clamp(intVec3.z, 0, size.z - 1);
      if (num1 == position.x && num2 == position.z)
        return;
      ((Thing) this).Position = new IntVec3(num1, position.y, num2);
    }

    public virtual Vector3 DrawPos
    {
      get
      {
        Vector3 drawPos = WormUtility.PhysicsPosToDrawPos(new Vector3(this.exactPosition.x, Mathf.Clamp(this.exactPosition.y, -2f, 3.5f), this.exactPosition.z));
        drawPos.y = Altitudes.AltitudeFor((AltitudeLayer) 28) + this.VisualLayerOffset;
        return drawPos;
      }
    }

    public virtual void ExposeData()
    {
      base.ExposeData();
      Scribe_Values.Look<Vector3>(ref this.exactPosition, "exactPosition", new Vector3(), false);
      Scribe_Values.Look<Vector3>(ref this.exactVelocity, "exactVelocity", new Vector3(), false);
      Scribe_Values.Look<Vector3>(ref this.bodyFacing, "bodyFacing", new Vector3(), false);
      if (Scribe.mode != LoadSaveMode.PostLoadInit)
        return;
      this.UpdateVisualRotation();
    }

    private static MaterialPropertyBlock MPB
    {
      get => WormThingBase._mpb ?? (WormThingBase._mpb = new MaterialPropertyBlock());
    }

    protected virtual void DrawAt(Vector3 drawLoc, bool flip = false)
    {
      if (this.IsVisualHidden || (double) this.exactPosition.y < -1.0)
        return;
      if (this._visuals == null && !this._materialsInitialized)
      {
        this._visuals = ((Def) ((Thing) this).def).GetModExtension<WormSegmentVisuals>();
        if (this._visuals?.parts != null)
          GenCollection.SortBy<WormVisualPart, int>(this._visuals.parts, (Func<WormVisualPart, int>) (p => p.sortOrder));
        this._materialsInitialized = true;
      }
      GraphicData graphicData1 = ((Thing) this).def.graphicData;
      Vector2 vector2 = graphicData1 != null ? graphicData1.drawSize : Vector2.one;
      Vector3 scale;
      // ISSUE: explicit constructor call
      scale = new Vector3(vector2.x, 1f, vector2.y);
      GraphicData graphicData2 = ((Thing) this).def.graphicData;
      Vector3 vector3 = graphicData2 != null ? graphicData2.drawOffset : Vector3.zero;
      Vector3 drawLoc1 = (drawLoc + (this.visualRotation * vector3));
      if (this._visuals?.parts != null && this._visuals.parts.Count > 0)
      {
        this.DrawAnimatedParts(drawLoc1, scale);
      }
      else
      {
        Matrix4x4 matrix4x4 = Matrix4x4.TRS(drawLoc1, this.visualRotation, scale);
        Graphics.DrawMesh(MeshPool.plane10, matrix4x4, ((Thing) this).Graphic.MatSingle, 0);
      }
      if ((double) this.exactPosition.y <= 0.10000000149011612)
        return;
      WormUtility.DrawShadow(((Thing) this).DrawPos, this.exactPosition.y, vector2.x, WormThingBase.ShadowMat);
    }

    protected virtual void DrawAnimatedParts(Vector3 drawLoc, Vector3 scale)
    {
      float currentAnimateFactor = this.CurrentAnimateFactor;
      Quaternion visualRotation = this.visualRotation;
      List<WormVisualPart> parts = this._visuals.parts;
      for (int index = 0; index < parts.Count; ++index)
      {
        WormThingBase.MPB.Clear();
        parts[index].Draw(drawLoc, visualRotation, scale, currentAnimateFactor, WormThingBase.MPB);
      }
    }

    public void ForceSetPosition(Vector3 newPos, Vector3 newVel, Vector3 newFacing)
    {
      this.exactPosition = newPos;
      this.exactVelocity = newVel;
      this.bodyFacing = newFacing;
      this.PreviousPosition = newPos;
      IntVec3 intVec3 = IntVec3Utility.ToIntVec3(newPos);
      if (!GenGrid.InBounds(intVec3, ((Thing) this).Map))
        return;
      ((Thing) this).Position = intVec3;
    }
  }
}
