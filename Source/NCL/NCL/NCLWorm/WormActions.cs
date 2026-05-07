// Decompiled with JetBrains decompiler
// Type: NCLWorm.WormActions
// Assembly: NCL_WormBoss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B627DDDD-3AC9-4D6A-89F6-EA72F95FB570
// Assembly location: C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3555799437\1.6\Assemblies\NCL_WormBoss (3).dll

using UnityEngine;
using Verse;

#nullable disable
namespace NCLWorm
{
  public static class WormActions
  {
    public static void SetVentState(WormBody seg, float openFactor) => seg.SetVentState(openFactor);

    public static void GlitchVentState(WormBody seg, float intensity = 1f)
    {
      seg.ServoSpeedMultiplier = (float) (1.0 + 2.0 * (double) intensity);
      if (!Gen.IsHashIntervalTick((Thing) seg, 5))
        return;
      float num = Rand.Value;
      if ((double) num < 0.30000001192092896)
        seg.SetVentState(0.0f);
      else if ((double) num < 0.60000002384185791)
        seg.SetVentState(1f);
      else
        seg.SetVentState(Rand.Range(0.2f, 0.8f));
    }

    public static void OrderFireAtMainTarget(WormBody seg, WormVerbTag tag)
    {
      TC_WormWeaponController weapon = seg.Weapon;
      if (weapon == null || seg.Head == null)
        return;
      CompWormTargeter comp = seg.Head.GetComp<CompWormTargeter>();
      int num;
      if (comp != null)
      {
        LocalTargetInfo currentTargetInfo = comp.CurrentTargetInfo;
        num = currentTargetInfo.IsValid ? 1 : 0;
      }
      else
        num = 0;
      if (num != 0)
        weapon.SetIntent(WeaponIntent.UseVerb(tag, comp.CurrentTargetInfo));
      else
        weapon.SetIntent(WeaponIntent.Stop);
    }

    public static void OrderFireAtSelf(WormBody seg, WormVerbTag tag)
    {
      seg.Weapon?.SetIntent(WeaponIntent.UseVerb(tag, new LocalTargetInfo(((Thing) seg).Position)));
    }

    public static void OrderFireSideSmart(
      WormBody seg,
      WormVerbTag tag,
      LocalTargetInfo target,
      float range = 50f)
    {
      TC_WormWeaponController weapon = seg.Weapon;
      if (weapon == null || !target.IsValid)
        return;
      Vector3 exactPosition = seg.ExactPosition;
      Vector3 vector3_1 = Vector3.Cross(seg.BodyFacing, Vector3.up);
      Vector3 normalized = vector3_1.normalized;
      Vector3 vector3_2 = (target.CenterVector3 - exactPosition);
      float num = Vector3.Dot(vector3_2.normalized, normalized);
      Vector3 vector3_3 = (double) num <= 0.05000000074505806 ? ((double) num >= -0.05000000074505806 ? (Rand.Bool ? normalized : (-normalized)) : (-normalized)) : normalized;
      Vector3 world = (exactPosition + (vector3_3 * range));
      weapon.SetIntent(new WeaponIntent()
      {
        Mode = FireMode.FireAtLocation,
        Tag = tag,
        Target = new LocalTargetInfo(WormUtility.ClampWorldPosToMapCell(((Thing) seg).Map, world))
      });
    }

    public static void OrderCeaseFire(WormBody seg)
    {
      seg.GetComp<TC_WormWeaponController>()?.SetIntent(WeaponIntent.Stop);
    }
  }
}
