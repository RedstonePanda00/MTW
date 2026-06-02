using System;
using System.Collections.Generic;
using NCL;
using NCL.Worm;
using RimWorld;
using UnityEngine;
using Verse;

namespace NCL.Stratagem
{
    public class StratagemDef : Def
    {
        public float rangeRadius = 15f;
        public Color guideLineColor = Color.red;
        // Red guide line + incoming countdown duration after the arc phase.
        public float executeDelaySeconds = 1.5f;
        // Parabolic approach mote duration before guide phase begins.
        public float arcPhaseSeconds = 0.4f;
        public float cooldownSeconds = 8f;
        public string iconPath;
        public string dropBuildingDefName;
        public float powerCost;
        public List<ResearchProjectDef> researchPrerequisites;
        public bool hideFromLoadoutPicker;
        public Type workerClass = typeof(StratagemWorker_NoOp);

        public const string ReinforceDefName = "MTW_DiverReinforce";

        public bool IsBuiltInReinforce => defName == ReinforceDefName;

        [Unsaved(false)]
        private StratagemWorker workerInt;

        [Unsaved(false)]
        private Texture2D iconTex;

        public StratagemWorker Worker
        {
            get
            {
                if (workerInt != null)
                {
                    return workerInt;
                }

                Type type = workerClass ?? typeof(StratagemWorker_NoOp);
                if (!typeof(StratagemWorker).IsAssignableFrom(type))
                {
                    type = typeof(StratagemWorker_NoOp);
                }

                workerInt = (StratagemWorker)Activator.CreateInstance(type);
                return workerInt;
            }
        }

        public Texture2D IconTex => iconTex ?? (iconTex = !iconPath.NullOrEmpty() ? ContentFinder<Texture2D>.Get(iconPath, reportFailure: false) : null);
    }

    public abstract class StratagemWorker
    {
        public virtual void Execute(Map map, IntVec3 targetCell, Pawn caster, StratagemDef def)
        {
        }
    }

    public class StratagemWorker_NoOp : StratagemWorker
    {
    }

    public class StratagemWorker_OrbitalPrecisionStrike : StratagemWorker
    {
        private const string LogPrefix = "[NCL_OrbitalStrike]";
        private static readonly bool DebugLog = false;
        private const float ExplosionRadius = 6f;
        private const float FullDamageInnerRadius = 2.2f;
        private const float CenterVaporizeDamage = 500f;
        private const float OuterVaporizeDamage = 250f;
        private const string DirectHitDamageDefName = "MTW_OrbitalPrecisionDirectHit";
        private const string DirectHitInjuryHediffDefName = "MTW_OrbitalPrecisionDirectHitInjury";
        private const float DirectHitFallbackEpsilon = 0.001f;

        public override void Execute(Map map, IntVec3 targetCell, Pawn caster, StratagemDef def)
        {
            if (map == null || !targetCell.InBounds(map))
            {
                Log.Warning($"{LogPrefix} Execute aborted: map={map?.uniqueID}, cell={targetCell}, inBounds={map != null && targetCell.InBounds(map)}");
                return;
            }

            int tick = Find.TickManager?.TicksGame ?? 0;
            Dbg($"Execute start tick={tick} map={map.uniqueID} cell={targetCell} caster={DescribeThing(caster)} def={def?.defName}");

            SpawnDownwardImpactVisual(map, targetCell);
            SpawnExplosionVisual(map, targetCell);
            ApplyDirectHit(map, targetCell, caster);
            ApplyVaporizeFalloff(map, targetCell, caster);

            Dbg($"Execute finished tick={tick} cell={targetCell}");
        }

        private static void Dbg(string message)
        {
            if (DebugLog)
            {
                Log.Message($"{LogPrefix} {message}");
            }
        }

        private static void SpawnDownwardImpactVisual(Map map, IntVec3 targetCell)
        {
            Vector3 targetPos = targetCell.ToVector3Shifted();
            Vector3 skyPos = targetPos + new Vector3(0f, 0f, 18f);
            float layer = Altitudes.AltitudeFor(AltitudeLayer.MetaOverlays);
            Material lineMat = SolidColorMaterials.SimpleSolidColorMaterial(Color.red);
            GenDraw.DrawLineBetween(skyPos, targetPos, layer, lineMat, 0.2f);
            FleckMaker.ThrowLightningGlow(targetPos, map, 3f);
        }

