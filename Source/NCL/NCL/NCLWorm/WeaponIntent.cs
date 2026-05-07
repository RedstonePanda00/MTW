// Decompiled with JetBrains decompiler
// Type: NCLWorm.WeaponIntent
// Assembly: NCL_WormBoss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B627DDDD-3AC9-4D6A-89F6-EA72F95FB570
// Assembly location: C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3555799437\1.6\Assemblies\NCL_WormBoss (3).dll

using Verse;

#nullable disable
namespace NCLWorm
{
  public struct WeaponIntent
  {
    public FireMode Mode;
    public LocalTargetInfo Target;
    public WormVerbTag Tag;

    public static WeaponIntent Stop
    {
      get
      {
        return new WeaponIntent()
        {
          Mode = FireMode.CeaseFire
        };
      }
    }

    public static WeaponIntent UseVerb(WormVerbTag tag, LocalTargetInfo target)
    {
      return new WeaponIntent()
      {
        Mode = FireMode.FireAtTarget,
        Tag = tag,
        Target = target
      };
    }
  }
}
