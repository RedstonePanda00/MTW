// Decompiled with JetBrains decompiler
// Type: NCLWorm.WormVisualPart
// Assembly: NCL_WormBoss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B627DDDD-3AC9-4D6A-89F6-EA72F95FB570
// Assembly location: C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3555799437\1.6\Assemblies\NCL_WormBoss (3).dll

using UnityEngine;
using Verse;

#nullable disable
namespace NCLWorm
{
  public abstract class WormVisualPart
  {
    public string texPath;
    public int sortOrder = 0;
    public Vector3 offset = Vector3.zero;
    public Vector2 drawSize = Vector2.one;
    public bool useBodyDrawSize = true;
    private Material _mat;

    public Material Mat
    {
      get
      {
        return this._mat ?? (this._mat = !string.IsNullOrEmpty(this.texPath) ? this.LoadMaterial() : (Material) null);
      }
    }

    protected virtual Material LoadMaterial()
    {
      return MaterialPool.MatFrom(this.texPath, ShaderDatabase.Cutout);
    }

    public abstract void Draw(
      Vector3 rootPos,
      Quaternion rootRot,
      Vector3 rootScale,
      float animateFactor,
      MaterialPropertyBlock mpb);

    protected Vector3 GetPosWithOrder(Vector3 pos)
    {
      return (pos + (Vector3.up * (float) this.sortOrder * 0.0002f));
    }
  }
}
