// Decompiled with JetBrains decompiler
// Type: TowerLaserDefense.LaserDefenceCore
// Assembly: TowerLaserDefense, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: D672ED81-8E05-4DF5-BFC2-02EBAE2446C3
// Assembly location: C:\Users\15858\OneDrive\Documents\Sundry\RwModCoop\MTW\1.6\Assemblies\TowerLaserDefense.dll

using RimWorld;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;
using Verse.Sound;

#nullable disable
namespace TowerLaserDefense
{
  [StaticConstructorOnStartup]
  public class LaserDefenceCore : IExposable
  {
    private bool _detectionEnabled = true;
    private FleckCreationData emberFleck = new FleckCreationData()
    {
      def = DefDatabase<FleckDef>.GetNamed("NCL_Fleck_BurnerUsedEmber", true)
    };
    private int _cleanCounter;
    private Effecter _cooldownEffecter;
    public static readonly List<LaserDefenceCore> Instances = new List<LaserDefenceCore>();
    public LaserDefenceProperties properties;
    public ILaserDefenceParent parent;
    private float _aimingAngle;
    private int _randomRot;
    private int _noRot;
    private CompPowerTrader _power;
    private List<LockedTargetData> _lockedTargets = new List<LockedTargetData>();
    private List<Thing> _finishedTargets = new List<Thing>();
    private int _destroyedCount = 0;
    private int _coolDownTicksLeft = 0;

    // After TryLockTarget, log LaserDefenceCore.Tick state each tick until this tick (inclusive).
    private int _diagCompTickTraceUntilTick = -1;

    // Throttle TICK-PROBE when only locked (no active COMP-TICK trace window).
    private int _lastTickProbeWhileLockedLog = -99999999;

    // Toggle to true when debugging; false silences [NCL LaserDefense] diagnostics (DbgMessage + gated warnings).
    public static bool LaserDefenceLoggingEnabled = false;

    private static int _lastGlobalSummaryTick = -999999;

    private static readonly Dictionary<int, int> ProjectileLastDiagTick = new Dictionary<int, int>();

    public static int DebugTicksGame => Current.Game != null ? Find.TickManager.TicksGame : -1;

    public static void DbgMessage(string message)
    {
      if (!LaserDefenceCore.LaserDefenceLoggingEnabled)
        return;
      Log.Message(string.Format("[NCL LaserDefense @{0}] {1}", (object) LaserDefenceCore.DebugTicksGame, (object) message));
    }

    public int DiagCoreIdentity => RuntimeHelpers.GetHashCode(this);

    public bool DiagIsCanonicalOnParent()
    {
      if (!(this.Parent is ThingWithComps twc))
        return false;
      CompLaserDefence comp = twc.TryGetComp<CompLaserDefence>();
      return comp != null && object.ReferenceEquals(comp.DefenceCore, this);
    }

    public int DiagCountOtherCoresSameParentInInstances()
    {
      Thing p = this.Parent;
      if (p == null)
        return 0;
      int id = p.thingIDNumber;
      int n = 0;
      for (int i = 0; i < LaserDefenceCore.Instances.Count; i++)
      {
        LaserDefenceCore x = LaserDefenceCore.Instances[i];
        if (x == null || object.ReferenceEquals(x, this))
          continue;
        if (x.Parent != null && x.Parent.thingIDNumber == id)
          ++n;
      }

      return n;
    }

    private string DiagFormatCoreContext()
    {
      bool inInst = LaserDefenceCore.Instances.Contains(this);
      return string.Format("coreIdentity={0} canonicalOnParent={1} instancesContainsThis={2} otherCoresSameParentInInstances={3}", (object) this.DiagCoreIdentity, (object) this.DiagIsCanonicalOnParent(), (object) inInst, (object) this.DiagCountOtherCoresSameParentInInstances());
    }

    public bool DiagShouldLogCompLayerEntry()
    {
      if (!LaserDefenceCore.LaserDefenceLoggingEnabled || Current.Game == null)
        return false;
      int now = Find.TickManager.TicksGame;
      return this._diagCompTickTraceUntilTick >= now || (this._lockedTargets != null && this._lockedTargets.Count > 0);
    }

    private void MaybeLogTickProbeAtTickStart()
    {
      if (!LaserDefenceCore.LaserDefenceLoggingEnabled || Current.Game == null || this.parent == null || this.Parent == null)
        return;
      int now = Find.TickManager.TicksGame;
      bool inTrace = this._diagCompTickTraceUntilTick >= now;
      bool hasLocks = this._lockedTargets != null && this._lockedTargets.Count > 0;
      if (!inTrace && !hasLocks)
        return;
      bool inInstances = LaserDefenceCore.Instances.Contains(this);
      bool canonical = this.DiagIsCanonicalOnParent();
      int otherSameParent = this.DiagCountOtherCoresSameParentInInstances();
      bool forceEveryTick = hasLocks && (!inInstances || !canonical || otherSameParent > 0);
      if (!inTrace && !forceEveryTick && now - this._lastTickProbeWhileLockedLog < 30)
        return;
      if (!inTrace)
        this._lastTickProbeWhileLockedLog = now;
      int firstTgt = -1;
      if (hasLocks && this._lockedTargets.Count > 0 && this._lockedTargets[0]?.target != null)
        firstTgt = this._lockedTargets[0].target.thingIDNumber;
      LaserDefenceCore.DbgMessage(string.Format("TICK-PROBE turretThingId={0} turretDef={1} {2} lockedCount={3} diagUntil@{4} firstLockedTgtId={5} detectionEnabled={6} spawned={7} destroyed={8}", (object) this.Parent.thingIDNumber, (object) this.Parent.def.defName, (object) this.DiagFormatCoreContext(), (object) (this._lockedTargets?.Count ?? 0), (object) this._diagCompTickTraceUntilTick, (object) firstTgt, (object) this._detectionEnabled, (object) this.Parent.Spawned, (object) this.Parent.Destroyed));
    }

