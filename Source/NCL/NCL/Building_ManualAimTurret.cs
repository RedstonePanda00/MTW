using RimWorld;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Verse;
using Verse.AI;

namespace NCL
{
    public class Building_ManualAimTurret : Building_TurretGun
    {
        // 使用反射访问父类私有字段
        private static readonly FieldInfo holdFireField =
            typeof(Building_TurretGun).GetField("holdFire", BindingFlags.NonPublic | BindingFlags.Instance);

        // 鼠标跟随控制
        private bool manualAimMode = false;
        private float manualRotation;
        private const float MouseFollowSpeed = 0.2f;

        // 射击状态
        private int manualShootCooldown = 0;
        private const int MaxShootCooldown = 60; // 射击冷却时间（帧数）

        // 炮弹定义
        private ThingDef bulletDef = DefDatabase<ThingDef>.GetNamed("Bullet_AA");

        // 辅助方法：设置开火状态
        private void SetHoldFire(bool value)
        {
            holdFireField.SetValue(this, value);
        }

        // 自定义设置炮台旋转方法
        private void SetTurretRotationDirectly(float rotation)
        {
            // 直接设置旋转角度（使用父类的TurretTop）
            this.top.CurRotation = rotation;
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);

            // 初始禁用自动索敌
            SetHoldFire(true);

            // 初始化旋转角度
            manualRotation = base.Rotation.AsAngle;
            SetTurretRotationDirectly(manualRotation);
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            // 使用手动旋转而非目标旋转
            SetTurretRotationDirectly(manualRotation);
            base.DrawAt(drawLoc, flip);
        }

        protected override void Tick()
        {
            base.Tick(); // 调用父类Tick

            // 更新手动射击冷却
            if (manualShootCooldown > 0)
            {
                manualShootCooldown--;
            }

            // 鼠标跟随模式
            if (manualAimMode && base.Map != null && Find.Selector.SelectedObjects.Contains(this))
            {
                Vector3 currentMousePos = UI.MouseMapPosition();
                Vector3 turretPos = this.DrawPos;

                // 计算鼠标相对位置（忽略Y轴） - 修正方向
                Vector2 relativePos = new Vector2(
                    currentMousePos.x - turretPos.x,
                    turretPos.z - currentMousePos.z  // 反转Z轴方向
                );

                // 计算旋转角度（单位：度） - 修正方向并添加90度偏移
                manualRotation = Mathf.Atan2(relativePos.y, relativePos.x) * Mathf.Rad2Deg + 90f; // 添加90度偏移

                // 限制角度在0-360之间
                if (manualRotation < 0) manualRotation += 360;
                if (manualRotation > 360) manualRotation -= 360;

                // 立即更新炮台旋转
                SetTurretRotationDirectly(manualRotation);

                // 检测按键按下（Misc1）
                if (KeyBindingDefOf.Misc1.IsDown && manualShootCooldown <= 0)
                {
                    ManualShoot();
                }
            }

            // 确保自动开火始终禁用
            SetHoldFire(true);
        }

        // 手动射击方法 - 直接生成炮弹
        public void ManualShoot()
        {
            if (bulletDef == null || manualShootCooldown > 0 || !this.Active || base.IsStunned)
                return;

            // 计算射击方向（使用修正后的角度）
            Vector3 shootDirection = Quaternion.AngleAxis(manualRotation, Vector3.up) * Vector3.forward;

            // 计算起始位置（炮台位置上方）
            Vector3 startPos = this.DrawPos + new Vector3(0f, 1f, 0f);
            IntVec3 startCell = startPos.ToIntVec3();

            // 计算目标位置（地图边缘）
            IntVec3 endCell = FindMapEdgeTarget(startCell, shootDirection);

            // 确保目标位置有效
            if (!startCell.InBounds(base.Map) || !endCell.InBounds(base.Map))
                return;

            // 创建炮弹
            Projectile projectile = (Projectile)ThingMaker.MakeThing(bulletDef, null);
            if (projectile == null)
                return;

            // 设置位置
            GenSpawn.Spawn(projectile, startCell, base.Map);

            // 发射炮弹
            projectile.Launch(
                launcher: this,
                origin: startPos,
                usedTarget: new LocalTargetInfo(endCell),
                intendedTarget: new LocalTargetInfo(endCell),
                hitFlags: ProjectileHitFlags.All
            );

            // 设置冷却
            manualShootCooldown = MaxShootCooldown;

            // 添加视觉反馈
            if (base.Spawned)
            {
                FleckMaker.ThrowDustPuff(startPos, base.Map, 1f);
                FleckMaker.Static(startPos, base.Map, FleckDefOf.ShotFlash);
            }
        }

