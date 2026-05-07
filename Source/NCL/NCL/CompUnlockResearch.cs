using NCL;
using RimWorld;
using System;
using UnityEngine;
using Verse;
using Verse.Noise;

namespace NCL
{
    public class CompProperties_UnlockResearch : CompProperties
    {
        // 原有属性保持不变...
        public ResearchProjectDef researchToUnlock;
        public float activateRange = 3f;
        public bool showActivationEffect = true;
        public int checkIntervalTicks = 60;
        public bool requireLineOfSight = true;
        public bool onlyAwakeColonists = true;

        // 新增的信件属性（保持简单）
        public LetterDef letterDef;
        public string letterLabel;
        public string letterText;
        public bool sendLetter = true;

        public CompProperties_UnlockResearch()
        {
            compClass = typeof(CompUnlockResearch);
        }
    }
}

namespace NCL
{
    public class CompUnlockResearch : ThingComp
    {
        private CompProperties_UnlockResearch Props => (CompProperties_UnlockResearch)props;
        private bool researchUnlocked;
        private int ticksUntilNextCheck;

        public override void Initialize(CompProperties props)
        {
            base.Initialize(props);
            ticksUntilNextCheck = Props.checkIntervalTicks;
        }

        public override void CompTick()
        {
            base.CompTick();

            if (researchUnlocked ||
                !parent.Spawned ||
                Find.ResearchManager == null ||
                Props.researchToUnlock?.IsFinished != false)
            {
                return;
            }

            if (--ticksUntilNextCheck <= 0)
            {
                ticksUntilNextCheck = Props.checkIntervalTicks;
                CheckColonistProximity();
            }
        }

        private void CheckColonistProximity()
        {
            Map map = parent.Map;
            if (map == null) return;

            int cellsToCheck = GenRadial.NumCellsInRadius(Props.activateRange);
            for (int i = 0; i < cellsToCheck; i++)
            {
                IntVec3 cell = parent.Position + GenRadial.RadialPattern[i];
                if (!cell.InBounds(map)) continue;

                foreach (Thing thing in cell.GetThingList(map))
                {
                    Pawn pawn = thing as Pawn;
                    if (IsValidColonist(pawn))
                    {
                        UnlockResearch(pawn);
                        return;
                    }
                }
            }
        }

        private bool IsValidColonist(Pawn pawn)
        {
            return pawn != null &&
                   pawn.IsColonistPlayerControlled &&
                   (!Props.onlyAwakeColonists || pawn.Awake()) &&
                   (!Props.requireLineOfSight || GenSight.LineOfSightToThing(pawn.Position, parent, parent.Map, false, null));
        }

        private void UnlockResearch(Pawn triggerer)
        {
            try
            {
                Find.ResearchManager.FinishProject(Props.researchToUnlock);
                researchUnlocked = true;

                // 新增信件发送（保持最简实现）
                if (Props.sendLetter && Props.letterDef != null)
                {
                    string label = Props.letterLabel.NullOrEmpty()
                        ? "ResearchUnlocked".Translate()
                        : Props.letterLabel;

                    string text = Props.letterText.NullOrEmpty()
                        ? "ResearchUnlockedBy".Translate(Props.researchToUnlock.LabelCap, triggerer.LabelShort)
                        : Props.letterText;

                    Find.LetterStack.ReceiveLetter(
                        label,
                        text,
                        Props.letterDef,
                        new LookTargets(parent),
                        null, null, null, null);
                }

                // 原有效果保持不变
                if (Props.showActivationEffect)
                {
                    FleckMaker.ThrowLightningGlow(parent.DrawPos, parent.Map, 1.5f);
                    Messages.Message(
                        "NCL_ResearchUnlocked".Translate(Props.researchToUnlock.LabelCap),
                        parent,
                        MessageTypeDefOf.PositiveEvent);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to unlock research {Props.researchToUnlock?.defName}: {ex}");
            }
        }

        public override string CompInspectStringExtra()
        {
            return !researchUnlocked && Props.researchToUnlock != null
                ? "NCL_WillUnlockResearch".Translate(Props.researchToUnlock.LabelCap)
                : null;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref researchUnlocked, "researchUnlocked", false);
            Scribe_Values.Look(ref ticksUntilNextCheck, "ticksUntilNextCheck", Props.checkIntervalTicks);
        }
    }
}