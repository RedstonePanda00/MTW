// Decompiled with JetBrains decompiler
// Type: NCLWorm.CompProperties_ProbeCombat
// Assembly: NCL_WormBoss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B627DDDD-3AC9-4D6A-89F6-EA72F95FB570
// Assembly location: C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3555799437\1.6\Assemblies\NCL_WormBoss (3).dll

#nullable disable
namespace NCLWorm
{
  public class CompProperties_ProbeCombat : CompProperties_ProbeBrain
  {
    public float followRadius = 10f;
    public float combatRange = 18f;
    public float returnDist = 50f;

    public CompProperties_ProbeCombat() => this.compClass = typeof (CompProbeBrain_Combat);
  }
}
