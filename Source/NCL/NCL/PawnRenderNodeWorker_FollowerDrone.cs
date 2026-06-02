using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace NCL
{
    public class PawnRenderNodeWorker_FollowerDrone : PawnRenderNodeWorker_FlipWhenCrawling
    {
        private const float BobSpeed = 0.045f;
        private const float BobAmplitude = 0.06f;
        private const float FollowSmoothTime = 0.22f;
        private const float FollowMaxSpeed = 8f;
        private const float MinKeepDistance = 0.90f;
        private const float MaxKeepDistance = 1.30f;
        private const int StateForgetTicks = 1200;

        private struct DroneFollowState
        {
            public bool initialized;
            public Vector3 worldPos;
            public Vector3 velocity;
            public int lastSeenTick;
        }

        private static readonly HashSet<int> LoggedPawnIds = new HashSet<int>();
        private static readonly Dictionary<int, DroneFollowState> FollowStates = new Dictionary<int, DroneFollowState>();

        public override Vector3 OffsetFor(PawnRenderNode node, PawnDrawParms parms, out Vector3 pivot)
        {
            Vector3 baseOffset = base.OffsetFor(node, parms, out pivot);
            if (parms.flags.FlagSet(PawnRenderFlags.Portrait))
                return baseOffset;

            if (LoggedPawnIds.Add(parms.pawn.thingIDNumber))
            {
                Log.Message($"[NCL DronePack] Render node active for pawn={parms.pawn.LabelShortCap} def={parms.pawn.def.defName} rot={parms.facing}");
            }

            int id = parms.pawn.thingIDNumber;
            int ticks = Find.TickManager.TicksGame;
            CleanupStaleStates(ticks);

            Vector3 pawnPos = parms.pawn.DrawPos;
            Vector3 desiredWorld = pawnPos + baseOffset;
            desiredWorld.y = 0f;

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
                state.worldPos = Vector3.SmoothDamp(state.worldPos, desiredWorld, ref state.velocity, FollowSmoothTime, FollowMaxSpeed, dt);
            }

            Vector3 relative = state.worldPos - pawnPos;
            relative.y = 0f;
            if (relative.sqrMagnitude < 0.0001f)
            {
                relative = desiredWorld - pawnPos;
                relative.y = 0f;
            }

            float dist = relative.magnitude;
            if (dist > 0.0001f)
            {
                float clampedDist = Mathf.Clamp(dist, MinKeepDistance, MaxKeepDistance);
                relative = relative / dist * clampedDist;
                state.worldPos = pawnPos + relative;
            }
            else
            {
                relative = baseOffset;
                relative.y = 0f;
            }

            state.lastSeenTick = ticks;
            FollowStates[id] = state;

            float t = ticks + id;
            relative.y = baseOffset.y + Mathf.Sin(t * BobSpeed) * BobAmplitude;
            Vector3 droneWorldPos = parms.pawn.DrawPos + relative;
            CompApparelFollowerDrone.SetDroneVisualPoint(id, droneWorldPos, ticks);
            return relative;
        }

        private static void CleanupStaleStates(int ticks)
        {
            if (ticks % 300 != 0 || FollowStates.Count == 0)
                return;

            List<int> staleIds = null;
            foreach (KeyValuePair<int, DroneFollowState> pair in FollowStates)
            {
                if (ticks - pair.Value.lastSeenTick <= StateForgetTicks)
                    continue;
                if (staleIds == null)
                    staleIds = new List<int>();
                staleIds.Add(pair.Key);
            }

            if (staleIds == null)
                return;

            for (int i = 0; i < staleIds.Count; i++)
                FollowStates.Remove(staleIds[i]);
        }
    }
}
