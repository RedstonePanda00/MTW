using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace NCL
{

    public class CompVehicleWeapon : ThingComp
    {

        public float CurrentAngle
        {
            get
            {
                return this._currentAngle;
            }
        }


        public float TargetAngle
        {
            get
            {
                Stance_Busy stance_Busy = this.pawn.stances.curStance as Stance_Busy;
                if (stance_Busy != null && stance_Busy.focusTarg.IsValid)
                {
                    Vector3 a;
                    if (stance_Busy.focusTarg.HasThing)
                    {
                        a = stance_Busy.focusTarg.Thing.DrawPos;
                    }
                    else
                    {
                        a = stance_Busy.focusTarg.Cell.ToVector3Shifted();
                    }
                    return (a - this.pawn.DrawPos).AngleFlat();
                }
                return this._turretFollowingAngle;
            }
        }


        public CompProperties_VehicleWeapon Props
        {
            get
            {
                return (CompProperties_VehicleWeapon)this.props;
            }
        }


        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            this.pawn = (this.parent as Pawn);
            if (this.pawn == null)
            {
                Log.Error("The CompVehicleWeapon is set on a non-pawn object.");
                return;
            }
            if (this.pawn.equipment.Primary == null && this.Props.defaultWeapon != null)
            {
                Thing thing = ThingMaker.MakeThing(this.Props.defaultWeapon, null);
                this.pawn.equipment.AddEquipment((ThingWithComps)thing);
            }
            if (respawningAfterLoad)
            {
                CompVehicleWeapon.cachedVehicldesPawns.Remove((Pawn)this.parent);
            }
            CompVehicleWeapon.cachedVehicldesPawns.Add((Pawn)this.parent, this);
        }


        public override void PostDeSpawn(Map map, DestroyMode destroyMode = DestroyMode.Vanish)
        {
            base.PostDeSpawn(map, DestroyMode.Vanish);
            CompVehicleWeapon.cachedVehicldesPawns.Remove((Pawn)this.parent);
        }


        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);
            CompVehicleWeapon.cachedVehicldesPawns.Remove((Pawn)this.parent);
        }


        public override void CompTickInterval(int delta)
        {
            base.CompTickInterval(delta);
            if (this.pawn == null)
            {
                return;
            }
            if (this.Props.turretRotationFollowPawn)
            {
                this._turretFollowingAngle = this.pawn.Rotation.AsAngle + this.Props.drawData.RotationOffsetForRot(this.pawn.Rotation);
            }
            else
            {
                this._turretFollowingAngle += this._turretAnglePerFrame;
            }
            if (this._lastRotation != this.pawn.Rotation)
            {
                this._lastRotation = this.pawn.Rotation;
                this._currentAngle = this._turretFollowingAngle;
            }
            this._currentAngle = Mathf.SmoothDampAngle(this._currentAngle, this.TargetAngle, ref this._rotationSpeed, this.Props.rotationSmoothTime * (float)delta);
        }


        public override void CompTickRare()
        {
            base.CompTickRare();
            this._turretAnglePerFrame = Rand.Range(-0.5f, 0.5f);
        }


        public Vector3 GetOffsetByRot()
        {
            if (this.Props.drawData != null)
            {
                return this.Props.drawData.OffsetForRot(this.pawn.Rotation);
            }
            return Vector3.zero;
        }


        public Pawn pawn;


        private float _turretFollowingAngle;


        private float _turretAnglePerFrame = 0.1f;


        private float _currentAngle;


        private float _rotationSpeed;


        private Rot4 _lastRotation;


        public static readonly Dictionary<Pawn, CompVehicleWeapon> cachedVehicldesPawns = new Dictionary<Pawn, CompVehicleWeapon>();
    }
}



namespace NCL
{
    // Token: 0x02000030 RID: 48
    public class CompProperties_VehicleWeapon : CompProperties
    {
        // Token: 0x060000EE RID: 238 RVA: 0x00006CF8 File Offset: 0x00004EF8
        public CompProperties_VehicleWeapon()
        {
            this.compClass = typeof(CompVehicleWeapon);
        }

        // Token: 0x0400008C RID: 140
        public DrawData drawData;

        // Token: 0x0400008D RID: 141
        public bool turretRotationFollowPawn;

        // Token: 0x0400008E RID: 142
        public bool horizontalFlip;

        // Token: 0x0400008F RID: 143
        public float rotationSmoothTime = 0.12f;

        // Token: 0x04000090 RID: 144
        public ThingDef defaultWeapon;

        // Token: 0x04000091 RID: 145
        public float drawSize;
    }
}
