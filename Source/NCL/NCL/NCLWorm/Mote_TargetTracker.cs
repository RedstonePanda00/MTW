// Decompiled with JetBrains decompiler
// Type: NCLWorm.Mote_TargetTracker
// Assembly: NCL_WormBoss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B627DDDD-3AC9-4D6A-89F6-EA72F95FB570
// Assembly location: C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3555799437\1.6\Assemblies\NCL_WormBoss (3).dll

using UnityEngine;
using Verse;

#nullable disable
namespace NCLWorm
{
  public class Mote_TargetTracker : MoteAttached
  {
    private MoteTargetTrackerVisuals _ext;
    private Material _matCrosshair;
    private Material _matRing;
    private Material _matTriangles;

    protected virtual void DrawAt(Vector3 drawLoc, bool flip = false)
    {
      link1.UpdateDrawPos();
      exactPosition = link1.LastDrawPos + def.mote.attachedDrawOffset;
      if (((Thing) this).def == null || ((Thing) this).def.mote == null)
        return;
      this.InitMaterialsIfNeeded();
      if (this._matCrosshair == null || this._matRing == null || this._matTriangles == null)
      {
        base.DrawAt(drawLoc, flip);
      }
      else
      {
        Color color = this._ext.customColor ?? ((Mote) this).instanceColor;
        color.a *= ((Mote) this).Alpha;
        if ((double) color.a <= 0.0099999997764825821)
          return;
        this._matCrosshair.color = color;
        this._matRing.color = color;
        this._matTriangles.color = color;
        float num1 = Altitudes.AltitudeFor(((BuildableDef) ((Thing) this).def).altitudeLayer) + 0.05f;
        Vector3 exactPosition = ((Mote) this).exactPosition;
        exactPosition.y = num1;
        this.DrawPart(this._matCrosshair, exactPosition, 0.0f, this._ext.scaleCrosshair * ((Thing) this).def.graphicData.drawSize.x);
        float rotation = (float) ((double) Time.realtimeSinceStartup * (double) this._ext.rotationSpeedRing % 360.0);
        this.DrawPart(this._matRing, exactPosition, rotation, this._ext.scaleRing * ((Thing) this).def.graphicData.drawSize.x);
        float num2 = this._ext.scaleTrianglesBase + Mathf.Sin(Time.realtimeSinceStartup * this._ext.breathSpeed) * this._ext.scaleTrianglesSwing;
        this.DrawPart(this._matTriangles, exactPosition, 0.0f, num2 * ((Thing) this).def.graphicData.drawSize.x);
      }
    }

    private void DrawPart(Material mat, Vector3 pos, float rotation, float scale)
    {
      Matrix4x4 matrix4x4 = new Matrix4x4();
      matrix4x4.SetTRS(pos, Quaternion.Euler(0.0f, rotation, 0.0f), new Vector3(scale, 1f, scale));
      Graphics.DrawMesh(MeshPool.plane10, matrix4x4, mat, 0);
    }

    private void InitMaterialsIfNeeded()
    {
      if (this._ext == null)
      {
        this._ext = ((Def) ((Thing) this).def).GetModExtension<MoteTargetTrackerVisuals>();
        if (this._ext == null)
        {
          Log.Warning("[WormBoss] Mote_TargetTracker requires MoteTargetTrackerVisuals DefModExtension!");
          this._ext = new MoteTargetTrackerVisuals();
        }
      }
      if (this._matCrosshair == null && !string.IsNullOrEmpty(this._ext.texCrosshair))
        this._matCrosshair = MaterialPool.MatFrom(this._ext.texCrosshair, ShaderDatabase.MoteGlow);
      if (this._matRing == null && !string.IsNullOrEmpty(this._ext.texRing))
        this._matRing = MaterialPool.MatFrom(this._ext.texRing, ShaderDatabase.MoteGlow);
      if (!this._matTriangles == null || string.IsNullOrEmpty(this._ext.texTriangles))
        return;
      this._matTriangles = MaterialPool.MatFrom(this._ext.texTriangles, ShaderDatabase.MoteGlow);
    }
  }
}
