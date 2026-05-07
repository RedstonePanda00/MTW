// Decompiled with JetBrains decompiler
// Type: NCLWorm.Verb_get_EquipmentSource_Patch
// Assembly: NCL_WormBoss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B627DDDD-3AC9-4D6A-89F6-EA72F95FB570
// Assembly location: C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3555799437\1.6\Assemblies\NCL_WormBoss (3).dll

using HarmonyLib;
using Verse;

#nullable disable
namespace NCLWorm
{
  [HarmonyPatch(typeof (Verb), "get_EquipmentSource")]
  public static class Verb_get_EquipmentSource_Patch
  {
    public static void Postfix(Verb __instance, ref ThingWithComps __result)
    {
      if (__result != null || !(__instance.DirectOwner is TC_WormWeaponController directOwner))
        return;
      __result = directOwner.parent;
    }
  }
}
