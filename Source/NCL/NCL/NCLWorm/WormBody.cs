// Decompiled with JetBrains decompiler
// Type: NCLWorm.WormBody
// Assembly: NCL_WormBoss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B627DDDD-3AC9-4D6A-89F6-EA72F95FB570
// Assembly location: C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3555799437\1.6\Assemblies\NCL_WormBoss (3).dll

using UnityEngine;
using Verse;

#nullable disable
namespace NCLWorm
{
  public class WormBody : WormThingBase
  {
    public WormThingBase Leader;
    public WormHead Head;
    public int SegmentIndex;
    private TC_WormWeaponController _cachedWeaponComp;
    private float _ventOpenFactor = 0.0f;
    private float _targetVentFactor = 0.0f;
    public float ServoSpeedMultiplier = 1f;
    private const float BASE_SERVO_SPEED = 0.05f;

    public TC_WormWeaponController Weapon
    {
      get
      {
        if (this._cachedWeaponComp == null)
          this._cachedWeaponComp = this.GetComp<TC_WormWeaponController>();
        return this._cachedWeaponComp;
      }
    }

    public float Spacing => this.Head == null ? 1.8f : this.Head.segmentSpacing;

    public override float CurrentAnimateFactor => this._ventOpenFactor;

    public float VentOpenFactor => this._ventOpenFactor;

    protected override float VisualLayerOffset
    {
      get
      {
        float visualLayerOffset = (float) -((double) (this.SegmentIndex + 1) * (1.0 / 1000.0));
        if (this.CachedVisuals != null && this.CachedVisuals.drawBehindBody)
          --visualLayerOffset;
        return visualLayerOffset;
      }
    }

    protected override void Tick()
    {
      if (this.Leader == null || !((Thing) this.Leader).Spawned || ((Thing) this.Leader).Destroyed)
      {
        ((Thing) this).Destroy((DestroyMode) 0);
      }
      else
      {
        base.Tick();
        this.UpdateServoState();
        this.UpdateKinematics();
      }
    }

    private void UpdateKinematics()
    {
      Vector3 exactPosition = this.Leader.ExactPosition;
      Vector3 vector3_1 = (this.exactPosition - exactPosition);
      if ((double) vector3_1.sqrMagnitude < 9.9999997473787516E-05)
        vector3_1 = (-this.Leader.BodyFacing);
      Vector3 normalized = vector3_1.normalized;
      float num = 0.0f;
      if (this.Head != null)
      {
        TC_WormDecisionController comp = this.Head.GetComp<TC_WormDecisionController>();
        if (comp != null)
          num = comp.SegmentReorientationStrength;
      }
      Vector3 vector3_2 = Vector3.zero;
      if ((double) num > 1.0 / 1000.0 && this.SegmentIndex >= 1)
      {
        if (this.Leader is WormBody leader2 && leader2.Leader != null)
          vector3_2 = (leader2.ExactPosition - leader2.Leader.ExactPosition);
        else if (this.Leader is WormHead leader1)
          vector3_2 = ((-leader1.BodyFacing) * this.Spacing);
        vector3_2.y = 0.0f;
      }
      Vector3 vector3_3;
      if ((double) num <= 1.0 / 1000.0)
      {
        vector3_3 = normalized;
      }
      else
      {
        Vector3 vector3_4 = (normalized + ((vector3_2 * num) * 0.5f));
        vector3_3 = vector3_4.normalized;
      }
      Vector3 pos = (exactPosition + (vector3_3 * this.Spacing));
      this.SetPhysicsState(pos, (pos - this.exactPosition));
      Vector3 vector3_5 = (exactPosition - pos);
      if ((double) vector3_5.sqrMagnitude <= 1.0 / 1000.0)
        return;
      Vector3 vector3_6 = (exactPosition - pos);
      this.SetBodyFacing(vector3_6.normalized);
    }

    private void UpdateServoState()
    {
      float num = 0.05f * this.ServoSpeedMultiplier;
      if ((double) Mathf.Abs(this._ventOpenFactor - this._targetVentFactor) <= 1.0 / 1000.0)
        return;
      this._ventOpenFactor = Mathf.MoveTowards(this._ventOpenFactor, this._targetVentFactor, num);
    }

    public void SetVentState(float target01) => this._targetVentFactor = Mathf.Clamp01(target01);

    public override void ExposeData()
    {
      base.ExposeData();
      Scribe_Values.Look<int>(ref this.SegmentIndex, "SegmentIndex", 0, false);
      Scribe_Values.Look<float>(ref this._ventOpenFactor, "ventOpenFactor", 0.0f, false);
      Scribe_Values.Look<float>(ref this._targetVentFactor, "targetVentFactor", 0.0f, false);
      Scribe_Values.Look<float>(ref this.ServoSpeedMultiplier, "servoSpeedMult", 1f, false);
      Scribe_References.Look<WormThingBase>(ref this.Leader, "Leader", false);
      Scribe_References.Look<WormHead>(ref this.Head, "Head", false);
    }
  }
}
