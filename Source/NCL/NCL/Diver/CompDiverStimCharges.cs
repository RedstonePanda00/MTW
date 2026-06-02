using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace NCL.Diver
{
    public class CompProperties_DiverStimCharges : CompProperties
    {
        public int maxCharges = 4;
        public HediffDef boostHediff;

        public CompProperties_DiverStimCharges()
        {
            compClass = typeof(CompDiverStimCharges);
        }
    }

    public class CompDiverStimCharges : ThingComp
    {
        private int charges = -1;

        private CompProperties_DiverStimCharges Props => (CompProperties_DiverStimCharges)props;

        public Pawn Pawn => parent as Pawn;

        public int Charges => charges;

        public int MaxCharges => Props.maxCharges;

        public static CompDiverStimCharges Get(Pawn pawn) => pawn?.GetComp<CompDiverStimCharges>();

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            EnsureChargesInitialized();
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref charges, "nclDiverStimCharges", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                EnsureChargesInitialized();
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }

            Pawn pawn = Pawn;
            if (pawn == null || pawn.Faction != Faction.OfPlayer || Props.boostHediff == null)
            {
                yield break;
            }

            Command_Action stim = new Command_Action
            {
                icon = ContentFinder<Texture2D>.Get("Diver/HD2Stim"),
                defaultLabel = "MTW_Diver_StimGizmo_Label".Translate(charges, Props.maxCharges),
                defaultDesc = "MTW_Diver_StimGizmo_Desc".Translate(),
                action = TryActivateStim
            };

            if (charges <= 0)
            {
                stim.Disable("MTW_Diver_StimGizmo_NoCharges".Translate());
            }
            else if (pawn.Dead)
            {
                stim.Disable("MTW_Diver_StimGizmo_Dead".Translate());
            }

            yield return stim;
        }

        private void EnsureChargesInitialized()
        {
            if (charges < 0)
            {
                charges = Props.maxCharges;
            }
        }

        private void TryActivateStim()
        {
            Pawn pawn = Pawn;
            if (pawn == null || pawn.Dead || Props.boostHediff == null || charges <= 0)
            {
                return;
            }

            Hediff existing = pawn.health.hediffSet.GetFirstHediffOfDef(Props.boostHediff);
            if (existing != null)
            {
                pawn.health.RemoveHediff(existing);
            }

            pawn.health.AddHediff(Props.boostHediff);
            charges--;
        }
    }
}
