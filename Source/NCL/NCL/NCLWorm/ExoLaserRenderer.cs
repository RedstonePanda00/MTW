// Decompiled with JetBrains decompiler
// Type: NCLWorm.ExoLaserRenderer
// Assembly: NCL_WormBoss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B627DDDD-3AC9-4D6A-89F6-EA72F95FB570
// Assembly location: C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3555799437\1.6\Assemblies\NCL_WormBoss (3).dll

using System.Collections.Generic;
using UnityEngine;
using Verse;

#nullable disable
namespace NCLWorm
{
  [StaticConstructorOnStartup]
  public static class ExoLaserRenderer
  {
    private static MaterialPropertyBlock _mpb_inst;
    private static List<Vector3> _controlPoints = new List<Vector3>();
    private static List<Vector3> _splinePoints = new List<Vector3>();
    private static List<Vector3> _vertices = new List<Vector3>();
    private static List<Color> _colors = new List<Color>();
    private static List<Vector2> _uvsMain = new List<Vector2>();
    private static List<Vector2> _uvsGrain = new List<Vector2>();
    private static List<int> _indices = new List<int>();

    private static MaterialPropertyBlock _mpb
    {
      get
      {
        return ExoLaserRenderer._mpb_inst ?? (ExoLaserRenderer._mpb_inst = new MaterialPropertyBlock());
      }
    }

    public static void RenderLaser(
      Mesh targetMesh,
      Vector3 start,
      Vector3 end,
      float width,
      float overheat,
      float time,
      bool isRainbow,
      Color cNormal,
      Color cHot,
      bool isTelegraph = false)
    {
      if ((double) width <= 0.10000000149011612 || targetMesh == null)
        return;
      Vector3 vector3 = (end - start);
      float magnitude = vector3.magnitude;
      if ((double) magnitude < 0.10000000149011612)
        return;
      if (ExoLaserRenderer.Config.DebugForceRainbow)
        isRainbow = true;
      Color baseColor = ExoLaserRenderer.CalculateBaseColor(overheat, isRainbow, cNormal, cHot);
      ExoLaserRenderer.UpdatePath(start, end, magnitude, overheat, time);
      ExoLaserRenderer.BuildMesh(targetMesh, start, magnitude, width, overheat, time, baseColor, isRainbow, isTelegraph);
      ExoLaserRenderer.DrawPasses(targetMesh, baseColor, isRainbow, isTelegraph);
    }

    private static void UpdatePath(
      Vector3 start,
      Vector3 end,
      float totalLength,
      float overheat,
      float time)
    {
      ExoLaserRenderer._controlPoints.Clear();
      int num1 = Mathf.Max(8, Mathf.CeilToInt(totalLength / 2f));
      float num2 = time * ExoLaserRenderer.Config.JitterFrequency;
      for (int index = 0; index < num1; ++index)
      {
        float num3 = (float) index / (float) (num1 - 1);
        Vector3 vector3 = Vector3.Lerp(start, end, num3);
        if ((double) overheat > 0.05000000074505806)
        {
          float num4;
          if (ExoLaserRenderer.Config.UseCometShape)
          {
            num4 = Mathf.Lerp(0.0f, ExoLaserRenderer.Config.TailTurbulence, num3 * num3);
            if ((double) num3 < 0.10000000149011612)
              num4 = 0.0f;
          }
          else
            num4 = Mathf.Sin(num3 * 3.14159274f);
          float num5 = (float) ((double) overheat * (double) overheat * 0.5) * num4;
          if ((double) num5 > 1.0 / 1000.0)
          {
            float num6 = Mathf.PerlinNoise(num3 * 3f, num2) - 0.5f;
            float num7 = Mathf.PerlinNoise((float) ((double) num3 * 3.0 + 13.0), num2) - 0.5f;
            float num8 = Mathf.PerlinNoise((float) ((double) num3 * 3.0 + 27.0), num2) - 0.5f;
            vector3 = (vector3 + (new Vector3(num6, num7, num8) * num5));
          }
        }
        ExoLaserRenderer._controlPoints.Add(vector3);
      }
      ExoLaserRenderer._splinePoints.Clear();
      ExoLaserRenderer.GenerateSplinePoints(ExoLaserRenderer._controlPoints, ExoLaserRenderer._splinePoints, 8);
    }

