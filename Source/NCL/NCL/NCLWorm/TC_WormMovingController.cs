// Decompiled with JetBrains decompiler
// Type: NCLWorm.TC_WormMovingController
// Assembly: NCL_WormBoss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B627DDDD-3AC9-4D6A-89F6-EA72F95FB570
// Assembly location: C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3555799437\1.6\Assemblies\NCL_WormBoss (3).dll

using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

#nullable disable
namespace NCLWorm
{
  public class TC_WormMovingController : ThingComp
  {
    private const float THRESHOLD_SINGULARITY_DIST = 1.5f;
    private const float THRESHOLD_DESTINATION = 1f;
    private const float VERTICAL_P_GAIN = 0.1f;
    private const float VERTICAL_MAX_SPEED = 0.3f;
    private const float VERTICAL_RESPONSE = 0.6f;
    private const float COLLISION_RADIUS = 3.5f;
    private const int COLLISION_COOLDOWN_TICKS = 15;
    private HashSet<Thing> _tmpCollidedThings = new HashSet<Thing>();
    private MovementIntent _currentIntent = MovementIntent.Invalid;
    private Vector3 _velocity;
    private int _collisionCooldown = 0;

    public TCP_WormMovingController Props => (TCP_WormMovingController) this.props;

    private WormThingBase Body => this.parent as WormThingBase;

    public bool AtDestination { get; private set; }

    public float CurrentSpeed => this._velocity.magnitude;

    public virtual void PostSpawnSetup(bool respawningAfterLoad)
    {
      base.PostSpawnSetup(respawningAfterLoad);
      if (this.Body == null || !(this._velocity == Vector3.zero))
        return;
      this._velocity = (this.Body.BodyFacing * WormMath.SpeedToCellsPerTick(this.Props.baseSpeed));
    }

    public void SetIntent(MovementIntent intent) => this._currentIntent = intent;

    public void ForceVelocity(Vector3 newVel)
    {
      this._velocity = newVel;
      this._collisionCooldown = 0;
      this.AtDestination = false;
    }

    public virtual void CompTick()
    {
      base.CompTick();
      if (this.Body == null)
        return;
      if (this._currentIntent.IsValid)
        this.ExecuteMovementLogic();
      else
        this.ApplyBrakingLogic();
      this.ApplyPhysicsStep();
      this.HandleCollisionDamage();
    }

    private void ExecuteMovementLogic()
    {
      Vector3 vector3 = Vector3.zero;
      if (this._currentIntent.RelativeTo != null && !this._currentIntent.RelativeTo.Destroyed && this._currentIntent.RelativeTo is WormThingBase relativeTo)
        vector3 = relativeTo.ExactVelocity;
      this._velocity = (this._velocity - vector3);
      float cellsPerTick = WormMath.SpeedToCellsPerTick(this.Props.baseSpeed * this._currentIntent.SpeedFactor);
      float radPerTick = WormMath.TurnRateToRadPerTick(this.Props.turnRate * this._currentIntent.TurnFactor);
      float speedChangePerTick = WormMath.AccelToSpeedChangePerTick(this.Props.acceleration * this._currentIntent.AccelFactor);
      Vector3 horizontalVelocity = this.CalculateHorizontalVelocity(cellsPerTick, radPerTick, speedChangePerTick);
      float num = this.ClampPitchVelocity(this.CalculateVerticalVelocity(speedChangePerTick), horizontalVelocity.magnitude);
      this._velocity = (new Vector3(horizontalVelocity.x, num, horizontalVelocity.z) + vector3);
      this.UpdateBodyFacing(radPerTick);
    }

