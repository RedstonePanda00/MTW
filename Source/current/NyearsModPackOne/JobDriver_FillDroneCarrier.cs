// Decompiled with JetBrains decompiler
// Type: NyarsModPackOne.JobDriver_FillDroneCarrier
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
  public class JobDriver_FillDroneCarrier : JobDriver
  {
    private const TargetIndex CarrierIndex = (TargetIndex) 1;
    private const TargetIndex ResourceIndex = (TargetIndex) 2;

    protected Thing Carrier
    {
      get
      {
        LocalTargetInfo target = this.job.GetTarget((TargetIndex) 1);
        return ((LocalTargetInfo) ref target).Thing;
      }
    }

    protected Thing Resource
    {
      get
      {
        LocalTargetInfo target = this.job.GetTarget((TargetIndex) 2);
        return ((LocalTargetInfo) ref target).Thing;
      }
    }

    public virtual bool TryMakePreToilReservations(bool errorOnFailed)
    {
      return ReservationUtility.Reserve(this.pawn, LocalTargetInfo.op_Implicit(this.Carrier), this.job, 1, -1, (ReservationLayerDef) null, errorOnFailed, false) && ReservationUtility.Reserve(this.pawn, LocalTargetInfo.op_Implicit(this.Resource), this.job, 1, -1, (ReservationLayerDef) null, errorOnFailed, false);
    }

    protected virtual IEnumerable<Toil> MakeNewToils()
    {
      ToilFailConditions.FailOnDespawnedNullOrForbidden<JobDriver_FillDroneCarrier>(this, (TargetIndex) 1);
      ToilFailConditions.FailOnDespawnedNullOrForbidden<JobDriver_FillDroneCarrier>(this, (TargetIndex) 2);
      yield return ToilFailConditions.FailOnSomeonePhysicallyInteracting<Toil>(Toils_Goto.GotoThing((TargetIndex) 2, (PathEndMode) 3, false), (TargetIndex) 2);
      yield return Toils_Haul.StartCarryThing((TargetIndex) 2, false, true, false, true, false);
      yield return Toils_Goto.GotoThing((TargetIndex) 1, (PathEndMode) 2, false);
      yield return new Toil()
      {
        initAction = (Action) (() => this.pawn.pather.StopDead()),
        defaultCompleteMode = (ToilCompleteMode) 3,
        defaultDuration = 80
      };
      yield return new Toil()
      {
        initAction = (Action) (() =>
        {
          CompDroneCarrier comp = ThingCompUtility.TryGetComp<CompDroneCarrier>(this.Carrier);
          if (comp == null)
            return;
          int stackCount = this.Resource.stackCount;
          comp.AddIngredient(this.Resource.def, stackCount);
          if (this.pawn.carryTracker.CarriedThing == this.Resource)
            this.pawn.carryTracker.DestroyCarriedThing();
          else
            this.Resource.Destroy((DestroyMode) 0);
        }),
        defaultCompleteMode = (ToilCompleteMode) 1
      };
    }
  }
}
