using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace NCL
{
    public class HediffCompProperties_FollowerDroneInterceptGround : HediffCompProperties
    {
        public int scanIntervalTicks = 15;
        public int interceptCooldownTicks = 180;
        public int interceptRadiusCells = 6;
        public bool debugLog;
        public bool enableConnectingLineFleck = true;
        public string connectingLineFleck = "NCL_LaserDefenseLine";
        public float connectingLineWidth = 1f;
        public string impactFleck = "NCL_LaserDefenseImpact";
        public float impactScale = 1f;
        public List<string> randomImpactFlecks = new List<string>
        {
            "NCL_LaserDefenseImpact_One",
            "NCL_LaserDefenseImpact_Two",
            "NCL_LaserDefenseImpact_Three",
            "NCL_LaserDefenseImpact_Four"
        };
        public bool enableSmokeEffect = true;
        public bool enableFireGlowEffect = true;
        public float smokeSize = 1f;

        public HediffCompProperties_FollowerDroneInterceptGround()
        {
            compClass = typeof(HediffComp_FollowerDroneInterceptGround);
        }
    }

    public class HediffComp_FollowerDroneInterceptGround : HediffComp
    {
        private HediffCompProperties_FollowerDroneInterceptGround PropsIntercept =>
            (HediffCompProperties_FollowerDroneInterceptGround)props;
        private int nextInterceptTick;

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            Pawn pawn = Pawn;
            if (pawn == null || !pawn.Spawned || pawn.Map == null || pawn.Dead)
                return;
            if (!pawn.IsHashIntervalTick(PropsIntercept.scanIntervalTicks))
                return;
            if (Find.TickManager.TicksGame < nextInterceptTick)
                return;
            TryInterceptOneProjectile(pawn);
        }

        private void TryInterceptOneProjectile(Pawn pawn)
        {
            Map map = pawn.Map;
            int r = PropsIntercept.interceptRadiusCells;
            Projectile best = null;
            float bestDistSq = float.MaxValue;
            int groupCount = map.listerThings.ThingsInGroup(ThingRequestGroup.Projectile).Count;
            int totalProjectiles = 0;
            int allProjectileCount = 0;
            int skippedDestroyed = 0;
            int skippedNoProjectileExt = 0;
            int skippedFlyOverhead = 0;
            int skippedNoLauncher = 0;
            int skippedFriendly = 0;
            int skippedOutOfRange = 0;

            foreach (Thing thing in map.listerThings.AllThings)
            {
                if (thing is not Projectile proj)
                    continue;
                allProjectileCount++;
                totalProjectiles++;
                if (thing.Destroyed)
                {
                    skippedDestroyed++;
                    continue;
                }
                if (proj.def?.projectile == null)
                {
                    skippedNoProjectileExt++;
                    continue;
                }
                if (proj.def.projectile.flyOverhead)
                {
                    skippedFlyOverhead++;
                    continue;
                }
                if (proj.Launcher == null)
                {
                    skippedNoLauncher++;
                    // Accept launcher-less projectiles as unknown hostile to improve compatibility with custom projectiles.
                }
                else if (!GenHostility.HostileTo(proj.Launcher, pawn) && proj.Launcher.Faction != null)
                {
                    skippedFriendly++;
                    continue;
                }

                Vector3 delta = proj.DrawPos - pawn.DrawPos;
                delta.y = 0f;
                float dSq = delta.sqrMagnitude;
                if (dSq > r * r)
                {
                    skippedOutOfRange++;
                    continue;
                }
                if (dSq < bestDistSq)
                {
                    bestDistSq = dSq;
                    best = proj;
                }
            }

            if (best == null)
            {
                if (PropsIntercept.debugLog)
                {
                    Log.Message(
                        $"[NCL DronePack] Scan no hit for {pawn.LabelShortCap}. total={totalProjectiles}, " +
                        $"destroyed={skippedDestroyed}, noProjExt={skippedNoProjectileExt}, overhead={skippedFlyOverhead}, " +
                        $"noLauncher={skippedNoLauncher}, nonHostile={skippedFriendly}, outOfRange={skippedOutOfRange}, " +
                        $"groupCount={groupCount}, allProjectileCount={allProjectileCount}, radius={r}");
                }
                return;
            }
            Vector3 pos = best.DrawPos;
            if (PropsIntercept.debugLog)
            {
                string launcherName = best.Launcher != null ? best.Launcher.LabelShortCap : "null";
                Log.Message(
                    $"[NCL DronePack] Intercepting projectile={best.def.defName} from launcher={launcherName}, " +
                    $"dist={Mathf.Sqrt(bestDistSq):0.00}, pawn={pawn.LabelShortCap}");
            }
            TriggerLaserStyleInterceptEffects(pawn, best, map);
            best.Destroy(DestroyMode.Vanish);
            nextInterceptTick = Find.TickManager.TicksGame + PropsIntercept.interceptCooldownTicks;
            FleckDef fleck = FleckDefOf.ShotHit_Dirt;
            if (fleck != null)
                FleckMaker.Static(pos, map, fleck, 0.35f);
        }

        private void TriggerLaserStyleInterceptEffects(Pawn pawn, Projectile target, Map map)
        {
            if (target == null || map == null)
                return;

            Vector3 source = DroneVisualSourceFor(pawn);
            Vector3 dest = target.DrawPos;

            if (PropsIntercept.enableConnectingLineFleck && !PropsIntercept.connectingLineFleck.NullOrEmpty())
            {
                FleckDef lineDef = DefDatabase<FleckDef>.GetNamedSilentFail(PropsIntercept.connectingLineFleck);
                if (lineDef != null)
                    FleckMaker.ConnectingLine(source, dest, lineDef, map, PropsIntercept.connectingLineWidth);
            }

            if (!PropsIntercept.impactFleck.NullOrEmpty())
            {
                FleckDef impactDef = DefDatabase<FleckDef>.GetNamedSilentFail(PropsIntercept.impactFleck);
                if (impactDef != null)
                    FleckMaker.Static(dest, map, impactDef, PropsIntercept.impactScale);
            }
            if (PropsIntercept.randomImpactFlecks != null && PropsIntercept.randomImpactFlecks.Count > 0)
            {
                string pick = PropsIntercept.randomImpactFlecks[Rand.Range(0, PropsIntercept.randomImpactFlecks.Count)];
                FleckDef randomDef = DefDatabase<FleckDef>.GetNamedSilentFail(pick);
                if (randomDef != null)
                    FleckMaker.Static(dest, map, randomDef, PropsIntercept.impactScale);
            }

            if (PropsIntercept.enableSmokeEffect)
                FleckMaker.ThrowSmoke(dest, map, Mathf.Clamp(PropsIntercept.smokeSize, 0.5f, 5f));
            if (PropsIntercept.enableFireGlowEffect)
                FleckMaker.ThrowFireGlow(dest, map, Mathf.Clamp(PropsIntercept.smokeSize, 0.5f, 5f));
        }

        private static Vector3 DroneVisualSourceFor(Pawn pawn)
        {
            if (CompApparelFollowerDrone.TryGetDroneVisualPoint(pawn.thingIDNumber, 120, out Vector3 livePos))
                return livePos;

            Vector3 p = pawn.DrawPos;
            Vector3 off;
            switch (pawn.Rotation.AsInt)
            {
                case 0: off = new Vector3(0.35f, 0f, 0.42f); break;
                case 1: off = new Vector3(0.42f, 0f, 0.12f); break;
                case 2: off = new Vector3(-0.35f, 0f, 0.42f); break;
                case 3: off = new Vector3(-0.42f, 0f, 0.12f); break;
                default: off = new Vector3(-0.35f, 0f, 0.42f); break;
            }
            return p + off;
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref nextInterceptTick, "nextInterceptTick", 0);
        }
    }
}
