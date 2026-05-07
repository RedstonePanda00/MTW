using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using Verse;

namespace NCL_Storyteller
{
    [HarmonyPatch(typeof(StorytellerUtility))]
    internal static class Patch_StorytellerUtility
    {
        [HarmonyPatch(nameof(StorytellerUtility.DefaultThreatPointsNow))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(
            IEnumerable<CodeInstruction> instructions,
            ILGenerator generator)
        {
            List<CodeInstruction> list = instructions.ToList();
            for (int index = 0; index < list.Count; index++)
            {
                if (list[index].opcode == OpCodes.Ldc_R4 && list[index].OperandIs(0f) &&
                    index + 1 < list.Count && list[index + 1].opcode == OpCodes.Stloc_3)
                {
                    list.RemoveAt(index);
                    list.InsertRange(index, new[]
                    {
                        new CodeInstruction(OpCodes.Ldloc_1),
                        new CodeInstruction(OpCodes.Call, NCL_StorytellerUtility.GetAdditionWealthCurveValue_Method)
                    });
                }

                if (list[index].opcode == OpCodes.Ldc_R4 && list[index].OperandIs(10000f))
                    GenCollection.Replace(list, list[index],
                        new CodeInstruction(OpCodes.Call, NCL_StorytellerUtility.MaxDefaultThreatPointsNow_Method));
            }
            return list;
        }
    }
}
