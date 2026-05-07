// Decompiled with JetBrains decompiler
// Type: NyarsModPackOne.CompAbilityEffect_SpawnDrone
// Assembly: NyearsModPackOne, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: FDB00AC3-5462-4449-9639-A371FA2E00F3
// Assembly location: C:\Users\15858\OneDrive\Documents\Sundry\RwModCoop\MTW\1.6\Assemblies\NyearsModPackOne.dll

using RimWorld;
using Verse;

#nullable disable
namespace NyarsModPackOne
{
  public class CompAbilityEffect_SpawnDrone : CompAbilityEffect
  {
    public CompProperties_AbilitySpawnDrone Props
    {
      get => (CompProperties_AbilitySpawnDrone) ((AbilityComp) this).props;
    }

    public virtual void Apply(LocalTargetInfo target, LocalTargetInfo dest)
    {
      Pawn pawn = ((AbilityComp) this).parent.pawn;
      PawnKindDef droneKind = this.Props.droneKind ?? PawnKindDef.Named("NCL_Dinergate_Drone");
      for (int index = 0; index < this.Props.droneCount; ++index)
      {
        Drone drone = Drone.MakeNewDrone(pawn, droneKind);
        if (drone != null)
          GenSpawn.Spawn((Thing) drone, ((Thing) pawn).PositionHeld, ((Thing) pawn).Map, (WipeMode) 2);
      }
    }
  }
}
