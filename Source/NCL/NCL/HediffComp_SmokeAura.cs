using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace NCL
{
    public class HediffComp_SmokeAura : HediffComp
    {
        public HediffCompProperties_SmokeAura Props =>
            (HediffCompProperties_SmokeAura)props;

        private int ticksUntilSmoke;

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            if (Pawn.Map == null || !Pawn.Spawned) return;

            if (--ticksUntilSmoke <= 0)
            {
                GenerateSmokeEffect();
                ticksUntilSmoke = Props.smokeInterval;
            }
        }

        private void GenerateSmokeEffect()
        {
            // 播放声音
            if (Props.soundDef != null)
            {
                Props.soundDef.PlayOneShot(new TargetInfo(Pawn.Position, Pawn.Map));
            }

            // 生成中心烟雾
            FleckMaker.ThrowSmoke(Pawn.DrawPos, Pawn.Map, Props.smokeSize);

            // 生成环形烟雾
            for (int i = 0; i < Props.smokesPerBurst; i++)
            {
                float angle = Rand.Range(180f, 360f);
                float distance = Rand.Range(Props.minDistance, Props.maxDistance);
                Vector3 offset = new Vector3(
                    Mathf.Cos(angle * Mathf.Deg2Rad) * distance,
                    0,
                    Mathf.Sin(angle * Mathf.Deg2Rad) * distance
                );

                IntVec3 cell = (Pawn.Position.ToVector3Shifted() + offset).ToIntVec3();

                if (cell.InBounds(Pawn.Map) && cell.Walkable(Pawn.Map))
                {
                    FleckMaker.ThrowSmoke(
                        cell.ToVector3Shifted() + new Vector3(0, 0, 0.5f),
                        Pawn.Map,
                        Props.smokeSize * 5f
                    );
                }
            }
        }
    }

    public class HediffCompProperties_SmokeAura : HediffCompProperties
    {
        // 烟雾参数
        public float minDistance = 5f;
        public float maxDistance = 6f;
        public float smokeSize = 5f;

        // 声音参数
        public SoundDef soundDef;

        // 生成参数
        public int smokeInterval = 120;  // 120 ticks = 2秒
        public int smokesPerBurst = 3;

        public HediffCompProperties_SmokeAura()
        {
            compClass = typeof(HediffComp_SmokeAura);
        }
    }
}