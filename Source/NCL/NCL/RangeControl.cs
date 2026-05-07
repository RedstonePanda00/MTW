using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Reflection;
using Verse;

namespace NCL
{
    // 核心组件：使拥有此组件的机械体无视指挥范围限制
    public class CompIgnoreCommandRange : ThingComp
    {
        public virtual bool InIgnoreCommandRange => true;
    }

    // 组件属性
    public class CompProperties_IgnoreCommandRange : CompProperties
    {
        public CompProperties_IgnoreCommandRange()
        {
            compClass = typeof(CompIgnoreCommandRange);
        }
    }

    // Harmony 补丁 - 修复地图判断版本
    [HarmonyPatch(typeof(MechanitorUtility), "InMechanitorCommandRange")]
    internal class Patch_CommanderControlRange
    {
        private static void Postfix(Pawn mech, LocalTargetInfo target, ref bool __result)
        {
            // 如果原始结果已为true，直接跳过
            if (__result) return;

            // 基础检查
            if (mech?.def == null || target == null) return;

            // 1. 自身检查：如果机械体带有组件，无视范围限制
            if (mech.TryGetComp<CompIgnoreCommandRange>() != null)
            {
                __result = true;
                return;
            }

            // 2. 获取机械体地图
            Map mechMap = mech.Map;
            if (mechMap == null) return;

            // 3. 获取控制者
            Pawn overseer = mech.GetOverseer();
            if (overseer?.mechanitor?.OverseenPawns == null) return;

            // 4. 搜索指挥官机械体
            foreach (Pawn commander in overseer.mechanitor.OverseenPawns)
            {
                // 跳过无效指挥官
                if (commander == null || !commander.Spawned) continue;

                // 5. 关键修复：直接比较地图引用
                if (commander.Map != mechMap) continue;

                // 6. 检查指挥官组件
                if (commander.TryGetComp<CompIgnoreCommandRange>() == null) continue;

                // 7. 双重距离检查 (机械体-指挥官 和 目标-指挥官)
                float mechToCommander = mech.Position.DistanceTo(commander.Position);
                float targetToCommander = commander.Position.DistanceTo(target.Cell);

                if (mechToCommander <= 1f && targetToCommander <= 1f)
                {
                    __result = true;
                    return;
                }
            }
        }
    }

    // NCL 主 Mod 类
    public class NCL_Mod : Mod
    {
        public static Harmony harmony;

        public NCL_Mod(ModContentPack content) : base(content)
        {
            Log.Message("NCL Mod: Initializing");

            // 只创建Harmony实例（如果不存在）
            if (harmony == null)
            {
                harmony = new Harmony("com.yourname.NCL");
                Log.Message("NCL Mod: Harmony instance created");
            }
        }
    }

    // 静态初始化器确保补丁在游戏启动时应用
    [StaticConstructorOnStartup]
    public static class NCL_Initializer
    {
        static NCL_Initializer()
        {
            try
            {
                Log.Message("NCL Mod: Applying Harmony patches");

                if (NCL_Mod.harmony == null)
                    NCL_Mod.harmony = new Harmony("com.yourname.NCL");

                NCL_Mod.harmony.PatchAll(typeof(NCL_Mod).Assembly);
                Log.Message("NCL Mod: Harmony PatchAll completed");
            }
            catch (Exception ex)
            {
                Log.Error($"NCL Mod: Failed to apply Harmony patches: {ex}");
            }
        }
    }
}
