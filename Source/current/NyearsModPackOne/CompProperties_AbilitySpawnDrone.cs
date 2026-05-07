// Decompiled with JetBrains decompiler
// Type: NyarsModPackOne.CompProperties_AbilitySpawnDrone
// Assembly: NyearsModPackOne, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: FDB00AC3-5462-4449-9639-A371FA2E00F3
// Assembly location: C:\Users\15858\OneDrive\Documents\Sundry\RwModCoop\MTW\1.6\Assemblies\NyearsModPackOne.dll

using RimWorld;
using Verse;

#nullable disable
namespace NyarsModPackOne
{
  public class CompProperties_AbilitySpawnDrone : CompProperties_AbilityEffect
  {
    public int droneCount = 1;
    public PawnKindDef droneKind;

    public CompProperties_AbilitySpawnDrone()
    {
      ((AbilityCompProperties) this).compClass = typeof (CompAbilityEffect_SpawnDrone);
    }
  }
}