    private void LogCompTickInterceptSnapshot(int num1ChargeBlocked)
    {
      if (!LaserDefenceCore.LaserDefenceLoggingEnabled)
      {
        return;
      }

      int now = Find.TickManager.TicksGame;
      if (this._diagCompTickTraceUntilTick < now)
      {
        return;
      }

      string lockDetail = string.Empty;
      for (int i = 0; i < this._lockedTargets.Count; i++)
      {
        LockedTargetData data = this._lockedTargets[i];
        if (data?.target == null || data.target.Destroyed)
        {
          lockDetail += "[dead/null];";
        }
        else
        {
          int distSq = (data.target.PositionHeld - this.Parent.PositionHeld).LengthHorizontalSquared;
          int maxSq = Mathf.RoundToInt((float) this.properties.range * (float) this.properties.range);
          int lockAge = data.lockedAtTick >= 0 ? now - data.lockedAtTick : -1;
          lockDetail += string.Format("{0}:chargeTime={1} lockAgeTicks={2} distSq={3}/{4};", (object) data.target.def.defName, (object) data.time, (object) lockAge, (object) distSq, (object) maxSq);
        }
      }

      LaserDefenceCore.DbgMessage(string.Format("COMP-TICK-SHOT turretThingId={0} pos={1} traceUntil@{2} num1={3}(1=pausedBranch) IsStunned={4} RequiresPower={5} PowerOn={6} coolDownTicksLeft={7} interceptTimeDef={8} lockedCount={9} {10} [{11}]", (object) this.Parent.thingIDNumber, (object) this.Parent.Position, (object) this._diagCompTickTraceUntilTick, (object) num1ChargeBlocked, (object) this.IsStunned, (object) this.RequiresPower, (object) (this.Power != null && this.Power.PowerOn), (object) this._coolDownTicksLeft, (object) this.properties.interceptTime, (object) this._lockedTargets.Count, (object) this.DiagFormatCoreContext(), (object) lockDetail));
    }

    private CompProperties_Stunnable StunComp
    {
      get => this.Parent?.def?.GetCompProperties<CompProperties_Stunnable>();
    }

    public bool DetectionEnabled
    {
      get => this._detectionEnabled;
      set
      {
        if (this._detectionEnabled == value)
          return;
        this._detectionEnabled = value;
        if (!value)
        {
          this.LogClearAllLocks("detection_disabled");
          this._lockedTargets.Clear();
        }
      }
    }

    public void ToggleDetection() => this.DetectionEnabled = !this.DetectionEnabled;

    private bool IsStunned
    {
      get
      {
        if (this.Parent is Pawn parent)
          return parent.stances.stunner.Stunned;
        if (!(this.Parent is Building))
          return false;
        CompStunnable comp = ThingCompUtility.TryGetComp<CompStunnable>(this.Parent);
        return comp != null && comp.StunHandler.Stunned;
      }
    }

    public static void CleanupAllInstances()
    {
      LaserDefenceCore.Instances.RemoveAll((Predicate<LaserDefenceCore>) (core => core?.Parent == null || core.Parent.Destroyed || !core.Parent.Spawned));
    }

    // Instances can hold a LaserDefenceCore that is no longer CompLaserDefence.DefenceCore (e.g. after Scribe_Deep reload).
    // TryLockTarget on that stale object never runs in CompTick on the live core — breaks locks and COMP-TICK-SHOT tracing.
    public static void RemoveStaleInstanceEntries()
    {
      for (int i = LaserDefenceCore.Instances.Count - 1; i >= 0; --i)
      {
        LaserDefenceCore c = LaserDefenceCore.Instances[i];
        if (c?.Parent == null)
        {
          LaserDefenceCore.Instances.RemoveAt(i);
          continue;
        }

        ThingWithComps twc = c.Parent as ThingWithComps;
        if (twc == null)
        {
          LaserDefenceCore.Instances.RemoveAt(i);
          continue;
        }

        CompLaserDefence comp = twc.TryGetComp<CompLaserDefence>();
        if (comp?.DefenceCore == null)
        {
          LaserDefenceCore.Instances.RemoveAt(i);
          continue;
        }

        if (!object.ReferenceEquals(c, comp.DefenceCore))
        {
          LaserDefenceCore.Instances.RemoveAt(i);
        }
      }
    }

    // Re-add any laser defence cores that are valid on maps but missing from Instances (e.g. after bad static clears).
    public static void ResyncInstancesFromMaps()
    {
      LaserDefenceCore.CleanupAllInstances();
      Game game = Current.Game;
      if (game == null)
        return;
      int added = 0;
      foreach (Map map in game.Maps)
      {
        if (map == null)
          continue;
        foreach (Thing thing in map.listerThings.AllThings)
        {
          if (thing is not ThingWithComps twc)
            continue;
          CompLaserDefence comp = twc.TryGetComp<CompLaserDefence>();
          if (comp?.DefenceCore == null)
            continue;
          if (!LaserDefenceCore.Instances.Contains(comp.DefenceCore))
          {
            LaserDefenceCore.Instances.Add(comp.DefenceCore);
            ++added;
          }
        }
      }

      LaserDefenceCore.RemoveStaleInstanceEntries();
      if (LaserDefenceCore.LaserDefenceLoggingEnabled && added > 0)
      {
        LaserDefenceCore.DbgMessage(string.Format("ResyncInstancesFromMaps added={0} totalInstances={1}", (object) added, (object) LaserDefenceCore.Instances.Count));
      }
    }

