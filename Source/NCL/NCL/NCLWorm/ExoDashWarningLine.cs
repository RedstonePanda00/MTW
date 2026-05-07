// Decompiled with JetBrains decompiler
// Type: NCLWorm.ExoDashWarningLine
// Assembly: NCL_WormBoss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B627DDDD-3AC9-4D6A-89F6-EA72F95FB570
// Assembly location: C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3555799437\1.6\Assemblies\NCL_WormBoss (3).dll

using UnityEngine;
using Verse;

#nullable disable
namespace NCLWorm
{
  [StaticConstructorOnStartup]
  public class ExoDashWarningLine : Thing
  {
    public Vector3 StartPos;
    public Vector3 EndPos;
    public int Duration = 60;
    public float Width = 1f;
    private Color _coreColor = new Color(1f, 0.9f, 0.9f, 1f);
    private Color _hazeColor = new Color(1f, 0.0f, 0.0f, 0.6f);
    private int _age = 0;
    private static MaterialPropertyBlock _mpb;

    public virtual void SpawnSetup(Map map, bool respawningAfterLoad)
    {
      base.SpawnSetup(map, respawningAfterLoad);
      this._age = 0;
      ExoDashVisuals modExtension = ((Def) this.def).GetModExtension<ExoDashVisuals>();
      if (modExtension == null)
        return;
      this._coreColor = modExtension.coreColor;
      this._hazeColor = modExtension.hazeColor;
    }

    protected virtual void Tick()
    {
      ++this._age;
      if (this._age < this.Duration)
        return;
      this.Destroy((DestroyMode) 0);
    }

    protected virtual void DrawAt(Vector3 drawLoc, bool flip = false)
    {
      if (ExoDashWarningLine._mpb == null)
        ExoDashWarningLine._mpb = new MaterialPropertyBlock();
      Vector3 pos1 = (this.StartPos == Vector3.zero) ? drawLoc : this.StartPos;
      Vector3 pos2 = (this.EndPos == Vector3.zero) ? (pos1 + Vector3.forward) : this.EndPos;
      float num1 = Altitudes.AltitudeFor((AltitudeLayer) 28);
      pos1.y = num1;
      pos2.y = num1;
      Vector3 vector3_1 = (pos2 - pos1);
      float magnitude = vector3_1.magnitude;
      if ((double) magnitude < 0.10000000149011612)
        return;
      Vector3 vector3_2 = ((pos1 + pos2) * 0.5f);
      Quaternion quaternion = (Quaternion.LookRotation(vector3_1) * Quaternion.Euler(0.0f, 90f, 0.0f));
      float num2 = (float) this._age / (float) this.Duration;
      float time = Time.time;
      float masterAlpha = 1f;
      if ((double) num2 < 0.15000000596046448)
        masterAlpha = num2 / 0.15f;
      else if ((double) num2 > 0.89999997615814209)
        masterAlpha = (float) ((1.0 - (double) num2) / 0.10000000149011612);
      float num3 = this.Width * (float) (1.0 + (double) Mathf.Sin(time * 15f) * 0.10000000149011612);
      ExoDashWarningLine._mpb.Clear();
      ExoDashWarningLine._mpb.SetColor(ShaderPropertyIDs.Color, (this._hazeColor * masterAlpha));
      ExoDashWarningLine._mpb.SetVector(ShaderPropertyIDs.Tiling, new Vector4(magnitude / 5f, 1f, (float) (-(double) time * 5.0), 0.0f));
      Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(vector3_2, quaternion, new Vector3(magnitude, 1f, num3)), ExoLaserAssets.Mat_Streak, 0, (Camera) null, 0, ExoDashWarningLine._mpb);
      ExoDashWarningLine._mpb.Clear();
      float num4 = (float) (0.800000011920929 + (double) Mathf.Sin(time * 60f) * 0.20000000298023224);
      ExoDashWarningLine._mpb.SetColor(ShaderPropertyIDs.Color, ((this._coreColor * masterAlpha) * num4));
      ExoDashWarningLine._mpb.SetVector(ShaderPropertyIDs.Tiling, new Vector4(magnitude / 2f, 1f, 0.0f, 0.0f));
      Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS((vector3_2 + (Vector3.up * 0.01f)), quaternion, new Vector3(magnitude, 1f, num3 * 0.2f)), ExoLaserAssets.Mat_Glow, 0, (Camera) null, 0, ExoDashWarningLine._mpb);
      if ((double) masterAlpha <= 0.10000000149011612)
        return;
      this.DrawLayeredFlare(pos1, num3 * 2.5f, masterAlpha, time);
      this.DrawLayeredFlare(pos2, num3 * 2.5f, masterAlpha, time + 1.23f);
    }

    private void DrawLayeredFlare(Vector3 pos, float baseSize, float masterAlpha, float time)
    {
      ExoDashWarningLine._mpb.Clear();
      Color hazeColor = this._hazeColor;
      hazeColor.a = 0.5f * masterAlpha;
      ExoDashWarningLine._mpb.SetColor(ShaderPropertyIDs.Color, hazeColor);
      Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS((pos + (Vector3.up * 0.02f)), Quaternion.AngleAxis(time * 50f, Vector3.up), new Vector3(baseSize * 1.5f, 1f, baseSize * 1.5f)), ExoLaserAssets.Mat_Flare, 0, (Camera) null, 0, ExoDashWarningLine._mpb);
      ExoDashWarningLine._mpb.Clear();
      Color coreColor = this._coreColor;
      coreColor.a = 1f * masterAlpha;
      ExoDashWarningLine._mpb.SetColor(ShaderPropertyIDs.Color, coreColor);
      Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS((pos + (Vector3.up * 0.03f)), Quaternion.AngleAxis((float) (-(double) time * 150.0), Vector3.up), new Vector3(baseSize * 0.7f, 1f, baseSize * 0.7f)), ExoLaserAssets.Mat_Flare, 0, (Camera) null, 0, ExoDashWarningLine._mpb);
    }
  }
}
