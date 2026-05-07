// Decompiled with JetBrains decompiler
// Type: NCLWorm.TCP_WormMovingController
// Assembly: NCL_WormBoss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B627DDDD-3AC9-4D6A-89F6-EA72F95FB570
// Assembly location: C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3555799437\1.6\Assemblies\NCL_WormBoss (3).dll

using Verse;

#nullable disable
namespace NCLWorm
{
  public class TCP_WormMovingController : CompProperties
  {
    public float baseSpeed = 6f;
    public float turnRate = 90f;
    public float acceleration = 2f;
    public float impactDamage = 20f;
    public float minImpactSpeed = 4f;
    public DamageDef impactDamageDef;
    public float impactArmorPenetration = 0.5f;
    public float headBaseLimbBreakFactor = 0.2f;
    public float headSpeedScale = 1f;
    public float headMaxLimbBreakFactor = 1.5f;

    public TCP_WormMovingController() => this.compClass = typeof (TC_WormMovingController);
  }
}
