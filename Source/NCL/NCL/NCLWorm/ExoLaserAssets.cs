// Decompiled with JetBrains decompiler
// Type: NCLWorm.ExoLaserAssets
// Assembly: NCL_WormBoss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B627DDDD-3AC9-4D6A-89F6-EA72F95FB570
// Assembly location: C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3555799437\1.6\Assemblies\NCL_WormBoss (3).dll

using UnityEngine;
using Verse;

#nullable disable
namespace NCLWorm
{
  [StaticConstructorOnStartup]
  public static class ExoLaserAssets
  {
    public static readonly Texture2D Tex_LaserStreak = ExoLaserAssets.GenerateStreakTexture(512, 128);
    public static readonly Texture2D Tex_NoiseGrain = ExoLaserAssets.GenerateSparseEnergyTexture(256, 256);
    public static readonly Texture2D Tex_LaserSolid = ExoLaserAssets.GenerateSolidTexture(128, 64);
    public static readonly Texture2D Tex_LaserGlow = ExoLaserAssets.GenerateGlowTexture(128, 32);
    public static readonly Texture2D Tex_Flare = ExoLaserAssets.GenerateRadialGradientTexture(128, 128);
    public static readonly Texture2D Tex_PortalSpiral = ExoLaserAssets.GenerateSpiralTexture(256, 256);
    public static readonly Material Mat_Core;
    public static readonly Material Mat_Streak;
    public static readonly Material Mat_Grain;
    public static readonly Material Mat_Glow = ExoLaserAssets.CreateAdditiveMaterial(ExoLaserAssets.Tex_LaserGlow, 3000);
    public static readonly Material Mat_Flare;
    public static readonly Material Mat_VoidDark;
    public static readonly Material Mat_SpiralGlow;

    static ExoLaserAssets()
    {
      ExoLaserAssets.Mat_Streak = ExoLaserAssets.CreateAdditiveMaterial(ExoLaserAssets.Tex_LaserStreak, 3001);
      ExoLaserAssets.Mat_Grain = ExoLaserAssets.CreateAdditiveMaterial(ExoLaserAssets.Tex_NoiseGrain, 3002);
      ExoLaserAssets.Mat_Core = ExoLaserAssets.CreateAdditiveMaterial(ExoLaserAssets.Tex_LaserSolid, 3003);
      ExoLaserAssets.Mat_Flare = ExoLaserAssets.CreateAdditiveMaterial(ExoLaserAssets.Tex_Flare, 3004);
      ExoLaserAssets.Mat_VoidDark = MaterialPool.MatFrom(new MaterialRequest((Texture) ExoLaserAssets.Tex_PortalSpiral, ShaderDatabase.Transparent));
      ExoLaserAssets.Mat_SpiralGlow = ExoLaserAssets.CreateAdditiveMaterial(ExoLaserAssets.Tex_PortalSpiral, 3500);
    }

    private static Material CreateAdditiveMaterial(Texture2D tex, int renderQueue)
    {
      Material additiveMaterial = new Material(ShaderDatabase.MoteGlow);
      additiveMaterial.mainTexture = (Texture) tex;
      additiveMaterial.color = Color.white;
      additiveMaterial.renderQueue = renderQueue;
      additiveMaterial.SetInt("_SrcBlend", 1);
      additiveMaterial.SetInt("_DstBlend", 1);
      additiveMaterial.SetInt("_ZWrite", 0);
      return additiveMaterial;
    }

    private static Texture2D GenerateSpiralTexture(int width, int height)
    {
      Texture2D spiralTexture = new Texture2D(width, height, (TextureFormat) 5, false);
      Color[] colorArray = new Color[width * height];
      Vector2 vector2_1;
      // ISSUE: explicit constructor call
      vector2_1 = new Vector2((float) width / 2f, (float) height / 2f);
      float num1 = (float) width / 2f;
      for (int index1 = 0; index1 < height; ++index1)
      {
        for (int index2 = 0; index2 < width; ++index2)
        {
          Vector2 vector2_2;
          // ISSUE: explicit constructor call
          vector2_2 = new Vector2((float) index2, (float) index1);
          float num2 = Vector2.Distance(vector2_2, vector2_1) / num1;
          float num3 = Mathf.Clamp01((float) (((double) Mathf.Sin((float) ((double) Mathf.Atan2((float) index1 - vector2_1.y, (float) index2 - vector2_1.x) * 3.0 + (double) num2 * 15.0)) + 1.0) * 0.5)) * Mathf.Pow(1f - num2, 2f) * Mathf.SmoothStep(0.0f, 0.2f, num2) * (float) (0.5 + (double) Mathf.PerlinNoise((float) index2 * 0.1f, (float) index1 * 0.1f) * 0.5);
          colorArray[index1 * width + index2] = new Color(1f, 1f, 1f, num3);
        }
      }
      spiralTexture.SetPixels(colorArray);
      spiralTexture.Apply();
      ((Texture) spiralTexture).wrapMode = (TextureWrapMode) 1;
      return spiralTexture;
    }

