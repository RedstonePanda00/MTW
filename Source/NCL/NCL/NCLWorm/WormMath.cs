// Decompiled with JetBrains decompiler
// Type: NCLWorm.WormMath
// Assembly: NCL_WormBoss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B627DDDD-3AC9-4D6A-89F6-EA72F95FB570
// Assembly location: C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3555799437\1.6\Assemblies\NCL_WormBoss (3).dll

using System;
using UnityEngine;

#nullable disable
namespace NCLWorm
{
  public static class WormMath
  {
    public const float TicksPerSecond = 60f;
    public const float GravityPerTickSq = 0.00272222236f;

    public static float SpeedToCellsPerTick(float cellsPerSecond) => cellsPerSecond / 60f;

    public static float SpeedToCellsPerSecond(float cellsPerTick) => cellsPerTick * 60f;

    public static float AccelToSpeedChangePerTick(float cellsPerSecondSquared)
    {
      return cellsPerSecondSquared / 3600f;
    }

    public static float TurnRateToRadPerTick(float degPerSecond)
    {
      return (float) ((double) degPerSecond * (Math.PI / 180.0) / 60.0);
    }

    public static float Distance2D(Vector3 a, Vector3 b)
    {
      float num1 = a.x - b.x;
      float num2 = a.z - b.z;
      return Mathf.Sqrt((float) ((double) num1 * (double) num1 + (double) num2 * (double) num2));
    }

    public static void StepPhysics3D(
      Vector3 currentPos,
      Vector3 currentVel,
      Vector3 commandedAccel,
      float dragCoeff,
      float gravityScale,
      out Vector3 newPos,
      out Vector3 newVel)
    {
      Vector3 vector3_1;
      // ISSUE: explicit constructor call
      vector3_1 = new Vector3(0.0f, -0.00272222236f * gravityScale, 0.0f);
      float sqrMagnitude = currentVel.sqrMagnitude;
      Vector3 vector3_2 = Vector3.zero;
      if ((double) sqrMagnitude > 9.9999999747524271E-07)
        vector3_2 = ((-currentVel.normalized) * dragCoeff * sqrMagnitude);
      Vector3 vector3_3 = ((commandedAccel + vector3_1) + vector3_2);
      newVel = (currentVel + vector3_3);
      newPos = (currentPos + newVel);
    }

    public static Vector3 GetOrbitTangent(
      Vector3 currentPos,
      Vector3 centerPos,
      Vector3 normalAxis,
      float radius,
      bool clockwise)
    {
      Vector3 vector3_1 = (centerPos - currentPos);
      float num1 = Vector3.Dot(vector3_1, normalAxis);
      Vector3 vector3_2 = (vector3_1 - (normalAxis * num1));
      float magnitude = vector3_2.magnitude;
      if ((double) magnitude < 0.0099999997764825821)
      {
        Vector3 vector3_3 = Vector3.Cross(normalAxis, Vector3.forward);
        return vector3_3.normalized;
      }
      Vector3 vector3_4 = (vector3_2 / magnitude);
      Vector3 vector3_5 = Vector3.Cross(normalAxis, vector3_4);
      if (!clockwise)
        vector3_5 = (-vector3_5);
      float num2 = Mathf.Clamp((magnitude - radius) * 0.5f, -1f, 1f);
      Vector3 vector3_6 = (vector3_5 + (vector3_4 * num2));
      return vector3_6.normalized;
    }

    public static Vector3 AerodynamicTurn(
      Vector3 currentVelocity,
      Vector3 targetVelocity,
      float maxRadiansPerTick,
      float maxSpeedChangePerTick)
    {
      return (double) currentVelocity.sqrMagnitude < 9.9999997473787516E-06 ? Vector3.MoveTowards(currentVelocity, targetVelocity, maxSpeedChangePerTick) : (Vector3.RotateTowards(currentVelocity.normalized, targetVelocity.normalized, maxRadiansPerTick, 0.0f) * Mathf.MoveTowards(currentVelocity.magnitude, targetVelocity.magnitude, maxSpeedChangePerTick));
    }

    public static float Distance2DSquared(Vector3 a, Vector3 b)
    {
      float num1 = a.x - b.x;
      float num2 = a.z - b.z;
      return (float) ((double) num1 * (double) num1 + (double) num2 * (double) num2);
    }
  }
}