    public static void MaybeLogPeriodicSummary()
    {
      if (!LaserDefenceCore.LaserDefenceLoggingEnabled || Current.Game == null)
        return;
      int ticksGame = Find.TickManager.TicksGame;
      if (ticksGame - LaserDefenceCore._lastGlobalSummaryTick < 480)
        return;
      LaserDefenceCore._lastGlobalSummaryTick = ticksGame;
      int count1 = LaserDefenceCore.Instances.Count;
      int count2 = GameComponent_BulletsCache.BulletsCache.Count;
      if (count1 == 0 && count2 == 0)
        return;
      LaserDefenceCore.DbgMessage(string.Format("summary laserInstances={0} bulletsCache={1}", (object) count1, (object) count2));
      if (count1 == 0 && count2 > 0)
      {
        Log.Warning(string.Format("[NCL LaserDefense @{0}] bullets in cache but laserInstances=0 (turrets not registered or cleared).", (object) ticksGame));
      }
    }

    public static bool ShouldLogProjectileDiag(Thing projectile, int curTick)
    {
      if (!LaserDefenceCore.LaserDefenceLoggingEnabled || projectile == null)
        return false;
      int thingIdNumber = projectile.thingIDNumber;
      if (LaserDefenceCore.ProjectileLastDiagTick.TryGetValue(thingIdNumber, out int num) && curTick - num < 120)
        return false;
      LaserDefenceCore.ProjectileLastDiagTick[thingIdNumber] = curTick;
      if (LaserDefenceCore.ProjectileLastDiagTick.Count > 400 && curTick % 2000 == 0)
      {
        LaserDefenceCore.ProjectileLastDiagTick.Clear();
      }

      return true;
    }

    public string DebugExplainCannotSee(Thing target)
    {
      if (target == null || target.Destroyed || !target.Spawned)
      {
        return "target null/destroyed/not spawned";
      }

      if (this.Parent == null || this.Parent.Map == null)
      {
        return "parent null or no map";
      }

      if (this.Parent is Pawn parent1 && parent1.stances.stunner.Stunned)
      {
        return "parent pawn stunned";
      }

      if (target is Projectile projectile)
      {
        Thing launcher = projectile.Launcher;
        if (launcher?.Faction != null && !GenHostility.HostileTo(launcher, this.Parent))
        {
          return string.Format("non-hostile launcher faction={0}", (object) launcher.Faction.def.defName);
        }

        if (this.properties.ignoreAirProjectiles && ((Thing) projectile).def.projectile.flyOverhead)
        {
          return "ignoreAirProjectiles and flyOverhead";
        }

        if (this.properties.ignoreGroundProjectiles && !((Thing) projectile).def.projectile.flyOverhead)
        {
          return string.Format("ignoreGroundProjectiles excludes ground projectile def={0} flyOverhead=false", (object) projectile.def.defName);
        }
      }

      if (this.RequiresPower && this.Parent is Building parent2)
      {
        CompPowerTrader comp = ((ThingWithComps) parent2).GetComp<CompPowerTrader>();
        if (comp == null || !comp.PowerOn)
        {
          return comp == null ? "requiresPower but no CompPowerTrader" : "requiresPower but PowerOn=false";
        }
      }

      if (!target.Spawned || target.Map != this.Parent.Map)
      {
        return string.Format("wrong map or not spawned (targetMap={0} parentMap={1})", target.Map == null ? "null" : "ok", this.Parent.Map == null ? "null" : "ok");
      }

      IntVec3 intVec3 = target.PositionHeld - this.Parent.PositionHeld;
      double rangeSq = (double) this.properties.range * (double) this.properties.range;
      if ((double) intVec3.LengthHorizontalSquared > rangeSq)
      {
        return string.Format("out of range distSq={0:F0} maxSq={1:F0}", (object) (float) intVec3.LengthHorizontalSquared, (object) (float) rangeSq);
      }

      if (this.properties.needSight && !GenSight.LineOfSight(this.Parent.Position, target.Position, this.Parent.Map))
      {
        return "needSight blocked LOS";
      }

      return null;
    }

    public string DebugExplainCannotLock(Thing target)
    {
      if (this._coolDownTicksLeft > 0)
      {
        return string.Format("cooldownTicksLeft={0}", (object) this._coolDownTicksLeft);
      }

      if (this._lockedTargets.Count >= this.properties.interceptCount)
      {
        return string.Format("intercept slots full ({0}/{1})", (object) this._lockedTargets.Count, (object) this.properties.interceptCount);
      }

      foreach (LaserDefenceCore instance in LaserDefenceCore.Instances)
      {
        if (instance == null || instance._lockedTargets == null)
        {
          continue;
        }

        foreach (LockedTargetData lockedTargetData in instance._lockedTargets)
        {
          if (lockedTargetData.target != null && lockedTargetData.target.GetUniqueLoadID() == target.GetUniqueLoadID())
          {
            return string.Format("already locked by turret def={0} id={1}", (object) (instance.Parent?.def?.defName ?? "?"), (object) (instance.Parent?.thingIDNumber ?? -1));
          }
        }
      }

      string cannotSee = this.DebugExplainCannotSee(target);
      return cannotSee != null ? "cannot see: " + cannotSee : "unknown (CanSeeTarget mismatch)";
    }

