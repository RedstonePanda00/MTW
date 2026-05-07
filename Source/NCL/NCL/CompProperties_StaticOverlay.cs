using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace NCL
{
    public class CompProperties_DualOverlay : CompProperties
    {
        // 静态贴图属性
        public string staticGraphicPath;
        public Vector3 staticOffset = Vector3.zero;
        public float staticRotation = 0f;
        public float staticScale = 1f;
        public AltitudeLayer staticAltitudeLayer = AltitudeLayer.BuildingOnTop;

        // 浮动贴图属性
        public string floatingGraphicPath;
        public Vector3 floatingOffset = Vector3.zero;
        public float floatingRotation = 0f;
        public float floatingScale = 1f;
        public AltitudeLayer floatingAltitudeLayer = AltitudeLayer.MoteOverhead;

        // 浮动动画参数
        public float floatAmplitude = 0.15f;
        public float floatFrequency = 0.02f;

        public CompProperties_DualOverlay()
        {
            compClass = typeof(CompDualOverlay);
        }
    }

    public class CompDualOverlay : ThingComp
    {
        private Graphic staticGraphic;
        private Graphic floatingGraphic;

        private CompProperties_DualOverlay Props => (CompProperties_DualOverlay)props;

        public override void PostDraw()
        {
            base.PostDraw();

            // 绘制静态贴图
            DrawStaticOverlay();

            // 绘制浮动贴图
            DrawFloatingOverlay();
        }

        private void DrawStaticOverlay()
        {
            if (string.IsNullOrEmpty(Props.staticGraphicPath)) return;

            // 初始化贴图
            if (staticGraphic == null)
            {
                staticGraphic = GraphicDatabase.Get<Graphic_Single>(
                    Props.staticGraphicPath,
                    ShaderDatabase.Transparent,
                    Vector2.one * Props.staticScale,
                    Color.white
                );
            }

            // 计算位置（包含偏移）
            Vector3 drawPos = parent.DrawPos + Props.staticOffset;

            // 设置海拔高度
            drawPos.y = Altitudes.AltitudeFor(Props.staticAltitudeLayer);

            // 创建变换矩阵
            Matrix4x4 matrix = Matrix4x4.TRS(
                drawPos,
                Quaternion.AngleAxis(Props.staticRotation, Vector3.up),
                Vector3.one * Props.staticScale
            );

            // 绘制贴图
            Graphics.DrawMesh(
                MeshPool.plane10,
                matrix,
                staticGraphic.MatSingle,
                0
            );
        }

        private void DrawFloatingOverlay()
        {
            if (string.IsNullOrEmpty(Props.floatingGraphicPath)) return;

            // 初始化贴图
            if (floatingGraphic == null)
            {
                floatingGraphic = GraphicDatabase.Get<Graphic_Single>(
                    Props.floatingGraphicPath,
                    ShaderDatabase.Transparent,
                    Vector2.one * Props.floatingScale,
                    Color.white
                );
            }

            // 计算基础位置（包含偏移）
            Vector3 basePos = parent.DrawPos + Props.floatingOffset;

            // 添加浮动效果
            float verticalOffset = Mathf.Sin(Find.TickManager.TicksGame * Props.floatFrequency) * Props.floatAmplitude;
            Vector3 drawPos = new Vector3(
                basePos.x,
                Altitudes.AltitudeFor(Props.floatingAltitudeLayer),
                basePos.z + verticalOffset
            );

            // 创建变换矩阵
            Matrix4x4 matrix = Matrix4x4.TRS(
                drawPos,
                Quaternion.AngleAxis(Props.floatingRotation, Vector3.up),
                Vector3.one * Props.floatingScale
            );

            // 绘制贴图
            Graphics.DrawMesh(
                MeshPool.plane10,
                matrix,
                floatingGraphic.MatSingle,
                0
            );
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            // 为管理员添加位置调试工具
            if (DebugSettings.godMode)
            {
                // 静态贴图调整工具
                yield return new Command_Action
                {
                    defaultLabel = "Static: Offset +X",
                    action = () => Props.staticOffset.x += 0.1f
                };

                yield return new Command_Action
                {
                    defaultLabel = "Static: Offset -X",
                    action = () => Props.staticOffset.x -= 0.1f
                };

                yield return new Command_Action
                {
                    defaultLabel = "Static: Offset +Z",
                    action = () => Props.staticOffset.z += 0.1f
                };

                yield return new Command_Action
                {
                    defaultLabel = "Static: Offset -Z",
                    action = () => Props.staticOffset.z -= 0.1f
                };

                yield return new Command_Action
                {
                    defaultLabel = "Static: Rotate +15°",
                    action = () => Props.staticRotation = (Props.staticRotation + 15) % 360
                };

                // 浮动贴图调整工具
                yield return new Command_Action
                {
                    defaultLabel = "Floating: Offset +X",
                    action = () => Props.floatingOffset.x += 0.1f
                };

                yield return new Command_Action
                {
                    defaultLabel = "Floating: Offset -X",
                    action = () => Props.floatingOffset.x -= 0.1f
                };

                yield return new Command_Action
                {
                    defaultLabel = "Floating: Offset +Z",
                    action = () => Props.floatingOffset.z += 0.1f
                };

                yield return new Command_Action
                {
                    defaultLabel = "Floating: Offset -Z",
                    action = () => Props.floatingOffset.z -= 0.1f
                };

                yield return new Command_Action
                {
                    defaultLabel = "Floating: Rotate +15°",
                    action = () => Props.floatingRotation = (Props.floatingRotation + 15) % 360
                };

                // 浮动参数调整工具
                yield return new Command_Action
                {
                    defaultLabel = "Float: Amplitude +0.05",
                    action = () => Props.floatAmplitude += 0.05f
                };

                yield return new Command_Action
                {
                    defaultLabel = "Float: Amplitude -0.05",
                    action = () => Props.floatAmplitude = Mathf.Max(0, Props.floatAmplitude - 0.05f)
                };

                yield return new Command_Action
                {
                    defaultLabel = "Float: Frequency +0.005",
                    action = () => Props.floatFrequency += 0.005f
                };

                yield return new Command_Action
                {
                    defaultLabel = "Float: Frequency -0.005",
                    action = () => Props.floatFrequency = Mathf.Max(0, Props.floatFrequency - 0.005f)
                };
            }
        }

        // 数据保存
        public override void PostExposeData()
        {
            base.PostExposeData();

            // 保存调试调整后的值
            if (Scribe.mode == LoadSaveMode.PostLoadInit || Scribe.mode == LoadSaveMode.Saving)
            {
                if (DebugSettings.godMode)
                {
                    Scribe_Values.Look(ref Props.staticOffset, "staticOffset");
                    Scribe_Values.Look(ref Props.staticRotation, "staticRotation");
                    Scribe_Values.Look(ref Props.floatingOffset, "floatingOffset");
                    Scribe_Values.Look(ref Props.floatingRotation, "floatingRotation");
                    Scribe_Values.Look(ref Props.floatAmplitude, "floatAmplitude");
                    Scribe_Values.Look(ref Props.floatFrequency, "floatFrequency");
                }
            }
        }
    }
}
