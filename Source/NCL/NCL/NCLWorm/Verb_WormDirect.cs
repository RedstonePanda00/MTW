// Decompiled with JetBrains decompiler
// Type: NCLWorm.Verb_WormDirect
// Assembly: NCL_WormBoss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B627DDDD-3AC9-4D6A-89F6-EA72F95FB570
// Assembly location: C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3555799437\1.6\Assemblies\NCL_WormBoss (3).dll

using RimWorld;
using UnityEngine;
using Verse;

#nullable disable
namespace NCLWorm
{
  public class Verb_WormDirect : Verb
  {
    protected override bool TryCastShot()
    {
      if (this.caster == null || !this.caster.Spawned || this.caster.Map == null || this.currentTarget.HasThing && this.currentTarget.Thing.Map != this.caster.Map)
        return false;
      ThingDef defaultProjectile = this.verbProps.defaultProjectile;
      if (defaultProjectile == null)
        return false;
      Vector3 drawPos = this.caster.DrawPos;
      IntVec3 intVec3 = IntVec3Utility.ToIntVec3(drawPos);
      if (!GenGrid.InBounds(intVec3, this.caster.Map))
        return false;
      Vector3 centerVector3 = this.currentTarget.CenterVector3;
      Projectile projectile = (Projectile) GenSpawn.Spawn(defaultProjectile, intVec3, this.caster.Map, (WipeMode) 0);
      if (projectile == null)
        return false;
      projectile.Launch(this.caster, drawPos, this.currentTarget, this.currentTarget, ProjectileHitFlags.All, false, this.EquipmentSource, null);
      this.EquipmentSource?.GetComp<CompChangeableProjectile>()?.Notify_ProjectileLaunched();
      return true;
    }

    protected virtual int ShotsPerBurst => this.verbProps.burstShotCount;

    public virtual float HighlightFieldRadiusAroundTarget(out bool needLOSToCenter)
    {
      needLOSToCenter = true;
      return 0.0f;
    }
  }
}
