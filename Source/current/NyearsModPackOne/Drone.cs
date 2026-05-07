// Decompiled with JetBrains decompiler
// Type: NyarsModPackOne.Drone
// Assembly: NyearsModPackOne, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: FDB00AC3-5462-4449-9639-A371FA2E00F3
// Assembly location: C:\Users\15858\OneDrive\Documents\Sundry\RwModCoop\MTW\1.6\Assemblies\NyearsModPackOne.dll

using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

#nullable disable
namespace NyarsModPackOne
{
  public class Drone : Pawn
  {
    private const int DrawPosCycleTicks = 60;
    public Pawn owner;
    public int spawnTick;
    public bool activeExplosion;
    public static ModExtension_DroneProperties _modExtension;
    private const float ExplosionRadius = 7.9f;
    private static SimpleCurve drawPosCurve;

    public ModExtension_DroneProperties Props
    {
      get
      {
        return Drone._modExtension ?? (Drone._modExtension = ((Def) ((Thing) this).def).GetModExtension<ModExtension_DroneProperties>());
      }
    }

    public virtual Vector3 DrawPos
    {
      get
      {
        return Vector3.op_Addition(base.DrawPos, new Vector3(0.0f, 0.0f, Drone.drawPosCurve.Evaluate((float) ((Find.TickManager.TicksGame + ((Thing) this).thingIDNumber) % 60) / 60f)));
      }
    }

    protected virtual void Tick()
    {
      base.Tick();
      if (this.activeExplosion)
      {
        IntVec3 positionHeld = ((Thing) this).PositionHeld;
        Map mapHeld = ((Thing) this).MapHeld;
        this.SelfDestroy();
        GenExplosion.DoExplosion(positionHeld, mapHeld, 7.9f, DamageDefOf.Bomb, (Thing) null, 50, -1f, (SoundDef) null, (ThingDef) null, (ThingDef) null, (Thing) null, (ThingDef) null, 0.0f, 1, new GasType?(), new float?(), (int) byte.MaxValue, false, (ThingDef) null, 0.0f, 1, 0.0f, false, new float?(), (List<Thing>) null, new FloatRange?(), true, 1f, 0.0f, true, (ThingDef) null, 1f, (SimpleCurve) null, (List<IntVec3>) null, (ThingDef) null, (ThingDef) null);
      }
      else
      {
        if (Find.TickManager.TicksGame - this.spawnTick <= this.Props.maxSpawnTime)
          return;
        this.SelfDestroy();
      }
    }

    public virtual IEnumerable<Gizmo> GetGizmos()
    {
      foreach (Gizmo gizmo in base.GetGizmos())
        yield return gizmo;
      if (DebugSettings.ShowDevGizmos || ((Thing) this).Faction == Faction.OfPlayer)
      {
        Command_Action commandAction = new Command_Action();
        ((Command) commandAction).defaultLabel = "Boom!";
        ((Command) commandAction).defaultDesc = "Boom!";
        ((Command) commandAction).icon = (Texture) ContentFinder<Texture2D>.Get("UI/Commands/Detonate", true);
        commandAction.action = new Action(this.SelfDetonate);
        Command_Action action = commandAction;
        if (this.Downed || !this.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
          ((Gizmo) action).Disable(TaggedString.op_Implicit(Translator.Translate("Incapacitated")));
        yield return (Gizmo) action;
        Command_Action gizmo = new Command_Action();
        ((Command) gizmo).defaultLabel = "Destroy";
        ((Command) gizmo).defaultDesc = "Destroy";
        ((Command) gizmo).icon = (Texture) ContentFinder<Texture2D>.Get("UI/Commands/TryReconnect", true);
        gizmo.action = new Action(this.SelfDestroy);
        yield return (Gizmo) gizmo;
        action = (Command_Action) null;
      }
    }

    private void SelfDetonate()
    {
      Thing thing = (Thing) AttackTargetFinder.BestAttackTarget((IAttackTargetSearcher) this, (TargetScanFlags) 296, (Predicate<Thing>) (x => x.Faction != ((Thing) this).Faction), 0.0f, 9999f, ((Thing) this).PositionHeld, 999999f, false, true, false, false);
      if (thing == null)
        Messages.Message("No enemy", MessageTypeDefOf.RejectInput, false);
      else
        this.jobs.StartJob(JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("NCL_Dinergate_DroneDetonate", true), LocalTargetInfo.op_Implicit(thing)), (JobCondition) 1, (ThinkNode) null, false, true, (ThinkTreeDef) null, new JobTag?(), false, false, new bool?(), false, true, false);
    }

    private void SelfDestroy()
    {
      ((Thing) this).SetFaction((Faction) null, (Pawn) null);
      ((Thing) this).Kill(new DamageInfo?(), (Hediff) null);
      if (this.Corpse == null || ((Thing) this.Corpse).Destroyed)
        return;
      ((Thing) this.Corpse).Destroy((DestroyMode) 0);
    }

    public virtual void ExposeData()
    {
      base.ExposeData();
      Scribe_References.Look<Pawn>(ref this.owner, "owner", false);
      Scribe_Values.Look<int>(ref this.spawnTick, "spawnTick", 0, false);
      Scribe_Values.Look<bool>(ref this.activeExplosion, "activeExplosion", false, false);
    }

    public static Drone MakeNewDrone(Pawn origin, PawnKindDef droneKind)
    {
      PawnGenerationRequest generationRequest;
      // ISSUE: explicit constructor call
      ((PawnGenerationRequest) ref generationRequest).\u002Ector(droneKind, ((Thing) origin).Faction, (PawnGenerationContext) 2, new PlanetTile?(PlanetTile.op_Implicit(-1)), true, false, false, false, true, 1f, false, true, false, true, true, false, false, false, false, 0.0f, 0.0f, (Pawn) null, 1f, (Predicate<Pawn>) null, (Predicate<Pawn>) null, (IEnumerable<TraitDef>) null, (IEnumerable<TraitDef>) null, new float?(0.0f), new float?(origin.ageTracker.AgeBiologicalYearsFloat), new float?(origin.ageTracker.AgeChronologicalYearsFloat), new Gender?(), (string) null, (string) null, (RoyalTitleDef) null, (Ideo) null, false, false, false, false, (List<GeneDef>) null, (List<GeneDef>) null, (XenotypeDef) null, (CustomXenotype) null, (List<XenotypeDef>) null, 0.0f, (DevelopmentalStage) 8, (Func<XenotypeDef, PawnKindDef>) null, new FloatRange?(), new FloatRange?(), false, false, false, -1, 0, false);
      Pawn pawn = PawnGenerator.GeneratePawn(generationRequest);
      if (!(pawn is Drone drone))
      {
        Log.Error("生成无人机失败，生成的Pawn不是Drone类型。生成的是: " + pawn.GetType().Name);
        return (Drone) null;
      }
      drone.owner = origin;
      drone.spawnTick = Find.TickManager.TicksGame;
      return drone;
    }

    static Drone()
    {
      SimpleCurve simpleCurve = new SimpleCurve();
      simpleCurve.Add(new CurvePoint(0.0f, 0.0f), true);
      simpleCurve.Add(new CurvePoint(0.6f, 0.3f), true);
      simpleCurve.Add(new CurvePoint(1f, 0.0f), true);
      Drone.drawPosCurve = simpleCurve;
    }
  }
}
