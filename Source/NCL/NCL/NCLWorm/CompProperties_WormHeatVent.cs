// Decompiled with JetBrains decompiler
// Type: NCLWorm.CompProperties_WormHeatVent
// Assembly: NCL_WormBoss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B627DDDD-3AC9-4D6A-89F6-EA72F95FB570
// Assembly location: C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3555799437\1.6\Assemblies\NCL_WormBoss (3).dll

using Verse;

#nullable disable
namespace NCLWorm
{
  public class CompProperties_WormHeatVent : CompProperties
  {
    public float activationThreshold = 0.8f;
    public int checkInterval = 30;
    public float baseDamage = 10f;
    public DamageDef damageDef;
    public float armorPenetration = 0.0f;
    public float radius = 2.9f;
    public float knockbackDistance = 3f;
    public int stunTicks = 60;

    public CompProperties_WormHeatVent() => this.compClass = typeof (CompWormHeatVent);
  }
}
