// Decompiled with JetBrains decompiler
// Type: PenetrateThickRoof.PenetrateThickRoof
// Assembly: Penetrate Thick Roof, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 808ED690-E947-44D5-B572-4575C3E2C7AA
// Assembly location: C:\Users\15858\OneDrive\Documents\Sundry\RwModCoop\MTW\1.6\Assemblies\Penetrate Thick Roof.dll

using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Verse;

#nullable enable
namespace PenetrateThickRoof
{
  [StaticConstructorOnStartup]
  public static class PenetrateThickRoof
  {
    public static readonly Harmony harmony = new Harmony("AmCh.PenetrateThickRoof");
    public static readonly MethodInfo original = typeof (Projectile).GetMethod("ImpactSomething", BindingFlags.Instance | BindingFlags.NonPublic);
    public static readonly HarmonyMethod transpiler = new HarmonyMethod(typeof (PenetrateThickRoof.PenetrateThickRoof).GetMethod("Transpiler"));

    static PenetrateThickRoof()
    {
      PenetrateThickRoof.PenetrateThickRoof.harmony.Patch((MethodBase) PenetrateThickRoof.PenetrateThickRoof.original, transpiler: PenetrateThickRoof.PenetrateThickRoof.transpiler);
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
      List<CodeInstruction> list = instructions.ToList<CodeInstruction>();
      List<CodeInstruction> codeInstructionList = new List<CodeInstruction>(list.Count + 3);
      FieldInfo field = typeof (RoofDef).GetField("isThickRoof");
      MethodInfo method = typeof (PenetrateThickRoof.PenetrateThickRoof).GetMethod("ShouldBypassThickRoof");
      bool flag = false;
      for (int index = 0; index < list.Count; ++index)
      {
        codeInstructionList.Add(list[index]);
        if (!flag && index >= 3 && list[index - 3].opcode == OpCodes.Brfalse && list[index - 1].LoadsField(field) && list[index].opcode == OpCodes.Brfalse_S)
        {
          flag = true;
          codeInstructionList.Add(new CodeInstruction(OpCodes.Ldarg_0));
          codeInstructionList.Add(new CodeInstruction(OpCodes.Call, (object) method));
          codeInstructionList.Add(new CodeInstruction(OpCodes.Brtrue, list[index - 3].operand));
        }
      }
      return (IEnumerable<CodeInstruction>) codeInstructionList;
    }

    public static bool ShouldBypassThickRoof(Thing thing)
    {
      return ((Def) thing.def).HasModExtension<PenetrateThickRoofExtension>();
    }
  }
}
