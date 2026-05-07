using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using UnityEngine;
using Verse;

namespace NCL
{

        public class HediffComp_SolarDisruption : HediffComp
        {
            public HediffCompProperties_SolarDisruption Props =>
                (HediffCompProperties_SolarDisruption)props;

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            // 空值检查
            if (parent?.pawn?.Map == null || !parent.pawn.Spawned) return;

            // 获取局部光照强度
            float glow = GetLocalGlow(parent.pawn.Map, parent.pawn.Position);

            // 判断是否为低光照（光照强度<0.5）
            if (glow < Props.sunlightThreshold)  // 修改了条件判断
            {
                if (!parent.pawn.health.hediffSet.HasHediff(Props.solarDisruptionHediff))
                {
                    parent.pawn.health.AddHediff(Props.solarDisruptionHediff);
                }
            }
            else
            {
                Hediff existingHediff = parent.pawn.health.hediffSet.GetFirstHediffOfDef(Props.solarDisruptionHediff);
                existingHediff?.pawn.health.RemoveHediff(existingHediff);
            }
        }

        // 获取局部光照强度
        private float GetLocalGlow(Map map, IntVec3 position)
            {
                // 检查位置是否在地图范围内
                if (!position.InBounds(map)) return 0f;

                // 使用新版API获取光照值
                return map.glowGrid.GroundGlowAt(position);
            }
        }

        public class HediffCompProperties_SolarDisruption : HediffCompProperties
        {
            public float sunlightThreshold = 0.5f; // 光照强度阈值（0.0~1.0）
            public HediffDef solarDisruptionHediff; // 关联的Debuff Hediff

            public HediffCompProperties_SolarDisruption()
            {
                compClass = typeof(HediffComp_SolarDisruption);
            }
        }
    }
