// Decompiled with JetBrains decompiler
// Type: TowerLaserDefense.GameComponent_BulletsCache
// Assembly: TowerLaserDefense, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: D672ED81-8E05-4DF5-BFC2-02EBAE2446C3
// Assembly location: C:\Users\15858\OneDrive\Documents\Sundry\RwModCoop\MTW\1.6\Assemblies\TowerLaserDefense.dll

using System;
using System.Collections.Generic;
using Verse;

#nullable disable
namespace TowerLaserDefense
{
  public class GameComponent_BulletsCache : GameComponent
  {
    public static List<Thing> BulletsCache = new List<Thing>();

    public GameComponent_BulletsCache(Game game)
    {
    }

    public virtual void GameComponentTick()
    {
      for (int index = GameComponent_BulletsCache.BulletsCache.Count - 1; index >= 0; --index)
      {
        Thing target = GameComponent_BulletsCache.BulletsCache[index];
        if (target == null || target.Destroyed || !target.Spawned)
        {
          GameComponent_BulletsCache.BulletsCache.RemoveAt(index);
        }
        else
        {
          foreach (LaserDefenceCore instance in LaserDefenceCore.Instances)
          {
            if (instance?.Parent != null && !instance.Parent.Destroyed && instance.Parent.Spawned && instance.TryLockTarget(target))
            {
              GameComponent_BulletsCache.BulletsCache.RemoveAt(index);
              break;
            }
          }
        }
      }
    }

    public virtual void ExposeData()
    {
      if (Scribe.mode == LoadSaveMode.Saving)
      {
        GameComponent_BulletsCache.BulletsCache.RemoveAll(b => b == null || b.Destroyed);
      }
      else if (Scribe.mode == LoadSaveMode.LoadingVars)
      {
        GameComponent_BulletsCache.BulletsCache.Clear();
        LaserDefenceCore.Instances.Clear();
      }
    }

    public virtual void LoadedGame()
    {
      base.LoadedGame();
      LaserDefenceCore.CleanupAllInstances();
      GameComponent_BulletsCache.BulletsCache.RemoveAll((Predicate<Thing>) (b => b == null || b.Destroyed || !b.Spawned));
    }

    public virtual void StartedNewGame()
    {
      base.StartedNewGame();
      GameComponent_BulletsCache.BulletsCache.Clear();
      LaserDefenceCore.Instances.Clear();
    }

    public static void ClearStaticCache()
    {
      GameComponent_BulletsCache.BulletsCache.Clear();
      LaserDefenceCore.Instances.Clear();
    }
  }
}
