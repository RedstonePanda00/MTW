// Decompiled with JetBrains decompiler
// Type: NCLWorm.TCP_WormWeaponController
// Assembly: NCL_WormBoss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B627DDDD-3AC9-4D6A-89F6-EA72F95FB570
// Assembly location: C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3555799437\1.6\Assemblies\NCL_WormBoss (3).dll

using System.Collections.Generic;
using Verse;

#nullable disable
namespace NCLWorm
{
  public class TCP_WormWeaponController : CompProperties
  {
    public List<VerbProperties> verbs = new List<VerbProperties>();

    public TCP_WormWeaponController() => this.compClass = typeof (TC_WormWeaponController);
  }
}
