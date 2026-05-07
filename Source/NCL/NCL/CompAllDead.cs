using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace NCL
{

    public class CompProperties_AllDead : CompProperties
    {

        public CompProperties_AllDead()
        {
            this.compClass = typeof(CompAllDead);
        }


        public string toggleLabelKey;


        public string toggleDescKey;


        public string toggleIconPath;


        public string unableKey;


        public ThingDef mechanoidToKill;
    }
}


namespace NCL
{
    public class CompAllDead : ThingComp
    {
        public CompProperties_AllDead Props => this.props as CompProperties_AllDead;
        public Pawn Owner => this.parent as Pawn;
        public bool CanApply => this.Owner != null && !this.pawns.NullOrEmpty();

        public override void CompTick()
        {
            base.CompTick();
            if (this.Owner == null || !this.Owner.Spawned || this.Owner.Map?.mapPawns == null)
                return;

            this.ticks++;
            if (this.ticks > 100)
            {
                this.ticks = 0;
                this.pawns = this.Owner.Map.mapPawns.AllPawnsSpawned
                    .Where(x => x.def == this.Props.mechanoidToKill &&
                                x.Faction != null &&
                                x.Faction == this.Owner.Faction)
                    .ToList();
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }

            if (this.Owner?.Faction == Faction.OfPlayer)
            {
                Command_Action allDead = new Command_Action
                {
                    defaultLabel = this.Props.toggleLabelKey.Translate(),
                    defaultDesc = this.Props.toggleDescKey.Translate(),
                    icon = ContentFinder<Texture2D>.Get(this.Props.toggleIconPath, true),
                    action = delegate () { this.KillAll(); }
                };

                if (!this.CanApply)
                {
                    allDead.Disable(this.Props.unableKey.Translate());
                }
                yield return allDead;
            }
        }

        public void KillAll()
        {
            MoteMaker.ThrowText(this.Owner.DrawPos, this.Owner.Map,
                "NCL.ClearAllSplitterSpiders".Translate(string.Format("{0}", this.Owner.Name)), 5f);

            foreach (Pawn pawn in this.pawns)
            {
                if (pawn != null && !pawn.Destroyed)
                {
                    // 直接销毁单位，不会触发任何爆炸
                    pawn.Destroy();
                }
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref this.ticks, "ticks", 0);
        }

        public int ticks = 0;
        public List<Pawn> pawns;
    }
}
