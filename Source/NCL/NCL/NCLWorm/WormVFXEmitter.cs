// Decompiled with JetBrains decompiler
// Type: NCLWorm.WormVFXEmitter
// Assembly: NCL_WormBoss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B627DDDD-3AC9-4D6A-89F6-EA72F95FB570
// Assembly location: C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3555799437\1.6\Assemblies\NCL_WormBoss (3).dll

using UnityEngine;
using Verse;

#nullable disable
namespace NCLWorm
{
  public abstract class WormVFXEmitter
  {
    public float minFactor = 0.0f;
    public float maxFactor = 1.01f;
    public bool invertCondition = false;
    public IntRange intervalRange = new IntRange(5, 10);
    public float chancePerTick = 1f;
    public Vector3 offset = Vector3.zero;
    public bool mirror = false;

    public virtual VFXRuntimeState CreateState() => new VFXRuntimeState();

    public void Tick(WormBody body, VFXRuntimeState state, float currentFactor)
    {
      bool flag = (double) currentFactor >= (double) this.minFactor && (double) currentFactor <= (double) this.maxFactor;
      if (this.invertCondition)
        flag = !flag;
      if (this.IsEdgeTrigger)
      {
        if (flag && !state.wasActiveLastTick)
          this.TryEmit(body, state);
      }
      else if (flag)
      {
        if (state.cooldownTicks > 0)
        {
          --state.cooldownTicks;
        }
        else
        {
          if (Rand.Chance(this.chancePerTick))
            this.TryEmit(body, state);
          state.cooldownTicks = this.intervalRange.RandomInRange;
        }
      }
      else
        state.cooldownTicks = 0;
      state.wasActiveLastTick = flag;
    }

    public virtual WormVFXEmitter CreateMirroredCopy()
    {
      WormVFXEmitter mirroredCopy = (WormVFXEmitter) this.MemberwiseClone();
      mirroredCopy.offset.x = -mirroredCopy.offset.x;
      mirroredCopy.mirror = false;
      return mirroredCopy;
    }

    protected virtual bool IsEdgeTrigger => false;

    protected abstract void TryEmit(WormBody body, VFXRuntimeState state);

    protected Vector3 GetEmitPos(WormBody body)
    {
      return (this.offset == Vector3.zero) ? ((Thing) body).DrawPos : (((Thing) body).DrawPos + (Quaternion.LookRotation(body.BodyFacing) * this.offset));
    }
  }
}
