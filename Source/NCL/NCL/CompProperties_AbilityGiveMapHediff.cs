using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace NCL
{
    public class CompProperties_AbilityGiveMapHediff : CompProperties_AbilityGiveHediff
    {
        public override IEnumerable<string> ConfigErrors(AbilityDef parentDef)
        {
            foreach (string configStr in base.ConfigErrors(parentDef))
            {
                yield return configStr;
            }

            if (this.ignorePawnsInSameFaction && this.onlyPawnsInSameFaction)
            {
                yield return "ignorePawnsInSameFaction and onlyPawnsInSameFaction are both TRUE, causing ability to have no effect.";
            }
        }

        public CompProperties_AbilityGiveMapHediff()
        {
            this.compClass = typeof(CompAbilityEffect_GiveMapHediff);
        }

        public bool ignorePawnsInSameFaction;
        public bool onlyPawnsInSameFaction;
        public List<PawnKindDef> inavailablePawnKinds; // 改为黑名单
    }

    public class CompAbilityEffect_GiveMapHediff : CompAbilityEffect_WithDuration
    {
        public new CompProperties_AbilityGiveMapHediff Props => (CompProperties_AbilityGiveMapHediff)this.props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn pawn = this.parent.pawn;
            Map map = pawn.Map;

            if (map == null) return;

            Faction casterFaction = pawn.Faction;
            IEnumerable<Pawn> enumerable;

            if (casterFaction != null)
            {
                if (this.Props.onlyPawnsInSameFaction)
                {
                    enumerable = map.mapPawns.SpawnedPawnsInFaction(casterFaction).Where(ValidPawn);
                }
                else if (this.Props.ignorePawnsInSameFaction)
                {
                    enumerable = from p in map.mapPawns.AllPawnsSpawned
                                 where p.Faction != casterFaction && ValidPawn(p)
                                 select p;
                }
                else
                {
                    enumerable = map.mapPawns.AllPawnsSpawned.Where(ValidPawn);
                }
            }
            else
            {
                if (this.Props.onlyPawnsInSameFaction) return;
                enumerable = map.mapPawns.AllPawnsSpawned.Where(ValidPawn);
            }

            if (enumerable.Any())
            {
                foreach (Pawn target2 in enumerable)
                {
                    this.ApplyInner(target2, pawn);
                }
            }
        }

        protected void ApplyInner(Pawn target, Pawn other)
        {
            if (this.Props.replaceExisting)
            {
                Hediff existingHediff = target.health.hediffSet.GetFirstHediffOfDef(this.Props.hediffDef, false);
                if (existingHediff != null)
                {
                    target.health.RemoveHediff(existingHediff);
                }
            }

            Hediff hediff = HediffMaker.MakeHediff(this.Props.hediffDef, target, this.Props.onlyBrain ? target.health.hediffSet.GetBrain() : null);

            if (hediff.TryGetComp<HediffComp_Disappears>() is HediffComp_Disappears disappearsComp)
            {
                disappearsComp.ticksToDisappear = base.GetDurationSeconds(target).SecondsToTicks();
            }

            if (this.Props.severity >= 0f)
            {
                hediff.Severity = this.Props.severity;
            }

            if (hediff.TryGetComp<HediffComp_Link>() is HediffComp_Link linkComp)
            {
                linkComp.other = other;
                linkComp.drawConnection = (target == this.parent.pawn);
            }

            target.health.AddHediff(hediff);
        }

        protected bool ValidPawn(Pawn pawn)
        {
            // 基础无效检查（空、死亡、已销毁）
            if (pawn == null || pawn.Dead || pawn.Destroyed)
                return false;

            // 忽略自身检查
            if (this.Props.ignoreSelf && pawn == this.parent.pawn)
                return false;

            // 仅对机械族有效
            if (!pawn.RaceProps.IsMechanoid)
                return false;

            // 检查是否属于同一阵营（可选）
            if (this.parent.pawn.Faction != null && pawn.Faction != this.parent.pawn.Faction)
                return false;

            // 如果在黑名单中则无效
            if (this.Props.inavailablePawnKinds != null &&
                this.Props.inavailablePawnKinds.Contains(pawn.kindDef))
            {
                return false;
            }

            return true;
        }


        public override bool AICanTargetNow(LocalTargetInfo target)
        {
            return this.parent.def.aiCanUse && target.Pawn != null;
        }
    }
}
