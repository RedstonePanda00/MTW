// Decompiled with JetBrains decompiler
// Type: NyarsModPackOne.HarmonyPatches.PatchMain
// Assembly: NyearsModPackOne, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: FDB00AC3-5462-4449-9639-A371FA2E00F3
// Assembly location: C:\Users\15858\OneDrive\Documents\Sundry\RwModCoop\MTW\1.6\Assemblies\NyearsModPackOne.dll

using HarmonyLib;
using System.Reflection;
using Verse;

#nullable disable
namespace NyarsModPackOne.HarmonyPatches
{
  [StaticConstructorOnStartup]
  public class PatchMain
  {
    static PatchMain()
    {
      new Harmony("NyarsModPackOne_HarmonyPatch").PatchAll(Assembly.GetExecutingAssembly());
    }
  }
}
