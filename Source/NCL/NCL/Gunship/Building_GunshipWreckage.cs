using System.Collections.Generic;
using RimWorld;
using Verse;

namespace NCL
{
    public class Building_GunshipWreckage : Building, IThingHolder
    {
        private static readonly IntRange SlagCountRange = new IntRange(2, 4);

        private ThingOwner innerContainer;

        public Building_GunshipWreckage()
        {
            innerContainer = new ThingOwner<Thing>(this, oneStackOnly: false, LookMode.Deep);
        }

        public void AcceptCorpse(Corpse corpse)
        {
            if (corpse == null || corpse.Destroyed)
            {
                return;
            }

            if (corpse.Spawned)
            {
                corpse.DeSpawn(DestroyMode.Vanish);
            }

            innerContainer.TryAdd(corpse);
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            Map map = Map;
            IntVec3 pos = Position;
            bool releaseContents = mode == DestroyMode.Deconstruct
                || mode == DestroyMode.KillFinalize
                || mode == DestroyMode.Refund
                || mode == DestroyMode.FailConstruction;

            // Contents have to leave before the holder goes away, matching Building_Casket.
            if (map != null && releaseContents)
            {
                innerContainer.TryDropAll(pos, map, ThingPlaceMode.Near);
                SpawnMechanoidSlag(pos, map);
            }
            else
            {
                innerContainer.ClearAndDestroyContents();
            }

            base.Destroy(mode);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref innerContainer, "gunshipWreckInner", this);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && innerContainer == null)
            {
                innerContainer = new ThingOwner<Thing>(this, oneStackOnly: false, LookMode.Deep);
            }
        }

        public override string GetInspectString()
        {
            string text = base.GetInspectString();
            if (innerContainer != null && innerContainer.Count > 0)
            {
                string contents = "MTW.Gunship.WreckageContains".Translate(innerContainer.ContentsString);
                if (text.NullOrEmpty())
                {
                    return contents;
                }

                return text + "\n" + contents;
            }

            return text;
        }

        public ThingOwner GetDirectlyHeldThings()
        {
            return innerContainer;
        }

        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
        }

        private static void SpawnMechanoidSlag(IntVec3 pos, Map map)
        {
            ThingDef slagDef = ThingDefOf.ChunkMechanoidSlag;
            if (slagDef == null)
            {
                return;
            }

            int count = SlagCountRange.RandomInRange;
            for (int i = 0; i < count; i++)
            {
                Thing slag = ThingMaker.MakeThing(slagDef);
                GenPlace.TryPlaceThing(slag, pos, map, ThingPlaceMode.Near);
            }
        }
    }
}
