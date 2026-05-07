// Decompiled with JetBrains decompiler
// Type: NCLWorm.CompProperties_ProbeRepair
// Assembly: NCL_WormBoss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B627DDDD-3AC9-4D6A-89F6-EA72F95FB570
// Assembly location: C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3555799437\1.6\Assemblies\NCL_WormBoss (3).dll

#nullable disable
namespace NCLWorm
{
  public class CompProperties_ProbeRepair : CompProperties_ProbeBrain
  {
    public int repairInterval = 60;
    public int repairAmount = 20;
    public float orbitRadius = 4f;

    public CompProperties_ProbeRepair() => this.compClass = typeof (CompProbeBrain_Repair);
  }
}
