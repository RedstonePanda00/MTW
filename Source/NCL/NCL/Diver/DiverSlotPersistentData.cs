using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace NCL.Diver
{
    public class DiverSlotPersistentData : IExposable
    {
        public const int LoadoutCount = 4;

        public string nickname;
        public string cloakSetId = DiverCloakCatalog.DefaultSetId;
        public Color cloakColor = Color.white;
        public bool cloakVisible = true;
        public List<string> loadoutDefNames = new List<string>();
        public List<int> stratagemCooldownUntilTick = new List<int>();
        public List<string> recordDefNames = new List<string>();
        public List<float> recordValues = new List<float>();
        public string rimTalkPersonality;
        public float rimTalkChattiness;

        public void EnsureInitialized()
        {
            if (loadoutDefNames == null)
            {
                loadoutDefNames = new List<string>();
            }

            if (stratagemCooldownUntilTick == null)
            {
                stratagemCooldownUntilTick = new List<int>();
            }

            if (recordDefNames == null)
            {
                recordDefNames = new List<string>();
            }

            if (recordValues == null)
            {
                recordValues = new List<float>();
            }

            while (loadoutDefNames.Count < LoadoutCount)
            {
                loadoutDefNames.Add(null);
            }

            while (stratagemCooldownUntilTick.Count < LoadoutCount)
            {
                stratagemCooldownUntilTick.Add(0);
            }

            cloakSetId = DiverCloakCatalog.NormalizeSetId(cloakSetId);
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref nickname, "nickname");
            Scribe_Values.Look(ref cloakSetId, "cloakSetId", DiverCloakCatalog.DefaultSetId);
            Scribe_Values.Look(ref cloakColor, "cloakColor", Color.white);
            Scribe_Values.Look(ref cloakVisible, "cloakVisible", true);
            Scribe_Collections.Look(ref loadoutDefNames, "loadoutDefNames", LookMode.Value);
            Scribe_Collections.Look(ref stratagemCooldownUntilTick, "stratagemCooldownUntilTick", LookMode.Value);
            Scribe_Collections.Look(ref recordDefNames, "recordDefNames", LookMode.Value);
            Scribe_Collections.Look(ref recordValues, "recordValues", LookMode.Value);
            Scribe_Values.Look(ref rimTalkPersonality, "rimTalkPersonality");
            Scribe_Values.Look(ref rimTalkChattiness, "rimTalkChattiness", 0f);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                EnsureInitialized();
            }
        }

        public float GetRecordValue(RecordDef def)
        {
            if (def == null || recordDefNames == null || recordValues == null)
            {
                return 0f;
            }

            for (int i = 0; i < recordDefNames.Count && i < recordValues.Count; i++)
            {
                if (recordDefNames[i] == def.defName)
                {
                    return recordValues[i];
                }
            }

            return 0f;
        }

        public void AddToRecord(RecordDef def, float delta)
        {
            if (def == null || UnityEngine.Mathf.Abs(delta) < 0.001f)
            {
                return;
            }

            EnsureInitialized();
            for (int i = 0; i < recordDefNames.Count && i < recordValues.Count; i++)
            {
                if (recordDefNames[i] == def.defName)
                {
                    recordValues[i] += delta;
                    return;
                }
            }

            recordDefNames.Add(def.defName);
            recordValues.Add(delta);
        }

        public void SetRecordSnapshot(Dictionary<string, float> snapshot)
        {
            recordDefNames = new List<string>();
            recordValues = new List<float>();
            if (snapshot == null)
            {
                return;
            }

            foreach (KeyValuePair<string, float> kv in snapshot)
            {
                recordDefNames.Add(kv.Key);
                recordValues.Add(kv.Value);
            }
        }
    }
}
