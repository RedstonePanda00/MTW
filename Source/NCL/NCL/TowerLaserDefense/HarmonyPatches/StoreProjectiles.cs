// Decompiled with JetBrains decompiler
// Type: TowerLaserDefense.HarmonyPatches.StoreProjectiles
// Assembly: TowerLaserDefense, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: D672ED81-8E05-4DF5-BFC2-02EBAE2446C3
// Assembly location: C:\Users\15858\OneDrive\Documents\Sundry\RwModCoop\MTW\1.6\Assemblies\TowerLaserDefense.dll

using HarmonyLib;
using Verse;

#nullable disable
namespace TowerLaserDefense.HarmonyPatches
{
  [StaticConstructorOnStartup]
  public class StoreProjectiles
  {
    [HarmonyPatch(typeof (ThingWithComps), "SpawnSetup")]
    private static class ProjectilesSpawn_PostFix
    {
      [HarmonyPostfix]
      private static void Postfix(ThingWithComps __instance)
      {
        if (!(__instance is Projectile projectile))
          return;
        GameComponent_BulletsCache.BulletsCache.Add((Thing) projectile);
      }
    }

    [HarmonyPatch(typeof (ThingWithComps), "DeSpawn")]
    private static class ProjectilesDeSpawn_PostFix
    {
      [HarmonyPostfix]
      private static void Postfix(ThingWithComps __instance)
      {
        if (!(__instance is Projectile projectile))
          return;
        GameComponent_BulletsCache.BulletsCache.Remove((Thing) projectile);
      }
    }
  }
}
