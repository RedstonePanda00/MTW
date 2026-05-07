// Decompiled with JetBrains decompiler
// Type: NCLWorm.Mst_StaticTornado
// Assembly: NCL_WormBoss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B627DDDD-3AC9-4D6A-89F6-EA72F95FB570
// Assembly location: C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3555799437\1.6\Assemblies\NCL_WormBoss (3).dll

using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.Sound;

#nullable disable
namespace NCLWorm
{
  [StaticConstructorOnStartup]
  public class Mst_StaticTornado : ThingWithComps
  {
    private Vector2 realPosition;
    private int spawnTick;
    private int leftFadeOutTicks = -1;
    private Sustainer sustainer;
    private static MaterialPropertyBlock matPropertyBlock = new MaterialPropertyBlock();
    private static readonly Material TornadoMaterial = MaterialPool.MatFrom("Things/Ethereal/Tornado", ShaderDatabase.Transparent, MapMaterialRenderQueues.Tornado);
    private static readonly FloatRange PartsDistanceFromCenter = new FloatRange(1f, 10f);
    private static readonly float ZOffsetBias = -4f * Mst_StaticTornado.PartsDistanceFromCenter.min;
    private const float Wind = 5f;
    private const int CloseDamageIntervalTicks = 15;
    private const int RoofDestructionIntervalTicks = 20;
    private const float FarDamageMTBTicks = 15f;
    private const float CloseDamageRadius = 4.2f;
    private const float FarDamageRadius = 10f;
    private const int FadeInTicks = 120;
    private const int FadeOutTicks = 120;
    private List<IntVec3> removedRoofsTmp = new List<IntVec3>();
    private readonly List<IntVec3> _farDamageCellScratch = new List<IntVec3>();
    private List<Thing> tmpThings = new List<Thing>();

    private float FadeInOutFactor
    {
      get
      {
        return Mathf.Min(Mathf.Clamp01((float) (Find.TickManager.TicksGame - this.spawnTick) / 120f), this.leftFadeOutTicks < 0 ? 1f : Mathf.Min((float) this.leftFadeOutTicks / 120f, 1f));
      }
    }

    public virtual Vector2 DrawSize => new Vector2(45f, 100f);

    public virtual void ExposeData()
    {
      base.ExposeData();
      Scribe_Values.Look<Vector2>(ref this.realPosition, "realPosition", new Vector2(), false);
      Scribe_Values.Look<int>(ref this.spawnTick, "spawnTick", 0, false);
      Scribe_Values.Look<int>(ref this.leftFadeOutTicks, "leftFadeOutTicks", -1, false);
    }

    public virtual void SpawnSetup(Map map, bool respawningAfterLoad)
    {
      base.SpawnSetup(map, respawningAfterLoad);
      if (!respawningAfterLoad)
      {
        IntVec3 position = ((Thing) this).Position;
        Vector3 vector3Shifted = position.ToVector3Shifted();
        this.realPosition = new Vector2(vector3Shifted.x, vector3Shifted.z);
        this.spawnTick = Find.TickManager.TicksGame;
        this.leftFadeOutTicks = -1;
      }
      this.CreateSustainer();
    }

    protected virtual void Tick()
    {
      if (!((Thing) this).Spawned)
        return;
      if (this.sustainer == null)
        this.CreateSustainer();
      this.sustainer?.Maintain();
      this.UpdateSustainerVolume();
      this.GetComp<CompWindSource>().wind = 5f * this.FadeInOutFactor;
      if (this.leftFadeOutTicks > 0)
      {
        --this.leftFadeOutTicks;
        if (this.leftFadeOutTicks == 0)
        {
          ((Thing) this).Destroy((DestroyMode) 0);
          return;
        }
      }
      Vector3 drawPos = ((Thing) this).DrawPos;
      this.realPosition = new Vector2(drawPos.x, drawPos.z);
      if (Gen.IsHashIntervalTick((Thing) this, 15))
        this.DamageCloseThings();
      if (Rand.MTBEventOccurs(15f, 1f, 1f))
        this.DamageFarThings();
      if (Gen.IsHashIntervalTick((Thing) this, 20))
        this.DestroyRoofs();
      if (!Gen.IsHashIntervalTick((Thing) this, 4) || this.CellImmuneToDamage(((Thing) this).Position))
        return;
      float num = Rand.Range(0.6f, 1f);
      Vector3 vector3;
      // ISSUE: explicit constructor call
      vector3 = new Vector3(this.realPosition.x, 0.0f, this.realPosition.y);
      vector3.y = Altitudes.AltitudeFor((AltitudeLayer) 28);
      FleckMaker.ThrowTornadoDustPuff((vector3 + Vector3Utility.RandomHorizontalOffset(1.5f)), ((Thing) this).Map, Rand.Range(1.5f, 3f), new Color(num, num, num));
    }

