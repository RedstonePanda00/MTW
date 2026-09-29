using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace NCL
{
    public class ITab_GunshipCargo : ITab_ContentsBase
    {
        private static readonly List<Thing> EmptyContainer = new List<Thing>();

        public override IList<Thing> container
        {
            get
            {
                CompGunshipCargo cargo = Cargo;
                if (cargo == null)
                {
                    return EmptyContainer;
                }

                return cargo.GetDirectlyHeldThings();
            }
        }

        public override bool IsVisible => Cargo != null && SelThing?.Faction == Faction.OfPlayer;

        public override bool UseDiscardMessage => false;

        private CompGunshipCargo Cargo => GunshipDefCache.GetCargo(SelThing as Pawn);

        public ITab_GunshipCargo()
        {
            labelKey = "MTW.Gunship.Cargo.Tab";
            containedItemsKey = "MTW.Gunship.Cargo.Tab";
            size = new Vector2(460f, 450f);
        }

        protected override void OnDropThing(Thing t, int count)
        {
            Thing carrier = SelThing;
            if (carrier?.Map == null)
            {
                return;
            }

            GenDrop.TryDropSpawn(t, carrier.Position, carrier.Map, ThingPlaceMode.Near, out Thing _);
        }
    }
}
