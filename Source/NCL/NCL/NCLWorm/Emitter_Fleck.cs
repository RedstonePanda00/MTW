// Decompiled with JetBrains decompiler
// Type: NCLWorm.Emitter_Fleck
// Assembly: NCL_WormBoss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B627DDDD-3AC9-4D6A-89F6-EA72F95FB570
// Assembly location: C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3555799437\1.6\Assemblies\NCL_WormBoss (3).dll

using RimWorld;
using UnityEngine;
using Verse;

#nullable disable
namespace NCLWorm
{
  public class Emitter_Fleck : WormVFXEmitter
  {
    public FleckDef fleckDef;
    public float scaleMin = 1f;
    public float scaleMax = 1.5f;
    public float angle = 0.0f;
    public float velocitySpeed = 0.0f;
    public Color? color = new Color?();

    public override WormVFXEmitter CreateMirroredCopy()
    {
      Emitter_Fleck mirroredCopy = (Emitter_Fleck) base.CreateMirroredCopy();
      mirroredCopy.angle = -mirroredCopy.angle;
      return (WormVFXEmitter) mirroredCopy;
    }

    protected override void TryEmit(WormBody body, VFXRuntimeState state)
    {
      if (this.fleckDef == null)
        return;
      Vector3 emitPos = this.GetEmitPos(body);
      Vector3 vector3_1 = Vector3.zero;
      if ((double) this.velocitySpeed > 1.0 / 1000.0)
      {
        Vector3 vector3_2 = (Quaternion.Euler(0.0f, this.angle, 0.0f) * Vector3.forward);
        vector3_1 = ((Quaternion.LookRotation(body.BodyFacing) * vector3_2) * this.velocitySpeed);
      }
      float num = Rand.Range(this.scaleMin, this.scaleMax);
      FleckCreationData dataStatic = FleckMaker.GetDataStatic(emitPos, ((Thing) body).Map, this.fleckDef, num);
      dataStatic.rotation = Rand.Range(0.0f, 360f);
      dataStatic.velocity = new Vector3?(vector3_1);
      if (this.color.HasValue)
        dataStatic.instanceColor = new Color?(this.color.Value);
      ((Thing) body).Map.flecks.CreateFleck(dataStatic);
    }
  }
}