        private static void SpawnExplosionVisual(Map map, IntVec3 targetCell)
        {
            GenExplosion.DoExplosion(
                targetCell,
                map,
                ExplosionRadius,
                DamageDefOf.Bomb,
                instigator: null,
                damAmount: 0,
                armorPenetration: 0f,
                chanceToStartFire: 0f,
                doVisualEffects: true,
                doSoundEffects: true
            );

            FleckMaker.Static(targetCell.ToVector3Shifted(), map, FleckDefOf.ExplosionFlash, ExplosionRadius * 1.5f);
            FleckMaker.ThrowHeatGlow(targetCell, map, ExplosionRadius);
            FleckMaker.ThrowSmoke(targetCell.ToVector3Shifted(), map, ExplosionRadius * 1.25f);

            foreach (IntVec3 c in GenRadial.RadialCellsAround(targetCell, ExplosionRadius, useCenter: true))
            {
                if (!c.InBounds(map))
                {
                    continue;
                }

                if ((c - targetCell).LengthHorizontal <= 1.5f || Rand.Chance(0.18f))
                {
                    FleckMaker.ThrowExplosionCell(c, map, FleckDefOf.DustPuff, Color.white);
                }

                if (Rand.Chance(0.35f))
                {
                    FleckMaker.ThrowSmoke(c.ToVector3Shifted(), map, Rand.Range(0.7f, 1.5f));
                }
            }
        }

        private static void ApplyDirectHit(Map map, IntVec3 targetCell, Pawn caster)
        {
            Dbg($"ApplyDirectHit called cell={targetCell}");

            DamageDef directDef = DefDatabase<DamageDef>.GetNamedSilentFail(DirectHitDamageDefName) ?? DamageDefOf.Crush;
            float directDamage = ResolveDamageAmount(directDef, fallback: 500f);
            float armorPen = ResolveArmorPenetration(directDef);
            List<Thing> centerThings = map.thingGrid.ThingsListAtFast(targetCell);

            Dbg($"ApplyDirectHit damageDef={directDef.defName} amount={directDamage} armorPen={armorPen} thingsInCell={centerThings.Count}");

            int candidates = 0;
            int applied = 0;
            int fallbacks = 0;
            List<Thing> thingsSnapshot = new List<Thing>(centerThings);
            for (int i = 0; i < thingsSnapshot.Count; i++)
            {
                Thing thing = thingsSnapshot[i];
                if (thing == null)
                {
                    Dbg($"ApplyDirectHit skip index={i}: null");
                    continue;
                }

                if (thing.Destroyed)
                {
                    Dbg($"ApplyDirectHit skip index={i}: destroyed {DescribeThing(thing)}");
                    continue;
                }

                if (!(thing is Pawn) && !thing.def.useHitPoints)
                {
                    Dbg($"ApplyDirectHit skip index={i}: not damageable {DescribeThing(thing)}");
                    continue;
                }

                candidates++;
                DamageInfo dinfo = new DamageInfo(directDef, directDamage, armorPen, instigator: caster);
                dinfo.SetIgnoreInstantKillProtection(true);
                dinfo.SetAllowDamagePropagation(true);
                DamageWorker.DamageResult damageResult = thing.TakeDamage(dinfo);
                applied++;

                bool usedFallback = false;
                if (thing is Pawn pawn
                    && !pawn.Destroyed
                    && !pawn.Dead
                    && damageResult.totalDamageDealt <= DirectHitFallbackEpsilon
                    && (damageResult.hediffs == null || damageResult.hediffs.Count == 0))
                {
                    usedFallback = TryApplyDirectHitInjuryFallback(pawn, directDef, directDamage, armorPen, caster);
                    if (usedFallback)
                    {
                        fallbacks++;
                    }
                }

                Dbg($"ApplyDirectHit hit {DescribeThing(thing)} totalDamageDealt={damageResult.totalDamageDealt} deflected={damageResult.deflected} hediffs={damageResult.hediffs?.Count ?? 0} fallback={usedFallback} destroyedAfter={thing.Destroyed} deadAfter={thing is Pawn hitPawn && (hitPawn.Dead || hitPawn.Downed)}");
            }

            Dbg($"ApplyDirectHit done cell={targetCell} candidates={candidates} applied={applied} fallbacks={fallbacks}");
        }

