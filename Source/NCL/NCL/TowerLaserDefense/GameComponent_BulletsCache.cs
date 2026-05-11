// Decompiled with JetBrains decompiler
// Type: TowerLaserDefense.GameComponent_BulletsCache
// Assembly: TowerLaserDefense, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: D672ED81-8E05-4DF5-BFC2-02EBAE2446C3
// Assembly location: C:\Users\15858\OneDrive\Documents\Sundry\RwModCoop\MTW\1.6\Assemblies\TowerLaserDefense.dll

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
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

    public override void GameComponentTick()
    {
      base.GameComponentTick();
      LaserDefenceCore.RemoveStaleInstanceEntries();
      LaserDefenceCore.MaybeLogPeriodicSummary();
      int ticksGame = Find.TickManager.TicksGame;
      for (int index = GameComponent_BulletsCache.BulletsCache.Count - 1; index >= 0; --index)
      {
        Thing target = GameComponent_BulletsCache.BulletsCache[index];
        if (target == null || target.Destroyed || !target.Spawned)
        {
          GameComponent_BulletsCache.BulletsCache.RemoveAt(index);
        }
        else
        {
          bool locked = false;
          HashSet<LaserDefenceCore> seenCanonical = new HashSet<LaserDefenceCore>();
          List<LaserDefenceCore> tryOrder = new List<LaserDefenceCore>(LaserDefenceCore.Instances.Count);
          foreach (LaserDefenceCore entry in LaserDefenceCore.Instances)
          {
            if (entry?.Parent == null || entry.Parent.Destroyed || !entry.Parent.Spawned || entry.Parent.Map != target.Map)
            {
              continue;
            }

            ThingWithComps twc = entry.Parent as ThingWithComps;
            if (twc == null)
            {
              continue;
            }

            CompLaserDefence comp = twc.TryGetComp<CompLaserDefence>();
            LaserDefenceCore canonical = comp?.DefenceCore;
            if (canonical == null || !seenCanonical.Add(canonical))
            {
              continue;
            }

            tryOrder.Add(canonical);
          }

          tryOrder.Sort((LaserDefenceCore a, LaserDefenceCore b) =>
          {
            int distA = (a.Parent.Position - target.Position).LengthHorizontalSquared;
            int distB = (b.Parent.Position - target.Position).LengthHorizontalSquared;
            return distA.CompareTo(distB);
          });

          foreach (LaserDefenceCore instance in tryOrder)
          {
            if (instance.TryLockTarget(target))
            {
              locked = true;
              if (LaserDefenceCore.LaserDefenceLoggingEnabled)
              {
                LaserDefenceCore.DbgMessage(string.Format("GameComponent locked projectile={0} projThingId={1} by NEAREST capable turret={2} turretThingId={3} pos={4} (candidates={5}) lockedCoreIdentity={6} canonicalOnParent={7} instancesContainsLockedCore={8} otherCoresSameParent={9} note=removed_from_bullets_cache_same_tick", (object) target.def.defName, (object) target.thingIDNumber, (object) instance.Parent.def.defName, (object) instance.Parent.thingIDNumber, (object) instance.Parent.Position, (object) tryOrder.Count, (object) RuntimeHelpers.GetHashCode(instance), (object) instance.DiagIsCanonicalOnParent(), (object) LaserDefenceCore.Instances.Contains(instance), (object) instance.DiagCountOtherCoresSameParentInInstances()));
              }

              GameComponent_BulletsCache.BulletsCache.RemoveAt(index);
              break;
            }
          }

          if (!locked && target is Projectile && LaserDefenceCore.LaserDefenceLoggingEnabled && LaserDefenceCore.Instances.Count > 0 && LaserDefenceCore.ShouldLogProjectileDiag(target, ticksGame))
          {
            LaserDefenceCore sample = null;
            foreach (LaserDefenceCore core in LaserDefenceCore.Instances)
            {
              if (core?.Parent?.Map == target.Map)
              {
                sample = core;
                break;
              }
            }

            if (sample == null)
            {
              sample = LaserDefenceCore.Instances[0];
            }

            string reason = sample != null ? sample.DebugExplainCannotLock(target) : "no sample instance";
            Log.Warning(string.Format("[NCL LaserDefense @{0}] projectile in cache but no lock (throttled ~120 ticks/proj): proj={1} launcher={2} flyOverhead={3} sampleTurret={4} reason={5}", (object) ticksGame, (object) target.def.defName, (object) (((Projectile) target).Launcher?.LabelCap ?? "null"), (object) target.def.projectile.flyOverhead, (object) (sample?.Parent?.def?.defName ?? "null"), (object) reason));
          }
          else if (!locked && target is Projectile && LaserDefenceCore.LaserDefenceLoggingEnabled && LaserDefenceCore.Instances.Count == 0 && LaserDefenceCore.ShouldLogProjectileDiag(target, ticksGame))
          {
            Log.Warning(string.Format("[NCL LaserDefense @{0}] projectile in cache but Instances=0: proj={1}", (object) ticksGame, (object) target.def.defName));
          }
        }
      }
    }

    public override void ExposeData()
    {
      if (Scribe.mode == LoadSaveMode.Saving)
      {
        GameComponent_BulletsCache.BulletsCache.RemoveAll(b => b == null || b.Destroyed);
      }
      else if (Scribe.mode == LoadSaveMode.LoadingVars)
      {
        GameComponent_BulletsCache.BulletsCache.Clear();
        LaserDefenceCore.CleanupAllInstances();
      }
    }

    public override void LoadedGame()
    {
      base.LoadedGame();
      LaserDefenceCore.CleanupAllInstances();
      LaserDefenceCore.ResyncInstancesFromMaps();
      GameComponent_BulletsCache.BulletsCache.RemoveAll((Predicate<Thing>) (b => b == null || b.Destroyed || !b.Spawned));
    }

    public override void StartedNewGame()
    {
      base.StartedNewGame();
      GameComponent_BulletsCache.BulletsCache.Clear();
      // Do NOT clear LaserDefenceCore.Instances here: InitNewGame calls StartedNewGame after MapGenerator
      // has already spawned buildings (CompLaserDefence.PostSpawnSetup registered cores). Clearing would
      // leave no turrets in Instances for the rest of the session.
      LaserDefenceCore.CleanupAllInstances();
      LaserDefenceCore.ResyncInstancesFromMaps();
    }

    public static void ClearStaticCache()
    {
      GameComponent_BulletsCache.BulletsCache.Clear();
      LaserDefenceCore.Instances.Clear();
    }
  }
}
