using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace NCL
{
    // Put on a PawnKindDef to let it take over a share of another kind's spawns: whenever a non-player
    // pawn of sharesSlotWith is generated, this kind is generated instead with the given chance.
    public class PawnKindExtension_SharedSpawnSlot : DefModExtension
    {
        public PawnKindDef sharesSlotWith;
        public float chance = 0.3f;
    }

    // Hooked on the request overload every other GeneratePawn overload funnels into, so raids, mech
    // clusters, bossgroup escorts and ancient dangers all share the slot. Player-faction generation
    // such as gestation is left alone so a gestated lancer stays a lancer.
    [HarmonyPatch(typeof(PawnGenerator), nameof(PawnGenerator.GeneratePawn), new[] { typeof(PawnGenerationRequest) })]
    public static class Patch_SharedSpawnSlot
    {
        private struct Replacement
        {
            public PawnKindDef Kind;
            public float Chance;
        }

        private static Dictionary<PawnKindDef, List<Replacement>> replacements;

        public static void Prefix(ref PawnGenerationRequest request)
        {
            if (request.PawnKindDefGetter != null)
            {
                return;
            }

            PawnKindDef kind = request.KindDef;
            if (kind == null || !Replacements.TryGetValue(kind, out List<Replacement> options))
            {
                return;
            }

            if (request.Faction != null && request.Faction.IsPlayer)
            {
                return;
            }

            float roll = Rand.Value;
            for (int i = 0; i < options.Count; i++)
            {
                if (roll < options[i].Chance)
                {
                    request.KindDef = options[i].Kind;
                    return;
                }

                roll -= options[i].Chance;
            }
        }

        private static Dictionary<PawnKindDef, List<Replacement>> Replacements
        {
            get
            {
                if (replacements != null)
                {
                    return replacements;
                }

                replacements = new Dictionary<PawnKindDef, List<Replacement>>();
                foreach (PawnKindDef def in DefDatabase<PawnKindDef>.AllDefsListForReading)
                {
                    PawnKindExtension_SharedSpawnSlot ext = def.GetModExtension<PawnKindExtension_SharedSpawnSlot>();
                    if (ext?.sharesSlotWith == null || ext.chance <= 0f)
                    {
                        continue;
                    }

                    if (!replacements.TryGetValue(ext.sharesSlotWith, out List<Replacement> list))
                    {
                        list = new List<Replacement>();
                        replacements[ext.sharesSlotWith] = list;
                    }

                    list.Add(new Replacement { Kind = def, Chance = ext.chance });
                }

                return replacements;
            }
        }
    }
}
