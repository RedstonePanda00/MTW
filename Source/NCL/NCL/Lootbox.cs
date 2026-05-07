using RimWorld;
using System;
using System.Collections.Generic;
using Verse;

namespace NCL
{
    public class Lootbox : Building_Casket
    {
        public ModExtension_Lootbox Extension
        {
            get
            {
                return this.def.GetModExtension<ModExtension_Lootbox>();
            }
        }

        public override Graphic Graphic
        {
            get
            {
                bool hasAnyContents = base.HasAnyContents;
                Graphic graphic;
                if (hasAnyContents)
                {
                    graphic = base.Graphic;
                }
                else
                {
                    bool flag = this.openedGraphic == null;
                    if (flag)
                    {
                        Graphic graphic2 = base.Graphic;
                        bool flag2 = this.Extension.openedGraphicdata != null;
                        if (flag2)
                        {
                            this.openedGraphic = this.Extension.openedGraphicdata.Graphic;
                            return this.openedGraphic;
                        }
                        this.openedGraphic = base.Graphic;
                    }
                    graphic = this.openedGraphic;
                }
                return graphic;
            }
        }


        public void OpenByNPC(Pawn pawn)
        {
            this.innerContainer.TryTransferAllToContainer(pawn.inventory.innerContainer, true);
            this.contentsKnown = true;
        }


        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            bool flag = !this.inti;
            if (flag)
            {
                this.Extension.loots.RandomElement<ThingSetMakerDef>().root.Generate().ForEach(delegate (Thing t)
                {
                    bool spawned = t.Spawned;
                    if (spawned)
                    {
                        t.DeSpawn(DestroyMode.Vanish);
                    }
                    this.innerContainer.TryAddOrTransfer(t, true);
                });
                this.contentsKnown = false;
                this.inti = true;
            }
        }


        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look<bool>(ref this.inti, "inti", false, false);
            Scribe_Values.Look<int>(ref this.tickToOpen, "QE_LootBox_tickToOpen", 0, false);
        }


        public int tickToOpen = 100;


        public bool inti = false;


        public Graphic openedGraphic = null;
    }
}

namespace NCL
{

    public class ModExtension_Lootbox : DefModExtension
    {

        public SoundDef sound;

        public GraphicData openedGraphicdata;

        public List<ThingSetMakerDef> loots = new List<ThingSetMakerDef>();
    }
}
