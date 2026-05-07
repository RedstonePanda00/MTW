// Decompiled with JetBrains decompiler
// Type: NyarsModPackOne.ModExtension_EffecterOnMelee
// Assembly: NyearsModPackOne, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: FDB00AC3-5462-4449-9639-A371FA2E00F3
// Assembly location: C:\Users\15858\OneDrive\Documents\Sundry\RwModCoop\MTW\1.6\Assemblies\NyearsModPackOne.dll

using System.Collections.Generic;
using Verse;

#nullable disable
namespace NyarsModPackOne
{
  public class ModExtension_EffecterOnMelee : DefModExtension
  {
    public List<EffecterDef> effectersAtTarget = new List<EffecterDef>();
    public List<FleckDef> flecksAtTarget = new List<FleckDef>();
    public List<ThingDef> motesAtTarget = new List<ThingDef>();
    public List<EffecterDef> effectersAtCaster = new List<EffecterDef>();
    public List<FleckDef> flecksAtCaster = new List<FleckDef>();
    public List<ThingDef> motesAtCaster = new List<ThingDef>();
    public List<FleckDef> flecksLinkLine = new List<FleckDef>();
    public List<ThingDef> motesLinkLine = new List<ThingDef>();
  }
}
