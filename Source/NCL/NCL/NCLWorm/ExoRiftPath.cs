// Decompiled with JetBrains decompiler
// Type: NCLWorm.ExoRiftPath
// Assembly: NCL_WormBoss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B627DDDD-3AC9-4D6A-89F6-EA72F95FB570
// Assembly location: C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3555799437\1.6\Assemblies\NCL_WormBoss (3).dll

using UnityEngine;
using Verse;

#nullable disable
namespace NCLWorm
{
  [StaticConstructorOnStartup]
  public class ExoRiftPath : Thing
  {
    public Vector3 StartPos;
    public Vector3 EndPos;
    public int Duration = 70;
    public float Width = 3.5f;
    private int _age = 0;
    private Mesh _viewMesh;
    private static MaterialPropertyBlock _mpb_inst;
    private Color _energyBaseColor = new Color(1f, 0.35f, 0.05f);
    private Color _energyHotColor = new Color(1f, 0.1f, 0.1f);
    private Color _voidRimColor = new Color(1f, 0.2f, 0.05f, 0.9f);
    private Color _groundWarnColor = new Color(0.6f, 0.1f, 0.0f, 0.4f);

    private static MaterialPropertyBlock _mpb
    {
      get => ExoRiftPath._mpb_inst ?? (ExoRiftPath._mpb_inst = new MaterialPropertyBlock());
    }

    public virtual void SpawnSetup(Map map, bool respawningAfterLoad)
    {
      base.SpawnSetup(map, respawningAfterLoad);
      this._age = 0;
      ExoRiftVisuals modExtension = ((Def) this.def).GetModExtension<ExoRiftVisuals>();
      if (modExtension == null)
        return;
      this._energyBaseColor = modExtension.energyBaseColor;
      this._energyHotColor = modExtension.energyHotColor;
      this._voidRimColor = modExtension.voidRimColor;
      this._groundWarnColor = modExtension.groundWarnColor;
    }

    public virtual void Destroy(DestroyMode mode = 0)
    {
      if (this._viewMesh != null)
      {
        UnityEngine.Object.Destroy(this._viewMesh);
        this._viewMesh = (Mesh) null;
      }
      base.Destroy(mode);
    }

    protected virtual void Tick()
    {
      ++this._age;
      if (this._age < this.Duration)
        return;
      base.Destroy((DestroyMode) 0);
    }

    protected virtual void DrawAt(Vector3 drawLoc, bool flip = false)
    {
      if (this._viewMesh == null)
      {
        this._viewMesh = new Mesh();
        this._viewMesh.MarkDynamic();
        ((Object) this._viewMesh).name = "RiftMesh_" + this.thingIDNumber.ToString();
      }
      float num1 = (float) this._age / (float) this.Duration;
      float alpha = 1f;
      if ((double) num1 < 0.15000000596046448)
        alpha = num1 / 0.15f;
      else if ((double) num1 > 0.89999997615814209)
        alpha = (float) ((1.0 - (double) num1) / 0.10000000149011612);
      if ((double) alpha <= 0.0099999997764825821)
        return;
      float time = Time.time;
      float num2 = this.Width * alpha;
      float num3 = Altitudes.AltitudeFor((AltitudeLayer) 4);
      Vector3 startPos = this.StartPos;
      startPos.y = num3 + 0.05f;
      Vector3 endPos = this.EndPos;
      endPos.y = num3 + 0.05f;
      this.DrawGroundWarning(startPos, endPos, num2 * 1.4f, alpha * 0.6f);
      ExoLaserRenderer.RenderLaser(this._viewMesh, startPos, endPos, num2 * 1.3f, 1f, time, false, this._energyBaseColor, this._energyHotColor);
      this.DrawVoidCore(startPos, endPos, num2 * 0.65f, alpha);
    }

    private void DrawVoidCore(Vector3 s, Vector3 e, float width, float alpha)
    {
      Vector3 vector3_1 = (e - s);
      Vector3 normalized = vector3_1.normalized;
      Vector3 vector3_2 = (e - s);
      float num1 = Mathf.Max(0.0f, vector3_2.magnitude - 2.5f);
      Vector3 vector3_3 = ((s + e) * 0.5f);
      vector3_3.y += 0.15f;
      float num2 = (float) (1.0 + (double) Mathf.Sin(Time.time * 12f) * 0.029999999329447746);
      Color voidRimColor = this._voidRimColor;
      voidRimColor.a *= alpha;
      ExoRiftPath._mpb.Clear();
      ExoRiftPath._mpb.SetColor(ShaderPropertyIDs.Color, voidRimColor);
      Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(vector3_3, Quaternion.LookRotation(normalized), new Vector3(width * num2, 1f, num1)), ExoPortalAssets.Mat_Singularity, 0, (Camera) null, 0, ExoRiftPath._mpb);
    }

    private void DrawGroundWarning(Vector3 s, Vector3 e, float width, float alpha)
    {
      Vector3 vector3_1 = (e - s);
      Vector3 normalized = vector3_1.normalized;
      Vector3 vector3_2 = (e - s);
      float magnitude = vector3_2.magnitude;
      Vector3 vector3_3 = ((s + e) * 0.5f);
      vector3_3.y = Altitudes.AltitudeFor((AltitudeLayer) 4) + 0.02f;
      ExoRiftPath._mpb.Clear();
      ExoRiftPath._mpb.SetColor(ShaderPropertyIDs.Color, (this._groundWarnColor * alpha));
      Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(vector3_3, Quaternion.LookRotation(normalized), new Vector3(width, 1f, magnitude)), ExoLaserAssets.Mat_Glow, 0, (Camera) null, 0, ExoRiftPath._mpb);
    }
  }
}
