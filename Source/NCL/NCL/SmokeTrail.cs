using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using Verse;

namespace NCL
{
    public class Comp_SmokeTrail : ThingComp
    {
        private int ticksCounter;
        public CompProperties_SmokeTrail Props => (CompProperties_SmokeTrail)props;

        public override void CompTick()
        {
            base.CompTick();

            ticksCounter++;

            // 每10 ticks生成烟雾
            if (ticksCounter >= Props.intervalTicks)
            {
                ticksCounter = 0;

                // 确保子弹在有效地图上
                if (parent.Map != null && parent.Position.IsValid)
                {
                    // 生成烟雾粒子
                    FleckMaker.ThrowSmoke(parent.DrawPos, parent.Map, Props.smokeSize);

                }
            }
        }

        // 存档兼容
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref ticksCounter, "ticksCounter", 0);
        }
    }

    public class CompProperties_SmokeTrail : CompProperties
    {
        public int intervalTicks = 10;      // 烟雾生成间隔
        public float smokeSize = 1f;        // 烟雾大小
        public bool playSound = false;      // 是否播放音效
        public SoundDef soundDef;           // 自定义音效

        public CompProperties_SmokeTrail()
        {
            compClass = typeof(Comp_SmokeTrail);
        }
    }
}