    private static Texture2D GenerateSparseEnergyTexture(int width, int height)
    {
      Texture2D sparseEnergyTexture = new Texture2D(width, height, (TextureFormat) 5, false);
      Color[] colorArray = new Color[width * height];
      float num1 = Random.Range(0.0f, 100f);
      for (int index1 = 0; index1 < height; ++index1)
      {
        float num2 = (float) index1 / (float) height;
        float num3 = Mathf.Sin(num2 * 3.14159274f);
        for (int index2 = 0; index2 < width; ++index2)
        {
          float num4 = (float) index2 / (float) width;
          float num5 = Mathf.Clamp01((float) (((double) Mathf.PerlinNoise(num1 + num4 * 15f, num2 * 40f) - 0.60000002384185791) * 4.0)) * num3;
          colorArray[index1 * width + index2] = new Color(num5, num5, num5, num5);
        }
      }
      sparseEnergyTexture.SetPixels(colorArray);
      sparseEnergyTexture.Apply();
      ((Texture) sparseEnergyTexture).wrapMode = (TextureWrapMode) 0;
      ((Texture) sparseEnergyTexture).filterMode = (FilterMode) 0;
      return sparseEnergyTexture;
    }

    private static Texture2D GenerateStreakTexture(int width, int height)
    {
      Texture2D streakTexture = new Texture2D(width, height, (TextureFormat) 5, false);
      Color[] colorArray = new Color[width * height];
      for (int index1 = 0; index1 < height; ++index1)
      {
        float num1 = (float) index1 / (float) height;
        float num2 = Mathf.Abs(num1 - 0.5f) * 2f;
        float num3 = Mathf.Exp(-5f * num2 * num2);
        float num4 = 0.0f;
        for (int index2 = 0; index2 < 3; ++index2)
          num4 += Mathf.PerlinNoise((float) index2 * 10f, num1 * 50f * (float) (index2 + 1)) * (1f / (float) (index2 + 1));
        float num5 = Mathf.Clamp01((float) (0.30000001192092896 + (double) num4 * 0.699999988079071));
        for (int index3 = 0; index3 < width; ++index3)
        {
          float num6 = Mathf.PerlinNoise((float) index3 / (float) width * 20f, num1 * 5f) * 0.2f;
          float num7 = Mathf.Clamp01((num5 + num6) * num3);
          colorArray[index1 * width + index3] = new Color(num7, num7, num7, num7);
        }
      }
      streakTexture.SetPixels(colorArray);
      streakTexture.Apply();
      ((Texture) streakTexture).wrapMode = (TextureWrapMode) 0;
      return streakTexture;
    }

    private static Texture2D GenerateSolidTexture(int width, int height)
    {
      Texture2D solidTexture = new Texture2D(width, height, (TextureFormat) 5, false);
      Color[] colorArray = new Color[width * height];
      for (int index1 = 0; index1 < height; ++index1)
      {
        float num = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs((float) index1 / (float) height - 0.5f) * 2f), 3f);
        for (int index2 = 0; index2 < width; ++index2)
          colorArray[index1 * width + index2] = new Color(1f, 1f, 1f, num);
      }
      solidTexture.SetPixels(colorArray);
      solidTexture.Apply();
      ((Texture) solidTexture).wrapMode = (TextureWrapMode) 1;
      return solidTexture;
    }

    private static Texture2D GenerateGlowTexture(int width, int height)
    {
      Texture2D glowTexture = new Texture2D(width, height, (TextureFormat) 5, false);
      Color[] colorArray = new Color[width * height];
      for (int index1 = 0; index1 < height; ++index1)
      {
        float num1 = Mathf.Abs((float) index1 / (float) height - 0.5f) * 2f;
        float num2 = Mathf.Exp(-4f * num1 * num1);
        for (int index2 = 0; index2 < width; ++index2)
          colorArray[index1 * width + index2] = new Color(1f, 1f, 1f, num2);
      }
      glowTexture.SetPixels(colorArray);
      glowTexture.Apply();
      ((Texture) glowTexture).wrapMode = (TextureWrapMode) 1;
      return glowTexture;
    }

    private static Texture2D GenerateRadialGradientTexture(int width, int height)
    {
      Texture2D radialGradientTexture = new Texture2D(width, height, (TextureFormat) 5, false);
      Color[] colorArray = new Color[width * height];
      Vector2 vector2;
      // ISSUE: explicit constructor call
      vector2 = new Vector2((float) width / 2f, (float) height / 2f);
      float num1 = (float) Mathf.Min(width, height) / 2f;
      for (int index1 = 0; index1 < height; ++index1)
      {
        for (int index2 = 0; index2 < width; ++index2)
        {
          float num2 = Mathf.Pow(1f - Mathf.Clamp01(Vector2.Distance(new Vector2((float) index2, (float) index1), vector2) / num1), 2.5f);
          colorArray[index1 * width + index2] = new Color(1f, 1f, 1f, num2);
        }
      }
      radialGradientTexture.SetPixels(colorArray);
      radialGradientTexture.Apply();
      ((Texture) radialGradientTexture).wrapMode = (TextureWrapMode) 1;
      return radialGradientTexture;
    }
  }
}
