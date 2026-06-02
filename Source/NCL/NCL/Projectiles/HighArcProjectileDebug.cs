using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace NCL.Projectiles
{
    /// <summary>Optional diagnostics for high-arc projectiles. Off by default; set <see cref="Enabled"/> true to debug.</summary>
    public static class HighArcProjectileDebug
    {
        public const string LogPrefix = "[NCL HighArc]";

        /// <summary>Set true in debug builds or via dev console to emit [NCL HighArc] logs.</summary>
        public static bool Enabled;

        public static bool VerboseTicks;

        public static float ApexWindowMin = 0.45f;
        public static float ApexWindowMax = 0.55f;

        private static readonly Dictionary<int, ProjectileDebugState> States = new();

        public static bool IsEnabledFor(Thing thing) => Enabled && thing != null;

        public static void Message(string text)
        {
            if (!Enabled)
            {
                return;
            }

            Log.Message($"{LogPrefix} {text}");
        }

        public static void Warning(string text)
        {
            if (!Enabled)
            {
                return;
            }

            Log.Warning($"{LogPrefix} {text}");
        }

        public static ProjectileDebugState GetState(Thing thing)
        {
            int id = thing.thingIDNumber;
            if (!States.TryGetValue(id, out ProjectileDebugState state))
            {
                state = new ProjectileDebugState();
                States[id] = state;
            }

            return state;
        }

        public static void ClearState(Thing thing)
        {
            if (thing != null)
            {
                States.Remove(thing.thingIDNumber);
            }
        }

        public static bool InApexWindow(float progress) =>
            progress >= ApexWindowMin && progress <= ApexWindowMax;

        public static int BeginScope(Thing thing, string scope)
        {
            if (!IsEnabledFor(thing))
            {
                return 0;
            }

            ProjectileDebugState state = GetState(thing);
            state.depth++;
            state.TrackDepth(state.depth);
            int depth = state.depth;
            if (depth > 8)
            {
                Warning($"{Label(thing)} REENTRANT depth={depth} scope={scope} — possible infinite loop");
            }
            else if (depth > 1)
            {
                Message($"{Label(thing)} reenter depth={depth} scope={scope}");
            }

            return depth;
        }

        public static void EndScope(Thing thing, string scope)
        {
            if (!IsEnabledFor(thing))
            {
                return;
            }

            ProjectileDebugState state = GetState(thing);
            if (state.depth > 0)
            {
                state.depth--;
            }

            if (state.depth == 0 && state.pendingApexSummary)
            {
                FlushApexSummary(thing, state);
            }
        }

        public static void LogLaunch(
            Projectile_HighArcExplosiveBase proj,
            float startingTicksToImpact,
            int ticksToImpact,
            Vector3 origin,
            Vector3 destination)
        {
            if (!IsEnabledFor(proj))
            {
                return;
            }

            ProjectileDebugState state = GetState(proj);
            state.depth = 0;
            state.tickLogCount = 0;
            state.drawLogCount = 0;
            state.apexLogged = false;

            float z0 = origin.Yto0().z;
            float z1 = destination.Yto0().z;
            float zApex = HighArcTrajectory.ResolveApexZ(z0, z1, proj.def);

            Message(
                $"{Label(proj)} Launch def={proj.def.defName} ticksToImpact={ticksToImpact} startingTicks={startingTicksToImpact:F1} " +
                $"origin={V3(origin)} dest={V3(destination)} " +
                $"z0={z0:F2} z1={z1:F2} zApex={zApex:F2} lift={zApex - (z0 + z1) * 0.5f:F2}");
        }

        public static void LogTick(
            Projectile_HighArcExplosiveBase proj,
            int delta,
            float arcProgress,
            float startingTicksToImpact,
            int ticksToImpact)
        {
            if (!IsEnabledFor(proj))
            {
                return;
            }

            ProjectileDebugState state = GetState(proj);
            state.tickLogCount++;
            float t = arcProgress;

            bool apex = InApexWindow(t);
            if (apex && !state.apexLogged)
            {
                state.pendingApexSummary = true;
            }

            if (!VerboseTicks && !apex && state.tickLogCount % 60 != 1)
            {
                return;
            }

            Vector3 exact = proj.ExactPosition;
            Vector3 draw = proj.DrawPos;
            float arcZ = draw.z - exact.z;

            string line =
                $"{Label(proj)} Tick#{state.tickLogCount} d={delta} t={t:F4} ticksLeft={ticksToImpact} " +
                $"startTicks={startingTicksToImpact:F1} exact={V3(exact)} draw={V3(draw)} arcZ={arcZ:F2} cell={proj.Position}";

            if (apex)
            {
                Warning(line + " <<APEX>>");
            }
            else
            {
                Message(line);
            }
        }

        public static void LogDrawAt(Projectile_HighArcExplosiveBase proj, Vector3 drawLoc, float arcProgress)
        {
            if (!IsEnabledFor(proj))
            {
                return;
            }

            ProjectileDebugState state = GetState(proj);
            state.drawLogCount++;
            float t = arcProgress;
            bool apex = InApexWindow(t);

            if (!apex && state.drawLogCount % 30 != 1)
            {
                return;
            }

            Vector3 exact = proj.ExactPosition;
            string line =
                $"{Label(proj)} DrawAt#{state.drawLogCount} t={t:F4} drawLoc={V3(drawLoc)} exact={V3(exact)} " +
                $"deltaDrawZ={(drawLoc.z - exact.z):F2} useGraphic={proj.def.projectile.useGraphicClass}";

            if (apex)
            {
                Warning(line + " <<APEX>>");
            }
            else
            {
                Message(line);
            }
        }

        public static void LogDrawPosGet(Projectile_HighArcExplosiveBase proj, float arcProgress)
        {
            if (!IsEnabledFor(proj))
            {
                return;
            }

            float t = arcProgress;
            if (!InApexWindow(t))
            {
                return;
            }

            ProjectileDebugState state = GetState(proj);
            state.drawPosGetsAtApex++;
            if (state.drawPosGetsAtApex <= 5 || state.drawPosGetsAtApex % 50 == 0)
            {
                Message($"{Label(proj)} DrawPos getter at apex (#{state.drawPosGetsAtApex}) t={t:F4}");
            }
        }

        public static void LogImpact(Projectile_HighArcExplosiveBase proj, Thing hitThing, float arcProgress)
        {
            if (!IsEnabledFor(proj))
            {
                return;
            }

            Message($"{Label(proj)} Impact hit={(hitThing?.LabelCap ?? "null")} t={arcProgress:F4} cell={proj.Position}");
            ClearState(proj);
        }

        public static void LogSmoke(Projectile_HighArcExplosiveBase proj, Vector3 smokePos, float arcProgress)
        {
            if (!IsEnabledFor(proj))
            {
                return;
            }

            ProjectileDebugState state = GetState(proj);
            state.smokeLogCount++;
            if (state.smokeLogCount % 15 != 1 && !InApexWindow(arcProgress))
            {
                return;
            }

            Vector3 exact = proj.ExactPosition;
            Message(
                $"{Label(proj)} Smoke#{state.smokeLogCount} t={arcProgress:F4} smokePos={V3(smokePos)} " +
                $"drawPos={V3(proj.DrawPos)} exact={V3(exact)} smokeZ-exactZ={(smokePos.z - exact.z):F2}");
        }

        private static void FlushApexSummary(Thing thing, ProjectileDebugState state)
        {
            state.apexLogged = true;
            state.pendingApexSummary = false;
            Message(
                $"{Label(thing)} Apex summary: drawGets={state.drawPosGetsAtApex} tickLogs={state.tickLogCount} " +
                $"drawLogs={state.drawLogCount} maxDepth={state.maxDepthRecorded}");
        }

        private static string Label(Thing thing) => $"{thing.def.defName} id={thing.thingIDNumber}";

        private static string V3(Vector3 v) => $"({v.x:F2},{v.y:F2},{v.z:F2})";

        public class ProjectileDebugState
        {
            public int depth;
            public int maxDepthRecorded;
            public int tickLogCount;
            public int drawLogCount;
            public int drawPosGetsAtApex;
            public int smokeLogCount;
            public bool apexLogged;
            public bool pendingApexSummary;

            public void TrackDepth(int depth)
            {
                if (depth > maxDepthRecorded)
                {
                    maxDepthRecorded = depth;
                }
            }
        }
    }
}
