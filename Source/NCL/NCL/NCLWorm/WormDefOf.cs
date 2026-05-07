// Decompiled with JetBrains decompiler
// Type: NCLWorm.WormDefOf
// Assembly: NCL_WormBoss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B627DDDD-3AC9-4D6A-89F6-EA72F95FB570
// Assembly location: C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3555799437\1.6\Assemblies\NCL_WormBoss (3).dll

using RimWorld;
using Verse;

#nullable disable
namespace NCLWorm
{
  [DefOf]
  public static class WormDefOf
  {
    public static ThingDef Mst_Worm_Head;
    public static ThingDef Mst_Worm_Body_Normal;
    public static ThingDef Mst_Worm_Tail;
    public static ThingDef Mst_ExoTelegraphCone;
    public static ThingDef Mst_WormRiftPath;
    public static ThingDef Mst_ExoRedPortal;
    public static ThingDef Mst_HadesLaserEffect;
    public static ThingDef Mst_StaticTornado;
    [MayRequire("Ludeon.RimWorld")]
    public static WeatherDef RainyThunderstorm;

    static WormDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof (WormDefOf));
  }
}
