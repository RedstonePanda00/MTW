// Decompiled with JetBrains decompiler
// Type: NCLWorm.MovementIntent
// Assembly: NCL_WormBoss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B627DDDD-3AC9-4D6A-89F6-EA72F95FB570
// Assembly location: C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3555799437\1.6\Assemblies\NCL_WormBoss (3).dll

using UnityEngine;
using Verse;

#nullable disable
namespace NCLWorm
{
  public struct MovementIntent
  {
    public Vector3 TargetPosition;
    public Vector3? ForceFacing;
    public Thing RelativeTo;
    public float SpeedFactor;
    public float TurnFactor;
    public float AccelFactor;
    public float OverrideAltitude;
    public bool IsValid;

    public static MovementIntent Invalid
    {
      get => new MovementIntent() { IsValid = false };
    }

    public void ExposeData()
    {
      Scribe_Values.Look<Vector3>(ref this.TargetPosition, "targetPos", new Vector3(), false);
      Scribe_Values.Look<Vector3?>(ref this.ForceFacing, "forceFacing", new Vector3?(), false);
      Scribe_Values.Look<float>(ref this.SpeedFactor, "speedFactor", 1f, false);
      Scribe_Values.Look<float>(ref this.TurnFactor, "turnFactor", 1f, false);
      Scribe_Values.Look<float>(ref this.AccelFactor, "accelFactor", 1f, false);
      Scribe_Values.Look<float>(ref this.OverrideAltitude, "altitude", 0.0f, false);
      Scribe_Values.Look<bool>(ref this.IsValid, "isValid", false, false);
      Scribe_References.Look<Thing>(ref this.RelativeTo, "RelativeTo", false);
    }

    public static class Presets
    {
      public static MovementIntent Cruising(Vector3 dest, float alt = 1.5f)
      {
        return new MovementIntent()
        {
          TargetPosition = dest,
          OverrideAltitude = alt,
          SpeedFactor = 1f,
          TurnFactor = 1f,
          AccelFactor = 1f,
          IsValid = true
        };
      }

      public static MovementIntent Ramming(Vector3 dest)
      {
        return new MovementIntent()
        {
          TargetPosition = dest,
          OverrideAltitude = 0.5f,
          SpeedFactor = 3f,
          TurnFactor = 0.3f,
          AccelFactor = 2f,
          IsValid = true
        };
      }

      public static MovementIntent Precise(Vector3 dest, float alt = 1.5f)
      {
        return new MovementIntent()
        {
          TargetPosition = dest,
          OverrideAltitude = alt,
          SpeedFactor = 0.4f,
          TurnFactor = 2f,
          AccelFactor = 1f,
          IsValid = true
        };
      }

      public static MovementIntent Creeping(Vector3 dest, float alt = 1f)
      {
        return new MovementIntent()
        {
          TargetPosition = dest,
          OverrideAltitude = alt,
          SpeedFactor = 0.2f,
          TurnFactor = 0.25f,
          AccelFactor = 1f,
          IsValid = true
        };
      }

      public static MovementIntent Braking(Vector3 faceDir)
      {
        return new MovementIntent()
        {
          TargetPosition = Vector3.zero,
          ForceFacing = (double) faceDir.sqrMagnitude > 1.0 / 1000.0 ? new Vector3?(faceDir.normalized) : new Vector3?(),
          OverrideAltitude = 1.5f,
          SpeedFactor = 0.0f,
          TurnFactor = 1f,
          AccelFactor = 1f,
          IsValid = true
        };
      }
    }
  }
}
