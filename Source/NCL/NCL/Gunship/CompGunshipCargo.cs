using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace NCL
{
    // Passenger bay. The ThingOwner itself is the tree: holdingOwner.Owner is the parent link and
    // innerContainer is the child list, so nothing has to be persisted twice and save round-trips
    // cannot desync the hierarchy.
    public class CompGunshipCargo : ThingComp, IThingHolder, ISuspendableThingHolder
    {
        private ThingOwner<Pawn> innerContainer;
        private int ticksSinceLoaded;

        public CompProperties_GunshipCargo Props => (CompProperties_GunshipCargo)props;

        public Pawn Pawn => parent as Pawn;

        public bool IsContentsSuspended => true;

        public int PassengerCount => innerContainer?.Count ?? 0;

        public int MaxSlots => Mathf.Max(0, Props.maxSlots);

        public bool HasPassengers => PassengerCount > 0;

        public List<Pawn> Passengers => innerContainer.InnerListForReading;

        public CompGunshipCargo()
        {
            innerContainer = new ThingOwner<Pawn>(this, oneStackOnly: false, LookMode.Deep);
        }

        public ThingOwner GetDirectlyHeldThings()
        {
            return innerContainer;
        }

        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Deep.Look(ref innerContainer, "gunshipCargo", this);
            Scribe_Values.Look(ref ticksSinceLoaded, "gunshipCargoTicksSinceLoaded", 0);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && innerContainer == null)
            {
                innerContainer = new ThingOwner<Pawn>(this, oneStackOnly: false, LookMode.Deep);
            }
        }

        public override void CompTick()
        {
            base.CompTick();
            if (!HasPassengers)
            {
                return;
            }

            ticksSinceLoaded++;
            Pawn pawn = Pawn;
            if (pawn == null || !pawn.Spawned || pawn.Dead)
            {
                return;
            }

            if (pawn.Faction == null || pawn.Faction == Faction.OfPlayer)
            {
                return;
            }

            // Where and when to unload is decided by the gunship think tree. This is only a safety net
            // for a carrier that can no longer act at all (stunned in a loop, no reachable drop cell),
            // so that its riders are never lost for good.
            if (Props.autoUnloadAfterTicks > 0
                && ticksSinceLoaded >= Props.autoUnloadAfterTicks
                && pawn.IsHashIntervalTick(250))
            {
                DropAllPassengers(pawn.Position, pawn.Map);
            }
        }

        // Runs last in the death sequence, after PostDeSpawn and PostDestroy. CompGunshipFlight is
        // declared ahead of this comp and has already moved the whole bay into the crash faller when
        // the gunship died airborne, so anything still aboard here died on the ground.
        public override void Notify_Killed(Map prevMap, DamageInfo? dinfo = null)
        {
            base.Notify_Killed(prevMap, dinfo);
            if (!HasPassengers)
            {
                return;
            }

            if (prevMap == null)
            {
                innerContainer.ClearAndDestroyContents();
                return;
            }

            DropAllPassengers(parent.PositionHeld, prevMap);
        }

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            base.PostDeSpawn(map, mode);
            // Vanish is both the death path and the "loaded into another carrier" path; in either case
            // the bay must stay intact for whoever handles it next.
            if (HasPassengers && map != null && mode != DestroyMode.Vanish)
            {
                DropAllPassengers(parent.Position, map);
            }
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);
            if (!HasPassengers)
            {
                return;
            }

            // Notify_Killed still has to run for a dying pawn and owns the bay.
            Pawn pawn = Pawn;
            if (pawn != null && pawn.Dead)
            {
                return;
            }

            if (previousMap != null)
            {
                DropAllPassengers(parent.Position, previousMap);
                return;
            }

            innerContainer.ClearAndDestroyContents();
        }

        public override string CompInspectStringExtra()
        {
            if (MaxSlots <= 0)
            {
                return null;
            }

            string line = "MTW.Gunship.Cargo.Contents".Translate(PassengerCount, MaxSlots);
            if (!HasPassengers)
            {
                return line;
            }

            return line + "\n" + innerContainer.ContentsString;
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }

            Pawn pawn = Pawn;
            if (pawn == null || !pawn.Spawned || pawn.Dead || pawn.Faction != Faction.OfPlayer)
            {
                yield break;
            }

            Command_Target load = new Command_Target
            {
                defaultLabel = "MTW.Gunship.Cargo.Load".Translate(),
                defaultDesc = "MTW.Gunship.Cargo.LoadDesc".Translate(MaxSlots),
                icon = TexCommand.Install,
                targetingParams = BuildLoadTargetingParams(),
                action = target =>
                {
                    if (target.Thing is Pawn passenger)
                    {
                        OrderBoard(passenger);
                    }
                }
            };

            if (PassengerCount >= MaxSlots)
            {
                load.Disable("MTW.Gunship.Cargo.Full".Translate());
            }
            else if (!CarrierReadyForTransfer(out string reason))
            {
                load.Disable(reason);
            }

            yield return load;

            if (!HasPassengers)
            {
                yield break;
            }

            yield return new Command_Action
            {
                defaultLabel = "MTW.Gunship.Cargo.Unload".Translate(),
                defaultDesc = "MTW.Gunship.Cargo.UnloadDesc".Translate(),
                icon = TexCommand.DropCarriedPawn,
                action = () => DropAllPassengers(pawn.Position, pawn.Map)
            };
        }

        public TargetingParameters BuildLoadTargetingParams()
        {
            TargetingParameters targeting = TargetingParameters.ForPawns();
            targeting.canTargetSelf = false;
            targeting.validator = target => target.Thing is Pawn candidate && CanLoad(candidate, out _);
            return targeting;
        }

        public void OrderBoard(Pawn passenger)
        {
            if (!CanLoad(passenger, out string reason))
            {
                Messages.Message(reason, parent, MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            JobDef boardDef = NCL_JobDefOf.MTW_BoardGunship;
            if (boardDef == null)
            {
                return;
            }

            Job job = JobMaker.MakeJob(boardDef, parent);
            job.count = 1;
            passenger.jobs?.TryTakeOrderedJob(job, JobTag.Misc);
        }

        public bool CanLoad(Pawn candidate, out string reason)
        {
            reason = null;
            if (candidate == null || candidate == parent || candidate.Dead || candidate.Destroyed)
            {
                reason = "MTW.Gunship.Cargo.CannotLoad".Translate();
                return false;
            }

            if (PassengerCount >= MaxSlots)
            {
                reason = "MTW.Gunship.Cargo.Full".Translate();
                return false;
            }

            if (!CarrierReadyForTransfer(out reason))
            {
                return false;
            }

            // Loading the carrier (or any of its ancestors) into the candidate's own bay would make the
            // ThingOwner chain self-referential.
            if (IsDescendantOf(candidate))
            {
                reason = "MTW.Gunship.Cargo.CannotLoadCycle".Translate();
                return false;
            }

            if (Props.maxNestDepth > 0 && NestDepth() + 1 >= Props.maxNestDepth)
            {
                reason = "MTW.Gunship.Cargo.TooDeep".Translate();
                return false;
            }

            CompGunshipCargo candidateCargo = GunshipDefCache.GetCargo(candidate);
            if (candidateCargo != null && candidateCargo.Props.requireGroundedToLoad)
            {
                CompGunshipFlight candidateFlight = GunshipDefCache.GetFlight(candidate);
                if (candidateFlight != null && candidateFlight.FlightState != GunshipFlightState.Grounded)
                {
                    reason = "MTW.Gunship.Cargo.CannotLoadWhileAirborne".Translate();
                    return false;
                }
            }

            return true;
        }

        public bool TryLoad(Pawn passenger)
        {
            if (!CanLoad(passenger, out _))
            {
                return false;
            }

            if (passenger.Spawned)
            {
                passenger.DeSpawnOrDeselect();
            }

            if (!innerContainer.TryAddOrTransfer(passenger, canMergeWithExistingStacks: false))
            {
                return false;
            }

            ticksSinceLoaded = 0;
            return true;
        }

        public void DropAllPassengers(IntVec3 cell, Map map)
        {
            if (!HasPassengers)
            {
                return;
            }

            if (map == null)
            {
                innerContainer.ClearAndDestroyContents();
                return;
            }

            IntVec3 dropCell = cell.IsValid && cell.InBounds(map) ? cell : CellFinder.RandomCell(map);
            List<Pawn> dropped = new List<Pawn>(innerContainer.InnerListForReading);
            innerContainer.TryDropAll(dropCell, map, ThingPlaceMode.Near);
            ticksSinceLoaded = 0;
            AdoptIntoCarrierLord(dropped);
        }

        // Raid escorts ride in already despawned, so they are not part of the raid's lords. Handing them
        // to the carrier's lord on release gives them the assault duty they would have had on foot.
        private void AdoptIntoCarrierLord(List<Pawn> dropped)
        {
            Lord lord = Pawn?.GetLord();
            if (lord == null)
            {
                return;
            }

            for (int i = 0; i < dropped.Count; i++)
            {
                Pawn passenger = dropped[i];
                if (passenger == null || !passenger.Spawned || passenger.Dead)
                {
                    continue;
                }

                if (passenger.Faction != lord.faction || passenger.GetLord() != null || !lord.CanAddPawn(passenger))
                {
                    continue;
                }

                lord.AddPawn(passenger);
            }
        }

        // Moves the whole bay into another owner (the crash faller) without spawning anything.
        public void TransferAllTo(ThingOwner destination)
        {
            if (destination == null || !HasPassengers)
            {
                return;
            }

            innerContainer.TryTransferAllToContainer(destination, canMergeWithExistingStacks: false);
        }

        private bool CarrierReadyForTransfer(out string reason)
        {
            reason = null;
            if (!Props.requireGroundedToLoad)
            {
                return true;
            }

            CompGunshipFlight flight = GunshipDefCache.GetFlight(Pawn);
            if (flight == null || flight.FlightState == GunshipFlightState.Grounded)
            {
                return true;
            }

            reason = "MTW.Gunship.Cargo.CannotLoadWhileAirborne".Translate();
            return false;
        }

        // Iterative walk up the holder chain; recursion would be unbounded because nesting is unlimited.
        private bool IsDescendantOf(Thing possibleAncestor)
        {
            IThingHolder holder = parent.ParentHolder;
            while (holder != null)
            {
                if (holder is ThingComp comp)
                {
                    if (comp.parent == possibleAncestor)
                    {
                        return true;
                    }
                }
                else if (holder is Thing thing && thing == possibleAncestor)
                {
                    return true;
                }

                holder = holder.ParentHolder;
            }

            return false;
        }

        private int NestDepth()
        {
            int depth = 0;
            IThingHolder holder = parent.ParentHolder;
            while (holder != null)
            {
                if (holder is CompGunshipCargo)
                {
                    depth++;
                }

                holder = holder.ParentHolder;
            }

            return depth;
        }
    }
}
