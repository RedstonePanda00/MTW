// Decompiled with JetBrains decompiler
// Type: NCLWorm.ExoPortalAssets
// Assembly: NCL_WormBoss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B627DDDD-3AC9-4D6A-89F6-EA72F95FB570
// Assembly location: C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3555799437\1.6\Assemblies\NCL_WormBoss (3).dll

using UnityEngine;
using Verse;

#nullable disable
namespace NCLWorm
{
  [StaticConstructorOnStartup]
  public static class ExoPortalAssets
  {
    public static readonly Texture2D Tex_SpiralGalaxy = ExoPortalAssets.GenerateSpiralTexture(512);
    public static readonly Texture2D Tex_EventHorizon = ExoPortalAssets.GenerateHorizonTexture(512);
    public static readonly Material Mat_Accretion = MaterialPool.MatFrom(new MaterialRequest((Texture) ExoPortalAssets.Tex_SpiralGalaxy, ShaderDatabase.MoteGlow));
    public static readonly Material Mat_Singularity = new Material(ShaderDatabase.Transparent);

    static ExoPortalAssets()
    {
      ExoPortalAssets.Mat_Singularity.mainTexture = (Texture) ExoPortalAssets.Tex_EventHorizon;
      ExoPortalAssets.Mat_Singularity.renderQueue = 3200;
    }

    private static Texture2D GenerateSpiralTexture(int size)
    {
      Texture2D spiralTexture = new Texture2D(size, size, (TextureFormat) 5, false);
      Color[] colorArray = new Color[size * size];
      Vector2 vector2_1;
      // ISSUE: explicit constructor call
      vector2_1 = new Vector2((float) size / 2f, (float) size / 2f);
      float num1 = (float) size / 2f;
      for (int index1 = 0; index1 < size; ++index1)
      {
        for (int index2 = 0; index2 < size; ++index2)
        {
          Vector2 vector2_2 = (new Vector2((float) index2, (float) index1) - vector2_1);
          float num2 = Mathf.Clamp01(vector2_2.magnitude / num1);
          float num3 = Mathf.Clamp01((float) (((double) Mathf.Sin((Mathf.Atan2(vector2_2.y, vector2_2.x) + num2 * 12f) * 2f) + (double) (Mathf.PerlinNoise((float) index2 * 0.02f, (float) index1 * 0.02f) * 0.5f)) * (1.0 - (double) num2))) * Mathf.SmoothStep(0.0f, 0.3f, num2);
          colorArray[index1 * size + index2] = new Color(1f, 1f, 1f, num3);
        }
      }
      spiralTexture.SetPixels(colorArray);
      spiralTexture.Apply();
      return spiralTexture;
    }

    private static Texture2D GenerateHorizonTexture(int size)
    {
      Texture2D horizonTexture = new Texture2D(size, size, (TextureFormat) 5, false);
      Color[] colorArray = new Color[size * size];
      Vector2 vector2;
      // ISSUE: explicit constructor call
      vector2 = new Vector2((float) size / 2f, (float) size / 2f);
      float num1 = (float) size / 2f;
      float num2 = 0.6f;
      float num3 = 0.15f;
      for (int index1 = 0; index1 < size; ++index1)
      {
        for (int index2 = 0; index2 < size; ++index2)
        {
          float num4 = Vector2.Distance(new Vector2((float) index2, (float) index1), vector2) / num1;
          Color clear = Color.clear;
          if ((double) num4 < (double) num2)
          {
            // ISSUE: explicit constructor call
            clear = new Color(0.0f, 0.0f, 0.0f, 1f);
          }
          else if ((double) num4 < (double) num2 + (double) num3)
          {
            float num5 = Mathf.Pow(1f - (num4 - num2) / num3, 2f);
            // ISSUE: explicit constructor call
            clear = new Color(1f, 1f, 1f, num5);
          }
          colorArray[index1 * size + index2] = clear;
        }
      }
      horizonTexture.SetPixels(colorArray);
      horizonTexture.Apply();
      return horizonTexture;
    }
  }
}
