// Decompiled with JetBrains decompiler
// Type: NCLWorm.BossPhaseSelector
// Assembly: NCL_WormBoss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B627DDDD-3AC9-4D6A-89F6-EA72F95FB570
// Assembly location: C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3555799437\1.6\Assemblies\NCL_WormBoss (3).dll

using System;
using System.Collections.Generic;
using Verse;

#nullable disable
namespace NCLWorm
{
  public static class BossPhaseSelector
  {
    public static WormPhase SelectNext(
      TC_WormDecisionController brain,
      WormPhase lastPhase,
      List<PhaseEntry> phases,
      List<PhaseEntry> scratch)
    {
      if (phases == null || phases.Count == 0)
      {
        Log.ErrorOnce("[WormBoss] BossPhaseSelector: phases 列表为空！请检查 XML 中 TCP_WormDecisionController 的 <phases> 配置。", 19940801);
        return (WormPhase) null;
      }
      if (scratch == null)
      {
        Log.ErrorOnce("[WormBoss] BossPhaseSelector: scratch 列表为空。", 19940803);
        return (WormPhase) null;
      }
      BossAIMemory memory = brain.AIMemory;
      Type type = lastPhase?.GetType();
      scratch.Clear();
      for (int index = 0; index < phases.Count; ++index)
      {
        PhaseEntry phase = phases[index];
        if (!(phase.phaseClass == (Type) null) && !(phase.phaseClass == type))
          scratch.Add(phase);
      }
      if (scratch.Count == 0)
      {
        for (int index = 0; index < phases.Count; ++index)
        {
          PhaseEntry phase = phases[index];
          if (phase.phaseClass != (Type) null)
            scratch.Add(phase);
        }
      }
      if (scratch.Count == 0)
      {
        Log.ErrorOnce("[WormBoss] BossPhaseSelector: 没有有效的 phaseClass！请检查 XML 中 <phaseClass> 配置是否正确。", 19940802);
        return (WormPhase) null;
      }
      if (!GenCollection.TryRandomElementByWeight(scratch, e => memory.GetWeight(e.phaseClass), out PhaseEntry selected))
        selected = GenCollection.RandomElement(scratch);
      BossPhaseSelector.UpdateAllScores(memory, selected, phases);
      return (WormPhase)Activator.CreateInstance(selected.phaseClass);
    }

    private static void UpdateAllScores(
      BossAIMemory memory,
      PhaseEntry selected,
      List<PhaseEntry> phases)
    {
      for (int index = 0; index < phases.Count; ++index)
      {
        PhaseEntry phase = phases[index];
        if (!(phase.phaseClass == (Type) null))
        {
          if (phase.phaseClass == selected.phaseClass)
            memory.SetWeight(phase.phaseClass, selected.resetScore);
          else
            memory.AddWeight(phase.phaseClass, phase.growth);
        }
      }
    }
  }
}
