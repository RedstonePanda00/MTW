using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace NCL
{
    public class CompTransformable : ThingComp
    {
        public CompProperties_Transformable Props => (CompProperties_Transformable)props;

        private Gizmo transformGizmo;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);

            // 使用 ContentFinder 加载贴图
            Texture2D gizmoIcon = ContentFinder<Texture2D>.Get(Props.gizmoIconPath, false);

            transformGizmo = new Command_Action
            {
                icon = gizmoIcon, // 使用加载的贴图
                defaultLabel = Props.gizmoLabel,
                defaultDesc = Props.gizmoDescription,
                action = TransformBuilding
            };
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }

            if (parent.Faction == Faction.OfPlayer)
            {
                yield return transformGizmo;
            }
        }

        private void TransformBuilding()
        {
            if (Props.targetBuildingDef == null)
            {
                Log.Error("Target building def is not set in CompProperties_Transformable");
                return;
            }

            // 获取当前建筑的位置和旋转
            IntVec3 position = parent.Position;
            Map map = parent.Map;
            Rot4 rotation = parent.Rotation;
            Faction faction = parent.Faction;

            // 销毁当前建筑
            parent.Destroy();

            // 创建新建筑
            Thing newBuilding = ThingMaker.MakeThing(Props.targetBuildingDef);
            newBuilding.SetFactionDirect(faction);
            GenSpawn.Spawn(newBuilding, position, map, rotation);

            // 可选: 显示效果
            if (Props.transformationEffect != null)
            {
                Props.transformationEffect.Spawn(position, map);
            }
        }
    }

    public class CompProperties_Transformable : CompProperties
    {
        public ThingDef targetBuildingDef;
        public string gizmoLabel = "Transform Building";
        public string gizmoDescription = "Transform this building into another form";

        // 使用 ContentFinder 加载 PNG 贴图
        public string gizmoIconPath; // 贴图路径（如 "UI/Buttons/Transform"）
        public EffecterDef transformationEffect;

        public CompProperties_Transformable()
        {
            compClass = typeof(CompTransformable);
        }
    }
}