// Decompiled with JetBrains decompiler
// Type: NCLWorm.VFXRuntimeState
// Assembly: NCL_WormBoss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B627DDDD-3AC9-4D6A-89F6-EA72F95FB570
// Assembly location: C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3555799437\1.6\Assemblies\NCL_WormBoss (3).dll

using Verse;

#nullable disable
namespace NCLWorm
{
  public class VFXRuntimeState : IExposable
  {
    public int cooldownTicks = 0;
    public bool wasActiveLastTick = false;

    public void ExposeData()
    {
      Scribe_Values.Look<int>(ref this.cooldownTicks, "cooldownTicks", 0, false);
      Scribe_Values.Look<bool>(ref this.wasActiveLastTick, "wasActiveLastTick", false, false);
    }
  }
}
