using HarmonyLib;
using RimWorld;
using System.Linq;
using Verse;

namespace NCL
{
    // 定义 ModExtension 类
    public class HediffExtension_NoNotification : DefModExtension
    {
        // 不需要额外字段，仅作为标记
    }

    [HarmonyPatch(typeof(LetterStack))]
    [HarmonyPatch("ReceiveLetter")]
    public static class Patch_LetterStack_ReceiveLetter
    {
        // 指定要拦截的信件类型
        private static readonly LetterDef[] NotificationDefs = {
            LetterDefOf.NegativeEvent,
            LetterDefOf.Death,
 
        };

        public static bool Prefix(Letter letter)
        {
            // 检查是否是需要拦截的通知类型
            if (NotificationDefs.Contains(letter.def))
            {
                // 尝试从信件文本中提取Pawn
                Pawn affectedPawn = TryGetAffectedPawn(letter);

                if (affectedPawn != null && affectedPawn.IsColonist)
                {
                    // 检查是否有我们的 ModExtension
                    if (HasNoNotificationHediff(affectedPawn))
                    {
                        // 调试日志
                        // Log.Message($"阻止通知: {letter.label} - 目标: {affectedPawn.Label}");
                        return false; // 阻止邮件发送
                    }
                }
            }
            return true;
        }

        // 尝试从信件中提取受影响的Pawn
        private static Pawn TryGetAffectedPawn(Letter letter)
        {
            // 方法1: 从信件目标获取
            if (letter.lookTargets != null && letter.lookTargets.PrimaryTarget.Thing is Pawn directTarget)
            {
                return directTarget;
            }


            return null;
        }

        private static bool HasNoNotificationHediff(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null)
                return false;

            foreach (var hediff in pawn.health.hediffSet.hediffs)
            {
                if (hediff?.def?.GetModExtension<HediffExtension_NoNotification>() != null)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