    private static void BuildMesh(
      Mesh mesh,
      Vector3 startPos,
      float totalLength,
      float width,
      float overheat,
      float time,
      Color baseColor,
      bool isRainbow,
      bool isTelegraph)
    {
      ExoLaserRenderer._vertices.Clear();
      ExoLaserRenderer._colors.Clear();
      ExoLaserRenderer._indices.Clear();
      ExoLaserRenderer._uvsMain.Clear();
      ExoLaserRenderer._uvsGrain.Clear();
      Vector3 position = ((Component) Find.Camera).transform.position;
      float num1 = ExoLaserRenderer.Config.ScrollSpeedMain + overheat * 2f;
      float num2 = (float) -((double) time * (double) num1);
      float num3 = totalLength / 3f;
      float num4 = ExoLaserRenderer.Config.ScrollSpeedGrain + overheat * 6f;
      float num5 = (float) -((double) time * (double) num4);
      float num6 = totalLength / ExoLaserRenderer.Config.GrainStretching;
      int count = ExoLaserRenderer._splinePoints.Count;
      for (int index = 0; index < count; ++index)
      {
        float t = (float) index / (float) (count - 1);
        Vector3 splinePoint = ExoLaserRenderer._splinePoints[index];
        float laserWidth = ExoLaserRenderer.CalculateLaserWidth(t, width, overheat, time);
        Vector3 vector3_1 = Vector3.forward;
        Vector3 vector3_2;
        if (index < count - 1)
        {
          vector3_2 = (ExoLaserRenderer._splinePoints[index + 1] - splinePoint);
          vector3_1 = vector3_2.normalized;
        }
        else if (index > 0)
        {
          vector3_2 = (splinePoint - ExoLaserRenderer._splinePoints[index - 1]);
          vector3_1 = vector3_2.normalized;
        }
        Vector3 vector3_3 = vector3_1;
        vector3_2 = (splinePoint - position);
        Vector3 normalized = vector3_2.normalized;
        vector3_2 = Vector3.Cross(vector3_3, normalized);
        Vector3 vector3_4 = (vector3_2.normalized * laserWidth * 0.5f);
        ExoLaserRenderer._vertices.Add((splinePoint - vector3_4));
        ExoLaserRenderer._vertices.Add((splinePoint + vector3_4));
        float num7 = 1f;
        if (isTelegraph)
        {
          num7 = (float) (1.0 - (double) t * 0.60000002384185791);
          if ((double) t > 0.800000011920929)
            num7 *= (float) ((1.0 - (double) t) / 0.20000000298023224);
        }
        Color vertexColor = ExoLaserRenderer.CalculateVertexColor(t, time, baseColor, isRainbow);
        vertexColor.a *= num7;
        ExoLaserRenderer._colors.Add(vertexColor);
        ExoLaserRenderer._colors.Add(vertexColor);
        ExoLaserRenderer._uvsMain.Add(new Vector2(t * num3 + num2, 0.0f));
        ExoLaserRenderer._uvsMain.Add(new Vector2(t * num3 + num2, 1f));
        ExoLaserRenderer._uvsGrain.Add(new Vector2(t * num6 + num5, 0.0f));
        ExoLaserRenderer._uvsGrain.Add(new Vector2(t * num6 + num5, 1f));
        if (index < count - 1)
        {
          int num8 = index * 2;
          ExoLaserRenderer._indices.Add(num8);
          ExoLaserRenderer._indices.Add(num8 + 2);
          ExoLaserRenderer._indices.Add(num8 + 1);
          ExoLaserRenderer._indices.Add(num8 + 1);
          ExoLaserRenderer._indices.Add(num8 + 2);
          ExoLaserRenderer._indices.Add(num8 + 3);
        }
      }
      mesh.Clear();
      mesh.SetVertices(ExoLaserRenderer._vertices);
      mesh.SetColors(ExoLaserRenderer._colors);
      mesh.SetTriangles(ExoLaserRenderer._indices, 0);
      mesh.SetUVs(0, ExoLaserRenderer._uvsMain);
      mesh.RecalculateBounds();
      Bounds bounds = mesh.bounds;
      bounds.Expand(50f);
      mesh.bounds = bounds;
    }

