// Decompiled with JetBrains decompiler
// Type: TowerLaserDefense.PlaceWorker_TurretTop
// Assembly: TowerLaserDefense, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: D672ED81-8E05-4DF5-BFC2-02EBAE2446C3
// Assembly location: C:\Users\15858\OneDrive\Documents\Sundry\RwModCoop\MTW\1.6\Assemblies\TowerLaserDefense.dll

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
      CompProperties_LaserDefence compProperties = def.GetCompProperties<CompProperties_LaserDefence>();
      if (compProperties?.laserDefenceProperties?.graphicData == null)
        return;
      GraphicData graphicData = compProperties.laserDefenceProperties.graphicData;
      Shader shader = graphicData.shaderType?.Shader ?? ShaderDatabase.Cutout;
      Graphic graphic = GraphicDatabase.Get<Graphic_Single>(graphicData.texPath, shader, graphicData.drawSize, Color.white);
      Vector3 vector3 = GenThing.TrueCenter(loc, rot, ((BuildableDef) def).Size, Altitudes.AltitudeFor((AltitudeLayer) 39));
      GhostUtility.GhostGraphicFor(graphic, def, ghostCol, (ThingDef) null).DrawFromDef(vector3, rot, def, (float) TurretTop.ArtworkRotation);
    }
  }
}
