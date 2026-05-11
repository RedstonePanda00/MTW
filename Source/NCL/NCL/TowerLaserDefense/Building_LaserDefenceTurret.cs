using RimWorld;
using UnityEngine;
using Verse;

#nullable disable
namespace TowerLaserDefense
{
    // Vanilla-style turret top: same draw path as TurretTop.DrawTurret (Building_TurretGun), using building.turretTopMat from turretGunDef.
    public class Building_LaserDefenceTurret : Building
    {
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            CompLaserDefence comp = GetComp<CompLaserDefence>();
            if (comp?.DefenceCore != null && !LaserDefenceCore.Instances.Contains(comp.DefenceCore))
            {
                LaserDefenceCore.Instances.Add(comp.DefenceCore);
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            DrawLaserTurretTop(drawLoc);
            base.DrawAt(drawLoc, flip);
        }

        private void DrawLaserTurretTop(Vector3 drawLoc)
        {
            Material mat = def.building.turretTopMat;
            if (mat == null || mat == BaseContent.BadMat)
            {
                return;
            }

            CompLaserDefence comp = this.GetComp<CompLaserDefence>();
            float aimAngle = comp?.DefenceCore?.VisualAimAngle ?? 0f;

            Vector3 v = new Vector3(def.building.turretTopOffset.x, 0f, def.building.turretTopOffset.y);
            float turretTopDrawSize = def.building.turretTopDrawSize;
            const float recoilAngleOffset = 0f;
            Vector3 recoilDrawOffset = Vector3.zero;
            v = v.RotatedBy(recoilAngleOffset);
            v += recoilDrawOffset;

            // Texture faces broadside to aim vector; add +90° clockwise (map plane) around mesh center.
            const float topGraphicClockwiseYawDeg = 90f;
            float rotationForDraw = aimAngle + topGraphicClockwiseYawDeg;
            Vector3 pos = drawLoc + Altitudes.AltIncVect + v;
            Quaternion q = ((float) TurretTop.ArtworkRotation + rotationForDraw).ToQuat();
            Graphics.DrawMesh(
                MeshPool.plane10,
                Matrix4x4.TRS(pos, q, new Vector3(turretTopDrawSize, 1f, turretTopDrawSize)),
                mat,
                0);
        }
    }
}
