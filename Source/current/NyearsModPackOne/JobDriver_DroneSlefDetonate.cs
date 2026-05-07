// Decompiled with JetBrains decompiler
// Type: NyarsModPackOne.JobDriver_DroneSlefDetonate
// Assembly: NyearsModPackOne, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: FDB00AC3-5462-4449-9639-A371FA2E00F3
// Assembly location: C:\Users\15858\OneDrive\Documents\Sundry\RwModCoop\MTW\1.6\Assemblies\NyearsModPackOne.dll

using System;
using System.Collections.Generic;
using Verse;
using Verse.AI;

#nullable disable
namespace NyarsModPackOne
{
  public class JobDriver_DroneSlefDetonate : JobDriver
  {
    public virtual bool TryMakePreToilReservations(bool errorOnFailed)
    {
      ((Thing) this.pawn).Map.pawnDestinationReservationManager.Reserve(this.pawn, this.job, ((LocalTargetInfo) ref this.job.targetA).Cell);
      return true;
    }

    protected virtual IEnumerable<Toil> MakeNewToils()
    {
      ToilFailConditions.FailOnDestroyedOrNull<JobDriver_DroneSlefDetonate>(this, (TargetIndex) 1);
      Toil f = Toils_Goto.GotoThing((TargetIndex) 1, (PathEndMode) 2, false);
      yield return ToilFailConditions.FailOnDespawnedOrNull<Toil>(f, (TargetIndex) 1);
      Toil toil = ToilMaker.MakeToil(nameof (MakeNewToils));
      toil.initAction = (Action) (() => ((Drone) this.pawn).activeExplosion = true);
      toil.defaultCompleteMode = (ToilCompleteMode) 1;
      yield return toil;
    }
  }
}
