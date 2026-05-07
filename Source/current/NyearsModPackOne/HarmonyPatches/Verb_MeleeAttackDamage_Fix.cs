// Decompiled with JetBrains decompiler
// Type: NyarsModPackOne.HarmonyPatches.Verb_MeleeAttackDamage_Fix
// Assembly: NyearsModPackOne, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: FDB00AC3-5462-4449-9639-A371FA2E00F3
// Assembly location: C:\Users\15858\OneDrive\Documents\Sundry\RwModCoop\MTW\1.6\Assemblies\NyearsModPackOne.dll

using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

#nullable disable
namespace NyarsModPackOne.HarmonyPatches
{
  [StaticConstructorOnStartup]
  public class Verb_MeleeAttackDamage_Fix
  {
    [HarmonyPatch(typeof (Verb_MeleeAttackDamage), "ApplyMeleeDamageToTarget")]
    private static class ApplyMeleeDamageToTarget_PreFix
    {
      [HarmonyPrefix]
      private static void Prefix(Verb_MeleeAttackDamage __instance, LocalTargetInfo target)
      {
        ModExtension_EffecterOnMelee modExtension = ((Def) ((Verb) __instance).Caster?.def).GetModExtension<ModExtension_EffecterOnMelee>();
        if (modExtension == null)
          return;
        Vector3 vector3_1 = GenThing.TrueCenter(((Verb) __instance).Caster);
        IntVec3 position = ((Verb) __instance).Caster.Position;
        Vector3 centerVector3 = ((LocalTargetInfo) ref target).CenterVector3;
        IntVec3 cell = ((LocalTargetInfo) ref target).Cell;
        Map map = ((Verb) __instance).Caster.Map;
        float num = 0.2f;
        foreach (FleckDef fleckDef in (IEnumerable<FleckDef>) modExtension.flecksAtTarget ?? Enumerable.Empty<FleckDef>())
        {
          Vector3 vector3_2;
          // ISSUE: explicit constructor call
          ((Vector3) ref vector3_2).\u002Ector(Rand.Range(-num, num), 0.0f, Rand.Range(-num, num));
          FleckCreationData dataStatic = FleckMaker.GetDataStatic(Vector3.op_Addition(centerVector3, vector3_2), map, fleckDef, 1f);
          dataStatic.rotation = Rand.Range(0.0f, 360f);
          map.flecks.CreateFleck(dataStatic);
        }
        foreach (EffecterDef effecterDef in (IEnumerable<EffecterDef>) modExtension.effectersAtTarget ?? Enumerable.Empty<EffecterDef>())
          effecterDef.Spawn().Trigger(new TargetInfo(cell, map, false), new TargetInfo(cell, map, false), 300);
        foreach (ThingDef thingDef in (IEnumerable<ThingDef>) modExtension.motesAtTarget ?? Enumerable.Empty<ThingDef>())
        {
          Vector3 vector3_3;
          // ISSUE: explicit constructor call
          ((Vector3) ref vector3_3).\u002Ector(Rand.Range(-num, num), 0.0f, Rand.Range(-num, num));
          MoteMaker.MakeStaticMote(Vector3.op_Addition(centerVector3, vector3_3), map, thingDef, 1f, false, Rand.Range(0.0f, 360f));
        }
        foreach (FleckDef fleckDef in (IEnumerable<FleckDef>) modExtension.flecksAtCaster ?? Enumerable.Empty<FleckDef>())
        {
          Vector3 vector3_4;
          // ISSUE: explicit constructor call
          ((Vector3) ref vector3_4).\u002Ector(Rand.Range(-num, num), 0.0f, Rand.Range(-num, num));
          FleckCreationData dataStatic = FleckMaker.GetDataStatic(Vector3.op_Addition(vector3_1, vector3_4), map, fleckDef, 1f);
          dataStatic.rotation = Rand.Range(0.0f, 360f);
          map.flecks.CreateFleck(dataStatic);
        }
        foreach (EffecterDef effecterDef in (IEnumerable<EffecterDef>) modExtension.effectersAtCaster ?? Enumerable.Empty<EffecterDef>())
          effecterDef.Spawn().Trigger(new TargetInfo(position, map, false), new TargetInfo(position, map, false), 300);
        foreach (ThingDef thingDef in (IEnumerable<ThingDef>) modExtension.motesAtCaster ?? Enumerable.Empty<ThingDef>())
        {
          Vector3 vector3_5;
          // ISSUE: explicit constructor call
          ((Vector3) ref vector3_5).\u002Ector(Rand.Range(-num, num), 0.0f, Rand.Range(-num, num));
          MoteMaker.MakeStaticMote(Vector3.op_Addition(vector3_1, vector3_5), map, thingDef, 1f, false, Rand.Range(0.0f, 360f));
        }
        foreach (FleckDef fleckDef in (IEnumerable<FleckDef>) modExtension.flecksLinkLine ?? Enumerable.Empty<FleckDef>())
          FleckMaker.ConnectingLine(vector3_1, centerVector3, fleckDef, map, 1f);
        foreach (ThingDef thingDef in (IEnumerable<ThingDef>) modExtension.motesLinkLine ?? Enumerable.Empty<ThingDef>())
          MoteMaker.MakeConnectingLine(vector3_1, centerVector3, thingDef, map, 1f);
      }
    }
  }
}
