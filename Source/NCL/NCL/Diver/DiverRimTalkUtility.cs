using System.Reflection;
using HarmonyLib;
using RimWorld;
using RsPandaLibrary.Compatibility.RimTalk;
using Verse;

namespace NCL.Diver
{
    // Soft dependency on RimTalk: vocal link and persona are applied separately.
    public static class DiverRimTalkUtility
    {
        private const float DefaultChattiness = 0.5f;
        private const string VocalLinkDefName = "VocalLinkImplant";
        private const string PersonaDataDefName = "RimTalk_PersonaData";
        private const string LogPrefix = "[NCL_DiverRimTalk]";
        private static readonly bool DebugLog = false;

        public static bool DebugLoggingEnabled => DebugLog;

        private static bool initialized;
        private static bool rimTalkModLoaded;
        private static bool personaReady;

        private static HediffDef vocalLinkDef;
        private static HediffDef personaDataDef;

        private static System.Type hediffPersonaType;
        private static MethodInfo setPersonalityMethod;
        private static MethodInfo setTalkInitiationWeightMethod;

        private static FieldInfo personalityField;
        private static FieldInfo talkInitiationWeightField;

        public static bool IsRimTalkAvailable
        {
            get
            {
                EnsureInitialized();
                return rimTalkModLoaded && vocalLinkDef != null;
            }
        }

        public static bool CanManagePersona
        {
            get
            {
                EnsureInitialized();
                return personaReady;
            }
        }

        public static void EnsureInitialized()
        {
            if (initialized && personaReady)
            {
                return;
            }

            if (!initialized)
            {
                initialized = true;
                rimTalkModLoaded = ModLister.GetActiveModWithIdentifier("cj.rimtalk") != null
                    || ModLister.GetActiveModWithIdentifier("cj.rimtalk_steam") != null
                    || AccessTools.TypeByName("RimTalk.Data.Hediff_Persona") != null;

                if (!rimTalkModLoaded)
                {
                    LogDbg("Init: RimTalk mod not loaded.");
                    return;
                }
            }

            vocalLinkDef = DefDatabase<HediffDef>.GetNamedSilentFail(VocalLinkDefName);
            personaDataDef = DefDatabase<HediffDef>.GetNamedSilentFail(PersonaDataDefName);
            hediffPersonaType = AccessTools.TypeByName("RimTalk.Data.Hediff_Persona");

            if (hediffPersonaType == null)
            {
                LogDbg("Init: RimTalk.Data.Hediff_Persona type not found.");
                return;
            }

            personalityField = AccessTools.Field(hediffPersonaType, "Personality");
            talkInitiationWeightField = AccessTools.Field(hediffPersonaType, "TalkInitiationWeight");

            System.Type personaServiceType = ResolvePersonaServiceType();
            setPersonalityMethod = personaServiceType != null ? AccessTools.Method(personaServiceType, "SetPersonality") : null;
            setTalkInitiationWeightMethod = personaServiceType != null ? AccessTools.Method(personaServiceType, "SetTalkInitiationWeight") : null;

            personaReady = personaDataDef != null
                && personalityField != null
                && talkInitiationWeightField != null;

            LogDbg($"Init: modLoaded={rimTalkModLoaded} vocalLinkDef={vocalLinkDef?.defName ?? "null"} personaDataDef={personaDataDef?.defName ?? "null"} personaServiceType={personaServiceType?.FullName ?? "null"} personalityField={personalityField != null} talkInitiationWeightField={talkInitiationWeightField != null} personaReady={personaReady}");
        }

        private static System.Type ResolvePersonaServiceType()
        {
            return AccessTools.TypeByName("RimTalk.Data.PersonaService")
                ?? AccessTools.TypeByName("RimTalk.Service.PersonaService");
        }

