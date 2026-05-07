// Decompiled with JetBrains decompiler
// Type: NCLWorm.Verb_WormDeathRay
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
  public class Verb_WormDeathRay : Verb
  {
    private ExoDeathRay _laserEffect;
    private float _currentOverheat = 0.0f;
    private List<Thing> _cachedTargets = new List<Thing>();
    private HashSet<Thing> _uniqueThingsCache = new HashSet<Thing>();
    private HashSet<IntVec3> _scannedCellsCache = new HashSet<IntVec3>();
    private int _ticksUntilNextScan = 0;
    private Vector3 _lastCasterPos;
    private Vector3 _lastBeamEndPos;
    private const int SCAN_INTERVAL = 10;
    private const float BEAM_RANGE = 120f;

    private TC_WormWeaponController Controller
    {
      get => this.EquipmentSource?.GetComp<TC_WormWeaponController>();
    }

    private CompWormLaserWeapon LaserComp => this.EquipmentSource?.GetComp<CompWormLaserWeapon>();

    protected virtual int ShotsPerBurst => this.verbProps.burstShotCount;

    public virtual void WarmupComplete()
    {
      base.WarmupComplete();
      this._ticksUntilNextScan = 0;
      this._currentOverheat = 0.0f;
      this._cachedTargets.Clear();
      this._uniqueThingsCache.Clear();
      this._scannedCellsCache.Clear();
      this.EnsureLaserEffectCreated();
    }

    public virtual void Reset()
    {
      base.Reset();
      this._currentOverheat = 0.0f;
      if (this._laserEffect != null && !this._laserEffect.Destroyed)
        ((Thing) this._laserEffect).Destroy((DestroyMode) 0);
      this._laserEffect = (ExoDeathRay) null;
      this._cachedTargets.Clear();
    }

    public virtual void BurstingTick()
    {
      base.BurstingTick();
      if (this.Controller == null || this.caster == null || !this.caster.Spawned)
        return;
      float num1 = (float) (1.0 - (double) this.burstShotsLeft / (double) this.verbProps.burstShotCount);
      this._currentOverheat = Mathf.Lerp(this._currentOverheat, this.LaserComp?.Props.overheatCurve != null ? this.LaserComp.Props.overheatCurve.Evaluate(num1) : num1 * num1, 0.15f);
      this.Controller.SyncOverheatData(this._currentOverheat, (double) this._currentOverheat > 0.40000000596046448);
      CompWormLaserWeapon laserComp1 = this.LaserComp;
      float num2 = laserComp1 != null ? laserComp1.Props.normalWidth : 2f;
      CompWormLaserWeapon laserComp2 = this.LaserComp;
      float num3 = laserComp2 != null ? laserComp2.Props.overheatWidth : 6f;
      float num4 = Mathf.Lerp(num2, num3, this._currentOverheat);
      if ((double) this._currentOverheat > 0.30000001192092896)
        num4 += (float) ((double) Random.Range(-0.4f, 1.2f) * (double) this._currentOverheat * 0.5);
      CompWormLaserWeapon laserComp3 = this.LaserComp;
      Color cNormal = laserComp3 != null ? laserComp3.Props.normalColor : Color.cyan;
      CompWormLaserWeapon laserComp4 = this.LaserComp;
      Color cHot = laserComp4 != null ? laserComp4.Props.overheatColor : Color.red;
      Vector3 start;
      Vector3 endDir;
      this.CalculateBeamVectors(out start, out endDir);
      Vector3 end = (start + (endDir * 120f));
      this.EnsureLaserEffectCreated();
      this._laserEffect?.UpdateData(start, endDir, 120f, num4, this._currentOverheat, cNormal, cHot);
      --this._ticksUntilNextScan;
      int num5;
      if (this._ticksUntilNextScan > 0)
      {
        Vector3 vector3_1 = (this.caster.DrawPos - this._lastCasterPos);
        if ((double) vector3_1.sqrMagnitude <= 0.0099999997764825821)
        {
          Vector3 vector3_2 = (end - this._lastBeamEndPos);
          num5 = (double) vector3_2.sqrMagnitude > 0.10000000149011612 ? 1 : 0;
          goto label_10;
        }
      }
      num5 = 1;
label_10:
      if (num5 != 0)
      {
        this.ScanTargets(start, end, num4);
        this._ticksUntilNextScan = 10;
        this._lastCasterPos = this.caster.DrawPos;
        this._lastBeamEndPos = end;
      }
      if ((double) this._currentOverheat <= 0.25 || Find.TickManager.TicksGame % 3 != 0)
        return;
      Find.CameraDriver.shaker.DoShake((float) (0.15000000596046448 + (double) this._currentOverheat * 0.60000002384185791));
    }

    protected override bool TryCastShot()
    {
      if (this._cachedTargets.Count > 0)
        this.ApplyDamageToCachedTargets((double) this._currentOverheat > 0.40000000596046448);
      return true;
    }

    private void CalculateBeamVectors(out Vector3 start, out Vector3 endDir)
    {
      Vector3 vector3_1;
      if (!(this.caster is WormThingBase caster))
      {
        Rot4 rotation = this.caster.Rotation;
        IntVec3 facingCell = rotation.FacingCell;
        vector3_1 = facingCell.ToVector3();
      }
      else
        vector3_1 = caster.BodyFacing;
      Vector3 vector3_2 = vector3_1;
      vector3_2.y = 0.0f;
      if ((double) vector3_2.sqrMagnitude > 1.0 / 1000.0)
        vector3_2.Normalize();
      else
        vector3_2 = Vector3.forward;
      start = (this.caster.DrawPos + (vector3_2 * 4f));
      endDir = vector3_2;
    }

    private void ScanTargets(Vector3 start, Vector3 end, float beamWidth)
    {
      this._cachedTargets.Clear();
      this._uniqueThingsCache.Clear();
      this._scannedCellsCache.Clear();
      Map map = this.caster.Map;
      float num = beamWidth * 0.5f;
      foreach (IntVec3 cell1 in GenSight.PointsOnLineOfSight(WormUtility.ClampWorldPosToMapCell(map, start), WormUtility.ClampWorldPosToMapCell(map, end)))
      {
        if (GenGrid.InBounds(cell1, map))
        {
          if ((double) num <= 0.5)
          {
            this.AddThingsFromCell(cell1);
          }
          else
          {
            foreach (IntVec3 cell2 in GenRadial.RadialCellsAround(cell1, num, true))
            {
              if (this._scannedCellsCache.Add(cell2))
                this.AddThingsFromCell(cell2);
            }
          }
        }
      }
    }

    private void AddThingsFromCell(IntVec3 cell)
    {
      if (!GenGrid.InBounds(cell, this.caster.Map))
        return;
      List<Thing> thingList = GridsUtility.GetThingList(cell, this.caster.Map);
      Faction faction = this.caster.Faction;
      for (int index = thingList.Count - 1; index >= 0; --index)
      {
        Thing thing = thingList[index];
        if (thing != this.caster && !(thing is WormBody) && !(thing is WormHead) && (thing is Pawn || thing is Building) && (thing.def.building == null || !thing.def.building.isNaturalRock && !thing.def.mineable) && (!(thing is Building) || thing.Faction != null))
        {
          if (faction == Faction.OfPlayer)
          {
            if (thing.Faction == null || !FactionUtility.HostileTo(thing.Faction, Faction.OfPlayer))
              continue;
          }
          else if (faction != null && thing.Faction != null && !FactionUtility.HostileTo(thing.Faction, faction))
            continue;
          if (this._uniqueThingsCache.Add(thing))
            this._cachedTargets.Add(thing);
        }
      }
    }

    private void ApplyDamageToCachedTargets(bool isHot)
    {
      if (this._cachedTargets.Count == 0)
        return;
      float num1 = 0.9f;
      DamageDef damageDef = DamageDefOf.Burn;
      bool flag = false;
      float limbBreakFactor = 0.1f;
      if (this.LaserComp != null)
      {
        damageDef = this.LaserComp.DamageType;
        num1 = this.LaserComp.GetArmorPenetration();
        limbBreakFactor = this.LaserComp.GetCurrentLimbBreakFactor(this._currentOverheat);
        flag = true;
      }
      for (int index = 0; index < this._cachedTargets.Count; ++index)
      {
        Thing cachedTarget = this._cachedTargets[index];
        if (cachedTarget != null && !cachedTarget.Destroyed)
        {
          float num2 = flag ? BossFormula.CalculateTrueDamage(cachedTarget, limbBreakFactor) : 15f;
          DamageInfo damageInfo;
          // ISSUE: explicit constructor call
          damageInfo = new DamageInfo(damageDef, num2, num1, -1f, this.caster, (BodyPartRecord) null, ((Thing) this.EquipmentSource).def, (DamageInfo.SourceCategory) 0, (Thing) null, true, true, (QualityCategory) 2, true, false);
          damageInfo.SetIgnoreArmor(true);
          cachedTarget.TakeDamage(damageInfo);
        }
      }
    }

    private void EnsureLaserEffectCreated()
    {
      if (this._laserEffect != null && !this._laserEffect.Destroyed)
        return;
      this._laserEffect = (ExoDeathRay) ThingMaker.MakeThing(WormDefOf.Mst_HadesLaserEffect, (ThingDef) null);
      GenSpawn.Spawn((Thing) this._laserEffect, this.caster.Position, this.caster.Map, (WipeMode) 0);
    }
  }
}
