// Decompiled with JetBrains decompiler
// Type: NCLWorm.Part_Fader
// Assembly: NCL_WormBoss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B627DDDD-3AC9-4D6A-89F6-EA72F95FB570
// Assembly location: C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3555799437\1.6\Assemblies\NCL_WormBoss (3).dll

using UnityEngine;
using Verse;

#nullable disable
namespace NCLWorm
{
  public class Part_Fader : Part_Glow
  {
    public Color colorStart = new Color(1f, 1f, 1f, 0.0f);
    public Color colorEnd = Color.white;

    public override void Draw(
      Vector3 rootPos,
      Quaternion rootRot,
      Vector3 rootScale,
      float animateFactor,
      MaterialPropertyBlock mpb)
    {
      if (this.Mat == null || (double) animateFactor <= 0.0099999997764825821 && (double) this.colorStart.a <= 0.0099999997764825821)
        return;
      mpb.SetColor(ShaderPropertyIDs.Color, Color.Lerp(this.colorStart, this.colorEnd, animateFactor));
      base.Draw(rootPos, rootRot, rootScale, animateFactor, mpb);
    }
  }
}
