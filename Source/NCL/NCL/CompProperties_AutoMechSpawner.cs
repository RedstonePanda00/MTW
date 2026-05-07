using System;
using System.Collections.Generic;
using System.Reflection;
using Verse;
using Verse.AI;
using static System.Collections.Specialized.BitVector32;
using UnityEngine; // 添加这个命名空间引用
using System.Reflection;

namespace RimWorld
{
    /* 自动机械生成器组件属性 */
    public class CompProperties_AutoMechSpawner : CompProperties
    {
        public CompProperties_AutoMechSpawner()
        {
            compClass = typeof(CompAutoMechSpawner);
        }
    }

    /* 自动机械生成器组件 */
    public class CompAutoMechSpawner : ThingComp
    {
        // 自动生成标记
        public bool autoSpawn = false;

        // 缓存关联的机械载体组件
        private CompMechCarrier mechCarrier;

        // 每帧检查间隔（避免每帧都检查）
        private const int CheckInterval = 60; // 约1秒

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            // 获取关联的机械载体组件
            mechCarrier = parent.GetComp<CompMechCarrier>();
        }

        // 核心自动生成逻辑
        public override void CompTick()
        {
            base.CompTick();

            // 只在固定间隔检查
            if (Find.TickManager.TicksGame % CheckInterval != 0)
                return;

            // 检查自动生成条件
            if (autoSpawn &&
                mechCarrier != null &&
                ShouldAutoSpawn())
            {
                // 调用原版方法生成机械单位
                mechCarrier.TrySpawnPawns();
            }
        }

        // 自动生成条件检查
        private bool ShouldAutoSpawn()
        {
            // 1. 检查宿主状态
            if (parent is Pawn pawn)
            {
                if (pawn.DestroyedOrNull() ||
                    !pawn.Spawned ||
                    pawn.Downed ||
                    pawn.Dead ||
                    !pawn.Awake())
                {
                    return false;
                }
            }

            // 2. 检查冷却时间（通过反射获取私有字段）
            FieldInfo cooldownField = typeof(CompMechCarrier).GetField("cooldownTicksRemaining",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (cooldownField != null)
            {
                int cooldown = (int)cooldownField.GetValue(mechCarrier);
                if (cooldown > 0) return false;
            }

            // 3. 检查资源是否充足（通过反射获取私有方法）
            PropertyInfo maxCanSpawnProperty = typeof(CompMechCarrier).GetProperty("MaxCanSpawn");
            if (maxCanSpawnProperty != null)
            {
                int maxCanSpawn = (int)maxCanSpawnProperty.GetValue(mechCarrier);
                return maxCanSpawn > 0;
            }

            return false;
        }

        // UI控制 - 添加自动生成开关
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            // 只在玩家阵营显示
            if (parent.Faction != Faction.OfPlayer) yield break;

            // 确保有机械载体组件
            if (mechCarrier == null) yield break;

            // 添加自动生成开关
            yield return new Command_Toggle
            {
                icon = ContentFinder<Texture2D>.Get("ModIcon/CompAutoMechSpawner"),
                defaultLabel = "AutoReleaseMechs".Translate(),
                defaultDesc = "AutoReleaseMechsDesc".Translate(),
                isActive = () => autoSpawn,
                toggleAction = () => autoSpawn = !autoSpawn
            };

            // 开发工具
            if (Prefs.DevMode)
            {
                yield return new Command_Action
                {
                    defaultLabel = "DEV: Toggle AutoSpawn",
                    action = () => autoSpawn = !autoSpawn
                };
            }
        }

        // 存储系统
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref autoSpawn, "autoSpawn", false);
        }
    }
}
