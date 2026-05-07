// Decompiled with JetBrains decompiler
// Type: NCLWorm.WormUtility
// Assembly: NCL_WormBoss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B627DDDD-3AC9-4D6A-89F6-EA72F95FB570
// Assembly location: C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3555799437\1.6\Assemblies\NCL_WormBoss (3).dll

using UnityEngine;
using Verse;

#nullable disable
namespace NCLWorm
{
  public static class WormUtility
  {
    public const float Y_TO_Z_SCALING = 0.84f;

    public static Vector3 PhysicsPosToDrawPos(Vector3 physicsPos)
    {
      Vector3 vector3;
      // ISSUE: explicit constructor call
      vector3 = new Vector3(physicsPos.x, 0.0f, physicsPos.z);
      float num = physicsPos.y * 0.84f;
      return new Vector3(vector3.x, 0.0f, vector3.z + num);
    }

    public static void DrawShadow(
      Vector3 drawPos,
      float height,
      float baseSize,
      Material shadowMat)
    {
      float num1 = Mathf.Clamp01((float) (1.0 - (double) height / 15.0));
      if ((double) num1 <= 0.10000000149011612)
        return;
      float num2 = baseSize * num1;
      Vector3 vector3 = drawPos;
      vector3.y = Altitudes.AltitudeFor((AltitudeLayer) 13);
      vector3.z -= height * 0.84f;
      Matrix4x4 matrix4x4 = Matrix4x4.TRS(vector3, Quaternion.identity, new Vector3(num2, 1f, num2));
      Graphics.DrawMesh(MeshPool.plane10, matrix4x4, shadowMat, 0);
    }

    public static Vector3 ClampToMap(Vector3 pos, Map map, int buffer = 5)
    {
      if (map == null)
        return pos;
      float num1 = (float) buffer;
      float num2 = (float) (map.Size.x - buffer);
      float num3 = (float) buffer;
      float num4 = (float) (map.Size.z - buffer);
      pos.x = Mathf.Clamp(pos.x, num1, num2);
      pos.z = Mathf.Clamp(pos.z, num3, num4);
      return pos;
    }

    public static IntVec3 ClampCellToMap(Map map, IntVec3 cell)
    {
      if (map == null || GenGrid.InBounds(cell, map))
        return cell;
      IntVec3 size = map.Size;
      return new IntVec3(Mathf.Clamp(cell.x, 0, size.x - 1), Mathf.Clamp(cell.y, 0, size.y - 1), Mathf.Clamp(cell.z, 0, size.z - 1));
    }

    public static IntVec3 ClampWorldPosToMapCell(Map map, Vector3 world)
    {
      return WormUtility.ClampCellToMap(map, IntVec3Utility.ToIntVec3(world));
    }
  }
}
