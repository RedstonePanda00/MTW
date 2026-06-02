using System.Collections.Generic;
using RimWorld;
using Verse;

namespace NCL.Diver
{
    public static class DiverRecordsUtility
    {
        public static bool CanTouchPawnRecords()
        {
            return Current.ProgramState == ProgramState.Playing
                && Scribe.mode != LoadSaveMode.LoadingVars
                && Scribe.mode != LoadSaveMode.ResolvingCrossRefs
                && Scribe.mode != LoadSaveMode.PostLoadInit
                && DefDatabase<RecordDef>.DefCount > 0;
        }

        public static Dictionary<string, float> CapturePawnRecords(Pawn pawn)
        {
            Dictionary<string, float> snapshot = new Dictionary<string, float>();
            if (!CanTouchPawnRecords() || pawn == null || pawn.DestroyedOrNull() || pawn.records == null)
            {
                return snapshot;
            }

            List<RecordDef> defs = DefDatabase<RecordDef>.AllDefsListForReading;
            if (defs == null)
            {
                return snapshot;
            }
            for (int i = 0; i < defs.Count; i++)
            {
                RecordDef def = defs[i];
                if (def == null || !ShouldSnapshotRecord(def))
                {
                    continue;
                }

                snapshot[def.defName] = pawn.records.GetValue(def);
            }

            return snapshot;
        }

        public static void SnapshotPawnRecordsToSlot(Pawn pawn, DiverSlotPersistentData slot)
        {
            if (pawn == null || slot == null)
            {
                return;
            }

            slot.SetRecordSnapshot(CapturePawnRecords(pawn));
        }

        public static void ApplySlotRecordsToPawn(DiverSlotPersistentData slot, Pawn pawn)
        {
            if (!CanTouchPawnRecords() || pawn == null || pawn.DestroyedOrNull()
                || pawn.records == null || slot == null || slot.recordDefNames == null || slot.recordValues == null)
            {
                return;
            }

            int count = UnityEngine.Mathf.Min(slot.recordDefNames.Count, slot.recordValues.Count);
            for (int i = 0; i < count; i++)
            {
                RecordDef def = DefDatabase<RecordDef>.GetNamedSilentFail(slot.recordDefNames[i]);
                if (def == null || !ShouldSnapshotRecord(def))
                {
                    continue;
                }

                float target = slot.recordValues[i];
                float current = pawn.records.GetValue(def);
                float delta = target - current;
                if (UnityEngine.Mathf.Abs(delta) > 0.001f)
                {
                    pawn.records.AddTo(def, delta);
                }
            }
        }

        public static float GetDisplayRecordValue(DiverSlotPersistentData slot, Pawn livePawn, bool useLivePawn, RecordDef def)
        {
            if (def == null)
            {
                return 0f;
            }

            if (useLivePawn && livePawn?.records != null)
            {
                return livePawn.records.GetValue(def);
            }

            return slot?.GetRecordValue(def) ?? 0f;
        }

        private static bool ShouldSnapshotRecord(RecordDef def)
        {
            return def.type == RecordType.Int || def.type == RecordType.Float;
        }
    }
}
