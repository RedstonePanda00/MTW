using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace NCL.Diver
{
    public class DiverCloakSetEntry
    {
        public string setId;
        public string label;
        public string texPath;
        public Texture2D icon;

        public string FullTexPath => texPath ?? $"Diver/Cloak/Cloak_{setId}";
    }

    [StaticConstructorOnStartup]
    public static class DiverCloakCatalog
    {
        public const string DefaultSetId = "B01";

        private static readonly List<DiverCloakSetEntry> Entries = new List<DiverCloakSetEntry>();

        public static IReadOnlyList<DiverCloakSetEntry> AllEntries => Entries;

        static DiverCloakCatalog()
        {
            Register(new DiverCloakSetEntry
            {
                setId = DefaultSetId,
                label = "B01",
                texPath = "Diver/Cloak/Cloak_B01"
            });
        }

        public static void Register(DiverCloakSetEntry entry)
        {
            if (entry == null || entry.setId.NullOrEmpty())
            {
                return;
            }

            entry.icon = ContentFinder<Texture2D>.Get(entry.FullTexPath + "_south", false)
                ?? ContentFinder<Texture2D>.Get(entry.FullTexPath + "_north", false);
            Entries.Add(entry);
        }

        public static DiverCloakSetEntry Get(string setId)
        {
            if (setId.NullOrEmpty())
            {
                return Entries.Count > 0 ? Entries[0] : null;
            }

            for (int i = 0; i < Entries.Count; i++)
            {
                if (Entries[i].setId == setId)
                {
                    return Entries[i];
                }
            }

            return Entries.Count > 0 ? Entries[0] : null;
        }

        public static string NormalizeSetId(string setId)
        {
            return Get(setId)?.setId ?? DefaultSetId;
        }
    }
}
