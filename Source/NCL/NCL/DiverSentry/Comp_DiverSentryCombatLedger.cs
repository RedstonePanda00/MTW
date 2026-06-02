using NCL.Worm;
using RimWorld;
using Verse;

namespace NCL
{
    public class Comp_DiverSentryCombatLedger : ThingComp
    {
        private const string DiverDefName = "MTW_Diver";

        private Pawn creditPawn;

        private int creditDiverSlotIndex = -1;

        private float pendingDamageDealt;

        private int pendingKills;

        private int pendingHeadshots;

        private bool flushed;

        public void Initialize(Pawn diver, int diverSlotIndex)
        {
            creditPawn = diver;
            creditDiverSlotIndex = diverSlotIndex;
        }

        public void AddDamageDealt(float amount)
        {
            if (amount > 0f)
            {
                pendingDamageDealt += amount;
            }
        }

        public void AddKill()
        {
            pendingKills++;
        }

        public void AddHeadshot()
        {
            pendingHeadshots++;
        }

        public static Comp_DiverSentryCombatLedger FromInstigator(Thing instigator)
        {
            return instigator?.TryGetComp<Comp_DiverSentryCombatLedger>();
        }

        public override void Notify_Killed(Map prevMap, DamageInfo? dinfo = null)
        {
            FlushOnce();
            base.Notify_Killed(prevMap, dinfo);
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            FlushOnce();
            base.PostDestroy(mode, previousMap);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.Look(ref creditPawn, "creditPawn");
            Scribe_Values.Look(ref creditDiverSlotIndex, "creditDiverSlotIndex", -1);
            Scribe_Values.Look(ref pendingDamageDealt, "pendingDamageDealt", 0f);
            Scribe_Values.Look(ref pendingKills, "pendingKills", 0);
            Scribe_Values.Look(ref pendingHeadshots, "pendingHeadshots", 0);
            Scribe_Values.Look(ref flushed, "flushed", false);
        }

        private void FlushOnce()
        {
            if (flushed)
            {
                return;
            }

            flushed = true;

            if (pendingDamageDealt <= 0f && pendingKills <= 0 && pendingHeadshots <= 0)
            {
                return;
            }

            float damage = pendingDamageDealt;
            int kills = pendingKills;
            int headshots = pendingHeadshots;
            pendingDamageDealt = 0f;
            pendingKills = 0;
            pendingHeadshots = 0;

            Pawn liveDiver = ResolveLiveCreditDiver();
            if (liveDiver?.records != null)
            {
                if (damage > 0f)
                {
                    liveDiver.records.AddTo(RecordDefOf.DamageDealt, damage);
                }

                for (int i = 0; i < kills; i++)
                {
                    liveDiver.records.Increment(RecordDefOf.Kills);
                }

                for (int i = 0; i < headshots; i++)
                {
                    liveDiver.records.Increment(RecordDefOf.Headshots);
                }

                return;
            }

            if (creditDiverSlotIndex >= 0)
            {
                GameComponent_DiverRespawnManager.ApplySlotCombatLedger(
                    creditDiverSlotIndex,
                    damage,
                    kills,
                    headshots);
            }
        }

        private Pawn ResolveLiveCreditDiver()
        {
            if (IsLivePlayerDiver(creditPawn))
            {
                return creditPawn;
            }

            if (creditDiverSlotIndex < 0)
            {
                return null;
            }

            Pawn tracked = Current.Game?.GetComponent<GameComponent_DiverRespawnManager>()
                ?.GetTrackedDiver(creditDiverSlotIndex);
            return IsLivePlayerDiver(tracked) ? tracked : null;
        }

        private static bool IsLivePlayerDiver(Pawn pawn)
        {
            return pawn != null
                && !pawn.DestroyedOrNull()
                && !pawn.Dead
                && pawn.Spawned
                && pawn.Faction == Faction.OfPlayer
                && pawn.def?.defName == DiverDefName;
        }
    }
}
