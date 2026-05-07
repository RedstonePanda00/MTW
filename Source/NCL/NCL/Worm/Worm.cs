using RimWorld;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Security.Cryptography;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Noise;
using Verse.Sound;
using static UnityEngine.GraphicsBuffer;

namespace NCL.Worm
{
    [DefOf]
    public static class NCLWormDefOf
    {
        public static ThingDef NCLJumpWithBomb_Flyer;
        public static ThingDef NCLBurrow_Flyer;
        public static ResearchProjectDef NCL_Archoworm;
        public static FactionDef NCL_factionEnemy;
        public static GameConditionDef NCL_WaitWorm;
        public static GameConditionDef NCL_WaitWormFight;
        public static FleckDef NCL_Fleck_BurnerUsedEmber; 
        public static FleckDef NCL_Fleck_BurnerUsedEmberBlack;
        public static FleckDef Fleck_BeamBurn; 
        public static EffecterDef MechBandElectricityArc;
        public static FleckDef Fleck_NCLStar;
        public static FleckDef Fleck_NCLStarFire;
        public static SoundDef Explosion_MechBandShockwave;
        public static SoundDef NCLLaserWarmup;



        public static ThingDef Mote_NCLWormLaser;
    }//DefOf
    [StaticConstructorOnStartup]
    public static class NCLWormTexCommand
    {
        public static readonly Texture2D NCLCourier = ContentFinder<Texture2D>.Get("UI/Misc/NCLCourier");
        public static readonly Texture2D NeedUnitDividerTex = ContentFinder<Texture2D>.Get("UI/Misc/NeedUnitDivider");
        public static readonly Texture2D ShieldGzimoBase = ContentFinder<Texture2D>.Get("UI/Misc/ShieldGzimoBase");
        public static readonly Texture2D WindowBase = ContentFinder<Texture2D>.Get("UI/Misc/WindowBase");
        //public static readonly Material BubbleMat = MaterialPool.MatFrom("Other/ShieldBubble", ShaderDatabase.Transparent);
    }//????
    public class NCL_Pawn_Worm : Pawn
    {
        public List<DamageDefWithTick> damageAbsLi = new List<DamageDefWithTick>();
        public long lastApplyDamagetick = Find.TickManager.gameStartAbsTick;
        public bool Sleep = false;
        public float LADHP = 1f;
        public float LADSH = 1f;
        protected override void Tick()
        {
            base.Tick();
            for (int i = damageAbsLi.Count - 1; i >= 0; i--)
            {
                damageAbsLi[i].tick--;
                if (damageAbsLi[i].tick <= 0)
                {
                    damageAbsLi.Remove(damageAbsLi[i]);
                }
            }//??????????
            for (int i = health.hediffSet.hediffs.Count - 1; i >= 0; i--)
            {
                if (health.hediffSet.hediffs[i].def.isBad && !(health.hediffSet.hediffs[i] is Hediff_Injury))
                {
                    health.RemoveHediff(health.hediffSet.hediffs[i]);
                }
            }//?????????

            //GetBodyPoi();
        }//?????????
        public override void PreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            base.PreApplyDamage(ref dinfo, out absorbed);
            if (dinfo.Instigator == this || Sleep)
            {
                absorbed = true;
                return;
            }
            {
                foreach (DamageDefWithTick item in damageAbsLi)
                {
                    if (item.damageDef == dinfo.Def)
                    {
                        absorbed = true;
                        return;
                    }
                }
                damageAbsLi.Add(new DamageDefWithTick(dinfo.Def, 60));
            }//??1???????????,??,???





        }//?????????
        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Collections.Look(ref damageAbsLi, "damageAbsLi", LookMode.Deep);
        }


        #region ?????
        /*
        public List<Vector3> BodyPoi = new List<Vector3>();
        public int BodyCount = 7;

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            base.DrawAt(drawLoc, flip);
            if (BodyPoi.Count < BodyCount)
            {
                GetBodyPoi();
            }
            for (int i = 0; i < BodyCount; i++)
            {
                if (i == 0)
                {
                    Graphic graphic = def.graphicData.GraphicColoredFor(this);
                    graphic.Draw(BodyPoi[i], flip ? Rotation.Opposite : Rotation, this);
                }
                else if (i == BodyCount - 1)
                {
                    Graphic graphic = def.graphicData.GraphicColoredFor(this);
                    graphic.Draw(BodyPoi[i], flip ? Rotation.Opposite : Rotation, this);
                }
                else
                {
                    Graphic graphic = def.graphicData.GraphicColoredFor(this);
                    graphic.Draw(BodyPoi[i], flip ? Rotation.Opposite : Rotation, this);
                }
            }
        }
        public void GetBodyPoi()
        {
            if(!Spawned)
            {
                return;
            }
            if (BodyPoi.Count<BodyCount)
            {
                for (int i = BodyPoi.Count; i < BodyCount; i++)
                {
                    BodyPoi.Add(this.DrawPos);
                }
            }
            if (this.IsHashIntervalTick(10))
            {
                for (int i = BodyCount-1; i > 0; i--)
                {
                    BodyPoi[i]= BodyPoi[i-1];
                }
                BodyPoi[0] = DrawPos;
                for (int i = 1;i < BodyCount; i++)
                {
                    Log.Warning("?"+i+"????:"+BodyPoi[i-1].ToString());
                }
            }

        }
        */
        #endregion





    }//???
    public class CompProperties_ShieldNCLWorm : CompProperties
    {
        public int RestTime = 60;
        public float minDrawSize = 1.2f;
        public float maxDrawSize = 1.55f;
        public float EnergyShieldEnergyMax = 100f;
        public float EnergyShieldRechargeRate = 1f;
        public float energyOnReset = 1f;

        public CompProperties_ShieldNCLWorm() => compClass = typeof(CompShieldNCLWorm);
    }//??
    public class CompShieldNCLWorm : ThingComp
    {
        private float power = 999;//??
        public int ticksToReset = -1;//??????
        private int lastKeepDisplayTick = -9999;//??????????
        private int lastBombTick = -9999;
        protected int StartingTicksToReset => Props.RestTime * 60;//?????

        protected Vector3 impactAngleVect;
        public CompProperties_ShieldNCLWorm Props => (CompProperties_ShieldNCLWorm)props;
        public void KeepDisplaying() => lastKeepDisplayTick = Find.TickManager.TicksGame;
        public float MaxPower//????
        {
            get
            {
                return Props.EnergyShieldEnergyMax;
            }
        }
        public float RePowerRate//???
        {
            get
            {
                return Props.EnergyShieldRechargeRate / 60;
            }
        }
        public float Power//????
        {
            get
            {
                return power;
            }
            set { power = value; }
        }
        public ShieldState shieldState//??????
        {
            get
            {
                if (PawnOwner.IsCharging() || PawnOwner.IsSelfShutdown())
                {
                    return ShieldState.Disabled;
                }
                if (ticksToReset > 0)
                {
                    return ShieldState.Resetting;
                }
                return ShieldState.Active;
            }
        }
        protected bool ShouldDisplay
        {
            get
            {
                if (!PawnOwner.Spawned || PawnOwner.Dead || PawnOwner.Downed)
                {
                    return false;
                }

                if (PawnOwner.InAggroMentalState)
                {
                    return true;
                }

                if (PawnOwner.Drafted)
                {
                    return true;
                }

                if (PawnOwner.Faction.HostileTo(Faction.OfPlayer) && !PawnOwner.IsPrisoner)
                {
                    return true;
                }

                if (Find.TickManager.TicksGame < lastKeepDisplayTick + 300)
                {
                    return true;
                }

                return false;
            }
        }
        protected Pawn PawnOwner
        {
            get
            {
                return (Pawn)parent;
            }
        }
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref power, "power", 0f, false);
            Scribe_Values.Look(ref ticksToReset, "Sleeptick", -1, false);
            Scribe_Values.Look(ref lastBombTick, "lastBombTick", -1, false);
            Scribe_Values.Look(ref lastKeepDisplayTick, "lastKeepDisplayTick", 0, false);
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo item in base.CompGetGizmosExtra())
            {
                yield return item;
            }
            Gizmo_MechNCLWorm_ShieldStatus gizmo_EnergyShieldStatus = new Gizmo_MechNCLWorm_ShieldStatus();
            gizmo_EnergyShieldStatus.shield = this;
            yield return gizmo_EnergyShieldStatus;
        }
        public override void CompTick()
        {
            base.CompTick();
            if (PawnOwner == null)
            {
                power = 0f;
            }
            else if (shieldState == ShieldState.Resetting)
            {
                ticksToReset--;
                if (ticksToReset <= 0)
                {
                    Reset();
                }
            }
            else if (shieldState == ShieldState.Active)
            {
                power += RePowerRate;
                if (power > MaxPower)
                {
                    power = MaxPower;
                }
            }
        }
        private void AbsorbedDamage(DamageInfo dinfo)
        {
            if (PawnOwner.Map != null)
            {
                SoundDefOf.EnergyShield_AbsorbDamage.PlayOneShot(new TargetInfo(PawnOwner.Position, PawnOwner.Map));
                impactAngleVect = Vector3Utility.HorizontalVectorFromAngle(dinfo.Angle);
                Vector3 loc = PawnOwner.TrueCenter() + impactAngleVect.RotatedBy(180f) * 0.5f;
                float num = Mathf.Min(10f, 2f + dinfo.Amount / 10f);
                FleckMaker.Static(loc, PawnOwner.Map, FleckDefOf.ExplosionFlash, num);
                int num2 = (int)num;
                for (int i = 0; i < num2; i++)
                {
                    FleckMaker.ThrowDustPuff(loc, PawnOwner.Map, Rand.Range(0.8f, 1.2f));
                }
            }
            KeepDisplaying();
        }
        public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            absorbed = false;
            if (shieldState == ShieldState.Resetting || PawnOwner == null) { return; }//?????

            if (!dinfo.Def.harmsHealth) { absorbed = true; return; }//??EMP
            if (dinfo.Instigator == PawnOwner) { AbsorbedDamage(dinfo); absorbed = true; return; }//??????
            else
            {
                AbsorbedDamage(dinfo);
                float num = power / MaxPower;
                power -= dinfo.Amount;
                if ((num >= 0.7f && (power / MaxPower) <= 0.7f) ||
                    (num >= 0.4f && (power / MaxPower) <= 0.4f) ||
                    (num >= 0.1f && (power / MaxPower) <= 0.1f))
                {
                    if ((Find.TickManager.TicksGame - lastBombTick) >= 6000)
                    {
                        GenExplosion.DoExplosion(parent.Position, parent.Map, 8f, DamageDefOf.Flame, parent, 10, 0.15f, null, null, null, null, null, 0, 1, null, null, 255, false, ThingDefOf.Filth_Fuel, 1, 1, 1);
                        GenExplosion.DoExplosion(parent.Position, parent.Map, 8f, DamageDefOf.EMP, parent, 100);
                        lastBombTick = Find.TickManager.TicksGame;
                    }
                }
                if (power < 0f)
                {
                    Break();
                }
                absorbed = true;
            }
        }
        private void Break()
        {
            if (parent.Spawned)
            {
                float scale = Mathf.Lerp(Props.minDrawSize, Props.maxDrawSize, power);
                EffecterDefOf.Shield_Break.SpawnAttached(parent, parent.MapHeld, scale);
                FleckMaker.Static(PawnOwner.TrueCenter(), PawnOwner.Map, FleckDefOf.ExplosionFlash, 12f);
                for (int i = 0; i < 6; i++)
                {
                    FleckMaker.ThrowDustPuff(PawnOwner.TrueCenter() + Vector3Utility.HorizontalVectorFromAngle(Rand.Range(0, 360)) * Rand.Range(0.3f, 0.6f), PawnOwner.Map, Rand.Range(0.8f, 1.2f));
                }
            }
            power = 0f;
            ticksToReset = StartingTicksToReset;
        }
        public void Reset()
        {
            if (PawnOwner.Spawned)
            {
                SoundDefOf.EnergyShield_Reset.PlayOneShot(new TargetInfo(PawnOwner.Position, PawnOwner.Map));
                FleckMaker.ThrowLightningGlow(PawnOwner.TrueCenter(), PawnOwner.Map, 3f);
            }

            ticksToReset = -1;
            power = Props.energyOnReset*Props.EnergyShieldEnergyMax;
        }
        /*public override void PostDraw()
        {
            base.PostDraw();
            if (shieldState == ShieldState.Active && ShouldDisplay)
            {
                float num = Mathf.Lerp(Props.minDrawSize, Props.maxDrawSize, power);
                Vector3 drawPos = PawnOwner.Drawer.DrawPos;
                drawPos.y = AltitudeLayer.MoteOverhead.AltitudeFor();
                int num2 = Find.TickManager.TicksGame - lastAbsorbDamageTick;
                if (num2 < 8)
                {
                    float num3 = (float)(8 - num2) / 8f * 0.05f;
                    drawPos += impactAngleVect * num3;
                    num -= num3;
                }

                float angle = Rand.Range(0, 360);
                Vector3 s = new Vector3(num, 1f, num);
                Matrix4x4 matrix = default(Matrix4x4);
                matrix.SetTRS(drawPos, Quaternion.AngleAxis(angle, Vector3.up), s);
                Graphics.DrawMesh(MeshPool.plane10, matrix, NCLTexture2D.BubbleMat, 0);
            }
        }*/
    }
    public class CompProperties_DamageAbsorbedAndBoom : CompProperties
    {
        public int AbsorbedTime = 24; // ????
        public int BoomTime = 60;     // ????
        public int radius = 6;        // ????

        // ??????????
        public string damageDefName = "Bomb"; // ??????

        public int amount = 80;           // ????
        public float armorPenetration = 1f; // ????

        // ?????????????
        [Unsaved]
        public DamageDef damageDef;

        public CompProperties_DamageAbsorbedAndBoom()
        {
            compClass = typeof(Comp_DamageAbsorbedAndBoom);
        }
    }//?????
    public class Comp_DamageAbsorbedAndBoom : ThingComp
    {

        public int absorbedTime = 24;
        public int boomTime = 60;
        public CompProperties_DamageAbsorbedAndBoom Props => (CompProperties_DamageAbsorbedAndBoom)props;
        public override void CompTick()
        {
            base.CompTick();
            if (absorbedTime > 0)
            {
                absorbedTime--;
            }
            if (boomTime > 0)
            {
                boomTime--;
            }
        }
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref absorbedTime, "absorbedTime");
            Scribe_Values.Look(ref boomTime, "boomTime");
        }
        public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            base.PostPreApplyDamage(ref dinfo, out absorbed);
            if (absorbedTime <= 0)
            {
                absorbed = true;
                absorbedTime = 24;
            }
            if (boomTime <= 0)
            {
                GenExplosion.DoExplosion(parent.Position, parent.Map, Props.radius, Props.damageDef, parent, Props.amount, Props.armorPenetration, null, null, null, null, null, 0, 1, null, null, 255, false, null, 0, 1, 0, false, null, new List<Thing> { parent });
                boomTime = 60;
            }
        }

    }
    public class CompProperties_BattleCrush : CompProperties
    {
        public EffecterDef battleEffect;     // ??????
        public int damageInterval = 60;      // ????(ticks)
        public float enemyScanRadius = 50f;  // ??????(?)
        public int damageRadius = 2;         // ????(??),5x5?? = ??2?
        public float damageFactor = 0.1f;    // ????(20%)

        public CompProperties_BattleCrush()
        {
            compClass = typeof(Comp_BattleCrush);
        }
    }

    // ????????
    public class Comp_BattleCrush : ThingComp
    {
        private CompProperties_BattleCrush Props => (CompProperties_BattleCrush)props;

        // ????
        private Effecter effecter;
        private bool isInCombat;             // ??????
        private bool wasInCombatLastTick;    // ??tick?????

        // ???
        private int combatCheckCooldown;
        private int damageCooldown;

        // ??
        private const int CombatCheckInterval = 240; // ?30ticks????????

        public override void CompTick()
        {
            base.CompTick();

            Pawn pawn = parent as Pawn;
            if (pawn == null || !pawn.Spawned || pawn.Dead || pawn.Downed)
            {
                ClearCombatState();
                return;
            }

            // ??????????
            combatCheckCooldown--;
            if (combatCheckCooldown <= 0)
            {
                combatCheckCooldown = CombatCheckInterval;
                isInCombat = CheckCombatState(pawn);
            }

            // ????????
            HandleCombatStateChange(pawn);

            // ??????????
            if (isInCombat)
            {
                damageCooldown--;
                if (damageCooldown <= 0)
                {
                    damageCooldown = Props.damageInterval;
                    ApplyAreaDamage(pawn);
                }
            }
        }

        // ??????
        private bool CheckCombatState(Pawn pawn)
        {
            Map map = pawn.Map;
            if (map == null) return false;

            // ????(?????)
            float cellRadius = Props.enemyScanRadius / 1.5f;
            float cellRadiusSq = cellRadius * cellRadius;

            // ???????????
            foreach (Pawn otherPawn in map.mapPawns.AllPawnsSpawned)
            {
                if (otherPawn == pawn || otherPawn.Dead || otherPawn.Downed)
                    continue;

                // ???????????
                if (otherPawn.HostileTo(pawn) && !otherPawn.IsPrisoner)
                {
                    // ??????????
                    float distSq = otherPawn.Position.DistanceToSquared(pawn.Position);
                    if (distSq <= cellRadiusSq)
                    {
                        return true; // ??????
                    }
                }
            }
            return false;
        }

        // ????????
        private void HandleCombatStateChange(Pawn pawn)
        {
            // ????????
            if (isInCombat && !wasInCombatLastTick)
            {
                CreateEffecter(pawn);
            }
            // ??????
            else if (!isInCombat && wasInCombatLastTick)
            {
                CleanupEffecter();
            }

            // ??????
            if (effecter != null)
            {
                effecter.EffectTick(new TargetInfo(pawn.Position, pawn.Map), TargetInfo.Invalid);
            }

            // ??????
            wasInCombatLastTick = isInCombat;
        }

        // ????
        private void CreateEffecter(Pawn pawn)
        {
            if (Props.battleEffect != null)
            {
                effecter = Props.battleEffect.Spawn();
                effecter.Trigger(new TargetInfo(pawn.Position, pawn.Map), TargetInfo.Invalid);
            }
        }

        // ????
        private void CleanupEffecter()
        {
            if (effecter != null)
            {
                effecter.Cleanup();
                effecter = null;
            }
        }

        // ??????
        private void ClearCombatState()
        {
            if (isInCombat)
            {
                isInCombat = false;
                CleanupEffecter();
            }
        }

        // ??????
        private void ApplyAreaDamage(Pawn attacker)
        {
            // ??????
            if (effecter != null)
            {
                effecter.Trigger(new TargetInfo(attacker.Position, attacker.Map), TargetInfo.Invalid);
            }

            Map map = attacker.Map;
            IntVec3 center = attacker.Position;
            int radius = Props.damageRadius;

            // ??5x5????????? (??2?)
            CellRect areaRect = CellRect.CenteredOn(center, radius);

            foreach (IntVec3 cell in areaRect)
            {
                if (!cell.InBounds(map)) continue;

                List<Thing> cellThings = map.thingGrid.ThingsListAt(cell);
                for (int j = 0; j < cellThings.Count; j++)
                {
                    Thing thing = cellThings[j];
                    if (thing == attacker) continue;

                    // ????????????????
                    if (thing is Pawn targetPawn &&
                        targetPawn.HostileTo(attacker) &&
                        !targetPawn.Downed &&
                        !targetPawn.IsPrisoner)
                    {
                        ApplyCrushDamage(attacker, targetPawn);
                    }
                }
            }
        }

        // ??????
        private void ApplyCrushDamage(Pawn attacker, Pawn target)
        {
            // ??????? (?????? × ????)
            float baseDamage = GetAveragePartHealth(target) * Props.damageFactor;

            // ????????? (IncomingDamageFactor)
            float damageFactor = target.GetStatValue(StatDefOf.IncomingDamageFactor);
            if (damageFactor <= 0) damageFactor = 0.01f; // ?????

            // ????? = ???? / ????
            float finalDamage = baseDamage / damageFactor;

            // ??????
            DamageInfo dinfo = new DamageInfo(
                def: DamageDefOf.Crush,
                amount: Mathf.Max(1, Mathf.RoundToInt(finalDamage)),
                armorPenetration: 2f,
                angle: -1f,
                instigator: attacker,
                hitPart: null // ????????
            );

            // ????
            target.TakeDamage(dinfo);

            // ????
            // if (Prefs.DevMode)
            // {
            //     Log.Message($"[BattleCrush] {attacker.LabelCap} dealt {finalDamage} crush damage to {target.LabelCap} " +
            //         $"(Base: {baseDamage}, Factor: {damageFactor})");
            // }
        }

        // ???????????
        private float GetAveragePartHealth(Pawn pawn)
        {
            float totalHealth = 0f;
            int partCount = 0;

            foreach (BodyPartRecord part in pawn.RaceProps.body.AllParts)
            {
                // ???????
                if (part.depth == BodyPartDepth.Outside)
                {
                    totalHealth += part.def.GetMaxHealth(pawn);
                    partCount++;
                }
            }

            return partCount > 0 ? totalHealth / partCount : 50f; // ???50
        }

        // ????
        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);
            CleanupEffecter();
        }

        public virtual void PostDeSpawn(Map map)
        {
            base.PostDeSpawn(map);
            CleanupEffecter();
        }

        // ???????
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref isInCombat, "isInCombat", false);
            Scribe_Values.Look(ref wasInCombatLastTick, "wasInCombatLastTick", false);
            Scribe_Values.Look(ref combatCheckCooldown, "combatCheckCooldown", 0);
            Scribe_Values.Look(ref damageCooldown, "damageCooldown", 0);
        }
    }


    public class CompProperties_CauseHediff_AoEAndRing : CompProperties
    {
        public HediffDef hediff;
        public float range;
        public int checkInterval = 60;
        public bool drawLines = true;

        public CompProperties_CauseHediff_AoEAndRing()
        {
            compClass = typeof(CompCauseHediff_AoEPlus);
        }
    }//????
    public class CompCauseHediff_AoEPlus : ThingComp
    {
        public float range;

        public CompProperties_CauseHediff_AoEAndRing Props => (CompProperties_CauseHediff_AoEAndRing)props;


        private bool IsPawnAffected(Pawn target)
        {

            if (target.Dead || target.health == null)
            {
                return false;
            }
            if (!target.HostileTo(parent))
            {
                return false;
            }
            if (target == parent)
            {
                return false;
            }
            return target.PositionHeld.DistanceTo(parent.PositionHeld) <= range;

        }
        #region ???
        public override void PostPostMake()
        {
            base.PostPostMake();
            range = Props.range;
        }
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref range, "range", 0f);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && range <= 0f)
            {
                range = Props.range;
            }
        }
        #endregion
        public override void CompTick()
        {
            if (!parent.IsHashIntervalTick(Props.checkInterval))
            {
                return;
            }
            if (!parent.SpawnedOrAnyParentSpawned)
            {
                return;
            }

            foreach (Pawn item in parent.MapHeld.mapPawns.AllPawnsSpawned)
            {
                if (IsPawnAffected(item))
                {
                    GiveOrUpdateHediff(item);
                }

                if (item.carryTracker.CarriedThing is Pawn target && IsPawnAffected(target))
                {
                    GiveOrUpdateHediff(target);
                }
            }
        }

        private void GiveOrUpdateHediff(Pawn target)
        {
            Hediff hediff = target.health.GetOrAddHediff(Props.hediff);

            HediffComp_Disappears hediffComp_Disappears = hediff.TryGetComp<HediffComp_Disappears>();
            if (hediffComp_Disappears == null)
            {
                Log.ErrorOnce("CompCauseHediff_AoE has a hediff in props which does not have a HediffComp_Disappears", 78939939);
            }
            else
            {
                hediffComp_Disappears.ticksToDisappear = Props.checkInterval;
            }
        }


        public override void PostDraw()
        {
            if (Props.drawLines && Find.Selector.IsSelected(parent))
            {
                GenDraw.DrawRadiusRing(parent.Position, Props.range);
            }

        }
    }

    public class DeathActionWorker_EndWar : DeathActionWorker
    {
        public override void PawnDied(Corpse corpse, Lord prevLord)
        {
            if (corpse.InnerPawn.Faction.HostileTo(Faction.OfPlayer))
            {

                Current.Game.GetComponent<GameComp_NCLWorm>().inWormWar = false;
                corpse.Map.weatherManager.curWeather = WeatherDefOf.Clear;


                ChoiceLetter choiceLetter = LetterMaker.MakeLetter("NCLWormWarEnd", "NCLWormWarEndDesc", LetterDefOf.NeutralEvent);
                Find.LetterStack.ReceiveLetter(choiceLetter);
            }
            if (corpse.InnerPawn.Faction.IsPlayer)
            {
                Current.Game.GetComponent<GameComp_NCLWorm>().ReLongTime = ((NCLCallTool_GiveLong)(DefDatabase<NCLCallDef>.GetNamed("NCLCommsConsole").NCLCallTools.Find(x => x is NCLCallTool_GiveLong))).ReLongTick;
            }
        }


    }


    public class JobGiver_UseNCLWormSkillList : ThinkNode_JobGiver
    {
        private readonly List<AbilityDef> tmpAttackss = new List<AbilityDef>();

        protected override Job TryGiveJob(Pawn pawn)
        {
            if (IsAnyAttackOnCooldown(pawn))
            {
                return null;
            }

            Thing thing = FindTarget(pawn);
            if (thing == null)
            {
                return null;
            }
            return GetRandomCanUseAbility(pawn, thing)?.GetJob(thing, null);
        }


        private static bool IsPawnTarget(Pawn pawn, Thing thing)
        {

            if (thing is Pawn pawnt && !pawnt.Dead && !pawnt.Downed && pawn.Position.InHorDistOf(pawnt.Position, 50) && pawn.CanSee(pawnt))//??????????
            {
                if (pawnt.Faction != null && pawn.Faction != null)
                {

                    return pawnt.Faction.HostileTo(pawn.Faction);
                }
                return pawnt.HostileTo(pawn);

            }

            return false;
        }
        //public static readonly int NociosphereLOSSqr = Mathf.FloorToInt(620.01f);
        private static readonly List<Thing> targetsTmp = new List<Thing>();
        public static Thing FindTarget(Pawn pawn)
        {
            List<Thing> source = pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.Pawn);
            CheckForTargets(pawn, source, targetsTmp, IsPawnTarget);

            Thing result = null;
            if (!targetsTmp.Empty())
            {
                result = targetsTmp.RandomElement();
            }

            targetsTmp.Clear();
            return result;
        }
        private static void CheckForTargets(Pawn pawn, List<Thing> source, List<Thing> output, Func<Pawn, Thing, bool> validator)
        {
            output.Clear();
            for (int i = 0; i < source.Count; i++)
            {
                if (validator(pawn, source[i]))
                {
                    output.Add(source[i]);
                }
            }
        }
        private bool IsAnyAttackOnCooldown(Pawn pawn)
        {
            List<AbilityDef> skills = pawn.TryGetComp<Comp_NCLWormSkillList>().Props.skill;
            for (int i = 0; i < skills.Count; i++)
            {
                Ability ability = pawn.abilities.GetAbility(skills[i]);
                if (!ability.CanCast)
                {
                    return true;
                }
            }
            return false;
        }
        private Ability GetRandomCanUseAbility(Pawn pawn, Thing target)
        {
            List<AbilityDef> skills = pawn.TryGetComp<Comp_NCLWormSkillList>().Props.skill;
            for (int i = 0; i < skills.Count; i++)
            {

                if (pawn.abilities.GetAbility(skills[i]).CanCast)
                {
                    tmpAttackss.Add(skills[i]);
                }
            }

            if (tmpAttackss.Empty())
            {
                return null;
            }


            return pawn.abilities.GetAbility(tmpAttackss.RandomElement());
        }
    }
    public class CompProperties_NCLWormSkillList : CompProperties
    {
        public List<AbilityDef> skill;

        public CompProperties_NCLWormSkillList()
        {
            compClass = typeof(Comp_NCLWormSkillList);
        }
    }
    public class Comp_NCLWormSkillList : ThingComp
    {
        public CompProperties_NCLWormSkillList Props => (CompProperties_NCLWormSkillList)props;

    }



    public class JobGiver_CastAbilityToAnyThing : JobGiver_AICastAbility
    {
        protected override LocalTargetInfo GetTarget(Pawn caster, Ability ability)
        {
            IEnumerable<IAttackTarget> i = from t in caster.Map.attackTargetsCache.GetPotentialTargetsFor(caster)
                                           where !t.ThreatDisabled(caster) && ability.verb.CanHitTarget(t.Thing)
                                           select t;
            if (i.Any())
            {
                return i.RandomElement().Thing;
            }
            return null;
        }
    }
    public class JobGiver_CastAbilityToEnemyTarget : JobGiver_AICastAbility
    {
        protected override LocalTargetInfo GetTarget(Pawn caster, Ability ability)
        {
            if (caster.mindState.enemyTarget != null && caster.mindState.enemyTarget.Spawned && ability.verb.CanHitTarget(caster.mindState.enemyTarget) && caster.CanSee(caster.mindState.enemyTarget))
            {
                return caster.mindState.enemyTarget;
            }
            return null;
        }
    }

    public class ThinkNode_NCLWormSleep : ThinkNode_Conditional
    {


        protected override bool Satisfied(Pawn pawn)
        {
            if (pawn is NCL_Pawn_Worm worm && worm.Sleep)
            {
                return true;
            }
            else
            {
                return false;
            }

        }
    }
    public class JobGiver_GetBeacon : ThinkNode_JobGiver
    {
        public static Building GetBeacon(Pawn pawn)
        {
            return (Building)GenClosest.ClosestThingReachable(pawn.Position, pawn.Map, ThingRequest.ForDef(DefDatabase<ThingDef>.GetNamed("NCLCommsConsole")), PathEndMode.InteractionCell, TraverseParms.For(pawn), 9999f, delegate (Thing t)
            {
                Building building_MechCharger = (Building)t;
                if (!pawn.CanReach(t, PathEndMode.InteractionCell, Danger.Deadly))
                {
                    return false;
                }
                return true;
            });
        }

        protected override Job TryGiveJob(Pawn pawn)
        {
            IntVec3 closestCharger = GetBeacon(pawn).InteractionCell;
            if (closestCharger.IsValid && !pawn.Position.InHorDistOf(closestCharger, 3))
            {
                Job job = JobMaker.MakeJob(JobDefOf.Goto, closestCharger);
                return job;
            }
            return null;
        }
    }

    /*public class Comp_NCLEXDoBomb : CompAbilityEffect
    {

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {

            Map map = parent.pawn.Map;
            IntVec3 poi = parent.pawn.Position;
            GenExplosion.DoExplosion(poi, map, 8f, DamageDefOf.Flame, parent.pawn, 10);
        }
    }
    public class Verb_CastAbilityWithBomb : Verb_CastAbility
    {
        public override bool TryStartCastOn(LocalTargetInfo castTarg, LocalTargetInfo destTarg, bool surpriseAttack = false, bool canHitNonTargetPawns = true, bool preventFriendlyFire = false, bool nonInterruptingSelfCast = false)
        {

            bool num = base.TryStartCastOn(castTarg, destTarg, surpriseAttack, canHitNonTargetPawns, preventFriendlyFire, nonInterruptingSelfCast);
            if (WarmupTicksLeft==1000 || WarmupTicksLeft == 500 || WarmupTicksLeft == 1)
            {
                Map map = caster.Map;
                IntVec3 poi = caster.Position;
                GenExplosion.DoExplosion(poi, map, 8f, DamageDefOf.Flame, caster, 10);
            }

            return num;
        }
    }*/
    /*public class ThinkNode_NCLWormShouldBomb : ThinkNode_Conditional
    {
        

        protected override bool Satisfied(Pawn pawn)
        {

            if (pawn is NCL_Pawn_Worm worm)
            {
                float ii = worm.HealthScale * 175;
                foreach (Hediff item in worm.health.hediffSet.hediffs)
                {
                    if (item is Hediff_Injury)
                    {
                        ii -= item.Severity;
                    }
                }
                float[] checkpoints = { 0.7f, 0.4f, 0.1f };
                if (worm.GetComp<CompShieldNCLWorm>().Power > 0)
                {
                    CompShieldNCLWorm comp = worm.GetComp<CompShieldNCLWorm>();

                    float SHfloat = comp.Power / comp.MaxPower;
                    foreach (float checkpoint in checkpoints)
                    {
                        if (worm.LADSH >= checkpoint && SHfloat < checkpoint)
                        {
                            return true;
                        }
                    }
                    worm.LADSH = SHfloat;
                }
                else
                {
                    float HPfloat = ii / (worm.HealthScale * 175);
                    foreach (float checkpoint in checkpoints)
                    {
                        if (worm.LADHP >= checkpoint && HPfloat < checkpoint)
                        {
                            return true;
                        }
                    }
                    worm.LADHP = HPfloat;
                }
            }
            else
            {
                return false;
            }

            return false;
        }
    }*/

    public class DamageDefWithTick : IExposable
    {
        public DamageDef damageDef;
        public int tick = 1;
        public string Label => damageDef.label;
        public string LabelCap => damageDef.LabelCap;
        public DamageDefWithTick()
        {
        }
        public DamageDefWithTick(DamageDef damageDef, int ttick)
        {
            if (ttick < 0)
            {
                Log.Warning("Tried to set DamageDefWithTick tick to " + ttick + ". thingDef=" + damageDef);
                ttick = 0;
            }

            this.damageDef = damageDef;
            this.tick = ttick;
        }
        public void ExposeData()
        {
            Scribe_Defs.Look(ref damageDef, "damageDef");
            Scribe_Values.Look(ref tick, "tick");
        }
        public override string ToString()
        {
            return "(" + damageDef + "x" + tick + "Tick)";
        }
        public override int GetHashCode()
        {
            return damageDef.shortHash + tick << 16;
        }
    }

    public class Verb_ShootWithprojList : Verb_Shoot
    {
        private int lii = 0;
        public List<ThingDef> things => EquipmentSource.def.descriptionHyperlinks
                                        .Where(item => item.def is ThingDef)
                                        .Select(item => (ThingDef)item.def)
                                        .ToList();

        public override ThingDef Projectile
        {
            get
            {
                lii++;
                if (lii >= things.Count)
                {
                    lii = 0;
                }
                return things[lii];



                //return base.Projectile;
            }
        }
        public override void ExposeData()
        {
            base.ExposeData();

        }
    }
    public class Verb_ShootBeamOneUse : Verb_ShootBeam
    {
        protected override bool TryCastShot()
        {
            if (base.TryCastShot())
            {
                if (burstShotsLeft <= 1)
                {
                    SelfConsume();
                }
                return true;
            }
            if (burstShotsLeft < base.BurstShotCount)
            {
                SelfConsume();
            }
            return false;
        }

        public override void Notify_EquipmentLost()
        {
            base.Notify_EquipmentLost();
            if (state == VerbState.Bursting && burstShotsLeft < base.BurstShotCount)
            {
                SelfConsume();
            }
        }

        private void SelfConsume()
        {
            if (base.EquipmentSource != null && !base.EquipmentSource.Destroyed)
            {
                base.EquipmentSource.Destroy();
            }
        }
    }

    public class Verb_GuangA : Verb
    {
        protected override int ShotsPerBurst => base.BurstShotCount;
        public override void WarmupComplete()
        {
            base.WarmupComplete();
        }
        protected override bool TryCastShot()
        {
            DamageOne();
            SelfConsume();
            return true;
        }

        public void DamageOne()
        {
            Vector3 drawPos2 = HeadOffsetAt(caster.DrawPos, caster.Rotation);
            drawPos2 = HeadOffsetAt(caster.DrawPos, caster.Rotation);
            Vector3 centerVector2 = currentTarget.CenterVector3;
            float angle2 = (drawPos2 - centerVector2).AngleFlat();
            drawPos2 = AngleIncrement(drawPos2, 2, angle2);
            // ????:??????
            var rayPath = new List<IntVec3>(250); // ?????(500/2)
            for (int k = 0; k < 500; k += 2)
            {
                Vector3 point = AngleIncrement(drawPos2, k, angle2);
                if (!point.InBounds(caster.Map)) break;
                rayPath.Add(point.ToIntVec3());
            }

            // ????:???????
            var resultCells = new HashSet<IntVec3>(); // ??HashSet????
            foreach (IntVec3 center in rayPath)
            {
                foreach (var offset in cellOffsets)
                {
                    IntVec3 celll = center + offset;
                    if (!celll.InBounds(caster.Map)) continue;
                    resultCells.Add(celll); // HashSet????????
                }
            }


            IntVec3 cell = currentTarget.Cell;
            Vector2 realPos = new Vector2(currentTarget.CenterVector3.x, currentTarget.CenterVector3.z);
            Mote_NCLWormLaser Laser = (Mote_NCLWormLaser)ThingMaker.MakeThing(NCLWormDefOf.Mote_NCLWormLaser);
            ((Mote_NCLWormLaser)GenSpawn.Spawn(Laser, cell, caster.Map)).AbSpawn(caster, realPos, resultCells.ToList());


        }
        private static readonly IntVec3[] cellOffsets = new IntVec3[]
        {
            new IntVec3(1, 0, 1), new IntVec3(1, 0, -1), new IntVec3(1, 0, 0),
            new IntVec3(0, 0, 1), IntVec3.Zero, new IntVec3(0, 0, -1),
            new IntVec3(-1, 0, 1), new IntVec3(-1, 0, 0), new IntVec3(-1, 0, -1)
        };//????????
        private static readonly IntVec3[] cellOffsetsMKII = new IntVec3[]
{
    new IntVec3(-2, 0, -2), new IntVec3(-2, 0, -1), new IntVec3(-2, 0, 0), new IntVec3(-2, 0, 1), new IntVec3(-2, 0, 2),
    new IntVec3(-1, 0, -2), new IntVec3(-1, 0, -1), new IntVec3(-1, 0, 0), new IntVec3(-1, 0, 1), new IntVec3(-1, 0, 2),
    new IntVec3(0, 0, -2), new IntVec3(0, 0, -1), IntVec3.Zero, new IntVec3(0, 0, 1), new IntVec3(0, 0, 2),
    new IntVec3(1, 0, -2), new IntVec3(1, 0, -1), new IntVec3(1, 0, 0), new IntVec3(1, 0, 1), new IntVec3(1, 0, 2),
    new IntVec3(2, 0, -2), new IntVec3(2, 0, -1), new IntVec3(2, 0, 0), new IntVec3(2, 0, 1), new IntVec3(2, 0, 2)
};//????????
        public Vector3 HeadOffsetAt(Vector3 BasePos, Rot4 rotation)
        {
            switch (rotation.AsInt)
            {
                case 0:
                    return BasePos + caster.def.race.headPosPerRotation[0];
                case 1:
                    return BasePos + caster.def.race.headPosPerRotation[1];
                case 2:
                    return BasePos + caster.def.race.headPosPerRotation[2];
                case 3:
                    return BasePos + caster.def.race.headPosPerRotation[3];
                default:
                    return BasePos;
            }
        }
        public override void Notify_EquipmentLost()
        {
            base.Notify_EquipmentLost();
            if (state == VerbState.Bursting && burstShotsLeft < base.BurstShotCount)
            {
                SelfConsume();
            }
        }
        private void SelfConsume()
        {
            if (base.EquipmentSource != null && !base.EquipmentSource.Destroyed)
            {
                base.EquipmentSource.Destroy();
            }
        }


        public Vector3 AngleIncrement(Vector3 center, float range, float angle)
        {
            float rad = angle * Mathf.Deg2Rad; // ?????
            float x = center.x - range * Mathf.Sin(rad);
            float z = center.z - range * Mathf.Cos(rad);
            return new Vector3(x, center.y, z);
        }//?????????
    }
    public class NCL_ConfocalLaser : Mote
    {
        private readonly SimpleCurve curve = new SimpleCurve()
        {
            Points =
            {
                new CurvePoint(0,0),
                new CurvePoint(0.7f,22.5f),
                new CurvePoint(1f,45f),
            }
        };
        private float Height
        {
            get
            {
                return curve.Evaluate(AgeSecs);
            }
        }

        public override Vector3 DrawPos => base.DrawPos + (Vector3.forward).RotatedBy(exactRotation + Height);
    }

    [StaticConstructorOnStartup]
    public class Mote_NCLWormLaser : ThingWithComps
    {

        #region ???
        public Color DarkRed => new Color((75 + lifeTick+Rand.Range(-80f,80f)) / 255f, ((lifeTick / 2) + 20 + Rand.Range(-80f, 80f)) / 255f, ((lifeTick / 2) + 20 + Rand.Range(-80f, 80f)) / 255f);
        public int lifeTick = 180;
        #endregion
        #region ???
        public Vector3 MinLaserPos_A_Start;
        public Vector3 MinLaserPos_A_End;
        public float MinLaserPos_A_Range = 0.5f;
        public float MinLaserPos_A_Range_Limit = 0.4f;
        public Vector3 MinLaserPos_B_Start;
        public Vector3 MinLaserPos_B_End;
        public float MinLaserPos_B_Range = -0.5f;
        public float MinLaserPos_B_Range_Limit = 0.4f;
        public Vector3 MinLaserPos_C_Start;
        public Vector3 MinLaserPos_C_End;
        public float MinLaserPos_C_Range = 0.2f;
        public float MinLaserPos_C_Range_Limit = 0.4f;
        public Vector3 MinLaserPos_D_Start;
        public Vector3 MinLaserPos_D_End;
        public float MinLaserPos_D_Range = -0.2f;
        public float MinLaserPos_D_Range_Limit = 0.4f;
        public Vector3 MinLaserPos_E_Start;
        public Vector3 MinLaserPos_E_End;
        public float MinLaserPos_E_Range = -0.1f;
        public float MinLaserPos_E_Range_Limit = 0.4f;
        public Vector3 MinLaserPos_F_Start;
        public Vector3 MinLaserPos_F_End;
        public float MinLaserPos_F_Range = 0.1f;
        public float MinLaserPos_F_Range_Limit = 0.4f;

        public Thing CCaster;
        //public int LaserScaleTick;
        public Vector2 RealPos;
        public float Angle;
        //public float MinLaser_Deviation_Range = 12f;
        //public float MinLaser_Deviation_Range_Speed = 15f;
        public float MinLaser_Rotate_Speed = 2f;
        private List<IntVec3> ListAllPos = new List<IntVec3>();
        #endregion

        public Vector3 HeadOffsetAt(Thing caster, Vector3 BasePos, Rot4 rotation)
        {
            switch (rotation.AsInt)
            {
                case 0:
                    return BasePos + caster.def.race.headPosPerRotation[0];
                case 1:
                    return BasePos + caster.def.race.headPosPerRotation[1];
                case 2:
                    return BasePos + caster.def.race.headPosPerRotation[2];
                case 3:
                    return BasePos + caster.def.race.headPosPerRotation[3];
                default:
                    return BasePos;
            }
        }//????,??????
        public void AbSpawn(Thing baseThing, Vector2 RealPos, List<IntVec3> ListAllPos)
        {
            CCaster = baseThing;
            this.RealPos = RealPos;
            this.ListAllPos = ListAllPos;
        }//?????,??????????

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref MinLaserPos_A_Range, "MinLaserPos_A_Range", 0f);
            Scribe_Values.Look(ref MinLaserPos_A_UpOrDown, "MinLaserPos_A_UpOrDown", defaultValue: false);
            Scribe_Values.Look(ref MinLaserPos_B_Range, "MinLaserPos_B_Range", 0f);
            Scribe_Values.Look(ref MinLaserPos_B_UpOrDown, "MinLaserPos_B_UpOrDown", defaultValue: false);
            Scribe_Values.Look(ref MinLaserPos_C_Range, "MinLaserPos_C_Range", 0f);
            Scribe_Values.Look(ref MinLaserPos_C_UpOrDown, "MinLaserPos_C_UpOrDown", defaultValue: false);
            Scribe_Values.Look(ref MinLaserPos_D_Range, "MinLaserPos_D_Range", 0f);
            Scribe_Values.Look(ref MinLaserPos_D_UpOrDown, "MinLaserPos_D_UpOrDown", defaultValue: false);
            Scribe_Values.Look(ref MinLaserPos_E_Range, "MinLaserPos_E_Range", 0f);
            Scribe_Values.Look(ref MinLaserPos_E_UpOrDown, "MinLaserPos_E_UpOrDown", defaultValue: false);
            Scribe_Values.Look(ref MinLaserPos_F_Range, "MinLaserPos_F_Range", 0f);
            Scribe_Values.Look(ref MinLaserPos_F_UpOrDown, "MinLaserPos_F_UpOrDown", defaultValue: false);
            Scribe_References.Look(ref CCaster, "CCaster");
            Scribe_Values.Look(ref RealPos, "RealPos");
            Scribe_Values.Look(ref Angle, "Angle", 0f);
            DeepProfiler.Start("Load All ListPos");
            Scribe_Collections.Look(ref ListAllPos, "ListAllPos", LookMode.Value);
            DeepProfiler.End();
        }//??,????

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            base.DrawAt(drawLoc, flip);  // ?????????
            {
                // 1. ????
                Vector3 drawPos = HeadOffsetAt(CCaster, CCaster.DrawPos, CCaster.Rotation);  // ?????????
                Vector3 vector = new Vector3(RealPos.x, DrawPos.y, RealPos.y);  // ????????

                // 2. ????????
                float x = 2f;  // ????
                int num = 20;   // ??????tick
                int num2 = 160; // ??????tick
                int num3 = 230; // ????tick
                int num4 = 240; // ????tick

                // 2.1 ????(0-20 tick):??????
                if ((180-lifeTick) < num)
                {
                    x = (float)(180 - lifeTick) / 10f;
                }
                // 2.2 ??????(160-170 tick):????15
                if ((180 - lifeTick) <= num3 && (180 - lifeTick) > num2)
                {
                    x = 15f;
                }
                // 2.3 ????(170-180 tick):??????
                else if ((180 - lifeTick) <= num4 && (180 - lifeTick) > num3)
                {
                    x = 15f - (float)((180 - lifeTick) - num3) * 1.5f;
                }

                // 3. ????????
                float num5 = (drawPos - vector).AngleFlat();  // ?????????????

                // 4. ?????
                float a = 0.8f;  // ?????
                                 // ????(175+ tick):???????
                if ((180 - lifeTick) >= 235)
                {
                    a = 0.8f - (float)((180 - lifeTick) - 235) / 62f;
                }

                // 5. ????????
                Vector3 vector3_By_AngleFlat = drawPos;  // ??????????(????????????)
                vector3_By_AngleFlat.y = AltitudeLayer.PawnRope.AltitudeFor(3f);  // ?????

                // 6. ????????(?????)
                Vector3 vect = default(Vector3);
                // ?50??????????????????
                for (int i = 0; i < 500; i += 50)
                {
                    Vector3 vector3_By_AngleFlat2 = AngleIncrement(vector, i, num5);
                    if (!vector3_By_AngleFlat2.InBounds(Map))  // ????
                    {
                        break;
                    }
                    vect = vector3_By_AngleFlat2;  // ????????
                }

                // 7. ??????
                float lengthHorizontal = (vector3_By_AngleFlat.ToIntVec3() - vect.ToIntVec3()).LengthHorizontal;  // ????
                float num6 = 2f;  // ??????
                float z = lengthHorizontal * num6 + 100f;  // ????=????*2+100

                // 8. ???????
                Color color = DarkRed;  // ?????
                color.a = a;  // ?????
                              // ??????
                Material material = MaterialPool.MatFrom(ContentFinder<Texture2D>.Get("UI/Misc/Laser"),
                                                      ShaderDatabase.Transparent,
                                                      color);

                // 9. ??????
                Matrix4x4 matrix = default(Matrix4x4);
                matrix.SetTRS(
                    vector3_By_AngleFlat,  // ??:??
                    Quaternion.AngleAxis(num5, Vector3.up),  // ??:??????
                    new Vector3(x, 1f, z));  // ??:x=??,y=1,z=??

                // 10. ??????
                Graphics.DrawMesh(MeshPool.plane10,  // ??????
                                 matrix,  // ????
                                 material,  // ????
                                 0);  // ???
            }//?????
            {
                // 1. ????
                Vector3 drawPos = HeadOffsetAt(CCaster, CCaster.DrawPos, CCaster.Rotation);  // ?????????
                Vector3 vector = new Vector3(RealPos.x, DrawPos.y, RealPos.y);

                float angle1 = (drawPos - vector).AngleFlat();
                drawPos = AngleIncrement(drawPos, 2f, angle1);

                float x = 2f;  // ????
                float a = 0.8f;
                if (lifeTick <= 180)
                {
                    if (lifeTick >= 20)
                    {
                        x = 12f - (180 - lifeTick) * 0.06875f;
                        a = 0.3f + (180 - lifeTick) * (0.5f / 160f); // 0.3?0.8
                    }
                    else if (lifeTick >= 15)
                    {
                        x = 1f;
                        a = 0.8f;
                    }
                    else
                    {
                        float t = (15 - lifeTick) / 15f;
                        x = 1f + Mathf.Pow(t, 0.4f) * 5f;
                        a = 0.8f - Mathf.Pow(t, 0.4f) * 0.7f; // 0.8?0.1
                    }
                }

                // 5. ????????
                Vector3 vector3_By_AngleFlat = drawPos;  // ??????????(????????????)
                vector3_By_AngleFlat.y = AltitudeLayer.PawnUnused.AltitudeFor(3f);  // ?????

                // 8. ???????
                Color color = Color.white; //new Color(0.7f,0.1f,0.1f);  // ?????
                color.a = a;  // ?????
                              // ??????
                Material material = MaterialPool.MatFrom(ContentFinder<Texture2D>.Get("UI/Misc/NCLRing"),
                                                      ShaderDatabase.Transparent,
                                                      color);

                // 9. ??????
                Matrix4x4 matrix = default(Matrix4x4);
                matrix.SetTRS(
                    vector3_By_AngleFlat,  // ??:??
                    Quaternion.AngleAxis(0, Vector3.up),  // ??:??????
                    new Vector3(x, 1f, x));  // ??:x=??,y=1,z=??

                // 10. ??????
                Graphics.DrawMesh(MeshPool.plane10,  // ??????
                                 matrix,  // ????
                                 material,  // ????
                                 0);  // ???
            }//????
            {
                Draw_MinLaserPos(MinLaserPos_A_UpOrDown, MinLaserPos_A_Start, MinLaserPos_A_End);
                Draw_MinLaserPos(MinLaserPos_B_UpOrDown, MinLaserPos_B_Start, MinLaserPos_B_End);
                Draw_MinLaserPos(MinLaserPos_C_UpOrDown, MinLaserPos_C_Start, MinLaserPos_C_End);
                Draw_MinLaserPos(MinLaserPos_D_UpOrDown, MinLaserPos_D_Start, MinLaserPos_D_End);
                Draw_MinLaserPos(MinLaserPos_E_UpOrDown, MinLaserPos_E_Start, MinLaserPos_E_End);
                Draw_MinLaserPos(MinLaserPos_F_UpOrDown, MinLaserPos_F_Start, MinLaserPos_F_End);
            }
        }

        protected override void Tick()
        {
            base.Tick();
            lifeTick--;
            if (lifeTick >= 30&&lifeTick % 60 == 0)
            {
                NCLWormDefOf.NCLLaserWarmup.PlayOneShot(new TargetInfo(base.Position, base.Map));
            }
            if (lifeTick>=30&&lifeTick%5==0)
            {
                for (int i = 0; i < 8; i++)
                {
                    Vector3 drawPos = HeadOffsetAt(CCaster, CCaster.DrawPos, CCaster.Rotation);
                    Vector3 vector = new Vector3(RealPos.x, DrawPos.y, RealPos.y);

                    float angle11 = (drawPos - vector).AngleFlat();
                    drawPos = AngleIncrement(drawPos,2f, angle11);

                    FleckMaker.ThrowMicroSparks(drawPos, Map);
                    FleckMaker.ThrowLightningGlow(drawPos, Map, 0.5f);

                    var fleckData = FleckMaker.GetDataStatic(drawPos, base.Map, NCLWormDefOf.Fleck_NCLStarFire, Rand.Range(0.3f, 0.5f));
                    float angle = (new Vector3(RealPos.x, DrawPos.y, RealPos.y) - drawPos).AngleFlat() + Rand.Range(-40f, 40f);
                    if (angle > 180f) angle -= 360f;
                    if (angle < -180f) angle += 360f;
                    fleckData.velocityAngle = angle;
                    fleckData.velocitySpeed = Rand.Range(5f, 10f);
                    base.Map.flecks.CreateFleck(fleckData);
                    if(lifeTick % 20 == 0)
                    {
                        NCLWormDefOf.MechBandElectricityArc.Spawn(ListAllPos.RandomElement(), Map, 1);
                    }

                }
            }
            if (lifeTick <= 20 && lifeTick % 5 == 0)
            {
                KeliKeli();
            }
            if (lifeTick <= 20 && lifeTick >= 10 && lifeTick % 10 == 0)
            {
                NCLWormDefOf.Explosion_MechBandShockwave.PlayOneShot(new TargetInfo(base.Position, base.Map));
                TakeDamage();
            }  //?20??????
            if (lifeTick==-40)
            {
                Destroy();
            }//?-40??????
            {
                UpdateMinLaserPosition(ref MinLaserPos_A_UpOrDown, ref MinLaserPos_A_Range, MinLaserPos_A_Range_Limit, out MinLaserPos_A_Start, out MinLaserPos_A_End, 0f);
                UpdateMinLaserPosition(ref MinLaserPos_B_UpOrDown, ref MinLaserPos_B_Range, MinLaserPos_B_Range_Limit, out MinLaserPos_B_Start, out MinLaserPos_B_End, 180f);
                UpdateMinLaserPosition(ref MinLaserPos_C_UpOrDown, ref MinLaserPos_C_Range, MinLaserPos_C_Range_Limit, out MinLaserPos_C_Start, out MinLaserPos_C_End, 60f);
                UpdateMinLaserPosition(ref MinLaserPos_D_UpOrDown, ref MinLaserPos_D_Range, MinLaserPos_D_Range_Limit, out MinLaserPos_D_Start, out MinLaserPos_D_End, 240f);
                UpdateMinLaserPosition(ref MinLaserPos_E_UpOrDown, ref MinLaserPos_E_Range, MinLaserPos_E_Range_Limit, out MinLaserPos_E_Start, out MinLaserPos_E_End, 120f);
                UpdateMinLaserPosition(ref MinLaserPos_F_UpOrDown, ref MinLaserPos_F_Range, MinLaserPos_F_Range_Limit, out MinLaserPos_F_Start, out MinLaserPos_F_End, 300f);
            }
        }
        private Color[] Rainbow = new Color[]{Color.red,new Color(1f, 0.5f, 0f, 0.8f), Color.yellow,Color.green,Color.blue,new Color(0.29f, 0f, 0.51f, 0.8f),new Color(0.5f, 0f, 0.5f, 0.8f)};
        public void KeliKeli()
        {
            // ????????
            Vector3 drawPos = HeadOffsetAt(CCaster, CCaster.DrawPos, CCaster.Rotation);
            // ????????????????
            float angle = (DrawPos - drawPos).AngleFlat();
            // ????????????(?????)
            int particleCount = 0;

            if (lifeTick <= 20 && lifeTick > 0)
            {
                particleCount = 20;
            }
            else if (lifeTick <= 0 && lifeTick >= -40)
            {
                particleCount = (int)(20 * (1 - (Mathf.Abs(lifeTick) / 40f)));
            }
            // ??????????(120tick?????????)
            float lifeTimeFactor = Mathf.Max(0, (60 - lifeTick)) / 100f;

            // ???????????
            var particleSettings = new[] 
            {
                new { offset = 0.5f, useShifted = true },  // ???:???,??Shifted??
                new { offset = 0.3f, useShifted = false }  // ???:???,??????
            };

            // ????????
            foreach (var settings in particleSettings)
            {
                // ?????????
                for (int i = 0; i <= particleCount; i++)
                {
                    // ??????
                    float randomOffset = Rand.Range(-settings.offset, settings.offset);
                    // ??????(????????????)
                    Vector3 basePos = settings.useShifted
                        ? ListAllPos.RandomElement().ToVector3Shifted()
                        : ListAllPos.RandomElement().ToVector3();

                    // ??????
                    FleckCreationData data = FleckMaker.GetDataStatic(
                        basePos + new Vector3(randomOffset, 0f, randomOffset),  // ????
                        base.Map,                                               // ????
                        NCLWormDefOf.Fleck_NCLStar,                            // ????(??)
                        Rand.Range(1f, 1.5f)                                   // ????
                    );

                    // ??????
                    data.rotation = angle + 90f;                   // ????(????+90?)
                    data.velocityAngle = angle;                    // ??????
                    data.velocitySpeed = Rand.Range(40f, 60f);     // ????
                    data.instanceColor = Rainbow.RandomElement();  // ?????
                    data.def.solidTime = 0.6f - lifeTimeFactor;    // ????(???????)

                    // ????????
                    base.Map.flecks.CreateFleck(data);
                }
            }
        }
        public void TakeDamage()
        {
            Map map = base.Map; 
            
            foreach (IntVec3 intVec in ListAllPos)
            {
                List<Thing> thingsAtPos = intVec.GetThingList(map);

                for (int j = 0; j < thingsAtPos.Count; j++)
                {
                    DamageInfo dinfo = new DamageInfo(
                        DefDatabase<DamageDef>.GetNamed("TW_HyperBeam_Damage", false) ?? DamageDefOf.Vaporize,
                        100,
                        5f,
                        0f,
                        CCaster
                    );
                    thingsAtPos[j].TakeDamage(dinfo);
                }

                Vector3 loc = intVec.ToVector3Shifted();
                if (loc.ShouldSpawnMotesAt(map) && Rand.Chance(0.8f))
                {
                    loc -= new Vector3(0.5f, 0f, 0.5f);
                    loc += new Vector3(Rand.Value, 0f, Rand.Value);
                    FleckCreationData dataStatic = FleckMaker.GetDataStatic(loc, map, NCLWormDefOf.NCL_Fleck_BurnerUsedEmber, Rand.Range(0.3f, 0.8f));

                    dataStatic.rotation = Rand.Range(0, 360f);
                    dataStatic.rotationRate = Rand.Range(-12f, 12f);
                    dataStatic.velocityAngle = Rand.Range(35, 45);
                    dataStatic.velocitySpeed = 1.2f;
                    map.flecks.CreateFleck(dataStatic);
                }
                FleckMaker.Static(intVec, map, NCLWormDefOf.Fleck_BeamBurn, 5f);

                if (intVec.Impassable(map))
                {
                    GenExplosion.DoExplosion(
                        intVec,
                        map,
                        5.6f,
                        DefDatabase<DamageDef>.GetNamed("TW_HyperBeam_Damage", false) ?? DamageDefOf.Vaporize, // ?????????
                        this,
                        800,
                        5f
                    );
                    break;

                }
            }
        }//??(???)?????

        private void UpdateMinLaserPosition(ref bool upOrDown, ref float range, float rangeLimit,out Vector3 startPos, out Vector3 endPos, float angleOffset)
        {
            // 1. ????(????)
            if (upOrDown)
            {
                range -= 0.01f;
                if (range <= -rangeLimit) upOrDown = false;
            }
            else
            {
                range += 0.01f;
                if (range >= rangeLimit) upOrDown = true;
            }
            // ????
            Vector3 drawPos = HeadOffsetAt(CCaster, CCaster.DrawPos, CCaster.Rotation);
            Vector3 targetPos = new Vector3(RealPos.x, DrawPos.y, RealPos.y);
            float mainAngle = (drawPos - targetPos).AngleFlat();

            // ???????????
            float perpendicularAngle = mainAngle - 90f;
            if (perpendicularAngle < 0f) perpendicularAngle += 360f;

            Vector3 mainLaserPoint = drawPos;// MYDE_ModFront.GetVector3_By_AngleFlat(drawPos, Liu, mainAngle);
            startPos = AngleIncrement(mainLaserPoint, range, perpendicularAngle);

            //0:????????????
            float spiralRadius = 12 - (180f-lifeTick) / 15f;

            // ??????????
            float spiralAngle = angleOffset+(180 -lifeTick) * MinLaser_Rotate_Speed;//????+0~180*??
            spiralAngle %= 360f;
            if (spiralAngle < 0f) spiralAngle += 360f;

            endPos = AngleIncrement(targetPos, spiralRadius, spiralAngle);
        }

        public bool MinLaserPos_A_UpOrDown = true;
        public bool MinLaserPos_B_UpOrDown = false;
        public bool MinLaserPos_C_UpOrDown = true;
        public bool MinLaserPos_D_UpOrDown = false;
        public bool MinLaserPos_E_UpOrDown = true;
        public bool MinLaserPos_F_UpOrDown = false;

        public void Draw_MinLaserPos(bool UpOrDown, Vector3 Start, Vector3 End)
        {
            // 1. ?????????(UpOrDown?true?4f,false?2f)
            float incOffset = 4f;
            if (!UpOrDown)
            {
                incOffset = 2f;
            }

            // 2. ??????????????
            Vector3 vector = (Start + End) / 2f;
            vector.y = AltitudeLayer.PawnRope.AltitudeFor(incOffset);

            // 3. ????????(??LaserScaleTick?????)
            float x = 1f; // ????
            int num = 20;  // ??????tick
            int num2 = 160; // ??????tick
            int num3 = 170; // ????tick
            int num4 = 180; // ????tick

            // 3.1 ????(0-20 tick):???0?????1
            if ((180 - lifeTick) < num)
            {
                x = (float)(180 - lifeTick) / 20f;
            }
            // 3.2 ??????(160-170 tick):????5
            if ((180 - lifeTick) <= num3 && (180 - lifeTick) > num2)
            {
                x = 5f;
            }
            // 3.3 ????(170-180 tick):???5????
            else if ((180 - lifeTick) <= num4 && (180 - lifeTick) > num3)
            {
                x = 5f - (float)((180 - Mathf.Max(lifeTick, 0)) - num3) / 2f;
            }

            // 4. ????????(?End??Start?????)
            float angle = (Start - End).AngleFlat();

            // 5. ???????
            float a = 0.8f; // ?????
                            // ????(175+ tick):???????
            if (lifeTick<=5)
            {
                a = Mathf.Max(lifeTick, 0) * 0.2f;
            }

            // 6. ????????(??????WuDianWu)
            Vector3 vector3_By_AngleFlat = Start;// MYDE_ModFront.GetVector3_By_AngleFlat(Start, WuDianWu, angle);
            // ??????
            vector3_By_AngleFlat.y = AltitudeLayer.PawnRope.AltitudeFor(3f);

            // 7. ????????(???????)
            Vector3 vect = default(Vector3);
            // ?50??????????????????
            for (int i = 0; i < 500; i += 50)
            {
                Vector3 vector3_By_AngleFlat2 = AngleIncrement(End, i, angle);
                // ?????????????
                if (!vector3_By_AngleFlat2.InBounds(Map))
                {
                    break;
                }
                vect = vector3_By_AngleFlat2;
            }

            // 8. ????????(????*2 + 100)
            float lengthHorizontal = (vector3_By_AngleFlat.ToIntVec3() - vect.ToIntVec3()).LengthHorizontal;
            float num5 = 2f;  // ??????
            float z = lengthHorizontal * num5 + 100f; // ????

            // 9. ?????????
            Color color = DarkRed; // ????
            color.a = a; // ?????
                         // ??????
            Material material = MaterialPool.MatFrom(ContentFinder<Texture2D>.Get("UI/Misc/Laser"), ShaderDatabase.Transparent, color);

            // 10. ??????(??/??/??)
            Matrix4x4 matrix = default(Matrix4x4);
            matrix.SetTRS(
                vector3_By_AngleFlat, // ??:??????
                Quaternion.AngleAxis(angle, Vector3.up), // ??:??????
                new Vector3(x, 1f, z)); // ??:x=??,y=1,z=??

            // 11. ??????
            Graphics.DrawMesh(MeshPool.plane10, matrix, material, 0);
        }//????????
        public Vector3 AngleIncrement(Vector3 center, float range, float angle)
        {
            float rad = angle * Mathf.Deg2Rad; // ?????
            float x = center.x - range * Mathf.Sin(rad);
            float z = center.z - range * Mathf.Cos(rad);
            return new Vector3(x, center.y, z);
        }//?????????

    }

}