        /// <summary>
        /// Diver-owned RimTalk setup: vocal link + persona from slot (or first-roll into slot).
        /// </summary>
        public static void ApplySlotRimTalkToPawn(Pawn pawn, DiverSlotPersistentData slot, string context = null)
        {
            string ctx = FormatContext(context);
            if (pawn == null || slot == null)
            {
                LogDbg($"{ctx} Apply skipped: pawn/slot null.");
                return;
            }

            bool hadVocalLink = HasVocalLink(pawn);
            EnsureVocalLink(pawn, context);
            bool hasVocalLinkAfter = HasVocalLink(pawn);

            if (!CanManagePersona)
            {
                LogDbg($"{ctx} Apply {DescribePawn(pawn)} vocalLinkBefore={hadVocalLink} vocalLinkAfter={hasVocalLinkAfter} persona=skipped (CanManagePersona=false)");
                return;
            }

            bool rolledNewPersona = false;
            if (slot.rimTalkPersonality.NullOrEmpty())
            {
                rolledNewPersona = TryRollBuiltinPersonaIntoSlot(pawn, slot);
                if (rolledNewPersona)
                {
                    LogDbg($"{ctx} Apply {DescribePawn(pawn)} rolled new slot persona {PersonaSummary(slot.rimTalkPersonality)} chattiness={slot.rimTalkChattiness:F2}");
                }
            }

            if (slot.rimTalkPersonality.NullOrEmpty())
            {
                LogDbg($"{ctx} Apply {DescribePawn(pawn)} vocalLinkBefore={hadVocalLink} vocalLinkAfter={hasVocalLinkAfter} persona=empty (no slot text, roll failed)");
                return;
            }

            Hediff personaHediff = GetOrCreatePersonaHediff(pawn);
            if (personaHediff == null)
            {
                LogWarning($"{ctx} Apply {DescribePawn(pawn)} failed to create persona hediff.");
                return;
            }

            float chattiness = slot.rimTalkChattiness > 0f ? slot.rimTalkChattiness : DefaultChattiness;
            WritePersonaToHediff(personaHediff, slot.rimTalkPersonality, chattiness);

            bool readBackOk = TryReadPersonaFromPawn(pawn, out string readBackPersonality, out float readBackChattiness);
            LogDbg($"{ctx} Apply {DescribePawn(pawn)} vocalLinkBefore={hadVocalLink} vocalLinkAfter={hasVocalLinkAfter} rolledNew={rolledNewPersona} slotPersona={PersonaSummary(slot.rimTalkPersonality)} chattiness={chattiness:F2} readBackOk={readBackOk} readBackPersona={PersonaSummary(readBackPersonality)} readBackChattiness={readBackChattiness:F2}");
        }

        public static void EnsureVocalLink(Pawn pawn, string context = null)
        {
            if (!IsRimTalkAvailable || pawn?.health?.hediffSet == null)
            {
                LogDbg($"{FormatContext(context)} EnsureVocalLink skipped {DescribePawn(pawn)}: RimTalk unavailable or pawn has no health.");
                return;
            }

            if (pawn.health.hediffSet.HasHediff(vocalLinkDef))
            {
                return;
            }

            pawn.health.AddHediff(HediffMaker.MakeHediff(vocalLinkDef, pawn));
            LogDbg($"{FormatContext(context)} EnsureVocalLink added {VocalLinkDefName} to {DescribePawn(pawn)}");
        }

        public static Hediff GetOrCreatePersonaHediff(Pawn pawn)
        {
            if (!CanManagePersona || pawn?.health?.hediffSet == null || personaDataDef == null)
            {
                return null;
            }

            Hediff existing = pawn.health.hediffSet.GetFirstHediffOfDef(personaDataDef, false);
            if (existing != null)
            {
                return existing;
            }

            Hediff hediff = HediffMaker.MakeHediff(personaDataDef, pawn);
            pawn.health.AddHediff(hediff);
            return hediff;
        }

        public static bool TryRollBuiltinPersonaIntoSlot(Pawn pawn, DiverSlotPersistentData slot)
        {
            if (pawn == null || slot == null)
            {
                return false;
            }

            RimTalkPersonaExtension extension = pawn.kindDef.GetModExtension<RimTalkPersonaExtension>()
                ?? pawn.def.GetModExtension<RimTalkPersonaExtension>();
            if (extension == null || extension.personaKeys.NullOrEmpty())
            {
                return false;
            }

            string selectedKey = extension.personaKeys.RandomElement();
            string translated = selectedKey.Translate().Resolve();
            if (translated.NullOrEmpty())
            {
                return false;
            }

            slot.rimTalkPersonality = translated;
            slot.rimTalkChattiness = extension.chattiness > 0f ? extension.chattiness : DefaultChattiness;
            return true;
        }

