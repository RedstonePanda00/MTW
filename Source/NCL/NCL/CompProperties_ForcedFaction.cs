using RimWorld;
using Verse;

namespace NCL
{
    // 组件属性
    public class CompProperties_ForceFaction : CompProperties
    {
        public FactionDef factionDef; // 强制设置的阵营Def

        public CompProperties_ForceFaction()
        {
            compClass = typeof(CompForceFaction);
        }
    }

    // 组件实现
    public class CompForceFaction : ThingComp
    {
        private CompProperties_ForceFaction Props => (CompProperties_ForceFaction)props;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);

            // 只有在建筑完全建造后才设置阵营
            if (parent is Blueprint || parent is Frame)
                return;

            ApplyFaction();
        }

        public override void ReceiveCompSignal(string signal)
        {
            base.ReceiveCompSignal(signal);

            // 建筑完成时设置阵营
            if (signal == "SpawnedBuilding")
            {
                ApplyFaction();
            }
        }

        private void ApplyFaction()
        {
            // 如果已经有正确的阵营，不做任何事
            if (parent.Faction?.def == Props.factionDef)
                return;

            // 获取阵营
            Faction faction = Find.FactionManager.FirstFactionOfDef(Props.factionDef);

            // 设置阵营
            if (faction != null)
            {
                parent.SetFaction(faction);
            }
        }
    }
}