    private Vector3 CalculateHorizontalVelocity(
      float targetSpeedTick,
      float maxTurnRad,
      float maxAccel)
    {
      Vector3 vector3_1 = (this._currentIntent.TargetPosition - this.Body.ExactPosition);
      vector3_1.y = 0.0f;
      float magnitude = vector3_1.magnitude;
      float num = this._currentIntent.RelativeTo != null ? 0.1f : 1f;
      this.AtDestination = (double) magnitude < (double) num;
      Vector3 vector3_2;
      if ((double) magnitude < 1.5)
      {
        Vector3 bodyFacing = this.Body.BodyFacing;
        bodyFacing.y = 0.0f;
        vector3_2 = (double) bodyFacing.sqrMagnitude < 1.0 / 1000.0 ? Vector3.forward : bodyFacing.normalized;
        this.AtDestination = false;
      }
      else
        vector3_2 = this.AtDestination ? Vector3.zero : vector3_1.normalized;
      Vector3 currentVelocity;
      // ISSUE: explicit constructor call
      currentVelocity = new Vector3(this._velocity.x, 0.0f, this._velocity.z);
      return WormMath.AerodynamicTurn(currentVelocity, (vector3_2 * targetSpeedTick), maxTurnRad, maxAccel);
    }

    private float CalculateVerticalVelocity(float maxAccel)
    {
      return Mathf.MoveTowards(this._velocity.y, Mathf.Clamp((this._currentIntent.OverrideAltitude - this.Body.ExactPosition.y) * 0.1f, -0.3f, 0.3f), maxAccel * 0.6f);
    }

    private float ClampPitchVelocity(float velY, float speedH)
    {
      float num = Mathf.Max(speedH * 0.6f, 0.05f);
      return Mathf.Clamp(velY, -num, num);
    }

    private void ApplyBrakingLogic()
    {
      float speedChangePerTick = WormMath.AccelToSpeedChangePerTick(this.Props.acceleration);
      float radPerTick = WormMath.TurnRateToRadPerTick(this.Props.turnRate);
      Vector3 vector3_1;
      // ISSUE: explicit constructor call
      vector3_1 = new Vector3(this._velocity.x, 0.0f, this._velocity.z);
      Vector3 vector3_2 = Vector3.MoveTowards(vector3_1, Vector3.zero, speedChangePerTick);
      float num = Mathf.MoveTowards(this._velocity.y, 0.0f, speedChangePerTick);
      this._velocity = new Vector3(vector3_2.x, num, vector3_2.z);
      this.AtDestination = false;
      this.UpdateBodyFacing(radPerTick);
    }

    private void UpdateBodyFacing(float maxRadTick)
    {
      Vector3 vector3;
      if (this._currentIntent.IsValid && this._currentIntent.ForceFacing.HasValue)
      {
        vector3 = this._currentIntent.ForceFacing.Value;
        vector3.y = 0.0f;
      }
      else
      {
        vector3 = (double) this._velocity.sqrMagnitude > 9.9999997473787516E-05 ? this._velocity.normalized : this.Body.BodyFacing;
        vector3.y = Mathf.Clamp(vector3.y, -0.35f, 0.35f);
      }
      if ((double) vector3.sqrMagnitude <= 1.0 / 1000.0)
        return;
      vector3.Normalize();
      this.Body.SetBodyFacing(Vector3.RotateTowards(this.Body.BodyFacing, vector3, maxRadTick, 0.0f));
    }

    private void ApplyPhysicsStep()
    {
      Vector3 newPos;
      Vector3 newVel;
      WormMath.StepPhysics3D(this.Body.ExactPosition, this._velocity, Vector3.zero, 0.01f, 0.0f, out newPos, out newVel);
      this._velocity = newVel;
      this.Body.SetPhysicsState(newPos, this._velocity);
    }

