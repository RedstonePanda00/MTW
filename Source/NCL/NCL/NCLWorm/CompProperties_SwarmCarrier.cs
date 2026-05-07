// Decompiled with JetBrains decompiler
// Type: NCLWorm.CompProperties_SwarmCarrier
// Assembly: NCL_WormBoss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B627DDDD-3AC9-4D6A-89F6-EA72F95FB570
// Assembly location: C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3555799437\1.6\Assemblies\NCL_WormBoss (3).dll

using System.Collections.Generic;

#nullable disable
namespace NCLWorm
{
  public class CompProperties_SwarmCarrier : CompProperties_SwarmController
  {
    public List<ProbeSpawnOption> spawnOptions = new List<ProbeSpawnOption>();
    public int maxTotalProbes = 20;
    public int spawnInterval = 600;
    public int batchSize = 1;
    public float spawnOpenThreshold = 0.8f;

    public CompProperties_SwarmCarrier() => this.compClass = typeof (CompSwarmCarrier);
  }
}
