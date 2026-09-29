using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace NCL
{
    // Carrier-driven counterpart to JobDriver_BoardGunship: the gunship flies to a wounded ally and
    // pulls it aboard instead of waiting for the ally to walk over.
    public class JobDriver_GunshipLoadPassenger : JobDriver
    {
        private Pawn Passenger => job.GetTarget(TargetIndex.A).Thing as Pawn;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() =>
            {
                CompGunshipCargo cargo = GunshipDefCache.GetCargo(pawn);
                Pawn passenger = Passenger;
                return cargo == null || passenger == null || !cargo.CanLoad(passenger, out _);
            });

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            Toil load = ToilMaker.MakeToil("GunshipLoadPassenger");
            load.defaultCompleteMode = ToilCompleteMode.Instant;
            load.initAction = () =>
            {
                CompGunshipCargo cargo = GunshipDefCache.GetCargo(pawn);
                Pawn passenger = Passenger;
                if (cargo == null || passenger == null)
                {
                    return;
                }

                cargo.TryLoad(passenger);
            };

            yield return load;
        }
    }

    // Releases the whole bay at the carrier's feet. Used by the AI and by the player unload gizmo.
    public class JobDriver_GunshipUnloadCargo : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() =>
            {
                CompGunshipCargo cargo = GunshipDefCache.GetCargo(pawn);
                return cargo == null || !cargo.HasPassengers;
            });

            yield return Toils_General.Wait(30);

            Toil unload = ToilMaker.MakeToil("GunshipUnloadCargo");
            unload.defaultCompleteMode = ToilCompleteMode.Instant;
            unload.initAction = () =>
            {
                CompGunshipCargo cargo = GunshipDefCache.GetCargo(pawn);
                cargo?.DropAllPassengers(pawn.Position, pawn.Map);
            };

            yield return unload;
        }
    }
}
