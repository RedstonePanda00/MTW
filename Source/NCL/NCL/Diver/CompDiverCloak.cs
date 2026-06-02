using RimWorld;
using UnityEngine;
using Verse;

namespace NCL.Diver
{
    public class CompDiverCloak : ThingComp
    {
        private string cloakSetId = DiverCloakCatalog.DefaultSetId;
        private Color cloakColor = Color.white;
        private bool cloakVisible = true;

        private Color? previewCloakColor;
        private string previewCloakSetId;
        private bool? previewCloakVisible;

        public Pawn Pawn => parent as Pawn;

        public string CloakSetId => previewCloakSetId ?? cloakSetId;

        public Color CloakColor => previewCloakColor ?? cloakColor;

        public bool CloakVisible => previewCloakVisible ?? cloakVisible;

        public DiverCloakSetEntry CurrentSet => DiverCloakCatalog.Get(CloakSetId);

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref cloakSetId, "nclDiverCloakSetId", DiverCloakCatalog.DefaultSetId);
            Scribe_Values.Look(ref cloakColor, "nclDiverCloakColor", Color.white);
            Scribe_Values.Look(ref cloakVisible, "nclDiverCloakVisible", true);
        }

        public static CompDiverCloak Get(Pawn pawn)
        {
            return pawn?.GetComp<CompDiverCloak>();
        }

        public void SetCloakSet(string setId, bool markDirty = true)
        {
            cloakSetId = DiverCloakCatalog.NormalizeSetId(setId);
            if (markDirty)
            {
                NotifyCloakChanged();
            }
        }

        public void SetCloakColor(Color color, bool markDirty = true)
        {
            cloakColor = color;
            if (markDirty)
            {
                NotifyCloakChanged();
            }
        }

        public void SetCloakVisible(bool visible, bool markDirty = true)
        {
            cloakVisible = visible;
            if (markDirty)
            {
                NotifyCloakChanged();
            }
        }

        public void BeginPreview(string setId, Color color, bool visible)
        {
            previewCloakSetId = DiverCloakCatalog.NormalizeSetId(setId);
            previewCloakColor = color;
            previewCloakVisible = visible;
            NotifyCloakChanged();
            PortraitsCache.SetDirty(Pawn);
        }

        public void EndPreview(bool apply)
        {
            if (apply)
            {
                if (previewCloakSetId != null)
                {
                    SetCloakSet(previewCloakSetId, markDirty: false);
                }

                if (previewCloakColor.HasValue)
                {
                    SetCloakColor(previewCloakColor.Value, markDirty: false);
                }

                if (previewCloakVisible.HasValue)
                {
                    SetCloakVisible(previewCloakVisible.Value, markDirty: false);
                }
            }

            previewCloakSetId = null;
            previewCloakColor = null;
            previewCloakVisible = null;
            NotifyCloakChanged();
            PortraitsCache.SetDirty(Pawn);
        }

        public void NotifyCloakChanged()
        {
            Pawn pawn = Pawn;
            if (pawn == null)
            {
                return;
            }

            pawn.Drawer?.renderer?.renderTree?.SetDirty();
            PortraitsCache.SetDirty(pawn);
            GlobalTextureAtlasManager.TryMarkPawnFrameSetDirty(pawn);
        }

    }
}
