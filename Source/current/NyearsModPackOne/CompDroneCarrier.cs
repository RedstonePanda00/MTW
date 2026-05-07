// Decompiled with JetBrains decompiler
// Type: NyarsModPackOne.CompDroneCarrier
// Assembly: NyearsModPackOne, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: FDB00AC3-5462-4449-9639-A371FA2E00F3
// Assembly location: C:\Users\15858\OneDrive\Documents\Sundry\RwModCoop\MTW\1.6\Assemblies\NyearsModPackOne.dll

using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI.Group;

#nullable disable
namespace NyarsModPackOne
{
  public class CompDroneCarrier : ThingComp, IThingHolder
  {
    private ThingOwner innerContainer;
    public int cooldownTicksRemaining;
    public int maxToFill;
    public bool autoFill = true;
    public bool autoSpawn = false;
    public List<Drone> spawnedDrones = new List<Drone>();
    private DroneCarrierGizmo gizmo;
    private List<Thing> tmpResources = new List<Thing>();
    private const int LowIngredientThreshold = 250;

    public bool AutoFill
    {
      get => this.autoFill;
      set => this.autoFill = value;
    }

    public CompProperties_DroneCarrier Props => (CompProperties_DroneCarrier) this.props;

    public int IngredientCount
    {
      get
      {
        ThingOwner innerContainer = this.innerContainer;
        return innerContainer == null ? 0 : innerContainer.TotalStackCountOfDef(this.Props.fixedIngredient);
      }
    }

    public int AmountToAutofill => Mathf.Max(0, this.maxToFill - this.IngredientCount);

    public float FillPercentage
    {
      get => (float) this.IngredientCount / (float) this.Props.maxIngredientCount;
    }

    public int MaxCanSpawn
    {
      get
      {
        return Mathf.Min(Mathf.FloorToInt((float) this.IngredientCount / (float) this.Props.costPerDrone), this.Props.maxDronesPerSpawn);
      }
    }

    public bool LowIngredient => this.IngredientCount < 250;

    public float CooldownPercent
    {
      get
      {
        return this.Props.cooldownTicks <= 0 ? 0.0f : (float) this.cooldownTicksRemaining / (float) this.Props.cooldownTicks;
      }
    }

    public virtual void PostSpawnSetup(bool respawningAfterLoad)
    {
      base.PostSpawnSetup(respawningAfterLoad);
      if (respawningAfterLoad || this.innerContainer != null)
        return;
      this.innerContainer = (ThingOwner) new ThingOwner<Thing>((IThingHolder) this);
      if (this.Props.startingIngredientCount > 0)
        this.AddIngredient(this.Props.fixedIngredient, this.Props.startingIngredientCount);
      this.maxToFill = this.Props.startingIngredientCount;
    }

    public void AddIngredient(ThingDef ingredientDef, int amount)
    {
      if (this.innerContainer == null)
        return;
      int num = Mathf.Min(amount, this.Props.maxIngredientCount - this.IngredientCount);
      if (num <= 0)
        return;
      Thing thing = ThingMaker.MakeThing(ingredientDef, (ThingDef) null);
      thing.stackCount = num;
      this.innerContainer.TryAdd(thing, true);
    }

    public void TrySpawnDrones()
    {
      int maxCanSpawn = this.MaxCanSpawn;
      if (maxCanSpawn <= 0)
        return;
      Lord lord = this.parent is Pawn parent ? LordUtility.GetLord(parent) : (Lord) null;
      this.tmpResources.Clear();
      this.tmpResources.AddRange((IEnumerable<Thing>) this.innerContainer);
      for (int index = 0; index < maxCanSpawn; ++index)
      {
        Drone drone = Drone.MakeNewDrone(parent, this.DroneKindDef());
        if (drone != null)
        {
          GenPlace.TryPlaceThing((Thing) drone, ((Thing) this.parent).Position, ((Thing) this.parent).Map, (ThingPlaceMode) 1, (Action<Thing, int>) null, (Predicate<IntVec3>) null, new Rot4?(), 1);
          this.spawnedDrones.Add(drone);
          lord?.AddPawn((Pawn) drone);
          if (!this.ConsumeResources(this.Props.costPerDrone))
            Log.Warning(string.Format("Failed to consume resources for drone #{0}", (object) index));
        }
      }
      this.tmpResources.Clear();
      this.cooldownTicksRemaining = this.Props.cooldownTicks;
    }

    private PawnKindDef DroneKindDef()
    {
      return this.Props.droneKind != null ? this.Props.droneKind : PawnKindDef.Named("NCL_Dinergate_Drone");
    }

    private bool ConsumeResources(int amount)
    {
      int num1 = amount;
      foreach (Thing thing in new List<Thing>((IEnumerable<Thing>) this.innerContainer))
      {
        if (thing.def == this.Props.fixedIngredient && thing.stackCount > 0)
        {
          int num2 = Mathf.Min(thing.stackCount, num1);
          if (num2 >= thing.stackCount)
          {
            this.innerContainer.Remove(thing);
            thing.Destroy((DestroyMode) 0);
          }
          else if (num2 > 0)
            thing.SplitOff(num2)?.Destroy((DestroyMode) 0);
          num1 -= num2;
          if (num1 <= 0)
            break;
        }
      }
      return num1 <= 0;
    }

