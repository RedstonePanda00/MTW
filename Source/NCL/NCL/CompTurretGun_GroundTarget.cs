using NCL;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using UnityEngine;
using Verse;
using Verse.AI;

namespace NCL
{
    public class CompTurretGun_GroundTarget : ThingComp, IAttackTargetSearcher
    {
        // 所有必要字段
        public Thing gun;
        protected int burstWarmupTicksLeft;
        protected int burstCooldownTicksLeft;
        protected LocalTargetInfo currentTarget = LocalTargetInfo.Invalid;
        private bool fireAtWill = true;
        private bool holdFire = false; // 新增：停火状态字段
        private LocalTargetInfo lastAttackedTarget = LocalTargetInfo.Invalid;
        private int lastAttackTargetTick;
        public float curRotation;

        // 静态资源
        private static readonly CachedTexture ToggleTurretIcon = new CachedTexture("UI/Gizmos/ToggleTurret");

        // 基本属性
        public Thing Thing => this.parent;
        public CompProperties_TurretGun Props => (CompProperties_TurretGun)this.props;
        public Verb CurrentEffectiveVerb => this.AttackVerb;
        public LocalTargetInfo LastAttackedTarget => this.lastAttackedTarget;
        public int LastAttackTargetTick => this.lastAttackTargetTick;

        public CompEquippable GunCompEq =>
            this.gun?.TryGetComp<CompEquippable>();

        public Verb AttackVerb =>
            this.GunCompEq?.PrimaryVerb;

        private bool WarmingUp =>
            this.burstWarmupTicksLeft > 0;

        public bool AutoAttack =>
            this.Props?.autoAttack ?? true;

        // 地面目标转换
        private LocalTargetInfo GetGroundTarget(LocalTargetInfo original)
        {
            return original.IsValid && original.Thing != null ?
                new LocalTargetInfo(original.Thing.Position) :
                original;
        }

        // CanShoot逻辑
        private bool CanShoot
        {
            get
            {
                // 新增：检查停火状态
                if (this.holdFire)
                    return false;

                if (this.gun == null || this.AttackVerb == null || this.Props == null)
                    return false;

                Pawn pawn = this.parent as Pawn;
                if (pawn != null)
                {
                    if (!pawn.Spawned || pawn.Downed || pawn.Dead || !pawn.Awake())
                        return false;
                    if (pawn.stances?.stunner?.Stunned ?? false)
                        return false;
                    if (this.TurretDestroyed)
                        return false;
                    if (pawn.IsColonyMechPlayerControlled && !this.fireAtWill)
                        return false;
                }
                CompCanBeDormant compCanBeDormant = this.parent.TryGetComp<CompCanBeDormant>();
                return compCanBeDormant == null || compCanBeDormant.Awake;
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }

            // 新增：玩家阵营的停火按钮
            if (this.parent.Faction == Faction.OfPlayer)
            {
                yield return new Command_Toggle
                {
                    defaultLabel = "NCL.HoldFire".Translate(),
                    defaultDesc = "NCL.HoldFireDesc".Translate(),
                    icon = ContentFinder<Texture2D>.Get("UI/Commands/HoldFire"),
                    isActive = () => this.holdFire,
                    toggleAction = () => {
                        this.holdFire = !this.holdFire;
                        if (this.holdFire) ResetCurrentTarget();
                    }
                };
            }

            Pawn pawn = this.parent as Pawn;
            if (pawn != null && pawn.IsColonyMechPlayerControlled)
            {
                yield return new Command_Toggle
                {
                    defaultLabel = "CommandToggleTurret".Translate(),
                    defaultDesc = "CommandToggleTurretDesc".Translate(),
                    isActive = () => this.fireAtWill,
                    icon = ToggleTurretIcon.Texture,
                    toggleAction = () => this.fireAtWill = !this.fireAtWill
                };
            }
        }

        // TurretDestroyed逻辑
        private bool TurretDestroyed
        {
            get
            {
                Pawn pawn = this.parent as Pawn;
                if (pawn == null)
                    return false;

                Verb attackVerb = this.AttackVerb;
                if (attackVerb?.verbProps?.linkedBodyPartsGroup == null)
                    return false;

                return attackVerb.verbProps.ensureLinkedBodyPartsGroupAlwaysUsable &&
                       PawnCapacityUtility.CalculateNaturalPartsAverageEfficiency(
                           pawn.health.hediffSet,
                           attackVerb.verbProps.linkedBodyPartsGroup) <= 0f;
            }
        }