    public void StartFadeOut()
    {
      if (this.leftFadeOutTicks >= 0)
        return;
      this.leftFadeOutTicks = 120;
      Messages.Message((Translator.Translate("MessageTornadoDissipated")), (new TargetInfo(((Thing) this).Position, ((Thing) this).Map, false)), MessageTypeDefOf.PositiveEvent, true);
    }

    protected virtual void DrawAt(Vector3 drawLoc, bool flip = false)
    {
      Rand.PushState();
      Rand.Seed = ((Thing) this).thingIDNumber;
      for (int index = 0; index < 180; ++index)
      {
        FloatRange distanceFromCenter = Mst_StaticTornado.PartsDistanceFromCenter;
        this.DrawTornadoPart(distanceFromCenter.RandomInRange, Rand.Range(0.0f, 360f), Rand.Range(0.9f, 1.1f), Rand.Range(0.52f, 0.88f));
      }
      Rand.PopState();
    }

    private void DrawTornadoPart(
      float distanceFromCenter,
      float initialAngle,
      float speedMultiplier,
      float colorMultiplier)
    {
      int ticksGame = Find.TickManager.TicksGame;
      float num1 = 1f / distanceFromCenter;
      float num2 = 25f * speedMultiplier * num1;
      float num3 = (float) (((double) initialAngle + (double) ticksGame * (double) num2) % 360.0);
      Vector2 vector2 = Vector2Utility.Moved(this.realPosition, num3, this.AdjustedDistanceFromCenter(distanceFromCenter));
      vector2.y += distanceFromCenter * 4f;
      vector2.y += Mst_StaticTornado.ZOffsetBias;
      Vector3 vector3_1;
      // ISSUE: explicit constructor call
      vector3_1 = new Vector3(vector2.x, Altitudes.AltitudeFor((AltitudeLayer) 31) + 0.03658537f * Rand.Range(0.0f, 1f), vector2.y);
      float num4 = distanceFromCenter * 3f;
      float num5 = 1f;
      if ((double) num3 > 270.0)
        num5 = GenMath.LerpDouble(270f, 360f, 0.0f, 1f, num3);
      else if ((double) num3 > 180.0)
        num5 = GenMath.LerpDouble(180f, 270f, 1f, 0.0f, num3);
      float num6 = Mathf.Min(distanceFromCenter / (Mst_StaticTornado.PartsDistanceFromCenter.max + 2f), 1f);
      float num7 = Mathf.InverseLerp(0.18f, 0.4f, num6);
      Vector3 vector3_2;
      // ISSUE: explicit constructor call
      vector3_2 = new Vector3(Mathf.Sin((float) ticksGame / 1000f + (float) (((Thing) this).thingIDNumber * 10)) * 2f, 0.0f, 0.0f);
      Vector3 vector3_3 = (vector3_1 + (vector3_2 * num7));
      float num8 = Mathf.Max(1f - num6, 0.0f) * num5 * this.FadeInOutFactor;
      Color color;
      // ISSUE: explicit constructor call
      color = new Color(colorMultiplier, colorMultiplier, colorMultiplier, num8);
      Mst_StaticTornado.matPropertyBlock.SetColor(ShaderPropertyIDs.Color, color);
      Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(vector3_3, Quaternion.Euler(0.0f, num3, 0.0f), new Vector3(num4, 1f, num4)), Mst_StaticTornado.TornadoMaterial, 0, (Camera) null, 0, Mst_StaticTornado.matPropertyBlock);
    }

    private float AdjustedDistanceFromCenter(float distanceFromCenter)
    {
      float num1 = Mathf.Min(distanceFromCenter / 8f, 1f);
      float num2 = num1 * num1;
      return distanceFromCenter * num2;
    }

    private void CreateSustainer()
    {
      LongEventHandler.ExecuteWhenFinished((Action) (() =>
      {
        this.sustainer = SoundStarter.TrySpawnSustainer(SoundDefOf.Tornado, SoundInfo.InMap(((Thing) this), (MaintenanceType) 1));
        this.UpdateSustainerVolume();
      }));
    }

    private void UpdateSustainerVolume() => this.sustainer.info.volumeFactor = this.FadeInOutFactor;

