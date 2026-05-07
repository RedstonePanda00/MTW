// Decompiled with JetBrains decompiler
// Type: NCLWorm.Part_Slider
// Assembly: NCL_WormBoss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B627DDDD-3AC9-4D6A-89F6-EA72F95FB570
// Assembly location: C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3555799437\1.6\Assemblies\NCL_WormBoss (3).dll

using UnityEngine;
using Verse;

#nullable disable
namespace NCLWorm
{
  public class Part_Slider : WormVisualPart
  {
    public Vector3 slideDir = Vector3.right;
    public float slideDist = 0.35f;
    public bool mirrorTexture = false;

    public override void Draw(
      Vector3 rootPos,
      Quaternion rootRot,
      Vector3 rootScale,
      float animateFactor,
      MaterialPropertyBlock mpb)
    {
      if (this.Mat == null)
        return;
      Vector3 vector3_1 = (this.offset + (this.slideDir.normalized * animateFactor * this.slideDist));
      Vector3 posWithOrder = this.GetPosWithOrder((rootPos + (rootRot * vector3_1)));
      Vector3 vector3_2 = this.useBodyDrawSize ? rootScale : new Vector3(this.drawSize.x, 1f, this.drawSize.y);
      if (this.mirrorTexture)
        vector3_2.x = -vector3_2.x;
      Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(posWithOrder, rootRot, vector3_2), this.Mat, 0, (Camera) null, 0, mpb);
    }
  }
}
