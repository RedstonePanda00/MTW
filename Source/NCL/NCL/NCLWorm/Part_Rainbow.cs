// Decompiled with JetBrains decompiler
// Type: NCLWorm.Part_Rainbow
// Assembly: NCL_WormBoss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B627DDDD-3AC9-4D6A-89F6-EA72F95FB570
// Assembly location: C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3555799437\1.6\Assemblies\NCL_WormBoss (3).dll

using UnityEngine;
using Verse;

#nullable disable
namespace NCLWorm
{
  public class Part_Rainbow : Part_Glow
  {
    public float cycleSpeed = 0.5f;
    public float saturation = 1f;
    public float value = 1f;

    public override void Draw(
      Vector3 rootPos,
      Quaternion rootRot,
      Vector3 rootScale,
      float animateFactor,
      MaterialPropertyBlock mpb)
    {
      if (this.Mat == null || (double) animateFactor < 0.0099999997764825821)
        return;
      float num = (float) ((double) Time.time * (double) this.cycleSpeed % 1.0);
      mpb.SetColor(ShaderPropertyIDs.Color, Color.HSVToRGB(num, this.saturation, this.value));
      base.Draw(rootPos, rootRot, rootScale, animateFactor, mpb);
    }
  }
}
