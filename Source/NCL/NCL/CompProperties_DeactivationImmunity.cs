using RimWorld;
using System;
using System.Linq;
using System.Reflection;
using Verse;
using Verse.AI;

namespace NCL
{
    public class CompProperties_MechanoidShield : CompProperties
    {
        // 改为使用翻译键而不是直接翻译
        public string reactivateMessageKey = "MechShield_Reactivated";
        public int checkIntervalTicks = 30;

        public CompProperties_MechanoidShield()
        {
            compClass = typeof(CompMechanoidShield);
        }
    }
}


namespace NCL
{
    public class CompMechanoidShield : ThingComp
    {
        private static readonly FieldInfo deactivatedField =
            typeof(CompMechanoid).GetField("deactivated", BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly FieldInfo activeField =
            typeof(CompMechanoid).GetField("active", BindingFlags.NonPublic | BindingFlags.Instance);

        private CompProperties_MechanoidShield Props =>
            (CompProperties_MechanoidShield)props;

        public override void CompTick()
        {
            base.CompTick();

            if (!(parent is Pawn pawn) || !pawn.Spawned || !pawn.HostileTo(Faction.OfPlayer))
                return;

            if (Find.TickManager.TicksGame % Props.checkIntervalTicks != 0)
                return;

            var mechanoidComp = pawn.TryGetComp<CompMechanoid>();
            if (mechanoidComp == null) return;

            if (IsDeactivated(mechanoidComp))
            {
                ReactivateMech(pawn, mechanoidComp);
            }

            if (pawn.Downed)
            {
                UndownMech(pawn);
            }
        }

        private void ReactivateMech(Pawn pawn, CompMechanoid mechanoidComp)
        {
            SetActivationState(mechanoidComp, true);

            if (pawn.CurJobDef == JobDefOf.Deactivated)
            {
                pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
            }

            // 使用安全的翻译方法
            string message = Props.reactivateMessageKey.Translate(pawn.Named("PAWN"));
            if (!message.NullOrEmpty())
            {
                Messages.Message(message, pawn, MessageTypeDefOf.PositiveEvent);
            }
        }

        private static void UndownMech(Pawn pawn)
        {
            try
            {
                // 方法1：使用 Notify_Resurrected（更安全）
                pawn.health.Notify_Resurrected();
                pawn.health.forceDowned = false;

                // 方法2：或者使用反射
                // typeof(Pawn_HealthTracker).GetMethod("MakeUndowned", 
                //     BindingFlags.NonPublic | BindingFlags.Instance)
                //     ?.Invoke(pawn.health, new object[] { null });

                // 清除严重伤害
                pawn.health.hediffSet.hediffs
                    .Where(h => h.def.lethalSeverity > 0)
                    .ToList()
                    .ForEach(h => pawn.health.RemoveHediff(h));

                // 强制站立
                pawn.jobs?.StopAll();
                pawn.jobs?.StartJob(new Job(JobDefOf.Goto, pawn.Position),
                    JobCondition.InterruptForced);

                // 刷新UI
                PortraitsCache.SetDirty(pawn);
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to undown mechanoid {pawn.Label}: {ex}");
            }
        }

        private static bool IsDeactivated(CompMechanoid comp)
        {
            return comp != null && (bool)(deactivatedField?.GetValue(comp) ?? false);
        }

        private static void SetActivationState(CompMechanoid comp, bool active)
        {
            if (comp == null) return;

            deactivatedField?.SetValue(comp, !active);
            activeField?.SetValue(comp, active);
            comp.WakeUp();
        }
    }
}