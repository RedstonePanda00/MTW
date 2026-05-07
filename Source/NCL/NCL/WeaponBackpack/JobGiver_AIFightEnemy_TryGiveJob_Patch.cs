// Decompiled with JetBrains decompiler
// Type: Edited_BM_WeaponSummon.JobGiver_AIFightEnemy_TryGiveJob_Patch
// Assembly: WeaponBackpack, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 3D18544D-2643-4DE5-A8F8-3E0F3B074B2E
// Assembly location: C:\Users\15858\OneDrive\Documents\Sundry\RwModCoop\MTW\1.6\Assemblies\WeaponBackpack.dll

using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;
using Verse.AI;

#nullable disable
namespace Edited_BM_WeaponSummon
{
  [HarmonyPatch(typeof (JobGiver_AIFightEnemy), "TryGiveJob")]
  public static class JobGiver_AIFightEnemy_TryGiveJob_Patch
  {
    public static void Postfix(ref Job __result, Pawn pawn)
    {
      if (__result == null)
        return;
      JobGiver_AIFightEnemy_TryGiveJob_Patch.TrySwapToWeaponSummon(ref __result, pawn);
    }

    private static void TrySwapToWeaponSummon(ref Job __result, Pawn pawn)
    {
      if (pawn?.equipment?.Primary?.GetComp<CompSummonedWeapon>() != null)
        return;
      foreach (AbilityDef abilityDef in GenCollection.InRandomOrder<AbilityDef>(DefDatabase<AbilityDef>.AllDefs, (IList<AbilityDef>) null))
      {
        List<AbilityCompProperties> comps = abilityDef.comps;
        if (comps != null && comps.OfType<CompProperties_SummonWeapon>().Any<CompProperties_SummonWeapon>())
        {
          try
          {
            JobGiver_AICastSummonWeapon castSummonWeapon = new JobGiver_AICastSummonWeapon();
            typeof (JobGiver_AICastAbility).GetField("ability", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue((object) castSummonWeapon, (object) abilityDef);
            MethodInfo method = typeof (JobGiver_AICastAbility).GetMethod("TryGiveJob", BindingFlags.Instance | BindingFlags.NonPublic);
            object obj = method?.Invoke(castSummonWeapon, new object[] { pawn });
            Job job = (Job)obj;
            if (job != null)
            {
              __result = job;
              break;
            }
          }
          catch (Exception ex)
          {
            Log.Error(string.Format("[武器召唤] 创建任务失败: {0}", (object) ex));
          }
        }
      }
    }
  }
}
