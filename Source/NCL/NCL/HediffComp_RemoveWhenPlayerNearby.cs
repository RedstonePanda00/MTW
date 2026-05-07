using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace NCL
{
    public class HediffComp_RemoveWhenEnemyNearby : HediffComp
    {
        private const float DefaultCheckRadius = 35f;
        private const int CheckInterval = 60;
        private int ticksUntilNextCheck;

        public HediffCompProperties_RemoveWhenEnemyNearby Props =>
            (HediffCompProperties_RemoveWhenEnemyNearby)props;

        private float CheckRadius => Props.checkRadius ?? DefaultCheckRadius;

        public override void CompPostMake()
        {
            base.CompPostMake();
            ticksUntilNextCheck = CheckInterval;
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            if (ticksUntilNextCheck > 0)
            {
                ticksUntilNextCheck--;
                return;
            }

            ticksUntilNextCheck = CheckInterval;

            if (EnemyPawnNearby())
            {
                parent.pawn.health.RemoveHediff(parent);
            }
        }

        private bool EnemyPawnNearby()
        {
            Map map = parent.pawn.MapHeld;
            if (map == null) return false;

            return GenRadial.RadialDistinctThingsAround(parent.pawn.PositionHeld, map, CheckRadius, true)
                .OfType<Pawn>()
                .Where(p => p != parent.pawn)
                .Any(pawn => parent.pawn.HostileTo(pawn));
        }

        public override string CompDebugString()
        {
            return $"检测半径: {CheckRadius}格\n下次检查: {ticksUntilNextCheck} ticks";
        }
    }

    public class HediffCompProperties_RemoveWhenEnemyNearby : HediffCompProperties
    {
        public float? checkRadius;

        public HediffCompProperties_RemoveWhenEnemyNearby()
        {
            compClass = typeof(HediffComp_RemoveWhenEnemyNearby);
        }
    }
}
