// Decompiled with JetBrains decompiler
// Type: NCLWorm.WormWarpVisuals
// Assembly: NCL_WormBoss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B627DDDD-3AC9-4D6A-89F6-EA72F95FB570
// Assembly location: C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3555799437\1.6\Assemblies\NCL_WormBoss (3).dll

using UnityEngine;
using Verse;

#nullable disable
namespace NCLWorm
{
  public static class WormWarpVisuals
  {
    public static void TriggerPortalOpen(Vector3 pos, Map map, float size = 7f, int duration = 120)
    {
      if (map == null)
        return;
      ThingDef mstExoRedPortal = WormDefOf.Mst_ExoRedPortal;
      if (mstExoRedPortal != null)
      {
        IntVec3 mapCell = WormUtility.ClampWorldPosToMapCell(map, pos);
        ExoRedPortal exoRedPortal = (ExoRedPortal) ThingMaker.MakeThing(mstExoRedPortal, (ThingDef) null);
        exoRedPortal.MaxScale = size;
        exoRedPortal.Duration = duration;
        GenSpawn.Spawn((Thing) exoRedPortal, mapCell, map, (WipeMode) 0);
      }
      Find.CameraDriver.shaker.DoShake(0.5f);
    }
  }
}
