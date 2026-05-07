using System;
using System.IO;
using UnityEngine;
using Verse;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace NCL
{

    public class Verb_ArcSprayProjectile : Verb_ArcSpray
    {

        protected override void HitCell(IntVec3 cell)
        {
            base.HitCell(cell);
            ((Projectile)GenSpawn.Spawn(this.verbProps.defaultProjectile, this.caster.Position, this.caster.Map, WipeMode.Vanish)).Launch(this.caster, this.caster.DrawPos, cell, cell, ProjectileHitFlags.All, false, null, null);
        }
    }
}

