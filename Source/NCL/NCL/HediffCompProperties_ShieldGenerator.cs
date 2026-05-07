using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using Verse;

namespace NCL
{
    public class HediffComp_ShieldGenerator : HediffComp
    {
        public HediffCompProperties_ShieldGenerator Props => (HediffCompProperties_ShieldGenerator)this.props;

        // 修改：允许玩家阵营使用
        public bool CanApply
        {
            get
            {
                Pawn pawn = base.Pawn;
                // 条件：角色存在、已生成、未死亡、未倒地
                return pawn != null && pawn.Spawned && !pawn.Dead && !pawn.Downed;
            }
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            Pawn pawn = base.Pawn;

            if (this.CanApply)
            {
                List<Thing> projectiles = pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.Projectile);

                if (projectiles.Count > 0)
                {
                    // 优化：遍历所有投射物而不是只检查第一个
                    foreach (Thing thing in projectiles)
                    {
                        Projectile projectile = thing as Projectile;
                        if (projectile != null && projectile.Position.DistanceTo(pawn.Position) <= this.Props.range)
                        {
                            // 检查敌对关系
                            if (IsHostileProjectile(projectile, pawn))
                            {
                                this.GenerateShield(pawn.Position, pawn.Map, pawn.Faction, projectile.def.projectile.flyOverhead);
                                pawn.health.RemoveHediff(this.parent);
                                return; // 生成护盾后立即退出
                            }
                        }
                    }
                }
            }
        }

        // 新增：检查投射物是否敌对
        private bool IsHostileProjectile(Projectile projectile, Pawn owner)
        {
            // 无发射者或无效阵营的投射物视为敌对
            if (projectile.Launcher == null || projectile.Launcher.Faction == null)
                return true;

            // 玩家角色时，所有非友方投射物都视为敌对
            if (owner.Faction?.IsPlayer == true)
            {
                return !projectile.Launcher.Faction.HostileTo(owner.Faction);
            }

            // 非玩家角色时，只拦截敌对阵营的投射物
            return projectile.Launcher.Faction.HostileTo(owner.Faction);
        }

        public void GenerateShield(IntVec3 pos, Map map, Faction f, bool flyOverhead)
        {
            if (pos.IsValid && pos.InBounds(map))
            {
                ThingDef shieldDef = flyOverhead ?
                    NCLDefOf.NCL_FullAngelShieldProjector :
                    NCLDefOf.NCL_LowAngelShieldProjector;

                Thing shield = ThingMaker.MakeThing(shieldDef, null);
                shield.SetFaction(f, null);
                GenPlace.TryPlaceThing(shield, pos, map, ThingPlaceMode.Near);

                SpawnEffect(shield);
            }
        }

        private static void SpawnEffect(Thing projector)
        {
            FleckMaker.Static(projector.TrueCenter(), projector.Map, FleckDefOf.BroadshieldActivation, 1f);
        }
    }
}


namespace NCL
{
    // Token: 0x020001AD RID: 429
    [DefOf]
    public static class NCLDefOf
    {
        // Token: 0x0400042A RID: 1066
        public static ThingDef NCL_FullAngelShieldProjector;

        // Token: 0x0400042B RID: 1067
        public static ThingDef NCL_LowAngelShieldProjector;
        public static ThingDef CraterSmall;
        public static ThingDef CraterMedium;
        public static ThingDef CraterLarge;
        public static WorldObjectDef NCL_WorldObject_SkyfallerArtillery;
        public static ThingDef NCL_Eagle_Artillery_Shell_Up; // 替换为实际的 Skyfaller 定义名称
        public static ThingDef NCL_Eagle_Artillery_Shell_Down; // 替换为实际的 Skyfaller 定义名称
        public static EffecterDef NCL_ShellFortFightingeffects; // 替换为实际的 Skyfaller 定义名称
        public static SoundDef NCL_Artillery_Landed;
        public static SoundDef NCL_Artillery_Firing;
        public static DamageDef TW_Antiparticle_explosion;
        public static ThingDef Mote_HellsphereCannon_Target;
    }
}

namespace NCL
{
    // Token: 0x020001C9 RID: 457
    public class HediffCompProperties_ShieldGenerator : HediffCompProperties
    {
        // Token: 0x0600080B RID: 2059 RVA: 0x0003AA10 File Offset: 0x00038C10
        public HediffCompProperties_ShieldGenerator()
        {
            this.compClass = typeof(HediffComp_ShieldGenerator);
        }

        // Token: 0x040004CE RID: 1230
        public float range;
    }
}