    private void HandleCollisionDamage()
    {
      if (this._collisionCooldown > 0)
      {
        --this._collisionCooldown;
      }
      else
      {
        if (!Gen.IsHashIntervalTick((Thing) this.parent, this.parent is WormHead ? 3 : 10) || (double) this.Body.Altitude > 2.0 || (double) WormMath.SpeedToCellsPerSecond(this.CurrentSpeed) < (double) this.Props.minImpactSpeed)
          return;
        this._tmpCollidedThings.Clear();
        float num1 = 12.25f;
        int num2 = GenRadial.NumCellsInRadius(3.5f);
        IntVec3 position = ((Thing) this.parent).Position;
        Map map = ((Thing) this.parent).Map;
        bool flag = false;
        for (int index1 = 0; index1 < num2; ++index1)
        {
          IntVec3 intVec3 = (position + GenRadial.RadialPattern[index1]);
          if (GenGrid.InBounds(intVec3, map))
          {
            List<Thing> thingList = GridsUtility.GetThingList(intVec3, map);
            for (int index2 = thingList.Count - 1; index2 >= 0; --index2)
            {
              Thing thing = thingList[index2];
              if (this._tmpCollidedThings.Add(thing) && this.IsValidCollisionTarget(thing) && (double) WormMath.Distance2DSquared(thing.DrawPos, this.Body.ExactPosition) <= (double) num1)
              {
                this.DoImpactDamage(thing);
                flag = true;
              }
            }
          }
        }
        this._tmpCollidedThings.Clear();
        if (!flag)
          return;
        this._collisionCooldown = 15;
        if (Find.CurrentMap == ((Thing) this.parent).Map)
          Find.CameraDriver.shaker.DoShake(0.3f);
      }
    }

    private bool IsValidCollisionTarget(Thing t)
    {
      if (t == null || t.Destroyed || t == this.parent || t == this.Body || t is WormThingBase)
        return false;
      if (((Thing) this.parent).Faction == Faction.OfPlayer)
      {
        if (t.Faction == null || !FactionUtility.HostileTo(t.Faction, Faction.OfPlayer))
          return false;
      }
      else if (t.def.building != null && t.def.building.isNaturalRock || t is Building && t.Faction == null || ((Thing) this.parent).Faction != null && t.Faction != null && !FactionUtility.HostileTo(((Thing) this.parent).Faction, t.Faction))
        return false;
      return t is Pawn || t is Building;
    }

    private void DoImpactDamage(Thing victim)
    {
      float num1 = Mathf.Max(0.1f, WormMath.SpeedToCellsPerSecond(this.CurrentSpeed) / this.Props.baseSpeed);
      float num2 = this.Props.impactArmorPenetration;
      float num3;
      if (this.parent is WormHead)
      {
        float limbBreakFactor = Mathf.Min(this.Props.headBaseLimbBreakFactor * (1f + Mathf.Max(0.0f, num1 - 1f) * this.Props.headSpeedScale), this.Props.headMaxLimbBreakFactor);
        num3 = BossFormula.CalculateTrueDamage(victim, limbBreakFactor);
        num2 = 5f;
      }
      else
        num3 = this.Props.impactDamage * Mathf.Clamp(num1, 0.5f, 3f);
      DamageDef damageDef = this.Props.impactDamageDef ?? DamageDefOf.Blunt;
      DamageInfo damageInfo;
      // ISSUE: explicit constructor call
      damageInfo = new DamageInfo(damageDef, num3, num2, -1f, (Thing) this.parent, (BodyPartRecord) null, (ThingDef) null, (DamageInfo.SourceCategory) 0, (Thing) null, true, true, (QualityCategory) 2, true, false);
      Pawn pawn = null;
      int num4;
      if (this.parent is WormHead)
      {
        pawn = victim as Pawn;
        num4 = pawn != null ? 1 : 0;
      }
      else
        num4 = 0;
      if (num4 != 0)
        pawn.pather?.StopDead();
      victim.TakeDamage(damageInfo);
    }

    public virtual void PostExposeData()
    {
      base.PostExposeData();
      Scribe_Values.Look<Vector3>(ref this._velocity, "velocity", new Vector3(), false);
    }
  }
}
