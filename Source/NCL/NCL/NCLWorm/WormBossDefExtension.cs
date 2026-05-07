// Decompiled with JetBrains decompiler
// Type: NCLWorm.WormBossDefExtension
// Assembly: NCL_WormBoss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B627DDDD-3AC9-4D6A-89F6-EA72F95FB570
// Assembly location: C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3555799437\1.6\Assemblies\NCL_WormBoss (3).dll

using System.Collections.Generic;
using Verse;

#nullable disable
namespace NCLWorm
{
  public class WormBossDefExtension : DefModExtension
  {
    public int bodySegmentCount = 10;
    public float segmentSpacing = 1.8f;
    public float moveSpeed = 6f;
    public float turnSmoothTime = 0.15f;
    public float turnRate = 60f;
    public float acceleration = 0.2f;
    public string defaultBodyDef;
    public string tailDef;
    public List<BodyPartRule> specialSegments;
  }
}
