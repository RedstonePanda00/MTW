// Decompiled with JetBrains decompiler
// Type: NyarsModPackOne.WorkGiver_HaulResourcesToDroneCarrier
// Assembly: NyearsModPackOne, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: FDB00AC3-5462-4449-9639-A371FA2E00F3
// Assembly location: C:\Users\15858\OneDrive\Documents\Sundry\RwModCoop\MTW\1.6\Assemblies\NyearsModPackOne.dll

using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;

#nullable disable
namespace NyarsModPackOne
{
  public class WorkGiver_HaulResourcesToDroneCarrier : WorkGiver_Scanner
  {
    public virtual ThingRequest PotentialWorkThingRequest
    {
      get => ThingRequest.ForGroup((ThingRequestGroup) 12);
    }

    public virtual IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
    {
      return (IEnumerable<Thing>) ((Thing) pawn).Map.mapPawns.SpawnedPawnsInFaction(((Thing) pawn).Faction);
    }

    public virtual bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
    {
      if (!(t is Pawn pawn1) || !((Thing) pawn1).Spawned || pawn1.Downed)
        return false;
      CompDroneCarrier comp = ThingCompUtility.TryGetComp<CompDroneCarrier>((Thing) pawn1);
      if (comp == null)
        return false;
      int amountToAutofill = comp.AmountToAutofill;
      return amountToAutofill > 0 && ReservationUtility.CanReserve(pawn, LocalTargetInfo.op_Implicit(t), 1, -1, (ReservationLayerDef) null, forced) && !GenList.NullOrEmpty<Thing>((IList<Thing>) HaulAIUtility.FindFixedIngredientCount(pawn, comp.Props.fixedIngredient, amountToAutofill));
    }

    public virtual Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
    {
      CompDroneCarrier comp = ThingCompUtility.TryGetComp<CompDroneCarrier>(t);
      if (comp == null)
        return (Job) null;
      int amountToAutofill = comp.AmountToAutofill;
      if (amountToAutofill <= 0)
        return (Job) null;
      List<Thing> fixedIngredientCount = HaulAIUtility.FindFixedIngredientCount(pawn, comp.Props.fixedIngredient, amountToAutofill);
      if (GenList.NullOrEmpty<Thing>((IList<Thing>) fixedIngredientCount))
        return (Job) null;
      Job containerJob = HaulAIUtility.HaulToContainerJob(pawn, fixedIngredientCount[0], t);
      containerJob.count = Mathf.Min(containerJob.count, amountToAutofill);
      if (fixedIngredientCount.Count > 1)
        containerJob.targetQueueB = fixedIngredientCount.Skip<Thing>(1).Select<Thing, LocalTargetInfo>((Func<Thing, LocalTargetInfo>) (res => new LocalTargetInfo(res))).ToList<LocalTargetInfo>();
      return containerJob;
    }
  }
}
