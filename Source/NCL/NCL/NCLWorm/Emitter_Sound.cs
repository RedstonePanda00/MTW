// Decompiled with JetBrains decompiler
// Type: NCLWorm.Emitter_Sound
// Assembly: NCL_WormBoss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B627DDDD-3AC9-4D6A-89F6-EA72F95FB570
// Assembly location: C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3555799437\1.6\Assemblies\NCL_WormBoss (3).dll

using Verse;
using Verse.Sound;

#nullable disable
namespace NCLWorm
{
  public class Emitter_Sound : WormVFXEmitter
  {
    public SoundDef sound;
    public FloatRange pitchRange = new FloatRange(0.9f, 1.1f);

    protected override bool IsEdgeTrigger => true;

    protected override void TryEmit(WormBody body, VFXRuntimeState state)
    {
      if (this.sound == null)
        return;
      SoundInfo soundInfo = SoundInfo.InMap(new TargetInfo(((Thing) body).Position, ((Thing) body).Map, false), (MaintenanceType) 0);
      soundInfo.pitchFactor = this.pitchRange.RandomInRange;
      SoundStarter.PlayOneShot(this.sound, soundInfo);
    }
  }
}