    private static void DrawPasses(Mesh mesh, Color baseColor, bool isRainbow, bool isTelegraph)
    {
      ExoLaserRenderer._mpb.Clear();
      Color color1 = isRainbow ? Color.white : ExoLaserRenderer.ShiftHue(baseColor, -ExoLaserRenderer.Config.GlowHueOffset);
      color1.a = isTelegraph ? 0.2f : 0.3f;
      ExoLaserRenderer._mpb.SetColor(ShaderPropertyIDs.Color, color1);
      Graphics.DrawMesh(mesh, Matrix4x4.identity, ExoLaserAssets.Mat_Glow, 0, (Camera) null, 0, ExoLaserRenderer._mpb);
      ExoLaserRenderer._mpb.Clear();
      Color color2 = isRainbow ? Color.white : baseColor;
      color2.a = isTelegraph ? 0.35f : 0.8f;
      ExoLaserRenderer._mpb.SetColor(ShaderPropertyIDs.Color, color2);
      Graphics.DrawMesh(mesh, Matrix4x4.Translate(new Vector3(0.0f, 0.02f, 0.0f)), ExoLaserAssets.Mat_Streak, 0, (Camera) null, 0, ExoLaserRenderer._mpb);
      if (isTelegraph)
        return;
      mesh.SetUVs(0, ExoLaserRenderer._uvsGrain);
      ExoLaserRenderer._mpb.Clear();
      Color color3 = Color.Lerp(color2, Color.white, 0.7f);
      color3.a = 1f;
      ExoLaserRenderer._mpb.SetColor(ShaderPropertyIDs.Color, color3);
      Graphics.DrawMesh(mesh, Matrix4x4.Translate(new Vector3(0.0f, 0.04f, 0.0f)), ExoLaserAssets.Mat_Grain, 0, (Camera) null, 0, ExoLaserRenderer._mpb);
      mesh.SetUVs(0, ExoLaserRenderer._uvsMain);
      ExoLaserRenderer._mpb.Clear();
      ExoLaserRenderer._mpb.SetColor(ShaderPropertyIDs.Color, Color.white);
      Graphics.DrawMesh(mesh, Matrix4x4.Translate(new Vector3(0.0f, 0.06f, 0.0f)), ExoLaserAssets.Mat_Core, 0, (Camera) null, 0, ExoLaserRenderer._mpb);
    }

    private static Color CalculateBaseColor(
      float overheat,
      bool isRainbow,
      Color cNormal,
      Color cHot)
    {
      return !isRainbow ? Color.Lerp(cNormal, cHot, overheat) : Color.white;
    }

    private static float CalculateLaserWidth(float t, float baseWidth, float overheat, float time)
    {
      float num1;
      if (ExoLaserRenderer.Config.UseCometShape)
      {
        float num2 = (float) (1.0 + (double) Mathf.Lerp(Mathf.Sin((float) ((double) t * 3.1415927410125732 * 0.5)), t, ExoLaserRenderer.Config.ExpansionSaturation) * ((double) ExoLaserRenderer.Config.TailExpansion - 1.0));
        if ((double) overheat > 0.5)
          num2 += (overheat - 0.5f) * t;
        float num3 = 1f;
        if ((double) t < (double) ExoLaserRenderer.Config.ExpansionCurve)
        {
          float num4 = t / ExoLaserRenderer.Config.ExpansionCurve;
          num3 = Mathf.Lerp(ExoLaserRenderer.Config.ThroatWidth, 1f, Mathf.SmoothStep(0.0f, 1f, num4));
        }
        num1 = num3 * num2;
      }
      else
      {
        float num5 = 1f;
        if ((double) t < 0.05000000074505806)
          num5 = Mathf.SmoothStep(0.0f, 1f, t / 0.05f);
        else if ((double) t > 0.949999988079071)
          num5 = Mathf.SmoothStep(1f, 0.5f, (float) (((double) t - 0.949999988079071) / 0.05000000074505806));
        num1 = num5;
      }
      float num6 = (float) (1.0 + (double) Mathf.Sin((float) ((double) time * ((double) ExoLaserRenderer.Config.PulseSpeed + (double) overheat * 5.0) - (double) t * (double) ExoLaserRenderer.Config.PulseDensity)) * (double) ExoLaserRenderer.Config.PulseAmp);
      return baseWidth * num1 * num6;
    }

