// Decompiled with JetBrains decompiler
// Type: NyarsModPackOne.CompProperties_DroneCarrier
// Assembly: NyearsModPackOne, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: FDB00AC3-5462-4449-9639-A371FA2E00F3
// Assembly location: C:\Users\15858\OneDrive\Documents\Sundry\RwModCoop\MTW\1.6\Assemblies\NyearsModPackOne.dll

using Verse;

#nullable disable
namespace NyarsModPackOne
{
  public class CompProperties_DroneCarrier : CompProperties
  {
    public ThingDef fixedIngredient;
    public int costPerDrone = 25;
    public int maxIngredientCount = 1000;
    public int cooldownTicks = 60000;
    public int maxDronesPerSpawn = 5;
    public int startingIngredientCount;
    public PawnKindDef droneKind;
    public string gizmoIconPath = "Races/ScutigerDrone_south";

    public CompProperties_DroneCarrier() => this.compClass = typeof (CompDroneCarrier);
  }
}