        private static bool TryApplyDirectHitInjuryFallback(Pawn pawn, DamageDef directDef, float directDamage, float armorPen, Pawn caster)
        {
            if (pawn == null || pawn.Destroyed || pawn.Dead)
            {
                return false;
            }

            HediffDef hediffDef = directDef?.hediff
                ?? DefDatabase<HediffDef>.GetNamedSilentFail(DirectHitInjuryHediffDefName);
            if (hediffDef == null)
            {
                Log.Warning($"{LogPrefix} ApplyDirectHit fallback failed: missing hediff def.");
                return false;
            }

            BodyPartRecord part = pawn.health.hediffSet.GetRandomNotMissingPart(directDef);
            if (part == null || pawn.health.hediffSet.PartIsMissing(part))
            {
                Log.Warning($"{LogPrefix} ApplyDirectHit fallback failed: no valid body part on {DescribeThing(pawn)}.");
                return false;
            }

            if (!(HediffMaker.MakeHediff(hediffDef, pawn, part) is Hediff_Injury injury))
            {
                Log.Warning($"{LogPrefix} ApplyDirectHit fallback failed: hediff is not Hediff_Injury.");
                return false;
            }

            injury.Severity = directDamage;
            DamageInfo dinfo = new DamageInfo(directDef, directDamage, armorPen, instigator: caster);
            dinfo.SetIgnoreInstantKillProtection(true);
            pawn.health.AddHediff(injury, part, dinfo);

            Dbg($"ApplyDirectHit fallback applied hediff={hediffDef.defName} part={part.Label} severity={directDamage} on {DescribeThing(pawn)}");
            return true;
        }

        private static void ApplyVaporizeFalloff(Map map, IntVec3 targetCell, Pawn caster)
        {
            Dbg($"ApplyVaporizeFalloff called cell={targetCell} radius={ExplosionRadius}");

            DamageDef vaporizeDef = DamageDefOf.Vaporize ?? DamageDefOf.Bomb;
            float armorPen = ResolveArmorPenetration(vaporizeDef);
            int cellsScanned = 0;
            int hits = 0;
            foreach (IntVec3 c in GenRadial.RadialCellsAround(targetCell, ExplosionRadius, useCenter: true))
            {
                if (!c.InBounds(map))
                {
                    continue;
                }

                cellsScanned++;
                float dist = (c - targetCell).LengthHorizontal;
                float damage = dist <= FullDamageInnerRadius
                    ? CenterVaporizeDamage
                    : OuterVaporizeDamage + (CenterVaporizeDamage - OuterVaporizeDamage) * Mathf.Pow(1f - Mathf.Clamp01((dist - FullDamageInnerRadius) / Mathf.Max(0.01f, ExplosionRadius - FullDamageInnerRadius)), 2f);

                List<Thing> things = map.thingGrid.ThingsListAtFast(c);
                for (int i = 0; i < things.Count; i++)
                {
                    Thing thing = things[i];
                    if (thing == null || thing.Destroyed || (!(thing is Pawn) && !thing.def.useHitPoints))
                    {
                        continue;
                    }

                    DamageInfo dinfo = new DamageInfo(vaporizeDef, damage, armorPen, instigator: caster);
                    DamageWorker.DamageResult damageResult = thing.TakeDamage(dinfo);
                    hits++;
                    Dbg($"ApplyVaporizeFalloff hit cell={c} dist={dist:F2} dmg={damage:F0} {DescribeThing(thing)} totalDamageDealt={damageResult.totalDamageDealt} destroyedAfter={thing.Destroyed}");
                }
            }

            Dbg($"ApplyVaporizeFalloff done cell={targetCell} cellsScanned={cellsScanned} hits={hits} damageDef={vaporizeDef.defName}");
        }