        // 初始化
        public override void PostPostMake()
        {
            base.PostPostMake();
            this.MakeGun();
        }

        private void MakeGun()
        {
            if (this.Props?.turretDef != null)
            {
                try
                {
                    this.gun = ThingMaker.MakeThing(this.Props.turretDef, null);
                    this.UpdateGunVerbs();
                }
                catch (Exception ex)
                {
                    Log.Error($"Failed to create turret gun: {ex}");
                }
            }
        }

        private void UpdateGunVerbs()
        {
            var compEq = this.GunCompEq;
            if (compEq != null)
            {
                foreach (Verb verb in compEq.AllVerbs)
                {
                    if (verb != null && this.parent != null)
                    {
                        verb.caster = this.parent;
                        verb.castCompleteCallback = delegate ()
                        {
                            // 射击完成后设置冷却时间
                            if (this?.AttackVerb?.verbProps != null)
                            {
                                this.burstCooldownTicksLeft =
                                    this.AttackVerb.verbProps.defaultCooldownTime.SecondsToTicks();
                            }
                        };
                    }
                }
            }
        }

        // 主Tick逻辑 - 重构为原版风格
        public override void CompTick()
        {
            if (!this.CanShoot)
                return;

            Verb attackVerb = this.AttackVerb;
            if (attackVerb == null)
                return;

            // 持续瞄准当前目标（即使不在射击状态）
            LocalTargetInfo groundTarget = GetGroundTarget(this.currentTarget);
            if (groundTarget.IsValid)
            {
                Vector3 targetPos = groundTarget.Cell.ToVector3Shifted();
                Vector3 parentPos = this.parent.DrawPos;

                if (parentPos != Vector3.zero)
                {
                    this.curRotation = (targetPos - parentPos).AngleFlat() + this.Props.angleOffset;
                }
            }

            // 动词Tick
            if (attackVerb.caster != null)
            {
                attackVerb.VerbTick();
            }

            // 状态处理
            if (attackVerb.state != VerbState.Bursting)
            {
                if (this.WarmingUp)
                {
                    this.burstWarmupTicksLeft--;
                    if (this.burstWarmupTicksLeft == 0)
                    {
                        attackVerb.TryStartCastOn(groundTarget, false, true, false, true);
                        this.lastAttackTargetTick = Find.TickManager.TicksGame;
                        this.lastAttackedTarget = groundTarget;
                    }
                }
                else
                {
                    // 处理冷却
                    if (this.burstCooldownTicksLeft > 0)
                    {
                        this.burstCooldownTicksLeft--;
                    }

                    // 冷却结束且满足间隔条件时索敌
                    if (this.burstCooldownTicksLeft <= 0 && this.parent.IsHashIntervalTick(10))
                    {
                        IAttackTarget attackTarget = AttackTargetFinder.BestShootTargetFromCurrentPosition(
                            this,
                            TargetScanFlags.NeedThreat | TargetScanFlags.NeedAutoTargetable,
                            null,
                            0f,
                            9999f);

                        this.currentTarget = attackTarget != null ?
                            GetGroundTarget(new LocalTargetInfo((Thing)attackTarget)) :
                            LocalTargetInfo.Invalid;

                        if (this.currentTarget.IsValid)
                        {
                            this.burstWarmupTicksLeft = 1;
                        }
                        else
                        {
                            this.ResetCurrentTarget();
                        }
                    }
                }
            }
        }

        private void ResetCurrentTarget()
        {
            this.currentTarget = LocalTargetInfo.Invalid;
            this.burstWarmupTicksLeft = 0;
        }

        // Gizmos


        // 数据保存
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref this.burstCooldownTicksLeft, "burstCooldownTicksLeft", 0, false);
            Scribe_Values.Look(ref this.burstWarmupTicksLeft, "burstWarmupTicksLeft", 0, false);
            Scribe_Values.Look(ref this.holdFire, "holdFire", false, false); // 新增
            Scribe_TargetInfo.Look(ref this.currentTarget, "currentTarget");
            Scribe_Deep.Look(ref this.gun, "gun", Array.Empty<object>());
            Scribe_Values.Look(ref this.fireAtWill, "fireAtWill", true, false);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (this.gun == null)
                {
                    Log.Warning("CompTurretGun_GroundTarget had null gun after loading. Recreating.");
                    this.MakeGun();
                }
                this.UpdateGunVerbs();
            }
        }
    }
}

