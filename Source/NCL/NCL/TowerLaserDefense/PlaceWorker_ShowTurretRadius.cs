// Decompiled with JetBrains decompiler
// Type: TowerLaserDefense.PlaceWorker_ShowTurretRadius
// Assembly: TowerLaserDefense, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: D672ED81-8E05-4DF5-BFC2-02EBAE2446C3
// Assembly location: C:\Users\15858\OneDrive\Documents\Sundry\RwModCoop\MTW\1.6\Assemblies\TowerLaserDefense.dll

using Verse;

#nullable disable
namespace TowerLaserDefense
{
  public class PlaceWorker_ShowTurretRadius : PlaceWorker
  {
    public virtual AcceptanceReport AllowsPlacing(
      BuildableDef checkingDef,
      IntVec3 loc,
      Rot4 rot,
      Map map,
      Thing thingToIgnore = null,
      Thing thing = null)
    {
      GenDraw.DrawRadiusRing(loc, (checkingDef as ThingDef).GetCompProperties<CompProperties_LaserDefence>().laserDefenceProperties.range);
      return true;
    }
  }
}
