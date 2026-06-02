using System.Reflection;
using HarmonyLib;
using NCL.Worm;
using Verse;

namespace NCL.Diver
{
    // Safety net when RimTalk calls GetOrAddNew after diver slot logic already ran.
    public static class DiverRimTalkPersonaPersistence
    {
        private static bool patchesApplied;

        public static void TryPatch(Harmony harmony)
        {
            TryEnsurePatchesApplied(harmony);
        }

        public static void TryEnsurePatchesApplied(Harmony harmony)
        {
            if (patchesApplied || harmony == null)
            {
                return;
            }

            DiverRimTalkUtility.EnsureInitialized();

            System.Type hediffPersonaType = AccessTools.TypeByName("RimTalk.Data.Hediff_Persona");
            if (hediffPersonaType == null)
            {
                return;
            }

            MethodInfo getOrAddNew = AccessTools.Method(hediffPersonaType, "GetOrAddNew");
            if (getOrAddNew != null)
            {
                harmony.Patch(
                    getOrAddNew,
                    postfix: new HarmonyMethod(typeof(DiverRimTalkPersonaPersistence), nameof(Postfix_HediffPersona_GetOrAddNew))
                    {
                        priority = Priority.Last
                    });
            }

            System.Type personaServiceType = AccessTools.TypeByName("RimTalk.Data.PersonaService")
                ?? AccessTools.TypeByName("RimTalk.Service.PersonaService");
            MethodInfo setPersonality = personaServiceType != null ? AccessTools.Method(personaServiceType, "SetPersonality") : null;
            MethodInfo setTalkInitiationWeight = personaServiceType != null ? AccessTools.Method(personaServiceType, "SetTalkInitiationWeight") : null;
            if (setPersonality != null)
            {
                harmony.Patch(
                    setPersonality,
                    postfix: new HarmonyMethod(typeof(DiverRimTalkPersonaPersistence), nameof(Postfix_SetPersonality)));
            }

            if (setTalkInitiationWeight != null)
            {
                harmony.Patch(
                    setTalkInitiationWeight,
                    postfix: new HarmonyMethod(typeof(DiverRimTalkPersonaPersistence), nameof(Postfix_SetTalkInitiationWeight)));
            }

            patchesApplied = getOrAddNew != null || setPersonality != null || setTalkInitiationWeight != null;
            if (DiverRimTalkUtility.DebugLoggingEnabled)
            {
                Log.Message($"[NCL_DiverRimTalk] Harmony patches applied={patchesApplied} getOrAddNew={getOrAddNew != null} setPersonality={setPersonality != null} setTalkInitiationWeight={setTalkInitiationWeight != null} personaServiceType={personaServiceType?.FullName ?? "null"}");
            }
        }

        private static bool TryGetSlotForDiver(Pawn pawn, out DiverSlotPersistentData slot)
        {
            slot = null;
            GameComponent_DiverRespawnManager manager = Current.Game?.GetComponent<GameComponent_DiverRespawnManager>();
            int slotIndex = manager?.GetSlotIndexForPawn(pawn) ?? -1;
            if (manager == null || slotIndex < 0)
            {
                return false;
            }

            slot = manager.GetSlotData(slotIndex);
            return slot != null;
        }

        // After RsPandaLibrary random: restore slot-backed text if RimTalk re-initialized the hediff.
        public static void Postfix_HediffPersona_GetOrAddNew(Pawn pawn, Hediff __result, bool __state)
        {
            if (!DiverRimTalkUtility.CanManagePersona || __result == null || !DiverUtility.IsDiver(pawn))
            {
                return;
            }

            if (!TryGetSlotForDiver(pawn, out DiverSlotPersistentData slot) || slot.rimTalkPersonality.NullOrEmpty())
            {
                return;
            }

            float chattiness = slot.rimTalkChattiness > 0f ? slot.rimTalkChattiness : 0.5f;
            DiverRimTalkUtility.WritePersonaToHediff(__result, slot.rimTalkPersonality, chattiness);
        }

        public static void Postfix_SetPersonality(Pawn pawn, string personality)
        {
            if (!DiverRimTalkUtility.CanManagePersona || !DiverUtility.IsDiver(pawn) || personality.NullOrEmpty())
            {
                return;
            }

            if (!TryGetSlotForDiver(pawn, out DiverSlotPersistentData slot))
            {
                return;
            }

            slot.rimTalkPersonality = personality;
            DiverRimTalkUtility.EnsureVocalLink(pawn, "EditorSetPersonality");
        }

        public static void Postfix_SetTalkInitiationWeight(Pawn pawn, float frequency)
        {
            if (!DiverRimTalkUtility.CanManagePersona || !DiverUtility.IsDiver(pawn))
            {
                return;
            }

            if (!TryGetSlotForDiver(pawn, out DiverSlotPersistentData slot))
            {
                return;
            }

            slot.rimTalkChattiness = frequency;
        }
    }
}
