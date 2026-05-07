using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace NCL
{
    public class CompSpawnFleckEffectOnDestroyed : ThingComp
    {
        public CompProperties_SpawnFleckEffectOnDestroyed Props =>
            (CompProperties_SpawnFleckEffectOnDestroyed)props;

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);

            // 核心条件：仅Vanish模式且地图有效时触发
            if (mode != DestroyMode.Vanish || previousMap == null)
            {
                return;
            }

            // 投射物额外验证：必须实际击中目标/地面
            if (parent is Projectile projectile && !projectile.usedTarget.IsValid)
            {
                return;
            }

            SpawnEffects(previousMap);
        }

        private void SpawnEffects(Map map)
        {
            // 初始化效果跟踪器
            MapEffectDurationTracker tracker = map.GetComponent<MapEffectDurationTracker>();
            if (tracker == null)
            {
                tracker = new MapEffectDurationTracker(map);
                map.components.Add(tracker);
            }

            // 生成Fleck粒子
            if (Props.flecks != null)
            {
                foreach (var fleckData in Props.flecks)
                {
                    for (int i = 0; i < fleckData.count; i++)
                    {
                        FleckCreationData data = new FleckCreationData
                        {
                            def = fleckData.fleckDef,
                            spawnPosition = parent.Position.ToVector3Shifted() + GetRandomOffset(fleckData.maxOffset),
                            scale = fleckData.scale * Props.globalScaleFactor,
                            rotationRate = fleckData.rotationRate,
                            velocityAngle = fleckData.velocityAngle,
                            velocitySpeed = fleckData.velocitySpeed,
                            solidTimeOverride = fleckData.solidTime,
                            ageTicksOverride = fleckData.durationTicks
                        };
                        map.flecks.CreateFleck(data);
                    }
                }
            }

            // 生成Effect效果
            if (Props.effects != null)
            {
                foreach (var effectData in Props.effects)
                {
                    for (int i = 0; i < effectData.count; i++)
                    {
                        IntVec3 spawnPos = (parent.Position.ToVector3Shifted() + GetRandomOffset(effectData.maxOffset)).ToIntVec3();
                        Effecter effecter = effectData.effectDef.Spawn();
                        effecter.Trigger(new TargetInfo(spawnPos, map), new TargetInfo(spawnPos, map));

                        if (effectData.durationTicks > 0)
                        {
                            tracker.AddEffecterForDuration(effecter, effectData.durationTicks);
                        }
                        else
                        {
                            effecter.Cleanup();
                        }
                    }
                }
            }
        }

        private Vector3 GetRandomOffset(float maxOffset)
        {
            return maxOffset <= 0f ? Vector3.zero : new Vector3(
                Rand.Range(-maxOffset, maxOffset),
                0f,
                Rand.Range(-maxOffset, maxOffset));
        }
    }

    public class MapEffectDurationTracker : MapComponent
    {
        private List<EffecterDuration> activeEffects = new List<EffecterDuration>();

        public MapEffectDurationTracker(Map map) : base(map) { }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            for (int i = activeEffects.Count - 1; i >= 0; i--)
            {
                if (activeEffects[i].Tick())
                {
                    activeEffects.RemoveAt(i);
                }
            }
        }

        public void AddEffecterForDuration(Effecter effecter, int durationTicks)
        {
            activeEffects.Add(new EffecterDuration(effecter, durationTicks));
        }

        private class EffecterDuration
        {
            public Effecter effecter;
            public int ticksRemaining;

            public EffecterDuration(Effecter effecter, int durationTicks)
            {
                this.effecter = effecter;
                this.ticksRemaining = durationTicks;
            }

            public bool Tick()
            {
                if (--ticksRemaining <= 0)
                {
                    effecter.Cleanup();
                    return true;
                }
                return false;
            }
        }
    }

    public class CompProperties_SpawnFleckEffectOnDestroyed : CompProperties
    {
        public List<FleckData> flecks;
        public List<EffectData> effects;
        public float globalScaleFactor = 1.0f;

        public CompProperties_SpawnFleckEffectOnDestroyed()
        {
            compClass = typeof(CompSpawnFleckEffectOnDestroyed);
        }
    }

    public class FleckData
    {
        public FleckDef fleckDef;
        public int count = 1;
        public float scale = 1.0f;
        public float maxOffset = 0.2f;
        public float rotationRate;
        public float velocityAngle;
        public float velocitySpeed;
        public float solidTime = -1f;
        public int durationTicks = -1;
    }

    public class EffectData
    {
        public EffecterDef effectDef;
        public int count = 1;
        public float maxOffset = 0f;
        public int durationTicks = -1;
    }
}
