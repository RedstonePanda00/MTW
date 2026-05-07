using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using Verse;
using System.IO;

namespace NCL
{
    // Token: 0x020000A5 RID: 165
    public class Verb_AbilityTeleport : Verb_CastAbility
    {
        // Token: 0x060002B9 RID: 697 RVA: 0x00010D08 File Offset: 0x0000EF08
        protected override bool TryCastShot()
        {
            IntVec3 cell = this.currentTarget.Cell;
            bool flag = base.TryCastShot();
            bool result;
            if (flag)
            {
                Map map = this.caster.Map;
                bool flag2 = false;
                bool casterIsPawn = this.CasterIsPawn;
                if (casterIsPawn)
                {
                    bool flag3 = this.CasterPawn.drafter != null;
                    if (flag3)
                    {
                        flag2 = this.CasterPawn.drafter.Drafted;
                    }
                    this.CasterPawn.teleporting = true;
                }
                this.caster.DeSpawn(DestroyMode.Vanish);
                GenSpawn.Spawn(this.caster, cell, map, WipeMode.Vanish);
                bool casterIsPawn2 = this.CasterIsPawn;
                if (casterIsPawn2)
                {
                    this.CasterPawn.Notify_Teleported(true, true);
                    this.CasterPawn.teleporting = false;
                    bool flag4 = flag2;
                    if (flag4)
                    {
                        this.CasterPawn.drafter.Drafted = true;
                    }
                }
                result = true;
            }
            else
            {
                result = false;
            }
            return result;
        }
    }
}
