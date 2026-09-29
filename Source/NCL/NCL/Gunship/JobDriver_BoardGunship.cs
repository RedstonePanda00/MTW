using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace NCL
{
    [DefOf]
    public static class NCL_JobDefOf
    {
        public static JobDef MTW_BoardGunship;
        public static JobDef MTW_GunshipLoadPassenger;
        public static JobDef MTW_GunshipUnloadCargo;

        static NCL_JobDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(NCL_JobDefOf));
        }
    }

    public class JobDriver_BoardGunship : JobDriver
    {
        private Pawn Carrier => job.GetTarget(TargetIndex.A).Thing as Pawn;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOn(() =>
            {
                CompGunshipCargo cargo = GunshipDefCache.GetCargo(Carrier);
                return cargo == null || !cargo.CanLoad(pawn, out _);
            });

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            Toil board = ToilMaker.MakeToil("BoardGunship");
            board.defaultCompleteMode = ToilCompleteMode.Instant;
            board.initAction = () =>
            {
                CompGunshipCargo cargo = GunshipDefCache.GetCargo(Carrier);
                if (cargo == null)
                {
                    return;
                }

                // TryLoad despawns the pawn, which ends this job on its own.
                cargo.TryLoad(pawn);
            };

            yield return board;
        }
    }
}