    public bool HasEnoughPowerToFire()
    {
      if (!this.RequiresPower || !this.properties.enablePowerConsumption)
        return true;
      if (this.Power == null || !this.Power.PowerOn || ((CompPower) this.Power).PowerNet == null)
        return false;
      float consumptionPerShot = this.properties.powerConsumptionPerShot;
      return (double) ((CompPower) this.Power).PowerNet.CurrentEnergyGainRate() >= (double) consumptionPerShot || (double) this.GetTotalStoredEnergy(((CompPower) this.Power).PowerNet) >= (double) consumptionPerShot;
    }

    private float GetTotalStoredEnergy(PowerNet net)
    {
      float totalStoredEnergy = 0.0f;
      foreach (CompPowerBattery batteryComp in net.batteryComps)
        totalStoredEnergy += batteryComp.StoredEnergy;
      return totalStoredEnergy;
    }

    private void ConsumePowerFromNet(PowerNet net, float amount)
    {
      foreach (CompPowerBattery batteryComp in net.batteryComps)
      {
        if ((double) amount > 0.0)
        {
          if ((double) batteryComp.StoredEnergy > 0.0)
          {
            float num = Mathf.Min(amount, batteryComp.StoredEnergy);
            batteryComp.DrawPower(num);
            amount -= num;
          }
        }
        else
          break;
      }
      if ((double) amount <= 0.0)
        return;
      this.Power.PowerOutput -= amount;
    }

    private bool RequiresPower
    {
      get
      {
        return this.properties.requiresPower && ThingCompUtility.TryGetComp<CompPowerTrader>(this.Parent) != null;
      }
    }

    public LaserDefenceCore(ILaserDefenceParent parent, LaserDefenceProperties properties)
    {
      this.parent = parent;
      this.properties = properties;
    }

    public bool CanSeeTarget(Thing target)
    {
      if (target == null || target.Destroyed || !target.Spawned || this.Parent == null || this.Parent.Map == null || this.Parent is Pawn parent1 && parent1.stances.stunner.Stunned)
        return false;
      if (target is Projectile projectile)
      {
        Thing launcher = projectile.Launcher;
        if (launcher?.Faction != null && !GenHostility.HostileTo(launcher, this.Parent) || this.properties.ignoreAirProjectiles && ((Thing) projectile).def.projectile.flyOverhead || this.properties.ignoreGroundProjectiles && !((Thing) projectile).def.projectile.flyOverhead)
          return false;
      }
      int num1;
      if (this.RequiresPower && this.Parent is Building parent2)
      {
        CompPowerTrader comp = ((ThingWithComps) parent2).GetComp<CompPowerTrader>();
        num1 = comp != null ? (!comp.PowerOn ? 1 : 0) : 1;
      }
      else
        num1 = 0;
      if (num1 != 0)
        return false;
      int num2;
      if (target.Spawned && target.Map == this.Parent.Map)
      {
        IntVec3 intVec3 = target.PositionHeld - this.Parent.PositionHeld;
        if ((double)intVec3.LengthHorizontalSquared <= (double)this.properties.range * (double)this.properties.range)
        {
          num2 = !this.properties.needSight ? 1 : (GenSight.LineOfSight(this.Parent.Position, target.Position, this.Parent.Map) ? 1 : 0);
          goto label_14;
        }
      }
      num2 = 0;
label_14:
      return num2 != 0;
    }

    public bool CanLockTarget(Thing target)
    {
      if (!this.DetectionEnabled || this._coolDownTicksLeft > 0 || this._lockedTargets.Count >= this.properties.interceptCount || !this.CanSeeTarget(target))
        return false;
      foreach (LaserDefenceCore instance in LaserDefenceCore.Instances)
      {
        if (instance == null || instance._lockedTargets == null)
        {
          continue;
        }

        if (instance._lockedTargets.Any<LockedTargetData>((Func<LockedTargetData, bool>) (data => data.target != null && data.target.GetUniqueLoadID() == target.GetUniqueLoadID())))
        {
          return false;
        }
      }
      return true;
    }

    public bool TryLockTarget(Thing target)
    {
      try
      {
        if (target == null || this._lockedTargets == null || this.properties == null || !this.CanLockTarget(target))
          return false;
        string targetID = target.GetUniqueLoadID();
        if (GenCollection.Any<LockedTargetData>(this._lockedTargets, (Predicate<LockedTargetData>) (x => x.target?.GetUniqueLoadID() == targetID)))
          return false;
        LockedTargetData newLock = new LockedTargetData(target);
        newLock.lockedAtTick = Find.TickManager.TicksGame;
        this._lockedTargets.Add(newLock);
        this._diagCompTickTraceUntilTick = Find.TickManager.TicksGame + 120;
        this.GunRotate();
        LaserDefenceCore.DbgMessage(string.Format("TryLockTarget OK turret={0} thingId={1} at={2} target={3} tgtId={4} lockedCount={5} lockStartedTick={6} COMP-TICK-SHOT until@{7} instancesTotal={8} {9}", (object) this.Parent.def.defName, (object) this.Parent.thingIDNumber, (object) this.Parent.Position, (object) target.def.defName, (object) target.thingIDNumber, (object) this._lockedTargets.Count, (object) newLock.lockedAtTick, (object) this._diagCompTickTraceUntilTick, (object) LaserDefenceCore.Instances.Count, (object) this.DiagFormatCoreContext()));
        return true;
      }
      catch (Exception ex)
      {
        Log.Error(string.Format("Error in TryLockTarget: {0}", (object) ex));
        return false;
      }
    }

