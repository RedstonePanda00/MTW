// Decompiled with JetBrains decompiler
// Type: NCLWorm.TelegraphConePool
// Assembly: NCL_WormBoss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B627DDDD-3AC9-4D6A-89F6-EA72F95FB570
// Assembly location: C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3555799437\1.6\Assemblies\NCL_WormBoss (3).dll

using System.Collections.Generic;
using UnityEngine;
using Verse;

#nullable disable
namespace NCLWorm
{
  public static class TelegraphConePool
  {
    private static Stack<ExoTelegraphCone> _pool = new Stack<ExoTelegraphCone>();

    public static ExoTelegraphCone Get(Map map, Vector3 spawnPos)
    {
      if (map == null)
        return (ExoTelegraphCone) null;
      IntVec3 mapCell = WormUtility.ClampWorldPosToMapCell(map, spawnPos);
      while (TelegraphConePool._pool.Count > 0)
      {
        ExoTelegraphCone exoTelegraphCone = TelegraphConePool._pool.Pop();
        if (exoTelegraphCone != null && !exoTelegraphCone.Destroyed)
        {
          exoTelegraphCone.Position = mapCell;
          if (!exoTelegraphCone.Spawned)
            GenSpawn.Spawn((Thing) exoTelegraphCone, mapCell, map, (WipeMode) 0);
          exoTelegraphCone.ResetForPool();
          return exoTelegraphCone;
        }
      }
      ExoTelegraphCone exoTelegraphCone1 = (ExoTelegraphCone) ThingMaker.MakeThing(WormDefOf.Mst_ExoTelegraphCone, (ThingDef) null);
      GenSpawn.Spawn((Thing) exoTelegraphCone1, mapCell, map, (WipeMode) 0);
      return exoTelegraphCone1;
    }

    public static void Return(ExoTelegraphCone cone)
    {
      if (cone == null || cone.Destroyed)
        return;
      cone.PrepareForPool();
      if (!cone.Spawned)
        return;
      TelegraphConePool._pool.Push(cone);
    }

    public static void ClearAll()
    {
      while (TelegraphConePool._pool.Count > 0)
      {
        ExoTelegraphCone exoTelegraphCone = TelegraphConePool._pool.Pop();
        if (exoTelegraphCone != null && !exoTelegraphCone.Destroyed)
          ((Thing) exoTelegraphCone).Destroy((DestroyMode) 0);
      }
    }
  }
}