    private void DamageCloseThings()
    {
      int num = GenRadial.NumCellsInRadius(4.2f);
      for (int index = 0; index < num; ++index)
      {
        IntVec3 c = (((Thing) this).Position + GenRadial.RadialPattern[index]);
        if (GenGrid.InBounds(c, ((Thing) this).Map) && !this.CellImmuneToDamage(c))
        {
          Pawn firstPawn = GridsUtility.GetFirstPawn(c, ((Thing) this).Map);
          if (firstPawn == null || !firstPawn.Downed || !Rand.Bool)
          {
            float damageFactor = GenMath.LerpDouble(0.0f, 4.2f, 1f, 0.2f, IntVec3Utility.DistanceTo(c, ((Thing) this).Position));
            this.DoDamage(c, damageFactor);
          }
        }
      }
    }

    private void DamageFarThings()
    {
      this._farDamageCellScratch.Clear();
      foreach (IntVec3 intVec3 in GenRadial.RadialCellsAround(((Thing) this).Position, 10f, true))
      {
        if (GenGrid.InBounds(intVec3, ((Thing) this).Map))
          this._farDamageCellScratch.Add(intVec3);
      }
      if (this._farDamageCellScratch.Count == 0)
        return;
      IntVec3 c = GenCollection.RandomElement<IntVec3>((IEnumerable<IntVec3>) this._farDamageCellScratch);
      if (this.CellImmuneToDamage(c))
        return;
      this.DoDamage(c, 0.5f);
    }

    private void DestroyRoofs()
    {
      this.removedRoofsTmp.Clear();
      int num = GenRadial.NumCellsInRadius(4.2f);
      for (int index = 0; index < num; ++index)
      {
        IntVec3 c = (((Thing) this).Position + GenRadial.RadialPattern[index]);
        if (GenGrid.InBounds(c, ((Thing) this).Map) && !this.CellImmuneToDamage(c) && GridsUtility.Roofed(c, ((Thing) this).Map))
        {
          RoofDef roof = GridsUtility.GetRoof(c, ((Thing) this).Map);
          if (!roof.isThickRoof && !roof.isNatural)
          {
            RoofCollapserImmediate.DropRoofInCells(c, ((Thing) this).Map, (List<Thing>) null);
            this.removedRoofsTmp.Add(c);
          }
        }
      }
      if (this.removedRoofsTmp.Count <= 0)
        return;
      RoofCollapseCellsFinder.CheckCollapseFlyingRoofs(this.removedRoofsTmp, ((Thing) this).Map, true, false);
    }

    private bool CellImmuneToDamage(IntVec3 c)
    {
      if (GridsUtility.Roofed(c, ((Thing) this).Map) && GridsUtility.GetRoof(c, ((Thing) this).Map).isThickRoof)
        return true;
      Building edifice = GridsUtility.GetEdifice(c, ((Thing) this).Map);
      return edifice != null && ((Thing)edifice).def.category == ThingCategory.Building && (((Thing)edifice).def.building.isNaturalRock || ((Thing)edifice).def == ThingDefOf.Wall && ((Thing)edifice).Faction == null);
    }

    private void DoDamage(IntVec3 c, float damageFactor)
    {
      this.tmpThings.Clear();
      this.tmpThings.AddRange((IEnumerable<Thing>) GridsUtility.GetThingList(c, ((Thing) this).Map));
      float num1 = 0.0f;
      for (int index = 0; index < this.tmpThings.Count; ++index)
      {
        BattleLogEntry_DamageTaken entryDamageTaken = (BattleLogEntry_DamageTaken) null;
        switch (this.tmpThings[index].def.category)
        {
          case ThingCategory.Pawn:
            Pawn tmpThing = (Pawn)this.tmpThings[index];
            entryDamageTaken = new BattleLogEntry_DamageTaken(tmpThing, RulePackDefOf.DamageEvent_Tornado, (Pawn)null);
            Find.BattleLog.Add((LogEntry)entryDamageTaken);
            if ((double)tmpThing.RaceProps.baseHealthScale < 1.0)
              damageFactor *= tmpThing.RaceProps.baseHealthScale;
            if (tmpThing.RaceProps.Animal)
              damageFactor *= 0.75f;
            if (tmpThing.Downed)
              damageFactor *= 0.2f;
            break;
          case ThingCategory.Item:
            damageFactor *= 0.68f;
            break;
          case ThingCategory.Building:
            damageFactor *= 0.8f;
            break;
          case ThingCategory.Plant:
            damageFactor *= 1.7f;
            break;
        }
        int num2 = Mathf.Max(GenMath.RoundRandom(30f * damageFactor), 1);
        this.tmpThings[index].TakeDamage(new DamageInfo(DamageDefOf.TornadoScratch, (float) num2, 0.0f, num1, (Thing) this, (BodyPartRecord) null, (ThingDef) null, (DamageInfo.SourceCategory) 0, (Thing) null, true, true, (QualityCategory) 2, true, false)).AssociateWithLog((LogEntry_DamageResult) entryDamageTaken);
      }
      this.tmpThings.Clear();
    }
  }
}
