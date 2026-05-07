// Decompiled with JetBrains decompiler
// Type: Edited_BM_WeaponSummon.PreventWeaponDrop_Patch
// Assembly: WeaponBackpack, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 3D18544D-2643-4DE5-A8F8-3E0F3B074B2E
// Assembly location: C:\Users\15858\OneDrive\Documents\Sundry\RwModCoop\MTW\1.6\Assemblies\WeaponBackpack.dll

using HarmonyLib;
using RimWorld;
using Verse;

#nullable disable
namespace Edited_BM_WeaponSummon
{
  [HarmonyPatch(typeof (Pawn_EquipmentTracker), "TryDropEquipment")]
  public static class PreventWeaponDrop_Patch
  {
    private static bool Prefix(
      ThingWithComps eq,
      out ThingWithComps resultingEq,
      ref bool __result)
    {
      resultingEq = (ThingWithComps) null;
      if (eq == null || eq.GetComp<CompPreventDrop>() == null)
        return true;
      Messages.Message(Translator.Translate("CannotDropBoundWeapon"), MessageTypeDefOf.RejectInput, true);
      __result = false;
      return false;
    }
  }
}