        // 计算地图边缘目标位置
        private IntVec3 FindMapEdgeTarget(IntVec3 startCell, Vector3 direction)
        {
            Map map = base.Map;
            if (map == null) return startCell;

            // 确保方向向量是单位向量
            direction.Normalize();

            // 计算最大距离（地图对角线长度）
            float maxDistance = Mathf.Sqrt(map.Size.x * map.Size.x + map.Size.z * map.Size.z);

            // 沿着方向逐步检测，直到找到地图边界
            IntVec3 currentCell = startCell;
            IntVec3 lastValidCell = startCell;

            for (float distance = 1f; distance <= maxDistance; distance += 1f)
            {
                // 计算当前位置
                Vector3 currentPos = startCell.ToVector3() + direction * distance;
                IntVec3 testCell = currentPos.ToIntVec3();

                // 检查是否超出地图边界
                if (!testCell.InBounds(map))
                {
                    // 返回最后一个有效单元格
                    return lastValidCell;
                }

                // 更新最后一个有效单元格
                lastValidCell = testCell;
            }

            // 如果循环完成，返回最后一个有效单元格
            return lastValidCell;
        }

        // 切换手动瞄准模式
        public void ToggleManualAimMode()
        {
            manualAimMode = !manualAimMode;
            if (manualAimMode)
            {
                Messages.Message("ManualAimTurret_AimEnabled".Translate(), MessageTypeDefOf.NeutralEvent);
            }
            else
            {
                Messages.Message("ManualAimTurret_AimDisabled".Translate(), MessageTypeDefOf.NeutralEvent);
            }
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            // 获取父类Gizmo列表
            List<Gizmo> baseGizmos = new List<Gizmo>();
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                baseGizmos.Add(gizmo);
            }

            // 过滤掉Hold Fire Gizmo
            for (int i = baseGizmos.Count - 1; i >= 0; i--)
            {
                Command_Toggle toggle = baseGizmos[i] as Command_Toggle;
                if (toggle != null && toggle.defaultLabel == "CommandHoldFire".Translate())
                {
                    baseGizmos.RemoveAt(i);
                }
            }

            // 返回过滤后的父类Gizmo
            foreach (Gizmo gizmo in baseGizmos)
            {
                yield return gizmo;
            }

            // 手动瞄准开关
            Command_Toggle aimToggle = new Command_Toggle
            {
                defaultLabel = manualAimMode ?
                    "ManualAimTurret_DisableAim".Translate() :
                    "ManualAimTurret_EnableAim".Translate(),
                defaultDesc = "ManualAimTurret_AimDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get("UI/Commands/Attack", true),
                isActive = () => manualAimMode,
                toggleAction = ToggleManualAimMode
            };
            yield return aimToggle;

            // 手动射击按钮
            if (manualAimMode)
            {
                Command_Action shootCommand = new Command_Action
                {
                    defaultLabel = "ManualAimTurret_Shoot".Translate(),
                    defaultDesc = "ManualAimTurret_ShootDesc".Translate(),
                    icon = ContentFinder<Texture2D>.Get("UI/Commands/Shoot", true),
                    action = ManualShoot
                };

                // 添加冷却状态显示
                if (manualShootCooldown > 0)
                {
                    shootCommand.Disable("ManualAimTurret_Cooldown".Translate(manualShootCooldown.ToStringSecondsFromTicks()));
                }

                yield return shootCommand;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref manualAimMode, "manualAimMode", false);
            Scribe_Values.Look(ref manualRotation, "manualRotation", 0f);
            Scribe_Values.Look(ref manualShootCooldown, "manualShootCooldown", 0);
        }
    }
}