    private static Color CalculateVertexColor(
      float t,
      float time,
      Color baseColor,
      bool isRainbow)
    {
      float num = ExoLaserRenderer.Config.UseCometShape ? (float) (1.0 - (double) t * (double) ExoLaserRenderer.Config.TailAlphaDrop) : 1f;
      if ((double) t > 0.949999988079071)
        num *= (float) (1.0 - ((double) t - 0.949999988079071) / 0.05000000074505806);
      Color vertexColor = !isRainbow ? Color.white : Color.HSVToRGB(Mathf.Repeat((float) ((double) time * (double) ExoLaserRenderer.Config.RainbowCycleSpeed + (double) t * (double) ExoLaserRenderer.Config.RainbowColorSpread), 1f), 0.7f, 1f);
      vertexColor.a *= num;
      return vertexColor;
    }

    private static Color ShiftHue(Color c, float offset)
    {
      Color.RGBToHSV(c, out float num1, out float num2, out float num3);
      return Color.HSVToRGB(Mathf.Repeat(num1 + offset, 1f), num2 * 0.8f, num3);
    }

    private static void GenerateSplinePoints(
      List<Vector3> points,
      List<Vector3> output,
      int subdivisions)
    {
      if (points.Count < 2)
        return;
      for (int index1 = 0; index1 < points.Count - 1; ++index1)
      {
        Vector3 p0 = index1 > 0 ? points[index1 - 1] : points[index1];
        Vector3 point1 = points[index1];
        Vector3 point2 = points[index1 + 1];
        Vector3 p3 = index1 < points.Count - 2 ? points[index1 + 2] : points[index1 + 1];
        for (int index2 = 0; index2 < subdivisions; ++index2)
          output.Add(ExoLaserRenderer.GetCatmullRomPosition((float) index2 / (float) subdivisions, p0, point1, point2, p3));
      }
      output.Add(points[points.Count - 1]);
    }

    private static Vector3 GetCatmullRomPosition(
      float t,
      Vector3 p0,
      Vector3 p1,
      Vector3 p2,
      Vector3 p3)
    {
      return (0.5f * ((((2f * p1) + (((-p0) + p2) * t)) + ((((((2f * p0) - (5f * p1)) + (4f * p2)) - p3) * t) * t)) + (((((((-p0) + (3f * p1)) - (3f * p2)) + p3) * t) * t) * t)));
    }

    public static class Config
    {
      public static bool UseCometShape = true;
      public static bool DebugForceRainbow = false;
      public static float ThroatWidth = 0.01f;
      public static float TailExpansion = 3f;
      public static float ExpansionCurve = 0.1f;
      public static float ExpansionSaturation = 0.1f;
      public static float PulseSpeed = 5f;
      public static float PulseDensity = 14f;
      public static float PulseAmp = 0.05f;
      public static float ScrollSpeedMain = 20f;
      public static float ScrollSpeedGrain = 25f;
      public static float GrainStretching = 6f;
      public static float TailTurbulence = 1.5f;
      public static float JitterFrequency = 3f;
      public static float TailAlphaDrop = 0.95f;
      public static float GlowHueOffset = 0.1f;
      public static float RainbowCycleSpeed = 1f;
      public static float RainbowColorSpread = 0.3f;
    }
  }
}
