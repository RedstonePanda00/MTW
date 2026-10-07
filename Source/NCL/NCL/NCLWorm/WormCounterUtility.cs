using RimWorld;
using UnityEngine;
using Verse;

namespace NCLWorm
{
  public static class WormCounterUtility
  {
    public const int StaggerTicks = 300;
    public const int DroneLockoutTicks = 3600;

    public static bool CanBeDashCountered(WormHead head)
    {
      if (head == null || head.Destroyed || !head.Spawned || head.IsDying)
        return false;
      TC_WormDecisionController brain = head.Brain;
      return brain != null && !brain.IsDeparting && !brain.IsStaggered && brain.IsDashing;
    }

    // Interrupts the dash, wipes the swarm, staggers the worm and locks drone production.
    public static bool ApplyDashCounter(WormHead head, Thing counterer)
    {
      if (!CanBeDashCountered(head))
        return false;
      Vector3 knockDir = counterer != null && counterer.Spawned ? head.ExactPosition - counterer.DrawPos : -head.BodyFacing;
      head.Brain.Stagger(knockDir, StaggerTicks);
      head.Swarm?.KillAll();
      head.GetComp<CompSwarmCarrier>()?.SuppressSpawning(DroneLockoutTicks);
      Messages.Message("NCL_WormDashCountered".Translate(), new LookTargets(head), MessageTypeDefOf.PositiveEvent, false);
      return true;
    }
  }
}
