// Decompiled with JetBrains decompiler
// Type: NCLWorm.TCP_WormDecisionController
// Assembly: NCL_WormBoss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B627DDDD-3AC9-4D6A-89F6-EA72F95FB570
// Assembly location: C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3555799437\1.6\Assemblies\NCL_WormBoss (3).dll

using System;
using System.Collections.Generic;
using Verse;

#nullable disable
namespace NCLWorm
{
  public class TCP_WormDecisionController : CompProperties
  {
    public List<PhaseEntry> phases = new List<PhaseEntry>();
    public Type deathPhaseClass = typeof (Phase_DeathSequence);
    public FleckDef targetFleckDef;
    public float targetFleckScale = 1f;
    public ThingDef targetMoteDef;
    public float targetMoteScale = 1f;
    public int targetVisualInterval = 60;
    public int initialLifespanTicks = 300000;
    public float initialLifespanDays = -1f;

    public TCP_WormDecisionController() => this.compClass = typeof (TC_WormDecisionController);
  }
}
