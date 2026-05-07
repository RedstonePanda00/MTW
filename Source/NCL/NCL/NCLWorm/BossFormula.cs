// Decompiled with JetBrains decompiler
// Type: NCLWorm.BossFormula
// Assembly: NCL_WormBoss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B627DDDD-3AC9-4D6A-89F6-EA72F95FB570
// Assembly location: C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3555799437\1.6\Assemblies\NCL_WormBoss (3).dll

using RimWorld;
using System.Collections.Generic;
using Verse;

#nullable disable
namespace NCLWorm
{
  public static class BossFormula
  {
    public static float CalculateTrueDamage(Thing target, float limbBreakFactor)
    {
      if (target == null || target.Destroyed)
        return 0.0f;
      float num1 = 100f;
      switch (target)
      {
        case Pawn pawn:
          float num2 = 0.0f;
          int num3 = 0;
          List<BodyPartRecord> allParts = pawn.RaceProps.body.AllParts;
          for (int index = 0; index < allParts.Count; ++index)
          {
            BodyPartRecord bodyPartRecord = allParts[index];
            if (bodyPartRecord.depth == BodyPartDepth.Outside)
            {
              num2 += bodyPartRecord.def.GetMaxHealth(pawn);
              ++num3;
            }
          }
          num1 = num3 <= 0 ? 50f : num2 / (float) num3;
          break;
        case Building building:
          num1 = (float) ((Thing) building).MaxHitPoints * 0.25f;
          break;
      }
      float num4 = num1 * limbBreakFactor;
      if ((double) num4 < 1.0)
        num4 = 1f;
      float num5 = StatExtension.GetStatValue(target, StatDefOf.IncomingDamageFactor, true, -1);
      if ((double) num5 <= 0.0)
        num5 = 1f;
      else if ((double) num5 < 0.05000000074505806)
        num5 = 0.05f;
      return num4 / num5;
    }
  }
}
