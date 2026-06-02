using UnityEngine;
using Verse;

namespace NCL
{
    public class Gizmo_DiverSentryAmmunition : Gizmo_Slider
    {
        private readonly Comp_DiverSentryAmmunition ammoComp;

        protected override float Target
        {
            get => ammoComp.AmmoPercent;
            set { }
        }

        protected override float ValuePercent => ammoComp.AmmoPercent;

        protected override string Title => ammoComp.Props.ammoGizmoLabel;

        protected override bool IsDraggable => false;

        protected override string BarLabel =>
            ammoComp.AmmoRemaining + " / " + ammoComp.Props.ammoCapacity;

        protected override bool DraggingBar
        {
            get => false;
            set { }
        }

        public Gizmo_DiverSentryAmmunition(Comp_DiverSentryAmmunition ammoComp)
        {
            this.ammoComp = ammoComp;
        }

        protected override string GetTooltip() => string.Empty;
    }
}