    public virtual IEnumerable<Gizmo> CompGetGizmosExtra()
    {
      if (((Thing) this.parent).Faction == Faction.OfPlayer)
      {
        foreach (Gizmo g in base.CompGetGizmosExtra())
          yield return g;
        if (this.gizmo == null)
          this.gizmo = new DroneCarrierGizmo(this);
        yield return (Gizmo) this.gizmo;
        Command_Toggle commandToggle = new Command_Toggle();
        ((Command) commandToggle).icon = (Texture) ContentFinder<Texture2D>.Get("ModIcon/CompAutoMechSpawner", true);
        ((Command) commandToggle).defaultLabel = TaggedString.op_Implicit(Translator.Translate("AutoReleaseDrones"));
        ((Command) commandToggle).defaultDesc = TaggedString.op_Implicit(Translator.Translate("AutoReleaseDronesDesc"));
        commandToggle.isActive = (Func<bool>) (() => this.autoSpawn);
        commandToggle.toggleAction = (Action) (() => this.autoSpawn = !this.autoSpawn);
        yield return (Gizmo) commandToggle;
        Command_Action commandAction1 = new Command_Action();
        ((Command) commandAction1).icon = (Texture) ContentFinder<Texture2D>.Get(this.Props.gizmoIconPath, true);
        ((Command) commandAction1).defaultLabel = TaggedString.op_Implicit(Translator.Translate("ReleaseDrones"));
        NamedArgument namedArgument1 = NamedArgument.op_Implicit(this.MaxCanSpawn);
        PawnKindDef droneKind = this.Props.droneKind;
        NamedArgument namedArgument2 = NamedArgument.op_Implicit(droneKind != null ? ((Def) droneKind).LabelCap : Translator.Translate("Drone"));
        ((Command) commandAction1).defaultDesc = TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("ReleaseDronesDesc", namedArgument1, namedArgument2));
        ((Gizmo) commandAction1).disabledReason = this.DisabledReason;
        commandAction1.action = new Action(this.TrySpawnDrones);
        Command_Action spawnCommand = commandAction1;
        if (this.cooldownTicksRemaining > 0)
          ((Gizmo) spawnCommand).Disable(TaggedString.op_Implicit(TaggedString.op_Addition(TaggedString.op_Addition(Translator.Translate("Cooldown"), ": "), GenTicks.ToStringSecondsFromTicks(this.cooldownTicksRemaining))));
        yield return (Gizmo) spawnCommand;
        if (DebugSettings.ShowDevGizmos)
        {
          Command_Action commandAction2 = new Command_Action();
          ((Command) commandAction2).defaultLabel = "DEV: Fill Resources";
          commandAction2.action = (Action) (() => this.AddIngredient(this.Props.fixedIngredient, this.Props.maxIngredientCount));
          yield return (Gizmo) commandAction2;
          Command_Action commandAction3 = new Command_Action();
          ((Command) commandAction3).defaultLabel = "DEV: Reset Cooldown";
          commandAction3.action = (Action) (() => this.cooldownTicksRemaining = 0);
          yield return (Gizmo) commandAction3;
          Command_Action commandAction4 = new Command_Action();
          ((Command) commandAction4).defaultLabel = "DEV: Toggle AutoSpawn";
          commandAction4.action = (Action) (() => this.autoSpawn = !this.autoSpawn);
          yield return (Gizmo) commandAction4;
        }
      }
    }

    private bool CanSpawnNow
    {
      get
      {
        return this.MaxCanSpawn > 0 && this.cooldownTicksRemaining <= 0 && ((Thing) this.parent).Spawned && this.parent is Pawn parent && !parent.Downed;
      }
    }

    private string DisabledReason
    {
      get
      {
        if (this.cooldownTicksRemaining > 0)
          return TaggedString.op_Implicit(Translator.Translate("CooldownActive"));
        if (this.MaxCanSpawn <= 0)
          return TaggedString.op_Implicit(Translator.Translate("InsufficientResources"));
        return this.parent is Pawn parent && parent.Downed ? TaggedString.op_Implicit(Translator.Translate("Incapacitated")) : string.Empty;
      }
    }

    public virtual void PostExposeData()
    {
      base.PostExposeData();
      Scribe_Deep.Look<ThingOwner>(ref this.innerContainer, "innerContainer", new object[1]
      {
        (object) this
      });
      Scribe_Values.Look<int>(ref this.cooldownTicksRemaining, "cooldownTicksRemaining", 0, false);
      Scribe_Values.Look<int>(ref this.maxToFill, "maxToFill", 0, false);
      Scribe_Values.Look<bool>(ref this.autoSpawn, "autoSpawn", false, false);
      Scribe_Collections.Look<Drone>(ref this.spawnedDrones, "spawnedDrones", (LookMode) 3, Array.Empty<object>());
    }

    public void GetChildHolders(List<IThingHolder> outChildren)
    {
      ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, (IList<Thing>) this.GetDirectlyHeldThings());
    }

    public ThingOwner GetDirectlyHeldThings() => this.innerContainer;

    public virtual void PostDestroy(DestroyMode mode, Map previousMap)
    {
      base.PostDestroy(mode, previousMap);
      this.innerContainer?.ClearAndDestroyContents((DestroyMode) 0);
      foreach (Drone spawnedDrone in this.spawnedDrones)
      {
        if (!((Thing) spawnedDrone).Destroyed)
          ((Thing) spawnedDrone).Destroy((DestroyMode) 0);
      }
    }

    public virtual void CompTick()
    {
      base.CompTick();
      if (this.cooldownTicksRemaining > 0)
        --this.cooldownTicksRemaining;
      if (!this.autoSpawn || this.cooldownTicksRemaining > 0 || this.MaxCanSpawn <= 0 || !((Thing) this.parent).Spawned || !(this.parent is Pawn parent) || parent.Downed)
        return;
      this.TrySpawnDrones();
    }
  }
}
