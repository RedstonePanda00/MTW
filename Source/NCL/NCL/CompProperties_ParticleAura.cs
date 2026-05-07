using RimWorld;
using Verse;
using UnityEngine;
using System.Collections.Generic;

namespace NCL
{
    public class CompProperties_AuraEffect : CompProperties
    {
        // 粒子系统参数
        public int spawnIntervalTicks = 10;
        public FloatRange spawnRadius = new FloatRange(0.5f, 1.2f);
        public Vector3 offset = Vector3.zero;
        public FleckDef fleckDef;
        public int fleckCount = 1; // 新增：每次生成的Fleck数量
        public ThingDef moteDef;
        public Color particleColor = Color.white;
        public float scale = 1f;

        // Effecter 系统参数
        public EffecterDef effecterDef;
        public int effecterTriggerInterval = 30;

        public CompProperties_AuraEffect() => compClass = typeof(CompAuraEffect);
    }

    public class CompAuraEffect : ThingComp
    {
        private CompProperties_AuraEffect Props => (CompProperties_AuraEffect)props;
        private Pawn TargetPawn => parent as Pawn;

        private int ticksUntilParticleSpawn;
        private List<Mote> activeMotes = new List<Mote>();
        private Effecter effecter;
        private int ticksUntilEffecterTrigger;

        public override void Initialize(CompProperties props)
        {
            base.Initialize(props);
            ticksUntilParticleSpawn = Props.spawnIntervalTicks;

            if (Props.effecterDef != null)
            {
                effecter = Props.effecterDef.Spawn();
            }
            ticksUntilEffecterTrigger = Props.effecterTriggerInterval;
        }

        public override void CompTick()
        {
            base.CompTick();
            if (TargetPawn == null || !TargetPawn.Spawned) return;

            UpdateParticleSystem();
            UpdateEffecter();
        }

        private void UpdateParticleSystem()
        {
            if (--ticksUntilParticleSpawn <= 0)
            {
                ticksUntilParticleSpawn = Props.spawnIntervalTicks;
                SpawnParticles();
            }
            UpdateExistingMotes();
        }

        private void SpawnParticles()
        {
            Map map = TargetPawn.Map;
            if (map == null) return;

            Vector3 basePos = TargetPawn.DrawPos + Props.offset;

            // 新增：生成多个Fleck的逻辑
            if (Props.fleckDef != null)
            {
                for (int i = 0; i < Props.fleckCount; i++) // 循环生成指定数量的Fleck
                {
                    Vector3 fleckPos = basePos + GetRandomOffset();
                    FleckMaker.Static(fleckPos, map, Props.fleckDef, Props.scale);
                }
            }

            // Mote生成逻辑保持不变（单次生成）
            if (Props.moteDef != null)
            {
                Vector3 motePos = basePos + GetRandomOffset();
                if (motePos.ShouldSpawnMotesAt(map))
                {
                    Mote mote = (Mote)ThingMaker.MakeThing(Props.moteDef);
                    mote.exactPosition = motePos;
                    mote.Scale = Props.scale;
                    mote.instanceColor = Props.particleColor;

                    if (mote is MoteAttached attachedMote)
                    {
                        attachedMote.Attach(TargetPawn);
                    }
                    else
                    {
                        mote.LinkMote(TargetPawn);
                    }

                    GenSpawn.Spawn(mote, motePos.ToIntVec3(), map);
                    activeMotes.Add(mote);
                }
            }
        }

        // 新增：获取随机偏移位置的辅助方法
        private Vector3 GetRandomOffset()
        {
            return Quaternion.AngleAxis(Rand.Range(0, 360), Vector3.up) *
                   Vector3.forward *
                   Props.spawnRadius.RandomInRange;
        }

        private void UpdateExistingMotes()
        {
            activeMotes.RemoveAll(mote => mote == null || mote.Destroyed);
            foreach (Mote mote in activeMotes)
            {
                if (mote is MoteAttached) continue;
                mote.exactPosition = TargetPawn.DrawPos + Props.offset;
            }
        }

        private void UpdateEffecter()
        {
            if (effecter == null) return;

            effecter.EffectTick(TargetPawn, new TargetInfo(TargetPawn.Position, TargetPawn.Map));
            if (--ticksUntilEffecterTrigger <= 0)
            {
                ticksUntilEffecterTrigger = Props.effecterTriggerInterval;
                effecter.Trigger(TargetPawn, new TargetInfo(TargetPawn.Position, TargetPawn.Map));
            }
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);
            Cleanup();
        }

        private void Cleanup()
        {
            effecter?.Cleanup();
            effecter = null;
            activeMotes.Clear();
        }
    }

    public static class MoteExtensions
    {
        public static void LinkMote(this Mote mote, Thing target)
        {
            if (mote is MoteAttached attachedMote)
            {
                attachedMote.Attach(target);
            }
            else
            {
                mote.exactPosition = target.DrawPos;
            }
        }
    }
}
