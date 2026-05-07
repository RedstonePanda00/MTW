// Decompiled with JetBrains decompiler
// Type: NyarsModPackOne.JobGiver_FollowTarget
// Assembly: NyearsModPackOne, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: FDB00AC3-5462-4449-9639-A371FA2E00F3
// Assembly location: C:\Users\15858\OneDrive\Documents\Sundry\RwModCoop\MTW\1.6\Assemblies\NyearsModPackOne.dll

using RimWorld;
using Verse;
using Verse.AI;

#nullable disable
namespace NyarsModPackOne
{
  public class JobGiver_FollowTarget : ThinkNode_JobGiver
  {
    protected virtual Job TryGiveJob(Pawn pawn)
    {
      if (!(pawn is Drone drone) || drone.owner == null)
        return (Job) null;
      int num;
      if (((Thing) drone.owner).Spawned)
      {
        IntVec3 intVec3 = IntVec3.op_Subtraction(((Thing) drone.owner).Position, ((Thing) pawn).Position);
        num = (double) ((IntVec3) ref intVec3).LengthHorizontalSquared < 2.25 ? 1 : 0;
      }
      else
        num = 1;
      if (num != 0)
        return (Job) null;
      Job job = JobMaker.MakeJob(JobDefOf.FollowClose, LocalTargetInfo.op_Implicit((Thing) drone.owner));
      job.expiryInterval = 240;
      job.checkOverrideOnExpire = true;
      job.followRadius = 1.5f;
      return job;
    }
  }
}
