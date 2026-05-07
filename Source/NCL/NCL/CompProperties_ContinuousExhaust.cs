using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace NCL
{

[StaticConstructorOnStartup] 
    public class CompContinuousExhaust : ThingComp
    {
        // 组件属性
        public CompProperties_ContinuousExhaust Props => (CompProperties_ContinuousExhaust)props;

        // 尾焰材质
        private static readonly Material exhaustMaterial = MaterialPool.MatFrom("Things/Mote/Smoke",
            ShaderDatabase.MoteGlow,
            new Color(1f, 0.7f, 0.2f));

        // 动态粒子效果计数器
        private int fleckCounter;

        /// <summary>
        /// 每Tick更新
        /// </summary>
        public override void CompTick()
        {
            base.CompTick();

            // 只在父物体存在且激活时产生效果
            if (parent == null || parent.Destroyed || !parent.Spawned)
                return;

            // 更新粒子计数器
            fleckCounter++;

            // 每指定Tick产生一次粒子效果
            if (fleckCounter >= Props.fleckInterval)
            {
                fleckCounter = 0;
                EmitExhaustFleck();
            }
        }

        /// <summary>
        /// 生成尾焰粒子 - 优化烟雾显示
        /// </summary>
        private void EmitExhaustFleck()
        {
            // 确保地图有效
            if (parent.Map == null || Props.fleckTypes.Count == 0)
                return;

            // 计算位置
            Vector3 position = parent.DrawPos;

            // 应用偏移
            if (Props.offsetDirection != Vector3.zero)
            {
                position += Props.offsetDirection * Props.offsetDistance;
            }

            // 为每种粒子类型单独创建粒子
            foreach (var fleckDef in Props.fleckTypes)
            {
                // 设置不同粒子的高度
                float altitude = fleckDef == FleckDefOf.Smoke ?
                    AltitudeLayer.FogOfWar.AltitudeFor() : // 烟雾在较低高度
                    AltitudeLayer.MoteOverhead.AltitudeFor(); // 其他粒子在较高高度

                Vector3 fleckPos = new Vector3(position.x, altitude, position.z);

                // 烟雾使用不同的缩放比例
                float scale = fleckDef == FleckDefOf.Smoke ?
                    Props.smokeScale.RandomInRange :
                    Props.fleckScale.RandomInRange;

                // 烟雾使用不同的速度
                float speed = fleckDef == FleckDefOf.Smoke ?
                    Props.smokeSpeedRange.RandomInRange :
                    Props.fleckSpeedRange.RandomInRange;

                // 烟雾使用不同的角度范围
                float angle = fleckDef == FleckDefOf.Smoke ?
                    Props.smokeAngleRange.RandomInRange :
                    Props.fleckAngleRange.RandomInRange;

                // 生成粒子数据
                FleckCreationData data = FleckMaker.GetDataStatic(
                    fleckPos,
                    parent.Map,
                    fleckDef,
                    scale);

                // 设置粒子参数
                data.velocityAngle = angle;
                data.velocitySpeed = speed;
                data.rotation = Props.fleckRotationRange.RandomInRange;
                data.rotationRate = Props.fleckRotationRate.RandomInRange;

                // 创建粒子
                parent.Map.flecks.CreateFleck(data);
            }
        }

        /// <summary>
        /// 绘制静态尾焰
        /// </summary>
        public override void PostDraw()
        {
            base.PostDraw();

            // 只在父物体存在且激活时绘制
            if (parent == null || parent.Destroyed || !parent.Spawned)
                return;

            // 计算绘制位置和旋转
            Vector3 drawPos = parent.DrawPos;
            drawPos.y = AltitudeLayer.FogOfWar.AltitudeFor(); // 降低高度

            // 应用偏移
            Vector3 offset = Props.offsetDirection * Props.offsetDistance;
            drawPos += offset;

            // 计算旋转角度
            Quaternion rotation = Quaternion.identity;
            if (Props.offsetDirection != Vector3.zero)
            {
                rotation = Quaternion.LookRotation(Props.offsetDirection);
            }
            else if (parent is Pawn pawn && pawn.pather.Moving)
            {
                // 如果是移动中的Pawn，则朝向移动的反方向
                rotation = Quaternion.LookRotation(-pawn.pather.nextCell.ToVector3());
            }

            // 绘制尾焰
            Graphics.DrawMesh(
                MeshPool.plane10,
                drawPos,
                rotation,
                exhaustMaterial,
                0);
        }
    }

    /// <summary>
    /// 持续尾焰组件属性 - 添加烟雾专用参数
    /// </summary>
    public class CompProperties_ContinuousExhaust : CompProperties
    {
        // 粒子效果类型
        public List<FleckDef> fleckTypes = new List<FleckDef>() {
            FleckDefOf.Smoke,        // 烟雾
            FleckDefOf.FireGlow,     // 火焰光芒
            FleckDefOf.MicroSparks   // 火花
        };

        // 普通粒子大小范围
        public FloatRange fleckScale = new FloatRange(0.3f, 0.7f);

        // 烟雾粒子大小范围（通常比普通粒子大）
        public FloatRange smokeScale = new FloatRange(0.8f, 1.5f);

        // 普通粒子角度范围
        public FloatRange fleckAngleRange = new FloatRange(0f, 360f);

        // 烟雾粒子角度范围（更集中）
        public FloatRange smokeAngleRange = new FloatRange(-30f, 30f);

        // 普通粒子速度范围
        public FloatRange fleckSpeedRange = new FloatRange(0.05f, 0.15f);

        // 烟雾粒子速度范围（更慢）
        public FloatRange smokeSpeedRange = new FloatRange(0.01f, 0.03f);

        // 粒子旋转范围
        public FloatRange fleckRotationRange = new FloatRange(0f, 360f);

        // 粒子旋转速率范围
        public FloatRange fleckRotationRate = new FloatRange(-30f, 30f);

        // 粒子生成间隔（Tick）
        public int fleckInterval = 1;

        // 尾焰偏移方向（相对于物体朝向）
        public Vector3 offsetDirection = Vector3.back;

        // 尾焰偏移距离
        public float offsetDistance = 0.5f;

        public CompProperties_ContinuousExhaust()
        {
            compClass = typeof(CompContinuousExhaust);
        }
    }
}
