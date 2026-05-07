// Decompiled with JetBrains decompiler
// Type: NCLWorm.CompProperties_WormAura
// Assembly: NCL_WormBoss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B627DDDD-3AC9-4D6A-89F6-EA72F95FB570
// Assembly location: C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3555799437\1.6\Assemblies\NCL_WormBoss (3).dll

using Verse;

#nullable disable
namespace NCLWorm
{
  public class CompProperties_WormAura : CompProperties
  {
    public HediffDef hediff;
    public float range = 18f;
    public int checkInterval = 60;
    public bool drawLines = true;

    public CompProperties_WormAura() => this.compClass = typeof (CompWormAura);
  }
}
