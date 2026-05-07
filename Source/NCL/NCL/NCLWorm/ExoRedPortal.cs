// Decompiled with JetBrains decompiler
// Type: NCLWorm.ExoRedPortal
// Assembly: NCL_WormBoss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B627DDDD-3AC9-4D6A-89F6-EA72F95FB570
// Assembly location: C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3555799437\1.6\Assemblies\NCL_WormBoss (3).dll

using UnityEngine;
using Verse;

#nullable disable
namespace NCLWorm
{
  [StaticConstructorOnStartup]
  public class ExoRedPortal : Thing
  {
    public float MaxScale = 7f;
    public int Duration = 120;
    private Color _coronaColor = new Color(1f, 0.1f, 0.1f, 0.4f);
    private Color _diskMainColor = new Color(1f, 0.3f, 0.05f, 0.9f);
    private Color _diskGhostColor = new Color(0.0f, 1f, 1f, 0.25f);
    private Color _horizonRimColor = new Color(1f, 0.2f, 0.1f);
    private int _age;
    private static MaterialPropertyBlock _mpb;

    public virtual void SpawnSetup(Map map, bool respawningAfterLoad)
    {
      base.SpawnSetup(map, respawningAfterLoad);
      this._age = 0;
      ExoPortalVisuals modExtension = ((Def) this.def).GetModExtension<ExoPortalVisuals>();
      if (modExtension == null)
        return;
      this._coronaColor = modExtension.coronaColor;
      this._diskMainColor = modExtension.diskMainColor;
      this._diskGhostColor = modExtension.diskGhostColor;
      this._horizonRimColor = modExtension.horizonRimColor;
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
      if (ExoRedPortal._mpb == null)
        ExoRedPortal._mpb = new MaterialPropertyBlock();
      float num1 = (float) this._age / (float) this.Duration;
      float time = Time.time;
      float num2 = 1f;
      float num3 = 0.1f;
      float num4 = 0.15f;
      if ((double) num1 < (double) num3)
      {
        num2 = Mathf.Sin((float) ((double) num1 / (double) num3 * 3.1415927410125732 * 0.5)) * 1.1f;
        if ((double) num2 > 1.0)
          num2 = (float) (1.0 + ((double) num2 - 1.0) * 0.5);
      }
      else if ((double) num1 > 1.0 - (double) num4)
        num2 = (1f - num1) / num4;
      if ((double) num2 <= 0.0099999997764825821)
        return;
      float num5 = this.MaxScale * num2;
      Vector3 vector3_1 = drawLoc;
      vector3_1.y = Altitudes.AltitudeFor((AltitudeLayer) 28);
      float num6 = (float) (1.0 + (double) Mathf.Sin(time * 3f) * 0.019999999552965164);
      ExoRedPortal._mpb.Clear();
      ExoRedPortal._mpb.SetColor(ShaderPropertyIDs.Color, (this._coronaColor * num2));
      for (int index = 0; index < 3; ++index)
      {
        float num7 = num5 * (float) (1.6000000238418579 + (double) index * 0.30000001192092896) * num6;
        float num8 = (float) ((30.0 + (double) index * 25.0) * (index % 2 == 0 ? 1.0 : -1.0));
        Quaternion quaternion = Quaternion.AngleAxis((float) ((double) time * (double) num8 + (double) index * 45.0), Vector3.up);
        Matrix4x4 matrix4x4 = Matrix4x4.TRS(vector3_1, quaternion, new Vector3(num7, 1f, num7));
        Graphics.DrawMesh(MeshPool.plane10, matrix4x4, ExoLaserAssets.Mat_Flare, 0, (Camera) null, 0, ExoRedPortal._mpb);
      }
      float num9 = num5 * 1.4f * num6;
      Quaternion quaternion1 = Quaternion.AngleAxis(time * -90f, Vector3.up);
      ExoRedPortal._mpb.Clear();
      ExoRedPortal._mpb.SetColor(ShaderPropertyIDs.Color, this._diskMainColor);
      Matrix4x4 matrix4x4_1 = Matrix4x4.TRS((vector3_1 + (Vector3.up * 0.01f)), quaternion1, new Vector3(num9, 1f, num9));
      Graphics.DrawMesh(MeshPool.plane10, matrix4x4_1, ExoPortalAssets.Mat_Accretion, 0, (Camera) null, 0, ExoRedPortal._mpb);
      ExoRedPortal._mpb.SetColor(ShaderPropertyIDs.Color, this._diskGhostColor);
      Vector3 vector3_2 = ((new Vector3(Mathf.PerlinNoise(time * 10f, 0.0f), 0.0f, Mathf.PerlinNoise(0.0f, time * 10f)) - (Vector3.one * 0.5f)) * 0.1f);
      Matrix4x4 matrix4x4_2 = Matrix4x4.TRS(((vector3_1 + (Vector3.up * 0.02f)) + vector3_2), quaternion1, new Vector3(num9 * 1.05f, 1f, num9 * 1.05f));
      Graphics.DrawMesh(MeshPool.plane10, matrix4x4_2, ExoPortalAssets.Mat_Accretion, 0, (Camera) null, 0, ExoRedPortal._mpb);
      ExoRedPortal._mpb.Clear();
      ExoRedPortal._mpb.SetColor(ShaderPropertyIDs.Color, this._horizonRimColor);
      float num10 = num5 * 0.9f;
      Matrix4x4 matrix4x4_3 = Matrix4x4.TRS((vector3_1 + (Vector3.up * 0.05f)), Quaternion.identity, new Vector3(num10, 1f, num10));
      Graphics.DrawMesh(MeshPool.plane10, matrix4x4_3, ExoPortalAssets.Mat_Singularity, 0, (Camera) null, 0, ExoRedPortal._mpb);
    }
  }
}
