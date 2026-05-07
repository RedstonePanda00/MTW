// Decompiled with JetBrains decompiler
// Type: NCLWorm.Part_Rotator
// Assembly: NCL_WormBoss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B627DDDD-3AC9-4D6A-89F6-EA72F95FB570
// Assembly location: C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3555799437\1.6\Assemblies\NCL_WormBoss (3).dll

using UnityEngine;
using Verse;

#nullable disable
namespace NCLWorm
{
  public class Part_Rotator : WormVisualPart
  {
    public float baseAngle = 0.0f;
    public float rotateAngle = 45f;

    public override void Draw(
      Vector3 rootPos,
      Quaternion rootRot,
      Vector3 rootScale,
      float animateFactor,
      MaterialPropertyBlock mpb)
    {
      if (this.Mat == null)
        return;
      Quaternion quaternion = Quaternion.Euler(0.0f, this.baseAngle + this.rotateAngle * animateFactor, 0.0f);
      Vector3 posWithOrder = this.GetPosWithOrder((rootPos + (rootRot * this.offset)));
      Vector3 vector3 = this.useBodyDrawSize ? rootScale : new Vector3(this.drawSize.x, 1f, this.drawSize.y);
      Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(posWithOrder, (rootRot * quaternion), vector3), this.Mat, 0, (Camera) null, 0, mpb);
    }
  }
}