        public static bool TryReadPersonaFromPawn(Pawn pawn, out string personality, out float chattiness)
        {
            personality = null;
            chattiness = DefaultChattiness;
            if (!CanManagePersona || pawn?.health?.hediffSet == null)
            {
                return false;
            }

            foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
            {
                if (hediff == null || !IsPersonaHediff(hediff))
                {
                    continue;
                }

                personality = personalityField.GetValue(hediff) as string;
                object weight = talkInitiationWeightField.GetValue(hediff);
                if (weight is float weightValue)
                {
                    chattiness = weightValue;
                }

                return !personality.NullOrEmpty();
            }

            return false;
        }

        public static void WritePersonaToHediff(Hediff hediff, string personality, float chattiness)
        {
            if (!CanManagePersona || hediff == null || personality.NullOrEmpty())
            {
                return;
            }

            personalityField.SetValue(hediff, personality);
            talkInitiationWeightField.SetValue(hediff, chattiness);
        }

        public static void CapturePersonaFromPawn(Pawn pawn, DiverSlotPersistentData slot, string context = null)
        {
            string ctx = FormatContext(context);
            if (pawn == null || slot == null)
            {
                LogDbg($"{ctx} Capture skipped: pawn/slot null.");
                return;
            }

            if (!CanManagePersona)
            {
                LogDbg($"{ctx} Capture {DescribePawn(pawn)} skipped: CanManagePersona=false slotBefore={PersonaSummary(slot.rimTalkPersonality)}");
                return;
            }

            string slotBefore = PersonaSummary(slot.rimTalkPersonality);
            float slotChattinessBefore = slot.rimTalkChattiness;
            bool hasVocalLink = HasVocalLink(pawn);
            bool hasPersonaHediff = pawn.health?.hediffSet?.GetFirstHediffOfDef(personaDataDef, false) != null;

            if (!TryReadPersonaFromPawn(pawn, out string personality, out float chattiness))
            {
                LogDbg($"{ctx} Capture {DescribePawn(pawn)} no persona on pawn vocalLink={hasVocalLink} personaHediff={hasPersonaHediff} slotBefore={slotBefore} chattinessBefore={slotChattinessBefore:F2} -> slot unchanged");
                return;
            }

            slot.rimTalkPersonality = personality;
            slot.rimTalkChattiness = chattiness;
            LogDbg($"{ctx} Capture {DescribePawn(pawn)} vocalLink={hasVocalLink} personaHediff={hasPersonaHediff} slotBefore={slotBefore} chattinessBefore={slotChattinessBefore:F2} -> slotAfter={PersonaSummary(slot.rimTalkPersonality)} chattinessAfter={slot.rimTalkChattiness:F2}");
        }

        private static bool IsPersonaHediff(Hediff hediff)
        {
            return hediffPersonaType != null && hediffPersonaType.IsInstanceOfType(hediff);
        }

        private static bool HasVocalLink(Pawn pawn)
        {
            return IsRimTalkAvailable
                && pawn?.health?.hediffSet != null
                && vocalLinkDef != null
                && pawn.health.hediffSet.HasHediff(vocalLinkDef);
        }

        private static string DescribePawn(Pawn pawn)
        {
            if (pawn == null)
            {
                return "null";
            }

            return $"{pawn.LabelShort}(id={pawn.thingIDNumber})";
        }

        private static string PersonaSummary(string personality)
        {
            if (personality.NullOrEmpty())
            {
                return "(empty)";
            }

            string preview = personality.Length <= 48 ? personality : personality.Substring(0, 48) + "...";
            return $"len={personality.Length} \"{preview}\"";
        }

        private static string FormatContext(string context)
        {
            return context.NullOrEmpty() ? "[?]" : $"[{context}]";
        }

        private static void LogDbg(string message)
        {
            if (DebugLog)
            {
                Log.Message($"{LogPrefix} {message}");
            }
        }

        private static void LogWarning(string message)
        {
            if (DebugLog)
            {
                Log.Warning($"{LogPrefix} {message}");
            }
        }
    }
}
