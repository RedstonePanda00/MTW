// Decompiled with JetBrains decompiler
// Type: TowerLaserDefense.LaserDefenceProperties
// Assembly: TowerLaserDefense, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: D672ED81-8E05-4DF5-BFC2-02EBAE2446C3
// Assembly location: C:\Users\15858\OneDrive\Documents\Sundry\RwModCoop\MTW\1.6\Assemblies\TowerLaserDefense.dll

using System.Collections.Generic;
using Verse;

#nullable disable
namespace TowerLaserDefense
{
  public class LaserDefenceProperties
  {
    public float range;

    // Extra horizontal cells beyond `range` while a target is locked: Tick used the same radius as acquisition,
    // so fast shells left the circle on the next tick and never drew aim line / turret turn before intercept.
    public int lockLeashCells = 72;

    public int interceptCount = 1;
    public int interceptTime = 20;
    public bool ignoreGroundProjectiles = false;
    public bool ignoreAirProjectiles = false;
    public bool needSight = false;
    public bool requiresPower = true;
    public SoundDef interceptSound;
    public List<string> randomImpactFlecks = new List<string>();
    public GraphicData graphicData;
    public float laserOffsetX = 0.0f;
    public float laserOffsetY = 0.0f;
    public string laserLineTexture = "Motes/LaserLine";
    public string connectingLineFleck = "NCL_LaserDefenseLine";
    public string impactFleck = "NCL_LaserDefenseImpact";
    public bool enableLaserLine = true;
    public bool enableConnectingLineFleck = true;
    public float laserWidth = 0.7f;
    public float connectinglaserWidth = 1f;
    public float impactScale = 1f;
    public float smokeSize = 1f;
    public bool enablePowerConsumption = false;
    public float powerConsumptionPerShot = 50f;
    public bool enableSpecialBulletExplosion = false;
    public bool enableSmokeEffect = true;
    public bool enableFireGlowEffect = true;
    public string secondaryImpactFleck = "NCL_Fleck_BurnerUsedEmber";
    public float secondaryImpactScale = 1f;
    public string tertiaryImpactFleck = "";
    public float tertiaryImpactScale = 1f;
    public int coolDownAfterShots = 0;
    public int coolDownTicks = 0;
    public int maxTargetsPerTick = 1;
    public bool enableCooldownEffect = false;
  }
}
