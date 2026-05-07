using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace NCL
{
    // 组件属性（可在XML中配置）
    public class CompProperties_SmokepopOnHit : CompProperties
    {
        public float cooldownSeconds = 10f;      // 冷却时间（秒）
        public float smokeRadius = 3f;           // 烟雾覆盖半径
        public int smokeDurationTicks = 600;     // 烟雾持续时间（ticks，默认10秒）
        public SoundDef soundOnActivate;         // 激活时播放的声音

        public CompProperties_SmokepopOnHit()
        {
            compClass = typeof(CompSmokepopOnHit);
        }
    }

    // 组件实现
    public class CompSmokepopOnHit : ThingComp
    {
        private CompProperties_SmokepopOnHit Props => (CompProperties_SmokepopOnHit)props;
        private int lastTriggerTick = -1; // 上次触发时间
        private bool enabled = true; // 开关状态（默认开启）

        // 检查是否在冷却中
        private bool OnCooldown
        {
            get
            {
                if (lastTriggerTick < 0) return false;
                int cooldownTicks = (int)(Props.cooldownSeconds * 60);
                return Find.TickManager.TicksGame < lastTriggerTick + cooldownTicks;
            }
        }

        // 添加Gizmo开关
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }

            // 仅对玩家殖民者显示Gizmo
            Pawn pawn = parent as Pawn;
            if (pawn != null && pawn.Faction == Faction.OfPlayer && !pawn.Dead)
            {
                yield return new Command_Toggle
                {
                    defaultLabel = "NCL.SmokePopToggle".Translate(),
                    defaultDesc = "NCL.SmokePopToggleDesc".Translate(),
                    icon = ContentFinder<Texture2D>.Get("ModIcon/SmokePopToggle"),
                    isActive = () => enabled,
                    toggleAction = () => enabled = !enabled
                };
            }
        }

        // 当Pawn受到伤害时调用
        public override void PostPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.PostPostApplyDamage(dinfo, totalDamageDealt);

            // 检查是否有效伤害、是否在冷却中、是否已死亡以及是否开启功能
            if (!enabled || dinfo.Amount <= 0 || OnCooldown || parent.Destroyed || parent.Map == null)
                return;

            // 触发烟雾弹效果
            TriggerSmokepopEffect();
        }

        private void TriggerSmokepopEffect()
        {
            // 更新最后触发时间
            lastTriggerTick = Find.TickManager.TicksGame;

            // 播放激活声音
            if (Props.soundOnActivate != null)
            {
                Props.soundOnActivate.PlayOneShot(new TargetInfo(parent.Position, parent.Map));
            }

            // 使用与原版烟雾弹完全相同的爆炸调用
            GenExplosion.DoExplosion(
                center: parent.Position,
                map: parent.Map,
                radius: Props.smokeRadius,
                damType: DamageDefOf.Smoke,
                instigator: null,
                damAmount: -1,
                armorPenetration: -1f,
                explosionSound: null,
                weapon: null,
                projectile: null,
                intendedTarget: null,
                postExplosionSpawnThingDef: null,
                postExplosionSpawnChance: 0f,
                postExplosionSpawnThingCount: 1,
                applyDamageToExplosionCellsNeighbors: false,
                preExplosionSpawnThingDef: null,
                preExplosionSpawnChance: 0f,
                preExplosionSpawnThingCount: 1,
                chanceToStartFire: 0f,
                damageFalloff: false,
                direction: null,
                ignoredThings: null,
                postExplosionGasType: GasType.BlindSmoke
            );
        }

        // 调试信息显示
        public override string CompInspectStringExtra()
        {
            if (!enabled) return "NCL.SmokePopDisabled".Translate();

            int ticksRemaining = lastTriggerTick + (int)(Props.cooldownSeconds * 60) - Find.TickManager.TicksGame;
            if (ticksRemaining > 0)
            {
                return "NCL.SmokePopCoolingDown".Translate(ticksRemaining / 60f);
            }
            return "NCL.SmokePopReady".Translate();
        }

        // 保存/加载游戏状态
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref lastTriggerTick, "lastTriggerTick", -1);
            Scribe_Values.Look(ref enabled, "smokePopEnabled", true); // 保存开关状态
        }
    }
}