namespace NCL
{
    public class CompProperties_TurretGun_GroundTarget : CompProperties_TurretGun
    {
        public CompProperties_TurretGun_GroundTarget()
        {
            this.compClass = typeof(CompTurretGun_GroundTarget);
        }
    }
}



namespace NCL
{
    public class CompTurretGunPawnOnly : ThingComp, IAttackTargetSearcher
    {
        public Thing Thing => this.parent;

        public CompProperties_TurretGunPawnOnly Props => this.props as CompProperties_TurretGunPawnOnly;

        public Verb CurrentEffectiveVerb => this.AttackVerb;

        public LocalTargetInfo LastAttackedTarget => this.lastAttackedTarget;

        public int LastAttackTargetTick => this.lastAttackTargetTick;

        public CompEquippable GunCompEq
        {
            get
            {
                Thing thing = this.gun;
                return (thing != null) ? thing.TryGetComp<CompEquippable>() : null;
            }
        }

        public Verb AttackVerb
        {
            get
            {
                CompEquippable gunCompEq = this.GunCompEq;
                return (gunCompEq != null) ? gunCompEq.PrimaryVerb : null;
            }
        }

        private bool WarmingUp => this.burstWarmupTicksLeft > 0;

        public bool AutoAttack
        {
            get
            {
                CompProperties_TurretGunPawnOnly props = this.Props;
                return props == null || props.autoAttack;
            }
        }

        private bool CanShoot
        {
            get
            {
                bool flag = this.gun == null || this.AttackVerb == null || this.Props == null;
                bool result;
                if (flag)
                {
                    result = false;
                }
                else
                {
                    Pawn pawn = this.parent as Pawn;
                    bool flag2 = pawn != null;
                    if (flag2)
                    {
                        bool flag3 = !pawn.Spawned || pawn.Downed || pawn.Dead || !pawn.Awake();
                        if (flag3)
                        {
                            return false;
                        }
                        bool stunned = pawn.stances.stunner.Stunned;
                        if (stunned)
                        {
                            return false;
                        }
                        bool turretDestroyed = this.TurretDestroyed;
                        if (turretDestroyed)
                        {
                            return false;
                        }
                        bool flag4 = pawn.IsColonyMechPlayerControlled && !this.fireAtWill;
                        if (flag4)
                        {
                            return false;
                        }
                    }
                    CompCanBeDormant compCanBeDormant = this.parent.TryGetComp<CompCanBeDormant>();
                    result = (compCanBeDormant == null || compCanBeDormant.Awake);
                }
                return result;
            }
        }

        private bool TurretDestroyed
        {
            get
            {
                Pawn pawn = this.parent as Pawn;
                if (pawn != null)
                {
                    Verb attackVerb = this.AttackVerb;
                    bool flag;
                    if (attackVerb == null)
                    {
                        flag = (null != null);
                    }
                    else
                    {
                        VerbProperties verbProps = attackVerb.verbProps;
                        flag = (((verbProps != null) ? verbProps.linkedBodyPartsGroup : null) != null);
                    }
                    if (flag && this.AttackVerb.verbProps.ensureLinkedBodyPartsGroupAlwaysUsable)
                    {
                        return PawnCapacityUtility.CalculateNaturalPartsAverageEfficiency(pawn.health.hediffSet, this.AttackVerb.verbProps.linkedBodyPartsGroup) <= 0f;
                    }
                }
                return false;
            }
        }

        public override void PostPostMake()
        {
            base.PostPostMake();
            this.MakeGun();
        }

        private void MakeGun()
        {
            CompProperties_TurretGunPawnOnly props = this.Props;
            bool flag = ((props != null) ? props.turretDef : null) == null;
            if (flag)
            {
                Log.Error("CompTurretGunPawnOnly: turretDef is null in properties");
            }
            else
            {
                try
                {
                    this.gun = ThingMaker.MakeThing(this.Props.turretDef, null);
                    this.UpdateGunVerbs();
                }
                catch (Exception arg)
                {
                    Log.Error(string.Format("Failed to create turret gun: {0}", arg));
                }
            }
        }