    public Thing Parent => this.parent.Thing;

    public CompPowerTrader Power
    {
      get
      {
        return this.Parent is Building parent ? this._power ?? (this._power = ((ThingWithComps) parent).GetComp<CompPowerTrader>()) : (CompPowerTrader) null;
      }
    }

    public bool IsStunnedByEMP
    {
      get
      {
        if (!(this.Parent is Building))
          return false;
        CompStunnable comp = ThingCompUtility.TryGetComp<CompStunnable>(this.Parent);
        return comp != null && comp.StunHandler.StunFromEMP;
      }
    }

    private static string ClassifyTargetUnavailableReason(Thing t, Map parentMap)
    {
      if (t == null)
        return "target_null";
      if (t.Destroyed)
        return "target_destroyed";
      if (!t.Spawned)
        return "target_not_spawned";
      if (parentMap != null && t.Map != parentMap)
        return "target_wrong_map";
      return "target_unavailable_unknown";
    }

    private void LogLockEnd(Thing target, LockedTargetData data, string reason)
    {
      if (!LaserDefenceCore.LaserDefenceLoggingEnabled)
        return;
      int now = Find.TickManager.TicksGame;
      int lockDurationTicks = data != null && data.lockedAtTick >= 0 ? now - data.lockedAtTick : -1;
      int chargeAccumulated = data?.time ?? -1;
      int tgtId = target != null ? target.thingIDNumber : -1;
      string tgtDef = target?.def?.defName ?? "null";
      LaserDefenceCore.DbgMessage(string.Format("LockEnd turretThingId={0} tgtDef={1} tgtId={2} reason={3} lockDurationTicks={4} chargeAccumulated={5} {6}", (object) this.Parent.thingIDNumber, (object) tgtDef, (object) tgtId, (object) reason, (object) lockDurationTicks, (object) chargeAccumulated, (object) this.DiagFormatCoreContext()));
    }

    private void LogClearAllLocks(string reason)
    {
      if (!LaserDefenceCore.LaserDefenceLoggingEnabled || this._lockedTargets == null || this._lockedTargets.Count == 0)
        return;
      int now = Find.TickManager.TicksGame;
      for (int i = 0; i < this._lockedTargets.Count; i++)
      {
        LockedTargetData data = this._lockedTargets[i];
        Thing t = data?.target;
        int lockDurationTicks = data != null && data.lockedAtTick >= 0 ? now - data.lockedAtTick : -1;
        int chargeAccumulated = data?.time ?? -1;
        int tgtId = t != null ? t.thingIDNumber : -1;
        string tgtDef = t?.def?.defName ?? "null";
        LaserDefenceCore.DbgMessage(string.Format("LockEnd turretThingId={0} tgtDef={1} tgtId={2} reason={3} lockDurationTicks={4} chargeAccumulated={5} {6}", (object) this.Parent.thingIDNumber, (object) tgtDef, (object) tgtId, (object) reason, (object) lockDurationTicks, (object) chargeAccumulated, (object) this.DiagFormatCoreContext()));
      }
    }

    private void TryRemoveTarget(Thing target, string reason)
    {
      if (target == null)
        return;
      foreach (LockedTargetData lockedTargetData in this._lockedTargets.Where<LockedTargetData>((Func<LockedTargetData, bool>) (data => data.target == target)).ToList<LockedTargetData>())
      {
        this.LogLockEnd(target, lockedTargetData, reason);
        int index = this._lockedTargets.IndexOf(lockedTargetData);
        if (index >= 0 && index < this._lockedTargets.Count)
          this._lockedTargets.RemoveAt(index);
        else
          Log.Warning("尝试移除无效索引的目标: " + ((Entity) target).LabelCap);
      }
    }

