using RimWorld;
using UnityEngine;
using Verse;

#nullable disable
namespace TowerLaserDefense
{
  public class PlaceWorker_TurretTop : PlaceWorker
  {
    public virtual void DrawGhost(
      ThingDef def,
      IntVec3 loc,
      Rot4 rot,
      Color ghostCol,
      Thing thing = null)
    {
      GraphicData graphicData = ResolveTopGraphicData(def);
      if (graphicData == null)
        return;
      Shader shader = graphicData.shaderType?.Shader ?? ShaderDatabase.Cutout;
      Graphic graphic = GraphicDatabase.Get<Graphic_Single>(graphicData.texPath, shader, graphicData.drawSize, Color.white);
      Vector3 vector3 = GenThing.TrueCenter(loc, rot, def.Size, AltitudeLayer.BuildingOnTop.AltitudeFor());
      GhostUtility.GhostGraphicFor(graphic, def, ghostCol, (ThingDef) null).DrawFromDef(vector3, rot, def, (float) TurretTop.ArtworkRotation);
    }

    private static GraphicData ResolveTopGraphicData(BuildableDef def)
    {
      if (def is not ThingDef thingDef)
        return null;
      if (thingDef.building?.turretGunDef?.graphicData != null)
        return thingDef.building.turretGunDef.graphicData;
      CompProperties_LaserDefence compProperties = thingDef.GetCompProperties<CompProperties_LaserDefence>();
      return compProperties?.laserDefenceProperties?.graphicData;
    }
  }
}