        private void UpdateGunVerbs()
        {
            bool flag = this.gun == null;
            if (!flag)
            {
                CompEquippable gunCompEq = this.GunCompEq;
                bool flag2 = gunCompEq != null;
                if (flag2)
                {
                    foreach (Verb verb in gunCompEq.AllVerbs)
                    {
                        verb.caster = this.parent;
                        verb.castCompleteCallback = delegate ()
                        {
                            this.burstCooldownTicksLeft = this.AttackVerb.verbProps.defaultCooldownTime.SecondsToTicks();
                        };
                    }
                }
            }
        }

        private bool IsValidEnemyPawn(Thing target)
        {
            bool flag = target == null;
            bool result;
            if (flag)
            {
                result = false;
            }
            else
            {
                Pawn pawn = target as Pawn;
                bool flag2 = pawn == null;
                result = (!flag2 && (!pawn.Dead && !pawn.Downed && pawn.Faction != null && this.parent.Faction != null) && pawn.Faction.HostileTo(this.parent.Faction));
            }
            return result;
        }

        public override void CompTick()
        {
            bool flag = !this.CanShoot || this.Props == null;
            if (!flag)
            {
                bool isValid = this.currentTarget.IsValid;
                if (isValid)
                {
                    this.curRotation = (this.currentTarget.Cell.ToVector3Shifted() - this.parent.DrawPos).AngleFlat() + this.Props.angleOffset;
                }
                Verb attackVerb = this.AttackVerb;
                if (attackVerb != null)
                {
                    attackVerb.VerbTick();
                }
                Verb attackVerb2 = this.AttackVerb;
                bool flag2 = attackVerb2 == null || attackVerb2.state != VerbState.Bursting;
                if (flag2)
                {
                    bool warmingUp = this.WarmingUp;
                    if (warmingUp)
                    {
                        this.burstWarmupTicksLeft--;
                        bool flag3 = this.burstWarmupTicksLeft == 0;
                        if (flag3)
                        {
                            this.AttackVerb.TryStartCastOn(this.currentTarget, false, true, false, true);
                            this.lastAttackTargetTick = Find.TickManager.TicksGame;
                            this.lastAttackedTarget = this.currentTarget;
                        }
                    }
                    else
                    {
                        bool flag4 = this.burstCooldownTicksLeft > 0;
                        if (flag4)
                        {
                            this.burstCooldownTicksLeft--;
                        }
                        bool flag5 = this.burstCooldownTicksLeft <= 0 && this.parent.IsHashIntervalTick(10);
                        if (flag5)
                        {
                            IAttackTarget attackTarget = AttackTargetFinder.BestAttackTarget(this, TargetScanFlags.NeedThreat | TargetScanFlags.NeedAutoTargetable, (Thing t) => this.IsValidEnemyPawn(t), 0f, 9999f, default(IntVec3), float.MaxValue, false, true, false, false);
                            this.currentTarget = ((attackTarget != null) ? new LocalTargetInfo((Thing)attackTarget) : LocalTargetInfo.Invalid);
                            bool isValid2 = this.currentTarget.IsValid;
                            if (isValid2)
                            {
                                this.burstWarmupTicksLeft = 1;
                            }
                            else
                            {
                                this.ResetCurrentTarget();
                            }
                        }
                    }
                }
            }
        }

        private void ResetCurrentTarget()
        {
            this.currentTarget = LocalTargetInfo.Invalid;
            this.burstWarmupTicksLeft = 0;
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            // 简化为返回基础Gizmo
            return base.CompGetGizmosExtra();
        }