    public void Tick()
    {
      this.MaybeLogTickProbeAtTickStart();
      if (!this.DetectionEnabled)
      {
        this._randomRot = 0;
        this._noRot = 0;
      }
      else
      {
        try
        {
          if (++this._cleanCounter >= 250 || this._cleanCounter < 0)
          {
            this._cleanCounter = 0;
            for (int ci = this._lockedTargets.Count - 1; ci >= 0; --ci)
            {
              LockedTargetData stale = this._lockedTargets[ci];
              Thing st = stale?.target;
              if (st == null || st.Destroyed || !st.Spawned || st.Map != this.Parent.Map)
              {
                string r = LaserDefenceCore.ClassifyTargetUnavailableReason(st, this.Parent?.Map);
                if (st != null)
                  this.LogLockEnd(st, stale, r + "_periodicCleanup");
                else if (LaserDefenceCore.LaserDefenceLoggingEnabled)
                  LaserDefenceCore.DbgMessage(string.Format("LockEnd turretThingId={0} reason=stale_slot_null_target_periodicCleanup chargeAccumulated={1} {2}", (object) this.Parent.thingIDNumber, (object) (stale?.time ?? -1), (object) this.DiagFormatCoreContext()));
                this._lockedTargets.RemoveAt(ci);
              }
            }
          }
          if (this.Parent == null || this.Parent.Destroyed || this.properties == null || this._lockedTargets == null)
            return;
          if (this._coolDownTicksLeft > 0)
          {
            --this._coolDownTicksLeft;
            if (this._lockedTargets.Count > 0)
              this.LogClearAllLocks("cooldown_tick_clear");
            this._lockedTargets.Clear();
            if (this.properties.enableCooldownEffect && this._cooldownEffecter != null)
              this._cooldownEffecter.EffectTick(new TargetInfo(this.Parent.PositionHeld, this.Parent.Map, false), new TargetInfo(this.Parent.PositionHeld, this.Parent.Map, false));
            if (this._coolDownTicksLeft != 0 || this._cooldownEffecter == null)
              return;
            this._cooldownEffecter.Cleanup();
            this._cooldownEffecter = (Effecter) null;
          }
          else
          {
            if (this._cooldownEffecter != null)
            {
              this._cooldownEffecter.Cleanup();
              this._cooldownEffecter = (Effecter) null;
            }
            int num1;
            if (!this.IsStunned)
            {
              if (this.RequiresPower)
              {
                CompPowerTrader power = this.Power;
                num1 = power != null ? (!power.PowerOn ? 1 : 0) : 1;
              }
              else
                num1 = 0;
            }
            else
              num1 = 1;
            if (num1 != 0)
            {
              this.LogClearAllLocks("stun_or_no_power_clear");
              this._lockedTargets.Clear();
            }
            else
            {
              List<LockedTargetData> lockedTargetDataList = new List<LockedTargetData>((IEnumerable<LockedTargetData>) this._lockedTargets);
              int num2 = 0;
              foreach (LockedTargetData lockedTargetData in lockedTargetDataList)
              {
                if (num2 < Mathf.Min(this.properties.maxTargetsPerTick, lockedTargetDataList.Count))
                {
                  ++num2;
                  if (lockedTargetData.target == null || lockedTargetData.target.Destroyed || !lockedTargetData.target.Spawned || lockedTargetData.target.Map != this.Parent.Map)
                  {
                    if (lockedTargetData.target != null)
                    {
                      string ru = LaserDefenceCore.ClassifyTargetUnavailableReason(lockedTargetData.target, this.Parent?.Map);
                      this.TryRemoveTarget(lockedTargetData.target, ru + "_while_tracking");
                    }
                    else
                    {
                      if (LaserDefenceCore.LaserDefenceLoggingEnabled)
                        LaserDefenceCore.DbgMessage(string.Format("LockEnd turretThingId={0} tgtDef=null tgtId=-1 reason=target_null_while_tracking lockDurationTicks=-1 chargeAccumulated={1} {2}", (object) this.Parent.thingIDNumber, (object) lockedTargetData.time, (object) this.DiagFormatCoreContext()));
                      int ni = this._lockedTargets.IndexOf(lockedTargetData);
                      if (ni >= 0)
                        this._lockedTargets.RemoveAt(ni);
                    }
                  }
                  else
                  {
                    IntVec3 intVec3 = lockedTargetData.target.PositionHeld - this.Parent.PositionHeld;
                    if ((double)intVec3.LengthHorizontalSquared > (double)this.properties.range * (double)this.properties.range)
                      this.TryRemoveTarget(lockedTargetData.target, "left_horizontal_range");
                    else if (this.properties.needSight && !GenSight.LineOfSight(this.Parent.Position, lockedTargetData.target.Position, this.Parent.Map))
                    {
                      this.TryRemoveTarget(lockedTargetData.target, "lost_line_of_sight");
                    }
                    else
                    {
                      ++lockedTargetData.time;
                      if (lockedTargetData.time == 1)
                      {
                        LaserDefenceCore.DbgMessage(string.Format("intercept CHARGE tick=1/{0} turret={1} id={2} -> target={3} tgtId={4} {5}", (object) this.properties.interceptTime, (object) this.Parent.def.defName, (object) this.Parent.thingIDNumber, (object) lockedTargetData.target.def.defName, (object) lockedTargetData.target.thingIDNumber, (object) this.DiagFormatCoreContext()));
                      }

                      if (lockedTargetData.time >= this.properties.interceptTime)
                      {
                        bool destroyedProj = this.DestroyTarget(lockedTargetData.target);
                        if (destroyedProj)
                        {
                          ++this._destroyedCount;
                          if (this._destroyedCount >= this.properties.coolDownAfterShots)
                          {
                            this._coolDownTicksLeft = this.properties.coolDownTicks;
                            this._destroyedCount = 0;
                            if (this.properties.enableCooldownEffect)
                            {
                              this.TriggerCooldownEffect();
                              break;
                            }
                            break;
                          }
                        }

                        this.TryRemoveTarget(lockedTargetData.target, destroyedProj ? "intercept_destroyed" : "intercept_cycle_finished_not_destroyed");
                      }
                    }
                  }
                }
                else
                  break;
              }
              this.GunRotate();
            }

            this.LogCompTickInterceptSnapshot(num1);
          }
        }
        catch (Exception ex)
        {
          Log.Error(string.Format("LaserDefenceCore.Tick error: {0}", (object) ex));
        }
      }
    }

    private void TriggerCooldownEffect()
    {
      this._cooldownEffecter?.Cleanup();
      EffecterDef named = DefDatabase<EffecterDef>.GetNamed("BlastMechBandShockwave", true);
      if (named != null)
      {
        this._cooldownEffecter = named.Spawn();
        this._cooldownEffecter.Trigger(new TargetInfo(this.Parent.PositionHeld, this.Parent.Map, false), new TargetInfo(this.Parent.PositionHeld, this.Parent.Map, false), -1);
      }
      else
        Log.Warning("无法找到 'BlastMechBandShockwave' EffecterDef");
    }

