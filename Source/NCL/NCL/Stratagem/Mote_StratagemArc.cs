using UnityEngine;
using Verse;

namespace NCL.Stratagem
{
    public class Mote_StratagemArc : Mote
    {
        private Vector3 startPos;
        private Vector3 endPos;
        private float arcHeightZ;
        private int durationTicks;
        private int startTick;

        public void Initialize(Vector3 start, Vector3 end, float arcHeight, int durationTicks, Color color)
        {
            startPos = start;
            endPos = end;
            arcHeightZ = arcHeight;
            this.durationTicks = Mathf.Max(1, durationTicks);
            startTick = Find.TickManager.TicksGame;
            instanceColor = color;
            exactPosition = startPos;
            // Match mote lifespan to arc duration; base Tick() uses def.mote.Lifespan via TimeInterval.
            solidTimeOverride = this.durationTicks / 60f + 0.25f;
        }

        protected override void Tick()
        {
            base.Tick();

            int elapsed = Find.TickManager.TicksGame - startTick;
            if (elapsed >= durationTicks)
            {
                Destroy();
                return;
            }

            float t = elapsed / (float)durationTicks;
            exactPosition = EvaluateArcPoint(startPos, endPos, arcHeightZ, t);
        }

        public static Vector3 EvaluateArcPoint(Vector3 start, Vector3 end, float arcHeightZ, float t)
        {
            Vector3 pos = Vector3.Lerp(start, end, t);
            pos.z += Mathf.Sin(t * Mathf.PI) * arcHeightZ;
            return pos;
        }
    }
}
