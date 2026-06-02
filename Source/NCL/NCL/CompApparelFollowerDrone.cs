using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace NCL
{
    public class CompProperties_ApparelFollowerDrone : CompProperties
    {
        public HediffDef hediffDef;
        public bool debugLog;
        public string droneTexPath = "Races/Drone_south";
        public float droneDrawSize = 0.55f;
        public float followSmoothTime = 0.22f;
        public float followMaxSpeed = 8f;
        public float minKeepDistance = 0.90f;
        public float maxKeepDistance = 1.30f;

        public CompProperties_ApparelFollowerDrone()
        {
            compClass = typeof(CompApparelFollowerDrone);
        }

        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (string err in base.ConfigErrors(parentDef))
                yield return err;
            if (hediffDef == null)
                yield return $"{nameof(CompProperties_ApparelFollowerDrone)}: hediffDef is null.";
        }
    }

    public class CompProperties_ApparelFollowerDronePointDefense : CompProperties_ApparelFollowerDrone
    {
        public CompProperties_ApparelFollowerDronePointDefense()
        {
            compClass = typeof(CompApparelFollowerDronePointDefense);
        }
    }

    public class CompApparelFollowerDrone : ThingComp
    {
        private const string DiverDefName = "MTW_Diver";

        private struct DroneVisualPointState
        {
            public Vector3 worldPos;
            public int lastSeenTick;
        }

        private struct DroneFollowState
        {
            public bool initialized;
            public Vector3 worldPos;
            public Vector3 velocity;
            public int lastSeenTick;
        }

        private static readonly Dictionary<int, DroneFollowState> FollowStates = new Dictionary<int, DroneFollowState>();
        private static readonly Dictionary<int, DroneVisualPointState> LastVisualPoints = new Dictionary<int, DroneVisualPointState>();
        private static readonly HashSet<int> LoggedFallbackDrawPawns = new HashSet<int>();
        private static readonly Dictionary<string, Graphic> DroneGraphicCache = new Dictionary<string, Graphic>();

        protected CompProperties_ApparelFollowerDrone PropsDrone => (CompProperties_ApparelFollowerDrone)props;

        public override void Notify_Equipped(Pawn pawn)
        {
            base.Notify_Equipped(pawn);
            if (pawn == null || PropsDrone.hediffDef == null)
                return;
            if (PropsDrone.debugLog)
                Log.Message($"[NCL DronePack] Notify_Equipped on {pawn.LabelShortCap} ({pawn.def.defName}), hediff={PropsDrone.hediffDef.defName}");
            TryRemoveDroneHediff(pawn);
            Hediff hediff = HediffMaker.MakeHediff(PropsDrone.hediffDef, pawn);
            BodyPartRecord part = pawn.health.hediffSet.GetBrain();
            if (part == null)
                part = pawn.RaceProps.body.corePart;
            pawn.health.AddHediff(hediff, part);
            pawn.Drawer?.renderer?.SetAllGraphicsDirty();
            if (PropsDrone.debugLog)
                Log.Message($"[NCL DronePack] Hediff added to {pawn.LabelShortCap}, part={(part != null ? part.def.defName : "null")}");
        }

        public override void Notify_Unequipped(Pawn pawn)
        {
            base.Notify_Unequipped(pawn);
            if (pawn != null)
            {
                if (PropsDrone.debugLog)
                    Log.Message($"[NCL DronePack] Notify_Unequipped on {pawn.LabelShortCap} ({pawn.def.defName})");
                TryRemoveDroneHediff(pawn);
                pawn.Drawer?.renderer?.SetAllGraphicsDirty();
            }
        }

        private void TryRemoveDroneHediff(Pawn pawn)
        {
            if (PropsDrone.hediffDef == null)
                return;
            Hediff existing = pawn.health.hediffSet.GetFirstHediffOfDef(PropsDrone.hediffDef);
            if (existing != null)
            {
                pawn.health.RemoveHediff(existing);
                if (PropsDrone.debugLog)
                    Log.Message($"[NCL DronePack] Hediff removed from {pawn.LabelShortCap}");
            }
        }

        public override void CompDrawWornExtras()
        {
            base.CompDrawWornExtras();
            Apparel apparel = parent as Apparel;
            Pawn wearer = apparel?.Wearer;
            if (wearer == null || !wearer.Spawned || wearer.Map == null)
                return;

            // Diver now has its own render-tree path for apparel visuals.
            // Skip non-humanlike fallback draw here to prevent duplicate drone sprites.
            if (wearer.def?.defName == DiverDefName)
                return;

            // Vanilla apparel render node setup is humanlike-only; non-humanlike pawns need a fallback draw path.
            if (wearer.RaceProps?.Humanlike == true)
                return;

            Vector3 pawnPos = wearer.DrawPos;
            Vector3 desiredOffset = DesiredOffsetForRotation(wearer.Rotation);
            Vector3 desiredWorld = pawnPos + desiredOffset;
            desiredWorld.y = 0f;

            int id = wearer.thingIDNumber;
            int ticks = Find.TickManager.TicksGame;
            CleanupStaleStates(ticks);
            if (!FollowStates.TryGetValue(id, out DroneFollowState state))
                state = default;

            if (!state.initialized)
            {
                state.initialized = true;
                state.worldPos = desiredWorld;
                state.velocity = Vector3.zero;
            }
            else
            {
                float dt = Find.TickManager.Paused ? 0.016f : Mathf.Max(Time.unscaledDeltaTime, 0.016f);
                state.worldPos = Vector3.SmoothDamp(
                    state.worldPos,
                    desiredWorld,
                    ref state.velocity,
                    PropsDrone.followSmoothTime,
                    PropsDrone.followMaxSpeed,
                    dt);
            }

            Vector3 relative = state.worldPos - pawnPos;
            relative.y = 0f;
            float dist = relative.magnitude;
            if (dist > 0.0001f)
            {
                float clampedDist = Mathf.Clamp(dist, PropsDrone.minKeepDistance, PropsDrone.maxKeepDistance);
                relative = relative / dist * clampedDist;
            }
            else
            {
                relative = desiredOffset;
            }

            state.worldPos = pawnPos + relative;
            state.lastSeenTick = ticks;
            FollowStates[id] = state;

            Vector3 drawPos = pawnPos + relative;
            drawPos.y = AltitudeLayer.MoteOverhead.AltitudeFor();
            float bob = Mathf.Sin((ticks + id) * 0.045f) * 0.06f;
            drawPos.z += bob;
            SetDroneVisualPoint(id, drawPos, ticks);
            Graphic graphic = GetDroneGraphic();
            // Draw manually to avoid any implicit horizontal flip on non-humanlike fallback.
            if (graphic != null)
            {
                Material mat = graphic.MatSingleFor(parent);
                Matrix4x4 matrix = Matrix4x4.TRS(
                    drawPos,
                    Quaternion.identity,
                    new Vector3(PropsDrone.droneDrawSize, 1f, PropsDrone.droneDrawSize));
                Graphics.DrawMesh(MeshPool.plane10, matrix, mat, 0);
            }

            if (PropsDrone.debugLog && LoggedFallbackDrawPawns.Add(id))
            {
                Log.Message($"[NCL DronePack] Fallback worn-extra draw active for non-humanlike pawn={wearer.LabelShortCap} ({wearer.def.defName})");
            }
        }

        private Graphic GetDroneGraphic()
        {
            string key = $"{PropsDrone.droneTexPath}|{PropsDrone.droneDrawSize:0.###}";
            if (!DroneGraphicCache.TryGetValue(key, out Graphic g))
            {
                g = GraphicDatabase.Get<Graphic_Single>(
                    PropsDrone.droneTexPath,
                    ShaderDatabase.Cutout,
                    new Vector2(PropsDrone.droneDrawSize, PropsDrone.droneDrawSize),
                    Color.white);
                DroneGraphicCache[key] = g;
            }

            return g;
        }

        private static Vector3 DesiredOffsetForRotation(Rot4 rot)
        {
            switch (rot.AsInt)
            {
                case 0: return new Vector3(0.35f, 0f, 0.42f); // North
                case 1: return new Vector3(0.42f, 0f, 0.12f); // East
                case 2: return new Vector3(-0.35f, 0f, 0.42f); // South
                case 3: return new Vector3(-0.42f, 0f, 0.12f); // West
                default: return new Vector3(-0.35f, 0f, 0.42f);
            }
        }

        private static void CleanupStaleStates(int ticks)
        {
            if (ticks % 300 != 0 || FollowStates.Count == 0)
                return;
            List<int> stale = null;
            foreach (KeyValuePair<int, DroneFollowState> pair in FollowStates)
            {
                if (ticks - pair.Value.lastSeenTick <= 1200)
                    continue;
                if (stale == null)
                    stale = new List<int>();
                stale.Add(pair.Key);
            }

            if (stale == null)
                return;
            for (int i = 0; i < stale.Count; i++)
                FollowStates.Remove(stale[i]);
        }

        public static void SetDroneVisualPoint(int pawnId, Vector3 worldPos, int ticksGame)
        {
            LastVisualPoints[pawnId] = new DroneVisualPointState
            {
                worldPos = worldPos,
                lastSeenTick = ticksGame
            };
        }

        public static bool TryGetDroneVisualPoint(int pawnId, int maxAgeTicks, out Vector3 worldPos)
        {
            if (LastVisualPoints.TryGetValue(pawnId, out DroneVisualPointState state))
            {
                if (Find.TickManager.TicksGame - state.lastSeenTick <= maxAgeTicks)
                {
                    worldPos = state.worldPos;
                    return true;
                }
            }

            worldPos = default;
            return false;
        }
    }

    public class CompApparelFollowerDronePointDefense : CompApparelFollowerDrone
    {
    }
}
