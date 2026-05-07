using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Collections.Specialized.BitVector32;
using RimWorld;
using NCL;
using Verse.Sound;
using Verse;
using UnityEngine;

namespace NCL
{
    public class CompProperties_ThunderStrike : CompProperties_AbilityEffect
    {
        public int lightningCount = 3;          // 闪电数量
        public float radius = 3f;               // 作用半径
        public int damageAmount = 40;           // 单次伤害
        public DamageDef damageType;            // 伤害类型
        public ThingDef postEffectFilth;        // 爆炸后残留物

        public CompProperties_ThunderStrike()
        {
            compClass = typeof(CompAbilityEffect_ThunderStrike);
        }
    }

    // 组件逻辑实现
    public class CompAbilityEffect_ThunderStrike : CompAbilityEffect
    {
        public new CompProperties_ThunderStrike Props =>
            (CompProperties_ThunderStrike)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            for (int i = 0; i < Props.lightningCount; i++)
            {
                DoSingleStrike(target.Cell, parent.pawn.Map);
            }
        }

        private void DoSingleStrike(IntVec3 center, Map map)
        {
            // 随机选取目标位置
            IntVec3 strikePos = center + GenRadial.RadialPattern[Rand.Range(8, GenRadial.NumCellsInRadius(10))];


            // 伤害计算
            GenExplosion.DoExplosion(
                strikePos,
                map,
                Props.radius * 0.5f,
                Props.damageType,
                parent.pawn,
                Props.damageAmount
            );

            // 音效
            SoundDefOf.Thunder_OnMap.PlayOneShot(new TargetInfo(strikePos, map));

            FleckMaker.ThrowMicroSparks(strikePos.ToVector3Shifted(), map);
            FleckMaker.ThrowLightningGlow(strikePos.ToVector3Shifted(), map, 10f); // 第三个参数为缩放比例

            // 地面冲击波
            for (int i = 0; i < 3; i++)
            {
                IntVec3 offset = new IntVec3(Rand.Range(-3, 3), 0, Rand.Range(-3, 3));
                FleckMaker.ThrowSmoke(
                    (strikePos + offset).ToVector3Shifted(),
                    map,
                    Rand.Range(3f, 3f) // 烟雾半径
                );
            }

            // 电弧扩散（正确调用）
            for (int i = 0; i < 8; i++)
            {
                IntVec3 offset = new IntVec3(Rand.Range(-5, 5), 0, Rand.Range(-5, 5));
                FleckMaker.ThrowMicroSparks(
                    (strikePos + offset).ToVector3Shifted(),
                    map
                );
            }

            // 音效和伤害逻辑保持不变...
        }

    }
    }
