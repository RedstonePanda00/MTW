using RimWorld;
using System.Collections.Generic;
using Verse;

namespace NCL
{
    public class CompProperties_SpawnEffectOnDestroy : CompProperties
    {
        public string effectDefName;
        public float effectSize = 1f;
        public int durationTicks = 60;

        public CompProperties_SpawnEffectOnDestroy()
        {
            compClass = typeof(Comp_SpawnEffectOnDestroy);
        }
    }

    public class Comp_SpawnEffectOnDestroy : ThingComp
    {
        private CompProperties_SpawnEffectOnDestroy Props =>
            (CompProperties_SpawnEffectOnDestroy)props;

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);

            if (previousMap == null ||
                Current.ProgramState != ProgramState.Playing)
                return;

            EffecterDef effectDef = DefDatabase<EffecterDef>.GetNamedSilentFail(Props.effectDefName);
            if (effectDef == null)
            {
                Log.Warning($"特效定义未找到: {Props.effectDefName}");
                return;
            }

            MapEffecterTracker tracker = previousMap.GetComponent<MapEffecterTracker>();
            if (tracker == null)
            {
                tracker = new MapEffecterTracker(previousMap);
                previousMap.components.Add(tracker);
            }

            Effecter effect = effectDef.Spawn();
            effect.scale = Props.effectSize;
            TargetInfo target = new TargetInfo(parent.Position, previousMap);

            effect.Trigger(target, target);
            tracker.AddEffecter(effect, target, Props.durationTicks);
        }
    }
}

namespace NCL
{
    public class MapEffecterTracker : MapComponent
    {
        private List<ManagedEffecter> activeEffecters = new List<ManagedEffecter>();

        public MapEffecterTracker(Map map) : base(map)
        {
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();

            for (int i = activeEffecters.Count - 1; i >= 0; i--)
            {
                if (!activeEffecters[i].Tick())
                {
                    activeEffecters.RemoveAt(i);
                }
            }
        }

        public void AddEffecter(Effecter effecter, TargetInfo target, int duration)
        {
            activeEffecters.Add(new ManagedEffecter(effecter, target, duration));
        }

        private class ManagedEffecter
        {
            public Effecter effecter;
            public TargetInfo target;
            public int ticksLeft;

            public ManagedEffecter(Effecter effecter, TargetInfo target, int duration)
            {
                this.effecter = effecter;
                this.target = target;
                this.ticksLeft = duration;
            }

            public bool Tick()
            {
                if (ticksLeft <= 0) return false;

                ticksLeft--;
                effecter.EffectTick(target, target);

                if (ticksLeft <= 0)
                {
                    effecter.Cleanup();
                    return false;
                }

                return true;
            }
        }
    }
}
