using RimWorld;
using Verse;

namespace NCL
{
    public class Verb_DiverSentryBurst : Verb_Shoot
    {
        public override bool Available()
        {
            if (!HasSentryAmmo())
            {
                return false;
            }

            return base.Available();
        }

        protected override bool TryCastShot()
        {
            if (caster is Building_DiverSentryTurret sentry)
            {
                if (!sentry.TryRefreshBurstTarget(out LocalTargetInfo target))
                {
                    return false;
                }

                currentTarget = target;
            }

            if (!HasSentryAmmo())
            {
                return false;
            }

            if (!base.TryCastShot())
            {
                return false;
            }

            Comp_DiverSentryAmmunition ammoComp = GetSentryAmmoComp();
            ammoComp?.Notify_ShotFired();

            // Return false when depleted so TryCastNextBurstShot skips muzzle FX on a dying caster.
            return ammoComp == null || ammoComp.HasAmmo;
        }

        private bool HasSentryAmmo()
        {
            Comp_DiverSentryAmmunition comp = GetSentryAmmoComp();
            return comp == null || comp.HasAmmo;
        }

        private Comp_DiverSentryAmmunition GetSentryAmmoComp()
        {
            return caster?.TryGetComp<Comp_DiverSentryAmmunition>();
        }
    }

    public class Verb_DiverSentryMachineGun : Verb_DiverSentryBurst
    {
    }

    public class Verb_DiverSentryGatling : Verb_DiverSentryBurst
    {
    }
}