        public override List<PawnRenderNode> CompRenderNodes()
        {
            CompProperties_TurretGunPawnOnly props = this.Props;
            bool flag = ((props != null) ? props.renderNodeProperties : null) == null;
            List<PawnRenderNode> result;
            if (flag)
            {
                result = base.CompRenderNodes();
            }
            else
            {
                Pawn pawn = this.parent as Pawn;
                bool flag2 = pawn == null;
                if (flag2)
                {
                    result = base.CompRenderNodes();
                }
                else
                {
                    List<PawnRenderNode> list = new List<PawnRenderNode>();
                    foreach (PawnRenderNodeProperties pawnRenderNodeProperties in this.Props.renderNodeProperties)
                    {
                        bool flag3 = ((pawnRenderNodeProperties != null) ? pawnRenderNodeProperties.nodeClass : null) == null;
                        if (!flag3)
                        {
                            try
                            {
                                Type nodeClass = pawnRenderNodeProperties.nodeClass;
                                object[] array = new object[3];
                                array[0] = pawn;
                                array[1] = pawnRenderNodeProperties;
                                int num = 2;
                                Pawn_DrawTracker drawer = pawn.Drawer;
                                object obj;
                                if (drawer == null)
                                {
                                    obj = null;
                                }
                                else
                                {
                                    PawnRenderer renderer = drawer.renderer;
                                    obj = ((renderer != null) ? renderer.renderTree : null);
                                }
                                array[num] = obj;
                                PawnRenderNode pawnRenderNode = Activator.CreateInstance(nodeClass, array) as PawnRenderNode;
                                PawnRenderNode_TurretPawnOnly pawnRenderNode_TurretPawnOnly = pawnRenderNode as PawnRenderNode_TurretPawnOnly;
                                bool flag4 = pawnRenderNode_TurretPawnOnly != null;
                                if (flag4)
                                {
                                    pawnRenderNode_TurretPawnOnly.turretComp = this;
                                }
                                list.Add(pawnRenderNode);
                            }
                            catch (Exception arg)
                            {
                                Log.Error(string.Format("Failed to create render node: {0}", arg));
                            }
                        }
                    }
                    result = ((list.Count > 0) ? list : base.CompRenderNodes());
                }
            }
            return result;
        }

        public override IEnumerable<StatDrawEntry> SpecialDisplayStats()
        {
            // 简化为返回基础统计信息
            return base.SpecialDisplayStats();
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look<int>(ref this.burstCooldownTicksLeft, "burstCooldownTicksLeft", 0, false);
            Scribe_Values.Look<int>(ref this.burstWarmupTicksLeft, "burstWarmupTicksLeft", 0, false);
            Scribe_TargetInfo.Look(ref this.currentTarget, "currentTarget");
            Scribe_Deep.Look<Thing>(ref this.gun, "gun", Array.Empty<object>());
            Scribe_Values.Look<bool>(ref this.fireAtWill, "fireAtWill", true, false);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                // 修复点：确保在加载后更新动词绑定
                if (this.gun == null)
                {
                    Log.Warning("Recreating missing gun for CompTurretGunPawnOnly");
                    this.MakeGun();
                }
                else
                {
                    // 关键修复：总是更新动词绑定
                    this.UpdateGunVerbs();
                }

                // 额外保护：如果gun仍然为空则报错
                if (this.gun == null)
                {
                    Log.Error("Cannot recreate gun: turretDef is null or creation failed");
                }
            }
        }

        // Token: 0x0400000A RID: 10
        public Thing gun;

        // Token: 0x0400000B RID: 11
        protected int burstWarmupTicksLeft;

        // Token: 0x0400000C RID: 12
        protected int burstCooldownTicksLeft;

        // Token: 0x0400000D RID: 13
        protected LocalTargetInfo currentTarget = LocalTargetInfo.Invalid;

        // Token: 0x0400000E RID: 14
        private bool fireAtWill = true;

        // Token: 0x0400000F RID: 15
        private LocalTargetInfo lastAttackedTarget = LocalTargetInfo.Invalid;

        // Token: 0x04000010 RID: 16
        private int lastAttackTargetTick;

        // Token: 0x04000011 RID: 17
        public float curRotation;

        // Token: 0x04000012 RID: 18
        private static readonly CachedTexture ToggleTurretIcon = new CachedTexture("UI/Gizmos/ToggleTurret");
    }
}


public class PawnRenderNode_TurretPawnOnly : PawnRenderNode
    {
        public CompTurretGunPawnOnly turretComp;

        public PawnRenderNode_TurretPawnOnly(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree)
            : base(pawn, props, tree)
        {
        }

        public override Graphic GraphicFor(Pawn pawn)
        {
            if (this.turretComp?.Props?.turretDef?.graphicData?.texPath == null)
                return base.GraphicFor(pawn);

            return GraphicDatabase.Get<Graphic_Single>(
                this.turretComp.Props.turretDef.graphicData.texPath,
                ShaderDatabase.Cutout
            );
        }
    }


namespace NCL
{
    public class CompProperties_TurretGunPawnOnly : CompProperties_TurretGun
    {
        public CompProperties_TurretGunPawnOnly()
        {
            this.compClass = typeof(CompTurretGunPawnOnly);
        }

        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (string error in base.ConfigErrors(parentDef))
                yield return error;

            if (this.turretDef == null)
                yield return "turretDef must be defined";
        }
    }
}