    private int GetStunTicksLeft()
    {
      CompStunnable comp = ThingCompUtility.TryGetComp<CompStunnable>(this.Parent);
      return comp != null ? comp.StunHandler.StunTicksLeft : 0;
    }

    public float VisualAimAngle => this._aimingAngle;

    private void GunRotate()
    {
      Vector3 vector3_1 = Vector3.zero;
      foreach (LockedTargetData lockedTarget in this._lockedTargets)
      {
        Vector3 vector3_2 = vector3_1;
        Vector3 vector3_3 = lockedTarget.target.DrawPos - GenThing.TrueCenter(this.Parent);
        Vector3 normalized = vector3_3.normalized;
        vector3_1 = vector3_2 + normalized;
      }
      if (vector3_1 != Vector3.zero)
      {
        this._aimingAngle = Vector3Utility.AngleFlat(vector3_1);
        this._randomRot = 0;
        this._noRot = Rand.Range(30, 60);
      }
      else if (this._noRot > 0)
      {
        --this._noRot;
      }
      else
      {
        if (this._randomRot == 0)
          this._randomRot = Rand.Range(-75, 75);
        if (this._randomRot > 0)
        {
          --this._randomRot;
          --this._aimingAngle;
          if (this._randomRot == 0)
            this._noRot = Rand.Range(30, 60);
        }
        else
        {
          ++this._randomRot;
          ++this._aimingAngle;
          if (this._randomRot == 0)
            this._noRot = Rand.Range(30, 60);
        }
      }
    }

    public void DrawAt(Vector3 drawPos)
    {
      drawPos.y = 0.0f;
      if (this.properties.enableLaserLine)
      {
        float denom = Mathf.Max(1f, (float) this.properties.interceptTime);
        foreach (LockedTargetData lockedTarget in this._lockedTargets)
        {
          if (lockedTarget?.target == null || lockedTarget.target.Destroyed)
          {
            continue;
          }

          // First ticks used alpha=0 so the beam was invisible — looked like lock never started.
          float chargeT = Mathf.Clamp01((float) lockedTarget.time / denom);
          float alpha = Mathf.Lerp(0.45f, 0.95f, chargeT);
          string lineTex = this.properties.laserLineTexture.NullOrEmpty() ? "Motes/LaserLine" : this.properties.laserLineTexture;
          Material material = MaterialPool.MatFrom(lineTex, ShaderDatabase.TransparentPostLight, new Color(1f, 1f, 1f, alpha));
          Vector3 drawPos1 = lockedTarget.target.DrawPos;
          drawPos1.y = 0.0f;
          GenDraw.DrawLineBetween(drawPos, drawPos1, Altitudes.AltitudeFor(AltitudeLayer.BuildingBelowTop), material, 0.7f);
        }
      }
      if (this.properties.graphicData == null)
        return;
      if (this.parent.Thing is Building_LaserDefenceTurret)
        return;
      drawPos.y = Altitudes.AltitudeFor(AltitudeLayer.BuildingOnTop);
      this.properties.graphicData.GraphicColoredFor(this.parent.Thing).Draw(drawPos, Rot4.North, this.parent.Thing, this._aimingAngle);
    }

    private bool DestroyTarget(Thing target)
    {
      try
      {
        if (target == null || target.Destroyed || !target.Spawned || this.Parent == null || this.Parent.Destroyed || this.Parent.Map == null)
          return false;
        bool flag = this.properties.enableSpecialBulletExplosion && target is Projectile projectile && ((Def) ((Thing) projectile).def).defName == "Bullet_HellsphereCannonGun";
        Vector3 vector3 = GenThing.TrueCenter(this.parent.Thing);
        if ((double) Math.Abs(this.properties.laserOffsetX) > 0.0099999997764825821 || (double) Math.Abs(this.properties.laserOffsetY) > 0.0099999997764825821)
        {
          vector3.x += this.properties.laserOffsetX;
          vector3.z += this.properties.laserOffsetY;
        }
        if (this.properties.enableConnectingLineFleck && !string.IsNullOrEmpty(this.properties.connectingLineFleck))
        {
          FleckDef namedSilentFail = DefDatabase<FleckDef>.GetNamedSilentFail(this.properties.connectingLineFleck);
          if (namedSilentFail != null)
            FleckMaker.ConnectingLine(vector3, target.DrawPos, namedSilentFail, this.Parent.Map, this.properties.connectinglaserWidth);
        }
        if (!flag)
        {
          if (this.properties.randomImpactFlecks != null && this.properties.randomImpactFlecks.Count > 0)
          {
            FleckDef namedSilentFail = DefDatabase<FleckDef>.GetNamedSilentFail(this.properties.randomImpactFlecks[Rand.Range(0, this.properties.randomImpactFlecks.Count)]);
            if (namedSilentFail != null)
              FleckMaker.Static(target.DrawPos, this.Parent.Map, namedSilentFail, this.properties.impactScale);
          }
          if (!string.IsNullOrEmpty(this.properties.secondaryImpactFleck))
          {
            FleckDef namedSilentFail = DefDatabase<FleckDef>.GetNamedSilentFail(this.properties.secondaryImpactFleck);
            if (namedSilentFail != null)
              FleckMaker.Static(target.DrawPos, this.Parent.Map, namedSilentFail, this.properties.secondaryImpactScale);
          }
          if (!string.IsNullOrEmpty(this.properties.tertiaryImpactFleck))
          {
            FleckDef namedSilentFail = DefDatabase<FleckDef>.GetNamedSilentFail(this.properties.tertiaryImpactFleck);
            if (namedSilentFail != null)
              FleckMaker.Static(target.DrawPos, this.Parent.Map, namedSilentFail, this.properties.tertiaryImpactScale);
          }
          if (this.properties.enableSmokeEffect)
            FleckMaker.ThrowSmoke(target.DrawPos, this.Parent.Map, Mathf.Clamp(this.properties.smokeSize, 0.5f, 5f));
          if (this.properties.enableFireGlowEffect)
            FleckMaker.ThrowFireGlow(target.DrawPos, this.Parent.Map, Mathf.Clamp(this.properties.smokeSize, 0.5f, 5f));
        }
        if (this.properties.interceptSound != null)
          SoundStarter.PlayOneShot(this.properties.interceptSound, new TargetInfo(target.Position, this.Parent.Map, false));
        if (this.RequiresPower && this.properties.enablePowerConsumption && this.Power != null && ((CompPower) this.Power).PowerNet != null)
          this.ConsumePowerFromNet(((CompPower) this.Power).PowerNet, this.properties.powerConsumptionPerShot);
        if (!target.Spawned || target.Destroyed)
          return false;
        if (flag)
          this.TriggerBulletExplosion(target as Projectile);
        else
          target.Destroy((DestroyMode) 2);
        LaserDefenceCore.DbgMessage(string.Format("DestroyTarget OK turret={0} id={1} projectile={2}", (object) this.Parent.def.defName, (object) this.Parent.thingIDNumber, (object) target.def.defName));
        return true;
      }
      catch (Exception ex)
      {
        Log.Error(string.Format("摧毁目标时出错: {0}", (object) ex));
        return false;
      }
    }

