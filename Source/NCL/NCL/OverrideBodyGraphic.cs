using System;
using System.Collections.Generic;
using HarmonyLib;
using System.Linq;
using System.Reflection;
using RimWorld;
using Verse;
using System.Reflection.Emit;
using UnityEngine;




namespace NCL
{
    public class HediffComp_ForceBodyType : HediffComp
    {
        // 定义可配置的属性
        public HediffCompProperties_ForceBodyType Props => (HediffCompProperties_ForceBodyType)props;

        // 存储原始身体类型以便恢复
        private BodyTypeDef originalBodyType;

        public override void CompPostMake()
        {
            base.CompPostMake();

            // 保存pawn的原始身体类型
            originalBodyType = Pawn.story.bodyType;

            // 应用新的身体类型
            if (Props.bodyType != null)
            {
                Pawn.story.bodyType = Props.bodyType;
            }

            // 通知图形更新
            PortraitsCache.SetDirty(Pawn);
            GlobalTextureAtlasManager.TryMarkPawnFrameSetDirty(Pawn);
        }

        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();

            // 恢复原始身体类型
            if (originalBodyType != null)
            {
                Pawn.story.bodyType = originalBodyType;
            }

            // 通知图形更新
            PortraitsCache.SetDirty(Pawn);
            GlobalTextureAtlasManager.TryMarkPawnFrameSetDirty(Pawn);
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Defs.Look(ref originalBodyType, "originalBodyType");
        }
    }

    // 定义HediffComp的属性类
    public class HediffCompProperties_ForceBodyType : HediffCompProperties
    {
        public BodyTypeDef bodyType;

        public HediffCompProperties_ForceBodyType()
        {
            compClass = typeof(HediffComp_ForceBodyType);
        }
    }
}




namespace NCL
{
    public class HediffComp_ForceBody : HediffComp
    {
        public HediffCompProperties_ForceBody Props => (HediffCompProperties_ForceBody)props;

        private BodyTypeDef originalBodyType;
        private BodyDef originalBodyDef;

        public override void CompPostMake()
        {
            base.CompPostMake();
            StoreOriginalState();
            ApplyNewBody();
        }

        private void StoreOriginalState()
        {
            originalBodyType = Pawn.story?.bodyType;
            originalBodyDef = Pawn.def.race.body;
        }

        private void ApplyNewBody()
        {
            // 应用新的BodyType
            if (Props.bodyType != null && Pawn.story != null)
            {
                Pawn.story.bodyType = Props.bodyType;
            }

            // 应用新的BodyDef
            if (Props.bodyDef != null)
            {
                Pawn.def.race.body = Props.bodyDef;
            }

            UpdateGraphics();
        }

        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();
            RestoreOriginalState();
        }

        private void RestoreOriginalState()
        {
            // 恢复BodyType
            if (originalBodyType != null && Pawn.story != null)
            {
                Pawn.story.bodyType = originalBodyType;
            }

            // 恢复BodyDef
            if (originalBodyDef != null)
            {
                Pawn.def.race.body = originalBodyDef;
            }

            UpdateGraphics();
        }

        private void UpdateGraphics()
        {
            // 1.5版本兼容的图形更新方式
            if (Pawn.Drawer?.renderer is PawnRenderer renderer)
            {
                renderer.SetAllGraphicsDirty();

                // 强制重建所有图形
                if (renderer.renderTree.Resolved)
                {
                    renderer.renderTree.SetDirty();
                }
            }

            // 更新肖像和纹理图集
            PortraitsCache.SetDirty(Pawn);
            GlobalTextureAtlasManager.TryMarkPawnFrameSetDirty(Pawn);
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Defs.Look(ref originalBodyType, "originalBodyType");
            Scribe_Defs.Look(ref originalBodyDef, "originalBodyDef");
        }
    }

    public class HediffCompProperties_ForceBody : HediffCompProperties
    {
        public BodyTypeDef bodyType;
        public BodyDef bodyDef;

        public HediffCompProperties_ForceBody()
        {
            compClass = typeof(HediffComp_ForceBody);
        }
    }
}
