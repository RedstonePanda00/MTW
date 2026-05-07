// Decompiled with JetBrains decompiler
// Type: NCLWorm.BossAIMemory
// Assembly: NCL_WormBoss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B627DDDD-3AC9-4D6A-89F6-EA72F95FB570
// Assembly location: C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3555799437\1.6\Assemblies\NCL_WormBoss (3).dll

using System;
using System.Collections.Generic;
using Verse;

#nullable disable
namespace NCLWorm
{
  public class BossAIMemory : IExposable
  {
    private Dictionary<string, float> _weights = new Dictionary<string, float>();
    private const float DEFAULT_START_WEIGHT = 100f;

    public float GetWeight(Type type)
    {
      float num;
      return this._weights.TryGetValue(type.Name, out num) ? num : 100f;
    }

    public void SetWeight(Type type, float value) => this._weights[type.Name] = value;

    public void AddWeight(Type type, float delta)
    {
      string name = type.Name;
      if (!this._weights.ContainsKey(name))
        this._weights[name] = 100f;
      this._weights[name] += delta;
    }

    public void ExposeData()
    {
      Scribe_Collections.Look<string, float>(ref this._weights, "phaseWeights", (LookMode) 1, (LookMode) 1);
    }
  }
}
