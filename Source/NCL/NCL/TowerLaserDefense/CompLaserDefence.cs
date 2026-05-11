// Decompiled with JetBrains decompiler
// Type: TowerLaserDefense.CompLaserDefence
// Assembly: TowerLaserDefense, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: D672ED81-8E05-4DF5-BFC2-02EBAE2446C3
// Assembly location: C:\Users\15858\OneDrive\Documents\Sundry\RwModCoop\MTW\1.6\Assemblies\TowerLaserDefense.dll

using RimWorld;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using Verse;

#nullable disable
namespace TowerLaserDefense
{
  public class CompLaserDefence : ThingComp, ILaserDefenceParent
  {
    private LaserDefenceCore core;

    private CompProperties_LaserDefence Props => this.props as CompProperties_LaserDefence;

    public LaserDefenceCore DefenceCore
    {
      get
      {
        return this.core ?? (this.core = new LaserDefenceCore((ILaserDefenceParent) this, this.Props.laserDefenceProperties));
      }
    }

    public Thing Thing => (Thing) this.parent;

    public virtual void PostSpawnSetup(bool respawningAfterLoad)
    {
      LaserDefenceCore.Instances.Add(this.DefenceCore);
      if (LaserDefenceCore.LaserDefenceLoggingEnabled)
      {
        LaserDefenceCore.DbgMessage(string.Format("registered turret def={0} mapUniqueId={1} pos={2} instances={3}", (object) this.parent.def.defName, (object) (this.parent.Map == null ? -1 : this.parent.Map.uniqueID), (object) this.parent.Position, (object) LaserDefenceCore.Instances.Count));
      }
    }

    public virtual void PostDeSpawn() => LaserDefenceCore.Instances.Remove(this.DefenceCore);

    public virtual void PostDeSpawn(Map map)
    {
      if (this.core != null && LaserDefenceCore.Instances.Contains(this.core))
        LaserDefenceCore.Instances.Remove(this.core);
      this.PostDeSpawn(map, (DestroyMode) 0);
    }

    private bool HasEnoughPowerToFire()
    {
      return !this.Props.laserDefenceProperties.requiresPower || !this.Props.laserDefenceProperties.enablePowerConsumption || this.Power == null || this.DefenceCore.HasEnoughPowerToFire();
    }

    public virtual string CompInspectStringExtra()
    {
      string str = base.CompInspectStringExtra();
      string powerInfoString = this.GetPowerInfoString();
      return string.IsNullOrEmpty(str) ? powerInfoString : str + "\n" + powerInfoString;
    }

    private string GetPowerInfoString()
    {
      LaserDefenceProperties defenceProperties = this.Props.laserDefenceProperties;
      if (!defenceProperties.requiresPower || !defenceProperties.enablePowerConsumption || this.Power == null)
        return string.Empty;
      string powerInfoString = "LaserDefence_PowerPerShot".Translate(defenceProperties.powerConsumptionPerShot.ToString("F1"));
      if (!this.HasEnoughPowerToFire())
      {
        float totalStoredEnergy = this.GetTotalStoredEnergy();
        powerInfoString = powerInfoString + "\n" + "LaserDefence_PowerWarning".Translate(totalStoredEnergy.ToString("F1"));
      }
      return powerInfoString;
    }

    private float GetTotalStoredEnergy()
    {
      if (((CompPower) this.Power)?.PowerNet == null)
        return 0.0f;
      float totalStoredEnergy = 0.0f;
      foreach (CompPowerBattery batteryComp in ((CompPower) this.Power).PowerNet.batteryComps)
        totalStoredEnergy += batteryComp.StoredEnergy;
      return totalStoredEnergy;
    }

    private CompPowerTrader Power => this.parent.GetComp<CompPowerTrader>();

    public virtual void PostDestroy(DestroyMode mode, Map previousMap)
    {
      if (this.core != null && LaserDefenceCore.Instances.Contains(this.core))
        LaserDefenceCore.Instances.Remove(this.core);
      base.PostDestroy(mode, previousMap);
    }

    public virtual void PostExposeData()
    {
      Scribe_Deep.Look<LaserDefenceCore>(ref this.core, "CompLaserDefence_core", new object[2]
      {
        (object) this,
        (object) this.Props.laserDefenceProperties
      });
      if (Scribe.mode != LoadSaveMode.PostLoadInit || this.core == null || this.DetectionEnabled)
        return;
      this.core.DetectionEnabled = false;
    }

    public virtual void PostDrawExtraSelectionOverlays()
    {
      GenDraw.DrawRadiusRing(((Thing) this.parent).Position, this.Props.laserDefenceProperties.range);
    }

    public override void CompTick()
    {
      LaserDefenceCore d = this.DefenceCore;
      if (LaserDefenceCore.LaserDefenceLoggingEnabled && d != null && d.DiagShouldLogCompLayerEntry())
      {
        Thing t = (Thing) this.parent;
        int fieldId = this.core != null ? RuntimeHelpers.GetHashCode(this.core) : -1;
        int getterId = RuntimeHelpers.GetHashCode(d);
        LaserDefenceCore.DbgMessage(string.Format("COMP-TICK-ENTRY turretThingId={0} compFieldCoreIdentity={1} DefenceCoreGetterIdentity={2} fieldRefEqualsGetter={3}", (object) t.thingIDNumber, (object) fieldId, (object) getterId, (object) (this.core != null && object.ReferenceEquals(this.core, d))));
      }

      d.Tick();
    }

    public bool DetectionEnabled
    {
      get
      {
        LaserDefenceCore defenceCore = this.DefenceCore;
        return defenceCore == null || defenceCore.DetectionEnabled;
      }
      set
      {
        if (this.DefenceCore == null)
          return;
        this.DefenceCore.DetectionEnabled = value;
      }
    }

    public void ToggleDetection()
    {
      if (this.DefenceCore == null)
        return;
      this.DefenceCore.ToggleDetection();
    }

    public virtual IEnumerable<Gizmo> CompGetGizmosExtra()
    {
      foreach (Gizmo gizmo in base.CompGetGizmosExtra())
        yield return gizmo;
      if (((Thing) this.parent).Faction == Faction.OfPlayer && this.DefenceCore != null)
      {
        Command_Toggle commandToggle = new Command_Toggle();
        ((Command) commandToggle).icon = (Texture) ContentFinder<Texture2D>.Get("ModIcon/KillAllSplitterSpider", true);
        ((Command)commandToggle).defaultLabel = Translator.Translate("TLD.DetectionState");
        ((Command)commandToggle).defaultDesc = this.DefenceCore.DetectionEnabled ? Translator.Translate("TLD.DetectionEnabledDesc") : Translator.Translate("TLD.DetectionDisabledDesc");
        commandToggle.isActive = (Func<bool>) (() => this.DefenceCore.DetectionEnabled);
        commandToggle.toggleAction = new Action(this.ToggleDetection);
        yield return (Gizmo) commandToggle;
      }
    }

    public virtual void PostDraw()
    {
      this.DefenceCore.DrawAt(GenThing.TrueCenter((Thing) this.parent));
    }
  }
}
