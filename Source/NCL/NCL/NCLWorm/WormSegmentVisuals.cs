// Decompiled with JetBrains decompiler
// Type: NCLWorm.WormSegmentVisuals
// Assembly: NCL_WormBoss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B627DDDD-3AC9-4D6A-89F6-EA72F95FB570
// Assembly location: C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3555799437\1.6\Assemblies\NCL_WormBoss (3).dll

using System.Collections.Generic;
using Verse;

#nullable disable
namespace NCLWorm
{
  public class WormSegmentVisuals : DefModExtension
  {
    public List<WormVisualPart> parts = new List<WormVisualPart>();
    public List<WormVFXEmitter> emitters = new List<WormVFXEmitter>();
    public bool drawBehindBody = false;
  }
}