    private void TriggerBulletExplosion(Projectile bullet)
    {
      if (bullet == null || ((Thing) bullet).Destroyed || !((Thing) bullet).Spawned || ((Thing) bullet).Map == null)
        return;
      try
      {
        IntVec3 position = ((Thing) bullet).Position;
        Map map = ((Thing) bullet).Map;
        GenExplosion.DoExplosion(position, map, 4.9f, DamageDefOf.Vaporize, bullet.Launcher, 800, 1f, (SoundDef) null, (ThingDef) null, (ThingDef) null, (Thing) null, (ThingDef) null, 0.0f, 1, new GasType?(), new float?(), (int) byte.MaxValue, false, (ThingDef) null, 0.0f, 1, 0.0f, false, new float?(), (List<Thing>) null, new FloatRange?(), true, 1f, 0.0f, true, (ThingDef) null, 1f, (SimpleCurve) null, (List<IntVec3>) null, (ThingDef) null, (ThingDef) null);
        FleckMaker.ThrowLightningGlow(position.ToVector3(), map, 1.5f);
        FleckMaker.ThrowMicroSparks(position.ToVector3(), map);
        ((Thing) bullet).Destroy((DestroyMode) 0);
      }
      catch (Exception ex)
      {
        Log.Error(string.Format("触发子弹爆炸时出错: {0}", (object) ex));
      }
    }

    private void TrySetLauncherViaReflection(Projectile projectile, Thing launcher)
    {
      if (launcher == null)
        return;
      try
      {
        FieldInfo field = typeof (Projectile).GetField(nameof (launcher), BindingFlags.Instance | BindingFlags.NonPublic);
        if (field != (FieldInfo) null)
        {
          field.SetValue((object) projectile, (object) launcher);
        }
        else
        {
          FieldInfo[] array = ((IEnumerable<FieldInfo>) typeof (Projectile).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)).Where<FieldInfo>((Func<FieldInfo, bool>) (f => f.FieldType == typeof (Thing) && f.Name.ToLower().Contains(nameof (launcher)))).ToArray<FieldInfo>();
          if (array.Length != 0)
            array[0].SetValue((object) projectile, (object) launcher);
          else
            Log.Warning("无法设置地狱火炮子弹的发射器");
        }
      }
      catch (Exception ex)
      {
        Log.Error(string.Format("设置发射器时出错: {0}", (object) ex));
      }
    }

    public void ExposeData()
    {
      Scribe_Values.Look<bool>(ref this._detectionEnabled, "detectionEnabled", true, false);
      Scribe_Collections.Look<LockedTargetData>(ref this._lockedTargets, "_lockedTargets", (LookMode) 2, Array.Empty<object>());
      Scribe_Values.Look<int>(ref this._destroyedCount, "_destroyedCount", 0, false);
      Scribe_Values.Look<int>(ref this._coolDownTicksLeft, "_coolDownTicksLeft", 0, false);
      Scribe_Values.Look<int>(ref this._cleanCounter, "_cleanCounter", 0, false);
      if (Scribe.mode != LoadSaveMode.PostLoadInit || this._coolDownTicksLeft > 0 || this._cooldownEffecter == null)
        return;
      this._cooldownEffecter.Cleanup();
      this._cooldownEffecter = (Effecter) null;
    }

    public class MainComponent : GameComponent
    {
      public MainComponent(Game game)
      {
      }

      public override void GameComponentTick()
      {
        base.GameComponentTick();
        if (Find.TickManager.TicksGame % 1000 != 0)
          return;
        LaserDefenceCore.CleanupAllInstances();
      }
    }
  }
}
