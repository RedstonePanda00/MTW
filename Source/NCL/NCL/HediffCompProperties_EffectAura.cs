using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using Verse;
using UnityEngine;
using Verse.Sound;

namespace NCL
{
    public class HediffComp_EffectAura : HediffComp
    {
        public HediffCompProperties_EffectAura Props =>
            (HediffCompProperties_EffectAura)props;

        private int ticksUntilEffect;

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            if (Pawn.Map == null || !Pawn.Spawned) return;

            if (--ticksUntilEffect <= 0)
            {
                GenerateEffectRing();
                ticksUntilEffect = Props.EffectInterval;
            }
        }

        private void GenerateEffectRing()
        {
            Map map = Pawn.Map;
            Vector3 center = Pawn.DrawPos;

            for (int i = 0; i < Props.EffectsPerBurst; i++)
            {
                // 生成烟雾粒子
                float angle = Rand.Range(0f, 360f);
                float distance = Rand.Range(Props.minDistance, Props.maxDistance);
                Vector3 offset = Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward * distance;
                IntVec3 cell = (center + offset).ToIntVec3();

                if (cell.InBounds(map) && !cell.Fogged(map))
                {
                    // 生成烟雾前播放声音
                    if (Props.soundDef != null)
                    {
                        Props.soundDef.PlayOneShot(
                            new TargetInfo(cell, map) // 在烟雾生成位置播放
                        );
                    }

                    FleckCreationData data = FleckMaker.GetDataStatic(
                        cell.ToVector3Shifted() + new Vector3(0, 0, 1f),
                        map,
                        Props.EffectFleckDef,
                        Rand.Range(1.5f, 2f)
                    );
                    data.rotationRate = Rand.Range(-3f, 3f);
                    map.flecks.CreateFleck(data);
                }
            }
        }
    }

    public class HediffCompProperties_EffectAura : HediffCompProperties
    {
        public FleckDef EffectFleckDef;
        public SoundDef soundDef;  // 新增声音字段
        public float minDistance = 5f;
        public float maxDistance = 6f;
        public int EffectInterval = 120;
        public int EffectsPerBurst = 3;

        public HediffCompProperties_EffectAura() =>
            compClass = typeof(HediffComp_EffectAura);
    }
}