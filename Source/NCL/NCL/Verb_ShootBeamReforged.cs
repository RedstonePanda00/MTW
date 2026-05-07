using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace NCL
{
    // Token: 0x02000030 RID: 48
    public class Verb_ShootBeam : Verb
    {
        // Token: 0x17000016 RID: 22
        // (get) Token: 0x06000094 RID: 148 RVA: 0x00006144 File Offset: 0x00004344
        public float ShotProgress
        {
            get
            {
                return (float)this.ticksToNextPathStep / (float)base.TicksBetweenBurstShots;
            }
        }

        // Token: 0x17000017 RID: 23
        // (get) Token: 0x06000095 RID: 149 RVA: 0x00006155 File Offset: 0x00004355
        private int min
        {
            get
            {
                return Mathf.RoundToInt(this.verbProps.minRange);
            }
        }

        // Token: 0x17000018 RID: 24
        // (get) Token: 0x06000096 RID: 150 RVA: 0x00006167 File Offset: 0x00004367
        private DamageExtension modExtention
        {
            get
            {
                ThingWithComps equipmentSource = base.EquipmentSource;
                return (equipmentSource != null) ? equipmentSource.def.GetModExtension<DamageExtension>() : null;
            }
        }

        // Token: 0x17000019 RID: 25
        // (get) Token: 0x06000097 RID: 151 RVA: 0x00006180 File Offset: 0x00004380
        public Vector3 InterpolatedPosition
        {
            get
            {
                bool flag = this.currentTarget.HasThing && this.currentTarget.Thing.Map != this.caster.Map;
                Vector3 b;
                if (flag)
                {
                    b = this.LasttargetPosition - this.initialTargetPosition;
                }
                else
                {
                    this.LasttargetPosition = base.CurrentTarget.CenterVector3;
                    b = this.LasttargetPosition - this.initialTargetPosition;
                }
                return Vector3.Lerp(this.path[Mathf.Max(this.burstShotsLeft - this.min, 0)], this.path[Mathf.Min(Mathf.Max(this.burstShotsLeft + 1 - this.min, 1), this.path.Count - 1 - this.min)], this.ShotProgress) + b;
            }
        }

        // Token: 0x1700001A RID: 26
        // (get) Token: 0x06000098 RID: 152 RVA: 0x0000626C File Offset: 0x0000446C
        public override float? AimAngleOverride
        {
            get
            {
                bool flag = this.state != VerbState.Bursting;
                float? result;
                if (flag)
                {
                    result = null;
                }
                else
                {
                    result = new float?((this.InterpolatedPosition - this.caster.DrawPos).AngleFlat());
                }
                return result;
            }
        }

        public override void DrawHighlight(LocalTargetInfo target)
        {
            base.DrawHighlight(target);
            bool flag = target == null;
            if (!flag)
            {
                this.CalculatePath(target.CenterVector3, this.tmpPath, this.tmpPathCells, false);
                foreach (IntVec3 targetCell in this.tmpPathCells)
                {
                    ShootLine shootLine;
                    bool flag2 = base.TryFindShootLineFromTo(this.caster.Position, target, out shootLine, false);

                    // 修复：给 intVec 提供默认值
                    IntVec3 intVec = default(IntVec3); // 或者 new IntVec3()

                    bool flag3 = (this.verbProps.stopBurstWithoutLos && !flag2) || !this.TryGetHitCell(shootLine.Source, targetCell, out intVec);
                    if (!flag3)
                    {
                        foreach (IntVec3 item in GenRadial.RadialCellsAround(intVec, 2f, true).InRandomOrder(null))
                        {
                            this.tmpHighlightCells.Add(item);
                        }
                        bool flag4 = !this.verbProps.beamHitsNeighborCells;
                        if (!flag4)
                        {
                            foreach (IntVec3 intVec2 in this.GetBeamHitNeighbourCells(shootLine.Source, intVec))
                            {
                                bool flag5 = !this.tmpHighlightCells.Contains(intVec2);
                                if (flag5)
                                {
                                    foreach (IntVec3 item2 in GenRadial.RadialCellsAround(intVec2, 2f, true).InRandomOrder(null))
                                    {
                                        this.tmpSecondaryHighlightCells.Add(item2);
                                    }
                                }
                            }
                        }
                    }
                }
                this.tmpSecondaryHighlightCells.RemoveWhere((IntVec3 x) => this.tmpHighlightCells.Contains(x));
                bool flag6 = this.tmpHighlightCells.Any<IntVec3>();
                if (flag6)
                {
                    GenDraw.DrawFieldEdges(this.tmpHighlightCells.ToList<IntVec3>(), this.verbProps.highlightColor ?? Color.white, null, null, 2900);
                }
                bool flag7 = this.tmpSecondaryHighlightCells.Any<IntVec3>();
                if (flag7)
                {
                    GenDraw.DrawFieldEdges(this.tmpSecondaryHighlightCells.ToList<IntVec3>(), this.verbProps.secondaryHighlightColor ?? Color.white, null, null, 2900);
                }
                this.tmpHighlightCells.Clear();
                this.tmpSecondaryHighlightCells.Clear();
            }
        }


        // Token: 0x0600009A RID: 154 RVA: 0x000065D4 File Offset: 0x000047D4
        protected override bool TryCastShot()
        {
            ShootLine shootLine;
            bool flag = base.TryFindShootLineFromTo(this.caster.Position, this.currentTarget, out shootLine, false);
            bool flag2 = this.currentTarget.HasThing && this.currentTarget.Thing.Map != this.caster.Map;
            if (flag2)
            {
                shootLine = new ShootLine(this.caster.Position, this.LasttargetPosition.ToIntVec3());
            }
            bool flag3 = this.verbProps.stopBurstWithoutLos && !flag;
            bool result;
            if (flag3)
            {
                result = false;
            }
            else
            {
                bool flag4 = base.EquipmentSource != null;
                if (flag4)
                {
                    CompChangeableProjectile comp = base.EquipmentSource.GetComp<CompChangeableProjectile>();
                    if (comp != null)
                    {
                        comp.Notify_ProjectileLaunched();
                    }
                    CompApparelReloadable comp2 = base.EquipmentSource.GetComp<CompApparelReloadable>();
                    if (comp2 != null)
                    {
                        comp2.UsedOnce();
                    }
                }
                this.lastShotTick = Find.TickManager.TicksGame;
                this.ticksToNextPathStep = base.TicksBetweenBurstShots;
                IntVec3 targetCell = this.InterpolatedPosition.Yto0().ToIntVec3();
                IntVec3 intVec;
                bool flag5 = !this.TryGetHitCell(shootLine.Source, targetCell, out intVec);
                if (flag5)
                {
                    result = true;
                }
                else
                {
                    this.HitCell(intVec, shootLine.Source, 1f);
                    bool beamHitsNeighborCells = this.verbProps.beamHitsNeighborCells;
                    if (beamHitsNeighborCells)
                    {
                        this.hitCells.Add(intVec);
                        foreach (IntVec3 intVec2 in this.GetBeamHitNeighbourCells(shootLine.Source, intVec))
                        {
                            bool flag6 = !this.hitCells.Contains(intVec2);
                            if (flag6)
                            {
                                float damageFactor = this.pathCells.Contains(intVec2) ? 1f : 0.5f;
                                this.HitCell(intVec2, shootLine.Source, damageFactor);
                                this.hitCells.Add(intVec2);
                            }
                        }
                    }
                    result = true;
                }
            }
            return result;
        }

        // Token: 0x0600009B RID: 155 RVA: 0x000067E4 File Offset: 0x000049E4
        protected bool TryGetHitCell(IntVec3 source, IntVec3 targetCell, out IntVec3 hitCell)
        {
            IntVec3 intVec = GenSight.LastPointOnLineOfSight(source, targetCell, (IntVec3 c) => c.InBounds(this.caster.Map) && c.CanBeSeenOverFast(this.caster.Map), true);
            bool flag = this.verbProps.beamCantHitWithinMinRange && intVec.DistanceTo(source) < this.verbProps.minRange;
            bool result;
            if (flag)
            {
                hitCell = default(IntVec3);
                result = false;
            }
            else
            {
                hitCell = (intVec.IsValid ? intVec : targetCell);
                result = intVec.IsValid;
            }
            return result;
        }

        // Token: 0x0600009C RID: 156 RVA: 0x0000685C File Offset: 0x00004A5C
        protected IntVec3 GetHitCell(IntVec3 source, IntVec3 targetCell)
        {
            IntVec3 result;
            this.TryGetHitCell(source, targetCell, out result);
            return result;
        }

        // Token: 0x0600009D RID: 157 RVA: 0x0000687A File Offset: 0x00004A7A
        protected IEnumerable<IntVec3> GetBeamHitNeighbourCells(IntVec3 source, IntVec3 pos)
        {
            bool flag = !this.verbProps.beamHitsNeighborCells;
            if (flag)
            {
                yield break;
            }
            int num;
            for (int i = 0; i < 4; i = num + 1)
            {
                IntVec3 intVec = pos + GenAdj.CardinalDirections[i];
                bool flag2 = intVec.InBounds(this.Caster.Map) && (!this.verbProps.beamHitsNeighborCellsRequiresLOS || GenSight.LineOfSight(source, intVec, this.caster.Map));
                if (flag2)
                {
                    yield return intVec;
                }
                intVec = default(IntVec3);
                num = i;
            }
            yield break;
        }

        // Token: 0x0600009E RID: 158 RVA: 0x00006898 File Offset: 0x00004A98
        public override bool TryStartCastOn(LocalTargetInfo castTarg, LocalTargetInfo destTarg, bool surpriseAttack = false, bool canHitNonTargetPawns = true, bool preventFriendlyFire = false, bool nonInterruptingSelfCast = false)
        {
            return base.TryStartCastOn(this.verbProps.beamTargetsGround ? castTarg.Cell : castTarg, destTarg, surpriseAttack, canHitNonTargetPawns, preventFriendlyFire, nonInterruptingSelfCast);
        }

        // Token: 0x0600009F RID: 159 RVA: 0x000068D4 File Offset: 0x00004AD4
        public override void BurstingTick()
        {
            this.ticksToNextPathStep--;
            Vector3 vector = this.InterpolatedPosition;
            IntVec3 intVec = vector.ToIntVec3();
            Vector3 vector2 = this.InterpolatedPosition - this.caster.Position.ToVector3Shifted();
            float num = vector2.MagnitudeHorizontal();
            Vector3 normalized = vector2.Yto0().normalized;
            IntVec3 b = GenSight.LastPointOnLineOfSight(this.caster.Position, intVec, (IntVec3 c) => c.CanBeSeenOverFast(this.caster.Map), true);
            bool isValid = b.IsValid;
            if (isValid)
            {
                num -= (intVec - b).LengthHorizontal;
                vector = this.caster.Position.ToVector3Shifted() + normalized * num;
                intVec = vector.ToIntVec3();
            }
            Vector3 offsetA = normalized * this.verbProps.beamStartOffset;
            Vector3 vector3 = vector - intVec.ToVector3Shifted();
            bool flag = this.mote != null;
            if (flag)
            {
                this.mote.UpdateTargets(new TargetInfo(this.caster.Position, this.caster.Map, false), new TargetInfo(intVec, this.caster.Map, false), offsetA, vector3);
                this.mote.Maintain();
            }
            bool flag2 = this.caster.IsHashIntervalTick(base.TicksBetweenBurstShots) && base.EquipmentSource != null;
            if (flag2)
            {
                bool flag3 = this.modExtention != null && this.modExtention.circleMote != null;
                if (flag3)
                {
                    float rotation = normalized.AngleFlat();
                    FleckCreationData dataStatic = FleckMaker.GetDataStatic(vector, this.caster.Map, this.modExtention.circleMote, 1f);
                    dataStatic.rotation = rotation;
                    this.caster.Map.flecks.CreateFleck(dataStatic);
                }
            }
            bool flag4 = this.verbProps.beamGroundFleckDef != null && Rand.Chance(this.verbProps.beamFleckChancePerTick);
            if (flag4)
            {
                FleckMaker.Static(vector, this.caster.Map, this.verbProps.beamGroundFleckDef, 1f);
            }
            bool flag5 = this.endEffecter == null && this.verbProps.beamEndEffecterDef != null;
            if (flag5)
            {
                this.endEffecter = this.verbProps.beamEndEffecterDef.Spawn(intVec, this.caster.Map, vector3, 1f);
            }
            bool flag6 = this.endEffecter != null;
            if (flag6)
            {
                this.endEffecter.offset = vector3;
                this.endEffecter.EffectTick(new TargetInfo(intVec, this.caster.Map, false), TargetInfo.Invalid);
                this.endEffecter.ticksLeft--;
            }
            bool flag7 = this.verbProps.beamLineFleckDef != null;
            if (flag7)
            {
                float num2 = 1f * num;
                int num3 = 0;
                while ((float)num3 < num2)
                {
                    bool flag8 = Rand.Chance(this.verbProps.beamLineFleckChanceCurve.Evaluate((float)num3 / num2));
                    if (flag8)
                    {
                        Vector3 b2 = (float)num3 * normalized - normalized * Rand.Value + normalized / 2f;
                        FleckMaker.Static(this.caster.Position.ToVector3Shifted() + b2, this.caster.Map, this.verbProps.beamLineFleckDef, 1f);
                    }
                    num3++;
                }
            }
            Sustainer sustainer = this.sustainer;
            if (sustainer != null)
            {
                sustainer.Maintain();
            }
        }

        // Token: 0x060000A0 RID: 160 RVA: 0x00006C80 File Offset: 0x00004E80
        public override void WarmupComplete()
        {
            this.state = VerbState.Bursting;
            this.initialTargetPosition = this.currentTarget.CenterVector3;
            this.CalculatePath(this.currentTarget.CenterVector3, this.path, this.pathCells, true);
            this.burstShotsLeft = this.path.Count - 1;
            this.hitCells.Clear();
            bool flag = this.verbProps.beamMoteDef != null;
            if (flag)
            {
                this.mote = MoteMaker.MakeInteractionOverlay(this.verbProps.beamMoteDef, this.caster, new TargetInfo(this.path[0].ToIntVec3(), this.caster.Map, false));
            }
            base.TryCastNextBurstShot();
            this.ticksToNextPathStep = base.TicksBetweenBurstShots;
            Effecter effecter = this.endEffecter;
            if (effecter != null)
            {
                effecter.Cleanup();
            }
            bool flag2 = this.verbProps.soundCastBeam != null;
            if (flag2)
            {
                this.sustainer = this.verbProps.soundCastBeam.TrySpawnSustainer(SoundInfo.InMap(this.caster, MaintenanceType.PerTick));
            }
        }

        // Token: 0x060000A1 RID: 161 RVA: 0x00006D9C File Offset: 0x00004F9C
        private void CalculatePath(Vector3 target, List<Vector3> pathList, HashSet<IntVec3> pathCellsList, bool addRandomOffset = true)
        {
            pathList.Clear();
            IntVec3 intVec = target.ToIntVec3();
            float lengthHorizontal = (intVec - this.caster.Position).LengthHorizontal;
            float num = (float)(intVec.x - this.caster.Position.x) / lengthHorizontal;
            float num2 = (float)(intVec.z - this.caster.Position.z) / lengthHorizontal;
            intVec.x = Mathf.RoundToInt((float)this.caster.Position.x + num * this.verbProps.range);
            intVec.z = Mathf.RoundToInt((float)this.caster.Position.z + num2 * this.verbProps.range);
            List<IntVec3> list = GenSight.BresenhamCellsBetween(this.caster.Position, intVec);
            for (int i = 0; i < list.Count; i++)
            {
                IntVec3 c = list[i];
                bool flag = c.InBounds(this.Caster.Map);
                if (flag)
                {
                    pathList.Add(c.ToVector3Shifted());
                }
            }
            pathCellsList.Clear();
            foreach (Vector3 vect in pathList)
            {
                pathCellsList.Add(vect.ToIntVec3());
            }
            pathList.Reverse();
            pathCellsList.Reverse<IntVec3>();
        }

        // Token: 0x060000A2 RID: 162 RVA: 0x00006F28 File Offset: 0x00005128
        private bool CanHit(Thing thing)
        {
            bool flag = !thing.Spawned;
            return !flag && !CoverUtility.ThingCovered(thing, this.caster.Map);
        }

        // Token: 0x060000A3 RID: 163 RVA: 0x00006F60 File Offset: 0x00005160
        private void HitCell(IntVec3 cell, IntVec3 sourceCell, float damageFactor = 1f)
        {
            bool flag = cell.InBounds(this.caster.Map);
            if (flag)
            {
                foreach (IntVec3 intVec in GenRadial.RadialCellsAround(cell, 2f, true).InRandomOrder(null))
                {
                    bool flag2 = intVec.InBounds(this.caster.Map);
                    if (flag2)
                    {
                        this.ApplyDamage(VerbUtility.ThingsToHit(intVec, this.caster.Map, new Func<Thing, bool>(this.CanHit)).RandomElementWithFallback(null), sourceCell, damageFactor);
                    }
                }
                bool flag3 = this.verbProps.beamSetsGroundOnFire && Rand.Chance(this.verbProps.beamChanceToStartFire);
                if (flag3)
                {
                    FireUtility.TryStartFireIn(cell, this.caster.Map, 1f, this.caster, null);
                }
            }
        }

        // Token: 0x060000A4 RID: 164 RVA: 0x0000705C File Offset: 0x0000525C
        private void ApplyDamage(Thing thing, IntVec3 sourceCell, float damageFactor = 1f)
        {
            IntVec3 intVec = this.InterpolatedPosition.Yto0().ToIntVec3();
            IntVec3 intVec2 = GenSight.LastPointOnLineOfSight(sourceCell, intVec, (IntVec3 c) => c.InBounds(this.caster.Map) && c.CanBeSeenOverFast(this.caster.Map), true);
            bool isValid = intVec2.IsValid;
            if (isValid)
            {
                intVec = intVec2;
            }
            Map map = this.caster.Map;
            bool flag = thing == null || this.verbProps.beamDamageDef == null;
            if (!flag)
            {
                float angleFlat = (this.currentTarget.Cell - this.caster.Position).AngleFlat;
                BattleLogEntry_RangedImpact log = new BattleLogEntry_RangedImpact(this.caster, thing, this.currentTarget.Thing, base.EquipmentSource.def, null, null);
                bool flag2 = this.verbProps.beamTotalDamage > 0f;
                DamageInfo dinfo;
                if (flag2)
                {
                    float num = this.verbProps.beamTotalDamage / (float)this.pathCells.Count;
                    num *= damageFactor;
                    dinfo = new DamageInfo(this.verbProps.beamDamageDef, num, this.verbProps.beamDamageDef.defaultArmorPenetration, angleFlat, this.caster, null, base.EquipmentSource.def, DamageInfo.SourceCategory.ThingOrUnknown, this.currentTarget.Thing, true, true, QualityCategory.Normal, true, false);
                }
                else
                {
                    float amount = (float)this.verbProps.beamDamageDef.defaultDamage * damageFactor;
                    dinfo = new DamageInfo(this.verbProps.beamDamageDef, amount, this.verbProps.beamDamageDef.defaultArmorPenetration, angleFlat, this.caster, null, base.EquipmentSource.def, DamageInfo.SourceCategory.ThingOrUnknown, this.currentTarget.Thing, true, true, QualityCategory.Normal, true, false);
                }
                thing.TakeDamage(dinfo).AssociateWithLog(log);
                bool flag3 = thing.CanEverAttachFire();
                if (flag3)
                {
                    float chance = (this.verbProps.flammabilityAttachFireChanceCurve == null) ? this.verbProps.beamChanceToAttachFire : this.verbProps.flammabilityAttachFireChanceCurve.Evaluate(thing.GetStatValue(StatDefOf.Flammability, true, -1));
                    bool flag4 = Rand.Chance(chance);
                    if (flag4)
                    {
                        thing.TryAttachFire(this.verbProps.beamFireSizeRange.RandomInRange, this.caster);
                    }
                }
                else
                {
                    bool flag5 = Rand.Chance(this.verbProps.beamChanceToStartFire);
                    if (flag5)
                    {
                        FireUtility.TryStartFireIn(intVec, map, this.verbProps.beamFireSizeRange.RandomInRange, this.caster, this.verbProps.flammabilityAttachFireChanceCurve);
                    }
                }
            }
        }

        // Token: 0x060000A5 RID: 165 RVA: 0x000072C4 File Offset: 0x000054C4
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look<Vector3>(ref this.path, "path", LookMode.Value, Array.Empty<object>());
            Scribe_Values.Look<int>(ref this.ticksToNextPathStep, "ticksToNextPathStep", 0, false);
            Scribe_Values.Look<Vector3>(ref this.initialTargetPosition, "initialTargetPosition", default(Vector3), false);
            bool flag = Scribe.mode == LoadSaveMode.PostLoadInit && this.path == null;
            if (flag)
            {
                this.path = new List<Vector3>();
            }
        }

        // Token: 0x0400003F RID: 63
        private List<Vector3> path = new List<Vector3>();

        // Token: 0x04000040 RID: 64
        private List<Vector3> tmpPath = new List<Vector3>();

        // Token: 0x04000041 RID: 65
        private int ticksToNextPathStep;

        // Token: 0x04000042 RID: 66
        private Vector3 initialTargetPosition;

        // Token: 0x04000043 RID: 67
        private Vector3 LasttargetPosition;

        // Token: 0x04000044 RID: 68
        private MoteDualAttached mote;

        // Token: 0x04000045 RID: 69
        private Effecter endEffecter;

        // Token: 0x04000046 RID: 70
        private Sustainer sustainer;

        // Token: 0x04000047 RID: 71
        private HashSet<IntVec3> pathCells = new HashSet<IntVec3>();

        // Token: 0x04000048 RID: 72
        private HashSet<IntVec3> tmpPathCells = new HashSet<IntVec3>();

        // Token: 0x04000049 RID: 73
        private HashSet<IntVec3> tmpHighlightCells = new HashSet<IntVec3>();

        // Token: 0x0400004A RID: 74
        private HashSet<IntVec3> tmpSecondaryHighlightCells = new HashSet<IntVec3>();

        // Token: 0x0400004B RID: 75
        private HashSet<IntVec3> hitCells = new HashSet<IntVec3>();

        // Token: 0x0400004C RID: 76
        private const int NumSubdivisionsPerUnitLength = 1;
    }
}


namespace NCL
{
	// Token: 0x0200000F RID: 15
	public class DamageExtension : DefModExtension
	{
		// Token: 0x04000011 RID: 17
		public FloatRange pushBackDistance;

		// Token: 0x04000012 RID: 18
		public SoundDef soundOnDamage;

		// Token: 0x04000013 RID: 19
		public FleckDef fleckOnDamage;

		// Token: 0x04000014 RID: 20
		public FleckDef circleMote;

		// Token: 0x04000015 RID: 21
		public bool fleckOnInstigator;

		// Token: 0x04000016 RID: 22
		public int delayTicks;
	}
}
