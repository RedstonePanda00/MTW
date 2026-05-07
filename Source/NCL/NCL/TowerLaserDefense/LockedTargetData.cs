// Decompiled with JetBrains decompiler
// Type: TowerLaserDefense.LockedTargetData
// Assembly: TowerLaserDefense, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: D672ED81-8E05-4DF5-BFC2-02EBAE2446C3
// Assembly location: C:\Users\15858\OneDrive\Documents\Sundry\RwModCoop\MTW\1.6\Assemblies\TowerLaserDefense.dll

using Verse;

#nullable disable
namespace TowerLaserDefense
{
  public class LockedTargetData : IExposable
  {
    public Thing target;
    public int time;

    public LockedTargetData()
    {
    }

    public LockedTargetData(Thing target) => this.target = target;

    public void ExposeData()
    {
      Scribe_Values.Look<int>(ref this.time, "time", 0, false);
      Scribe_References.Look<Thing>(ref this.target, "target", false);
    }
  }
}
