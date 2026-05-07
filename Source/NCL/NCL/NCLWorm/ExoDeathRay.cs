// Decompiled with JetBrains decompiler
// Type: NCLWorm.ExoDeathRay
// Assembly: NCL_WormBoss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B627DDDD-3AC9-4D6A-89F6-EA72F95FB570
// Assembly location: C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3555799437\1.6\Assemblies\NCL_WormBoss (3).dll

using UnityEngine;
using Verse;

#nullable disable
namespace NCLWorm
{
  [StaticConstructorOnStartup]
  public class ExoDeathRay : Thing
  {
    private Vector3 _startPos;
    private Vector3 _direction;
    private float _length;
    private float _width;
    private float _overheat;
    private Color _colNormal = Color.cyan;
    private Color _colHot = Color.red;
    private int _lastUpdateTick;
    private bool _isFadingOut;
    private float _age = 0.0f;
    private float _currentLength = 0.0f;
    private float _currentWidth = 0.0f;
    private float _currentOverheat = 0.0f;
    private float _visualTime = 0.0f;
    private Mesh _viewMesh;
    private bool _isRainbowMode = false;
    private static MaterialPropertyBlock _mpb_inst;

    public virtual void SpawnSetup(Map map, bool respawningAfterLoad)
    {
      base.SpawnSetup(map, respawningAfterLoad);
      this._lastUpdateTick = Find.TickManager.TicksGame;
      this._isFadingOut = false;
      this._age = 0.0f;
      this._currentLength = 0.0f;
      this._currentWidth = 0.0f;
      this._currentOverheat = 0.0f;
      this._visualTime = 0.0f;
      if (respawningAfterLoad)
        return;
      if (ExoLaserRenderer.Config.DebugForceRainbow)
        this._isRainbowMode = true;
      else if (Rand.Chance(0.01f))
        this._isRainbowMode = true;
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

    public void UpdateData(
      Vector3 start,
      Vector3 dir,
      float length,
      float width,
      float overheat,
      Color cNormal,
      Color cHot)
    {
      this._startPos = start;
      this._direction = dir;
      this._length = length;
      this._width = width;
      this._overheat = overheat;
      this._colNormal = cNormal;
      this._colHot = cHot;
      this._lastUpdateTick = Find.TickManager.TicksGame;
      this._isFadingOut = false;
      Vector3 vector3 = (start + (dir * length * 0.5f));
      if (!GenGrid.InBounds(IntVec3Utility.ToIntVec3(vector3), this.Map))
        return;
      this.Position = IntVec3Utility.ToIntVec3(vector3);
    }

    protected virtual void Tick()
    {
      ++this._age;
      if (Find.TickManager.TicksGame > this._lastUpdateTick + 2)
        this._isFadingOut = true;
      float num1 = this._width;
      float num2 = 0.2f;
      if (this._isFadingOut)
      {
        num1 = 0.0f;
        num2 = 0.25f;
      }
      else
      {
        if ((double) this._currentLength < (double) this._length)
        {
          this._currentLength = Mathf.Lerp(this._currentLength, this._length + 2f, 0.5f);
          if ((double) this._currentLength > (double) this._length)
            this._currentLength = this._length;
        }
        if ((double) this._age < 6.0)
        {
          num1 *= 2f;
          num2 = 0.5f;
        }
      }
      this._currentWidth = Mathf.Lerp(this._currentWidth, num1, num2);
      this._currentOverheat = Mathf.Lerp(this._currentOverheat, this._overheat, 0.15f);
      if (!this._isFadingOut || (double) this._currentWidth >= 0.05000000074505806)
        return;
      base.Destroy((DestroyMode) 0);
    }

    private static MaterialPropertyBlock _mpb
    {
      get => ExoDeathRay._mpb_inst ?? (ExoDeathRay._mpb_inst = new MaterialPropertyBlock());
    }

    protected virtual void DrawAt(Vector3 drawLoc, bool flip = false)
    {
      if ((double) this._currentWidth <= 0.05000000074505806)
        return;
      if (this._viewMesh == null)
      {
        this._viewMesh = new Mesh();
        this._viewMesh.MarkDynamic();
        ((Object) this._viewMesh).name = "ExoLaserMesh_" + this.thingIDNumber.ToString();
      }
      this._visualTime += Time.unscaledDeltaTime;
      Vector3 vector3 = (this._startPos + (this._direction * this._currentLength));
      ExoLaserRenderer.RenderLaser(this._viewMesh, this._startPos, vector3, this._currentWidth, this._currentOverheat, this._visualTime, this._isRainbowMode, this._colNormal, this._colHot);
      float baseSize1 = this._currentWidth * 2f;
      if ((double) this._age < 5.0 && !this._isFadingOut)
        baseSize1 *= 1.5f;
      this.DrawLayeredFlare(this._startPos, baseSize1, this._currentOverheat, true);
      if (ExoLaserRenderer.Config.UseCometShape || (double) this._currentLength <= (double) this._length * 0.89999997615814209)
        return;
      float baseSize2 = this._currentWidth * 2.5f;
      this.DrawLayeredFlare(vector3, baseSize2, this._currentOverheat, false);
    }

    private void DrawLayeredFlare(Vector3 pos, float baseSize, float overheat, bool isMuzzle)
    {
      Color color1 = !this._isRainbowMode ? Color.Lerp(this._colNormal, this._colHot, overheat) : Color.HSVToRGB(Mathf.Repeat(this._visualTime * 1.5f, 1f), 0.7f, 1f);
      Quaternion quaternion1 = Quaternion.AngleAxis(this._visualTime * 100f, Vector3.up);
      float num1 = baseSize * 1.8f;
      Color color2 = color1;
      color2.a = 0.4f;
      ExoDeathRay._mpb.Clear();
      ExoDeathRay._mpb.SetColor(ShaderPropertyIDs.Color, color2);
      Matrix4x4 matrix4x4_1 = Matrix4x4.TRS((pos + (Vector3.up * 0.1f)), quaternion1, new Vector3(num1, 1f, num1));
      Graphics.DrawMesh(MeshPool.plane10, matrix4x4_1, ExoLaserAssets.Mat_Flare, 0, (Camera) null, 0, ExoDeathRay._mpb);
      float num2 = baseSize * 0.8f;
      Color color3 = Color.Lerp(color1, Color.white, 0.7f);
      color3.a = 1f;
      ExoDeathRay._mpb.Clear();
      ExoDeathRay._mpb.SetColor(ShaderPropertyIDs.Color, color3);
      Quaternion quaternion2 = Quaternion.AngleAxis((float) (-(double) this._visualTime * 200.0), Vector3.up);
      Matrix4x4 matrix4x4_2 = Matrix4x4.TRS((pos + (Vector3.up * 0.12f)), quaternion2, new Vector3(num2, 1f, num2));
      Graphics.DrawMesh(MeshPool.plane10, matrix4x4_2, ExoLaserAssets.Mat_Flare, 0, (Camera) null, 0, ExoDeathRay._mpb);
    }
  }
}
