// Decompiled with JetBrains decompiler
// Type: TowerLaserDefense.LaserDefenceCore
// Assembly: TowerLaserDefense, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: D672ED81-8E05-4DF5-BFC2-02EBAE2446C3
// Assembly location: C:\Users\15858\OneDrive\Documents\Sundry\RwModCoop\MTW\1.6\Assemblies\TowerLaserDefense.dll

using RimWorld;
using System;
using System.Collections.Generic;
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
          this._lockedTargets.Clear();
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
      if (this._coolDownTicksLeft > 0 || this._lockedTargets.Count >= this.properties.interceptCount || !this.CanSeeTarget(target))
        return false;
      foreach (LaserDefenceCore instance in LaserDefenceCore.Instances)
      {
        if (instance._lockedTargets.Any<LockedTargetData>((Func<LockedTargetData, bool>) (data => data.target.GetUniqueLoadID() == target.GetUniqueLoadID())))
          return false;
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
        this._lockedTargets.Add(new LockedTargetData(target));
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

    private void TryRemoveTarget(Thing target)
    {
      if (target == null)
        return;
      foreach (LockedTargetData lockedTargetData in this._lockedTargets.Where<LockedTargetData>((Func<LockedTargetData, bool>) (data => data.target == target)).ToList<LockedTargetData>())
      {
        int index = this._lockedTargets.IndexOf(lockedTargetData);
        if (index >= 0 && index < this._lockedTargets.Count)
          this._lockedTargets.RemoveAt(index);
        else
          Log.Warning("尝试移除无效索引的目标: " + ((Entity) target).LabelCap);
      }
    }

    public void Tick()
    {
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
            this._lockedTargets.RemoveAll((Predicate<LockedTargetData>) (data => data.target == null || data.target.Destroyed || !data.target.Spawned || data.target.Map != this.Parent.Map));
          }
          if (this.Parent == null || this.Parent.Destroyed || this.properties == null || this._lockedTargets == null)
            return;
          if (this._coolDownTicksLeft > 0)
          {
            --this._coolDownTicksLeft;
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
                    this.TryRemoveTarget(lockedTargetData.target);
                  }
                  else
                  {
                    IntVec3 intVec3 = lockedTargetData.target.PositionHeld - this.Parent.PositionHeld;
                    if ((double)intVec3.LengthHorizontalSquared > (double)this.properties.range * (double)this.properties.range)
                      this.TryRemoveTarget(lockedTargetData.target);
                    else if (this.properties.needSight && !GenSight.LineOfSight(this.Parent.Position, lockedTargetData.target.Position, this.Parent.Map))
                    {
                      this.TryRemoveTarget(lockedTargetData.target);
                    }
                    else
                    {
                      ++lockedTargetData.time;
                      if (lockedTargetData.time >= this.properties.interceptTime)
                      {
                        if (this.DestroyTarget(lockedTargetData.target))
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
                        this.TryRemoveTarget(lockedTargetData.target);
                      }
                    }
                  }
                }
                else
                  break;
              }
              this.GunRotate();
            }
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
        foreach (LockedTargetData lockedTarget in this._lockedTargets)
        {
          Material material = MaterialPool.MatFrom("Motes/LaserLine", ShaderDatabase.TransparentPostLight, new Color(1f, 1f, 1f, (float) lockedTarget.time * 0.8f / (float) this.properties.interceptTime));
          Vector3 drawPos1 = lockedTarget.target.DrawPos;
          drawPos1.y = 0.0f;
          GenDraw.DrawLineBetween(drawPos, drawPos1, Altitudes.AltitudeFor((AltitudeLayer) 16), material, 0.7f);
        }
      }
      if (this.properties.graphicData == null)
        return;
      drawPos.y = Altitudes.AltitudeFor((AltitudeLayer) 17);
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

      public virtual void GameComponentTick()
      {
        if (Find.TickManager.TicksGame % 1000 != 0)
          return;
        LaserDefenceCore.CleanupAllInstances();
      }
    }
  }
}
