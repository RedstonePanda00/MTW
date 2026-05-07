// Decompiled with JetBrains decompiler
// Type: NCLWorm.ExoTelegraphCone
// Assembly: NCL_WormBoss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B627DDDD-3AC9-4D6A-89F6-EA72F95FB570
// Assembly location: C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3555799437\1.6\Assemblies\NCL_WormBoss (3).dll

using System;
using UnityEngine;
using Verse;

#nullable disable
namespace NCLWorm
{
  public class ExoTelegraphCone : Thing
  {
    public Thing Anchor;
    private Vector3 _currentAimPos;
    private Vector3 _desiredAimPos;
    public float TrackingSpeed = 0.2f;
    public bool IsLocked = false;
    private bool _hasInitializedPos = false;
    public float TurnRate = -1f;
    public float BaseWidth = 2f;
    public float ConeLength = 50f;
    private Color _coreColor = new Color(1f, 0.0f, 0.0f, 0.2f);
    private Color _hazeColor = new Color(1f, 0.0f, 0.0f, 0.3f);
    private int _timeLeft = 2;
    private Mesh _viewMesh;

    public Vector3 CurrentAimPosition => this._currentAimPos;

    public bool IsInPool { get; private set; } = false;

    public void ResetForPool()
    {
      this._timeLeft = 2;
      this._hasInitializedPos = false;
      this.IsLocked = false;
      this.TurnRate = -1f;
      this.IsInPool = false;
    }

    public void PrepareForPool()
    {
      this.IsInPool = true;
      this.Anchor = (Thing) null;
    }

    public virtual void SpawnSetup(Map map, bool respawningAfterLoad)
    {
      base.SpawnSetup(map, respawningAfterLoad);
      this._timeLeft = 2;
      this._hasInitializedPos = false;
      ExoTelegraphVisuals modExtension = ((Def) this.def).GetModExtension<ExoTelegraphVisuals>();
      if (modExtension == null)
        return;
      this._coreColor = modExtension.coreColor;
      this._hazeColor = modExtension.hazeColor;
    }

    public virtual void Destroy(DestroyMode mode = 0)
    {
      if (this._viewMesh != null)
      {
        UnityEngine.Object.Destroy(this._viewMesh);
        this._viewMesh = (Mesh) null;
      }
      base.Destroy(mode);
    }

    protected virtual void Tick()
    {
      if (this.IsInPool)
        return;
      if (this.Anchor == null || this.Anchor.Destroyed || !this.Anchor.Spawned)
      {
        TelegraphConePool.Return(this);
      }
      else
      {
        if ((this.Anchor.Position != this.Position))
          this.Position = this.Anchor.Position;
        if (!this.IsLocked && this._hasInitializedPos)
        {
          if ((double) this.TurnRate > 0.0)
            this.UpdateRotationPhysics();
          else
            this._currentAimPos = Vector3.Lerp(this._currentAimPos, this._desiredAimPos, this.TrackingSpeed);
        }
        --this._timeLeft;
        if (this._timeLeft > 0)
          return;
        TelegraphConePool.Return(this);
      }
    }

    private void UpdateRotationPhysics()
    {
      Vector3 drawPos = this.Anchor.DrawPos;
      Vector3 vector3_1 = (this._currentAimPos - drawPos);
      Vector3 vector3_2 = (this._desiredAimPos - drawPos);
      vector3_1.y = 0.0f;
      vector3_2.y = 0.0f;
      if ((double) vector3_1.sqrMagnitude < 1.0 / 1000.0)
        vector3_1 = Vector3.forward;
      if ((double) vector3_2.sqrMagnitude < 1.0 / 1000.0)
        vector3_2 = Vector3.forward;
      float num = (float) ((double) this.TurnRate * (Math.PI / 180.0) / 60.0);
      Vector3 vector3_3 = Vector3.RotateTowards(vector3_1, vector3_2, num, 0.0f);
      float magnitude = vector3_2.magnitude;
      this._currentAimPos = (drawPos + (vector3_3.normalized * magnitude));
    }

    public void UpdateLock(Vector3 targetPos, bool lockStatus, bool snapToTarget = false)
    {
      if (!this.IsLocked)
        this._desiredAimPos = targetPos;
      if (snapToTarget || !this._hasInitializedPos)
      {
        this._currentAimPos = targetPos;
        this._hasInitializedPos = true;
      }
      this.IsLocked = lockStatus;
      this._timeLeft = 2;
    }

    protected virtual void DrawAt(Vector3 drawLoc, bool flip = false)
    {
      if (this.IsInPool || this.Anchor == null || !this._hasInitializedPos || this.Anchor is WormBody anchor && (double) anchor.CurrentAnimateFactor < 0.20000000298023224)
        return;
      if (this._viewMesh == null)
      {
        this._viewMesh = new Mesh();
        this._viewMesh.MarkDynamic();
        this._viewMesh.name = "TelegraphCone_" + this.thingIDNumber.ToString();
      }
      Vector3 drawPos = this.Anchor.DrawPos;
      Vector3 vector3 = (this._currentAimPos - drawPos);
      vector3.y = 0.0f;
      if ((double) vector3.sqrMagnitude < 1.0 / 1000.0)
        vector3 = Vector3.forward;
      else
        vector3.Normalize();
      float num = Altitudes.AltitudeFor((AltitudeLayer) 28);
      drawPos.y = num;
      Vector3 end = (drawPos + (vector3 * this.ConeLength));
      end.y = num;
      ExoLaserRenderer.RenderLaser(this._viewMesh, drawPos, end, this.BaseWidth, 0.0f, Time.time, false, this._hazeColor, this._coreColor, true);
    }
  }
}
