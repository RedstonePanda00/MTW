// Decompiled with JetBrains decompiler
// Type: TowerLaserDefense.MainGameComponent
// Assembly: TowerLaserDefense, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: D672ED81-8E05-4DF5-BFC2-02EBAE2446C3
// Assembly location: C:\Users\15858\OneDrive\Documents\Sundry\RwModCoop\MTW\1.6\Assemblies\TowerLaserDefense.dll

using Verse;

#nullable disable
namespace TowerLaserDefense
{
  public class MainGameComponent : GameComponent
  {
    public MainGameComponent(Game game)
    {
    }

    public virtual void FinalizeInit()
    {
      if (Current.ProgramState != 0)
        return;
      GameComponent_BulletsCache.ClearStaticCache();
      LaserDefenceCore.Instances.Clear();
    }
  }
}
