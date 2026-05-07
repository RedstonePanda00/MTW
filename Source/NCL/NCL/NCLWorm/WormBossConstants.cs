// Decompiled with JetBrains decompiler
// Type: NCLWorm.WormBossConstants
// Assembly: NCL_WormBoss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B627DDDD-3AC9-4D6A-89F6-EA72F95FB570
// Assembly location: C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3555799437\1.6\Assemblies\NCL_WormBoss (3).dll

using UnityEngine;

#nullable disable
namespace NCLWorm
{
  public static class WormBossConstants
  {
    public const int TICKS_PER_SEC = 60;
    public const float DIST_TOUCHING = 1.5f;
    public const float DIST_MELEE = 3.9f;
    public const float DIST_CLOSE = 15f;
    public const float DIST_MEDIUM = 30f;
    public const float DIST_LONG = 50f;
    public const float SERVO_CLOSED = 0.0f;
    public const float SERVO_HALF = 0.5f;
    public const float SERVO_OPEN = 1f;
    public const float ALTITUDE_GROUND = 0.0f;
    public const float ALTITUDE_LOW_FLY = 0.5f;
    public const float ALTITUDE_HOVER = 1.5f;
    public const float ALTITUDE_HIGH = 3.5f;

    public static int SecondsToTicks(float seconds) => Mathf.RoundToInt(seconds * 60f);
  }
}