        private static string DescribeThing(Thing thing)
        {
            if (thing == null)
            {
                return "null";
            }

            if (thing is Pawn pawn)
            {
                float hp = pawn.health?.summaryHealth?.SummaryHealthPercent ?? -1f;
                return $"Pawn({pawn.LabelCap}, id={pawn.thingIDNumber}, hp={hp:P0})";
            }

            return $"{thing.def?.defName ?? "?"}(id={thing.thingIDNumber}, useHP={thing.def?.useHitPoints})";
        }

        private static float ResolveArmorPenetration(DamageDef damageDef)
        {
            if (damageDef == null)
            {
                return 0f;
            }

            return damageDef.defaultArmorPenetration >= 0f ? damageDef.defaultArmorPenetration : 0f;
        }

        private static float ResolveDamageAmount(DamageDef damageDef, float fallback)
        {
            if (damageDef == null)
            {
                return fallback;
            }

            return damageDef.defaultDamage > 0 ? damageDef.defaultDamage : fallback;
        }
    }

    public class StratagemWorker_DropSentryPod : StratagemWorker
    {
        private const string DefaultPodDefName = "NCL_AtmosphericPlasmaDropPod";

        public override void Execute(Map map, IntVec3 targetCell, Pawn caster, StratagemDef def)
        {
            if (map == null || !targetCell.InBounds(map) || !targetCell.Standable(map))
            {
                Messages.Message("NCL_Stratagem_InvalidTarget".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }

            if (def?.dropBuildingDefName.NullOrEmpty() ?? true)
            {
                return;
            }

            ThingDef buildingDef = DefDatabase<ThingDef>.GetNamedSilentFail(def.dropBuildingDefName);
            if (buildingDef == null)
            {
                Log.Warning($"[NCL] StratagemWorker_DropSentryPod: missing building def '{def.dropBuildingDefName}'.");
                return;
            }

            ThingDef podDef = DefDatabase<ThingDef>.GetNamedSilentFail(DefaultPodDefName);
            if (podDef == null)
            {
                Log.Warning($"[NCL] StratagemWorker_DropSentryPod: missing pod def '{DefaultPodDefName}'.");
                return;
            }

            Thing thing = ThingMaker.MakeThing(podDef);
            if (!(thing is Skyfaller_AtmosphericPlasmaPod pod))
            {
                Log.Warning("[NCL] StratagemWorker_DropSentryPod: pod thingClass mismatch.");
                return;
            }

            pod.spawnBuildingDefName = def.dropBuildingDefName;
            pod.spawnAsPlayerFaction = true;
            pod.creditPawn = caster;
            GameComponent_DiverRespawnManager manager = Current.Game?.GetComponent<GameComponent_DiverRespawnManager>();
            pod.creditDiverSlotIndex = manager?.GetSlotIndexForPawn(caster) ?? -1;
            GenSpawn.Spawn(pod, targetCell, map, WipeMode.Vanish);
        }
    }

    public class StratagemWorker_DiverReinforce : StratagemWorker
    {
        public override void Execute(Map map, IntVec3 targetCell, Pawn caster, StratagemDef def)
        {
            if (map == null || !targetCell.InBounds(map))
            {
                return;
            }

            GameComponent_DiverRespawnManager manager = Current.Game?.GetComponent<GameComponent_DiverRespawnManager>();
            if (manager == null)
            {
                Messages.Message("MTW_Diver_ReinforceGizmo_NoManager".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }

            int launched = manager.LaunchReinforcementPodsAt(map, targetCell);
            if (launched > 0)
            {
                Messages.Message("MTW_Diver_Reinforce_Success".Translate(launched), MessageTypeDefOf.PositiveEvent);
            }
            else if (manager.GetOccupiedDiverSlotCount() >= GameComponent_DiverRespawnManager.TargetCount)
            {
                Messages.Message("MTW_Diver_Reinforce_Full".Translate(), MessageTypeDefOf.RejectInput);
            }
            else
            {
                Messages.Message("MTW_Diver_Reinforce_Failed".Translate(), MessageTypeDefOf.RejectInput);
            }
        }
    }
}
