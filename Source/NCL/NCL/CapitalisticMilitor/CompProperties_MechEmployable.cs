// Decompiled with JetBrains decompiler
// Type: NCL.CompProperties_MechEmployable
// Assembly: TW_Mech_Capitalistic_Militor, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 19431CE1-8FC8-4A04-970C-DC00BB350104
// Assembly location: C:\Users\15858\OneDrive\Documents\Sundry\RwModCoop\MTW\1.6\Assemblies\TW_Mech_Capitalistic_Militor.dll

using System.Collections.Generic;
using Verse;

#nullable disable
namespace NCL
{
  public class CompProperties_MechEmployable : CompProperties
  {
    public float silverPerDay = 100f;
    private Dictionary<Thing, float> enemyRecords;

    public CompProperties_MechEmployable()
    {
      this.compClass = typeof (Comp_MechEmployable);
      this.enemyRecords = new Dictionary<Thing, float>();
    }
  }
}
