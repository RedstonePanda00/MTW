using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace NCL
{
    public class CompMultiLegRig : ThingComp
    {
        public const string BodyPartKey = "Body";

        private float gaitPhase;
        private float moveBlend;
        private bool hasLanded;
        private int lastFootfallTick = -99999;
        private int recoilStartTick = -99999;
        private Vector3 lastDrawPos;
        private bool lastDrawPosValid;
        private bool[] legSwinging;
        private Dictionary<string, int> legIndexByKey;
        private float fireRecoilStartLevel;
        private int fireRecoilTick = -99999;
        private Vector3 fireRecoilDir = Vector3.forward;
        private float fireRecoilTwist;

        public CompProperties_MultiLegRig Props => (CompProperties_MultiLegRig)props;

        public Pawn Pawn => parent as Pawn;

        public bool HasLanded => hasLanded;

        public Vector3 BodyDrawOffset => ComputeBodyOffset(Pawn?.Rotation ?? Rot4.South);

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (!hasLanded && !respawningAfterLoad)
            {
                // Quarter phase puts both tripod groups at zero stride, matching the reference rest pose.
                gaitPhase = 0.25f;
                legSwinging = null;
            }

            hasLanded = true;
            lastDrawPosValid = false;
            EnsureLegState();
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref gaitPhase, "legRigPhase", 0f);
            Scribe_Values.Look(ref hasLanded, "legRigHasLanded", false);
        }

        public override void CompTick()
        {
            base.CompTick();
            Pawn pawn = Pawn;
            if (pawn == null || !pawn.Spawned || pawn.Dead)
            {
                return;
            }

            EnsureLegState();
            Vector3 drawPos = pawn.DrawPos;
            float distance = 0f;
            if (lastDrawPosValid)
            {
                Vector3 delta = drawPos - lastDrawPos;
                delta.y = 0f;
                distance = delta.magnitude;
                // Teleports and position snaps should not spin the gait.
                if (distance > 1.5f)
                {
                    distance = 0f;
                }
            }

            lastDrawPos = drawPos;
            lastDrawPosValid = true;

            bool moving = pawn.pather != null && pawn.pather.MovingNow && distance > 0.0001f;
            float blendStep = 1f / Mathf.Max(1, Props.moveBlendTicks);
            moveBlend = Mathf.Clamp01(moveBlend + (moving ? blendStep : -blendStep));

            // Phase only advances with real displacement; when idle, raised legs are lowered via moveBlend instead.
            if (!moving)
            {
                return;
            }

            gaitPhase = Mathf.Repeat(gaitPhase + distance / Props.cycleDistance, 1f);
            UpdateFootfalls(pawn);
        }

        public void PlayImpactRecoil()
        {
            recoilStartTick = Find.TickManager.TicksGame;
        }

        public void Notify_ShotFired(Vector3 targetPos)
        {
            Pawn pawn = Pawn;
            if (pawn == null || !pawn.Spawned)
            {
                return;
            }

            int now = Find.TickManager.TicksGame;
            // Multi-projectile verbs call TryCastShot several times per shot; one kick per tick.
            if (now == fireRecoilTick)
            {
                return;
            }

            // A follow-up shot kicks from wherever the body currently is, so bursts never pop.
            fireRecoilStartLevel = Mathf.Clamp01(FireRecoilLevelAt(now));

            Vector3 dir = targetPos - pawn.DrawPos;
            dir.y = 0f;
            fireRecoilDir = dir.sqrMagnitude > 0.0001f ? dir.normalized : ForwardFor(pawn.Rotation);

            // Shots off to one side twist the hull the other way; near-axial shots pick a random side.
            float cross = Vector3.Cross(ForwardFor(pawn.Rotation), fireRecoilDir).y;
            fireRecoilTwist = Mathf.Abs(cross) > 0.2f ? -Mathf.Sign(cross) : (Rand.Bool ? 1f : -1f);
            fireRecoilTick = now;
        }

        // 0 at rest, 1 at full kick; slightly negative during the settle wobble.
        private float FireRecoilLevelAt(int now)
        {
            int age = now - fireRecoilTick;
            if (fireRecoilTick < 0 || age < 0)
            {
                return 0f;
            }

            int attack = Mathf.Max(1, Props.fireRecoilAttackTicks);
            if (age < attack)
            {
                return Mathf.Lerp(fireRecoilStartLevel, 1f, SmoothStep(age / (float)attack));
            }

            int ret = Mathf.Max(1, Props.fireRecoilReturnTicks);
            int sinceReturn = age - attack;
            if (sinceReturn < ret)
            {
                return 1f - SmoothStep(sinceReturn / (float)ret);
            }

            float t = sinceReturn - ret;
            float decay = Mathf.Max(0.1f, Props.fireRecoilTailDecayTicks);
            if (Props.fireRecoilTailAmplitude <= 0f || Props.fireRecoilTailPeriodTicks <= 0 || t > decay * 5f)
            {
                return 0f;
            }

            return -Props.fireRecoilTailAmplitude * Mathf.Exp(-t / decay)
                * Mathf.Sin(t * Mathf.PI * 2f / Props.fireRecoilTailPeriodTicks);
        }

        public float BodyTiltAngle()
        {
            return fireRecoilTwist * Props.fireRecoilTiltAngle * FireRecoilLevelAt(Find.TickManager.TicksGame);
        }

        public Vector3 BodyScaleFactor()
        {
            float squash = Props.fireRecoilSquash * Mathf.Max(0f, FireRecoilLevelAt(Find.TickManager.TicksGame));
            if (squash <= 0f)
            {
                return Vector3.one;
            }

            // Compress along the dominant screen axis of the shot, bulge slightly across it.
            return Mathf.Abs(fireRecoilDir.x) >= Mathf.Abs(fireRecoilDir.z)
                ? new Vector3(1f - squash, 1f, 1f + squash * 0.5f)
                : new Vector3(1f + squash * 0.5f, 1f, 1f - squash);
        }

        public Vector3 GetPartOffset(string partKey, Rot4 facing)
        {
            Pawn pawn = Pawn;
            if (pawn == null || pawn.Dead)
            {
                return Vector3.zero;
            }

            if (partKey == BodyPartKey)
            {
                return ComputeBodyOffset(facing);
            }

            EnsureLegState();
            if (partKey == null || !legIndexByKey.TryGetValue(partKey, out int index))
            {
                return Vector3.zero;
            }

            LegRigLegEntry leg = Props.legs[index];
            float p = Mathf.Repeat(gaitPhase - leg.PhaseOffset, 1f);
            float lift = 0f;
            if (p < Props.swingFraction)
            {
                lift = Props.liftHeight * Mathf.Sin(Mathf.PI * (p / Props.swingFraction)) * moveBlend;
            }

            return StrideOffset(leg, facing) + new Vector3(0f, 0f, lift);
        }

        // Stride offset is kept when idle so planted feet stay put; only the lift fades out.
        private Vector3 StrideOffset(LegRigLegEntry leg, Rot4 facing)
        {
            float p = Mathf.Repeat(gaitPhase - leg.PhaseOffset, 1f);
            float swing = Props.swingFraction;
            float forwardAmount = p < swing
                ? Mathf.Lerp(-1f, 1f, SmoothStep(p / swing))
                : Mathf.Lerp(1f, -1f, (p - swing) / (1f - swing));
            return ForwardFor(facing) * (forwardAmount * Props.strideVisual);
        }

        public Vector3 FootWorldPos(LegRigLegEntry leg, Rot4 facing)
        {
            Vector2 foot = leg.FootFor(facing);
            Vector3 pos = Pawn.DrawPos + new Vector3(foot.x, 0f, foot.y) + StrideOffset(leg, facing);
            pos.y = AltitudeLayer.MoteOverhead.AltitudeFor();
            return pos;
        }

        private Vector3 ComputeBodyOffset(Rot4 facing)
        {
            Pawn pawn = Pawn;
            if (pawn == null || pawn.Dead || !pawn.Spawned)
            {
                return Vector3.zero;
            }

            int now = Find.TickManager.TicksGame;
            float sink = 0f;

            int bobAge = now - lastFootfallTick;
            if (bobAge >= 0 && bobAge < Props.bodyBobTicks)
            {
                float u = bobAge / (float)Props.bodyBobTicks;
                float curve = u < 0.2f ? SmoothStep(u / 0.2f) : 1f - SmoothStep((u - 0.2f) / 0.8f);
                sink += Props.bodyBobDepth * curve;
            }

            int recoilAge = now - recoilStartTick;
            if (recoilAge >= 0 && recoilAge < Props.recoilTicks)
            {
                float u = recoilAge / (float)Props.recoilTicks;
                float curve;
                if (u < 0.15f)
                {
                    curve = SmoothStep(u / 0.15f);
                }
                else
                {
                    float v = (u - 0.15f) / 0.85f;
                    curve = Mathf.Exp(-3.5f * v) * Mathf.Cos(v * Mathf.PI * 1.5f);
                }

                sink += Props.recoilDepth * curve;
            }

            float hover = 0f;
            if (Props.idleFloatAmplitude > 0f && Props.idleFloatPeriodTicks > 0)
            {
                float cycle = (now + pawn.thingIDNumber * 37) / (float)Props.idleFloatPeriodTicks;
                hover = Props.idleFloatAmplitude * Mathf.Sin(cycle * Mathf.PI * 2f);
            }

            float fire = FireRecoilLevelAt(now);
            sink += Props.fireRecoilSink * fire;
            Vector3 kick = -fireRecoilDir * (Props.fireRecoilKickback * fire);

            Vector3 side = facing.IsHorizontal ? new Vector3(0f, 0f, 1f) : new Vector3(1f, 0f, 0f);
            float sway = Props.bodySwayAmount * moveBlend * Mathf.Sin(gaitPhase * Mathf.PI * 2f);
            return new Vector3(0f, 0f, hover - sink) + side * sway + kick;
        }

        private void UpdateFootfalls(Pawn pawn)
        {
            bool anyLanded = false;
            for (int i = 0; i < Props.legs.Count; i++)
            {
                LegRigLegEntry leg = Props.legs[i];
                bool swinging = Mathf.Repeat(gaitPhase - leg.PhaseOffset, 1f) < Props.swingFraction;
                if (legSwinging[i] && !swinging)
                {
                    anyLanded = true;
                    SpawnFootDust(pawn, leg);
                }

                legSwinging[i] = swinging;
            }

            if (anyLanded)
            {
                lastFootfallTick = Find.TickManager.TicksGame;
                Props.footfallSound?.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
            }
        }

        private void SpawnFootDust(Pawn pawn, LegRigLegEntry leg)
        {
            Map map = pawn.Map;
            if (map == null)
            {
                return;
            }

            Vector3 foot = FootWorldPos(leg, pawn.Rotation);
            for (int i = 0; i < Props.dustPuffsPerFoot; i++)
            {
                Vector3 loc = foot + new Vector3(
                    Rand.Range(-Props.dustSpread, Props.dustSpread),
                    0f,
                    Rand.Range(-Props.dustSpread, Props.dustSpread));
                if (!loc.ShouldSpawnMotesAt(map))
                {
                    continue;
                }

                FleckMaker.ThrowDustPuffThick(loc, map, Props.dustScale.RandomInRange, Props.dustColor);
            }
        }

        private void EnsureLegState()
        {
            if (legSwinging != null && legSwinging.Length == Props.legs.Count)
            {
                return;
            }

            legSwinging = new bool[Props.legs.Count];
            legIndexByKey = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < Props.legs.Count; i++)
            {
                LegRigLegEntry leg = Props.legs[i];
                if (leg?.key != null)
                {
                    legIndexByKey[leg.key] = i;
                }

                legSwinging[i] = leg != null && Mathf.Repeat(gaitPhase - leg.PhaseOffset, 1f) < Props.swingFraction;
            }
        }

        private static Vector3 ForwardFor(Rot4 facing)
        {
            switch (facing.AsInt)
            {
                case 0:
                    return new Vector3(0f, 0f, 1f);
                case 1:
                    return new Vector3(1f, 0f, 0f);
                case 3:
                    return new Vector3(-1f, 0f, 0f);
                default:
                    return new Vector3(0f, 0f, -1f);
            }
        }

        private static float SmoothStep(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        public override List<PawnRenderNode> CompRenderNodes()
        {
            if (Props.renderNodeProperties.NullOrEmpty() || parent is not Pawn pawn)
            {
                return base.CompRenderNodes();
            }

            List<PawnRenderNode> nodes = new List<PawnRenderNode>();
            PawnRenderTree tree = pawn.Drawer?.renderer?.renderTree;
            foreach (PawnRenderNodeProperties nodeProps in Props.renderNodeProperties)
            {
                if (nodeProps?.nodeClass == null)
                {
                    continue;
                }

                try
                {
                    PawnRenderNode node = (PawnRenderNode)Activator.CreateInstance(nodeProps.nodeClass, pawn, nodeProps, tree);
                    if (node is PawnRenderNode_LegRigPart rigNode)
                    {
                        rigNode.Rig = this;
                    }

                    nodes.Add(node);
                }
                catch (Exception ex)
                {
                    Log.Error($"[NCL] Failed to create leg rig render node for {pawn}: {ex}");
                }
            }

            return nodes.Count > 0 ? nodes : base.CompRenderNodes();
        }
    }
}
