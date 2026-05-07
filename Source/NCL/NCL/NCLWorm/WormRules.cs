// Decompiled with JetBrains decompiler
// Type: NCLWorm.WormRules
// Assembly: NCL_WormBoss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B627DDDD-3AC9-4D6A-89F6-EA72F95FB570
// Assembly location: C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3555799437\1.6\Assemblies\NCL_WormBoss (3).dll

using Verse;

#nullable disable
namespace NCLWorm
{
  public static class WormRules
  {
    public static bool IsHead(int index) => index == 0;

    public static bool IsTail(int index, int total) => index == total - 1;

    public static bool EveryNth(int index, int n, int offset = 0) => n <= 0 || index % n == offset;

    public static bool InRange(int index, int minInclusive, int maxExclusive)
    {
      return index >= minInclusive && index < maxExclusive;
    }

    public static bool ChancePerTick(float chance) => Rand.Chance(chance);
  }
}
