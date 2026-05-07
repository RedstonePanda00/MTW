using NCL;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;



namespace NCL
{
    #region Common Components (Shared by both skills)

    public enum SkillType
    {
        Charge,
        FlyingKick
    }

    public class SkillBehaviorMode
    {
        public string behaviorId;
        public GraphicData graphicDataA;
        public GraphicData graphicDataB; // 用于FlyingKick的第二种状态图形
    }

    public static class SkillGraphics
    {
        private static Dictionary<SkillType, Dictionary<string, SkillBehaviorMode>> behaviorModes =
            new Dictionary<SkillType, Dictionary<string, SkillBehaviorMode>>();

        static SkillGraphics()
        {
            // 初始化Charge技能的行为模式
            behaviorModes[SkillType.Charge] = new Dictionary<string, SkillBehaviorMode>();
            behaviorModes[SkillType.Charge].Add("ChargeA", new SkillBehaviorMode
            {
                behaviorId = "ChargeA",
                graphicDataA = new GraphicData
                {
                    texPath = "ModIcon/Mantis_FlyingA",
                    graphicClass = typeof(Graphic_Single),
                    drawSize = new Vector2(4.5f, 4.5f)
                }
            });
            behaviorModes[SkillType.Charge].Add("ChargeB", new SkillBehaviorMode
            {
                behaviorId = "ChargeB",
                graphicDataA = new GraphicData
                {
                    texPath = "ModIcon/Mantis_FlyingA",
                    graphicClass = typeof(Graphic_Single),
                    drawSize = new Vector2(4.5f, 4.5f)
                }
            });

            // 初始化FlyingKick技能的行为模式
            behaviorModes[SkillType.FlyingKick] = new Dictionary<string, SkillBehaviorMode>();
            behaviorModes[SkillType.FlyingKick].Add("FlyingKickA", new SkillBehaviorMode
            {
                behaviorId = "FlyingKickA",
                graphicDataA = new GraphicData
                {
                    texPath = "Ability/PillBugFlyingB",
                    graphicClass = typeof(Graphic_Single),
                    drawSize = new Vector2(5f, 5f)
                }
            });
            behaviorModes[SkillType.FlyingKick].Add("FlyingKickB", new SkillBehaviorMode
            {
                behaviorId = "FlyingKickB",
                graphicDataA = new GraphicData
                {
                    texPath = "Ability/PillBugFlyingB",
                    graphicClass = typeof(Graphic_Single),
                    drawSize = new Vector2(5f, 5f),
                    shaderType = ShaderTypeDefOf.Transparent
                },
                graphicDataB = new GraphicData
                {
                    texPath = "Ability/PillBugFlyingC",
                    graphicClass = typeof(Graphic_Single),
                    drawSize = new Vector2(5f, 5f),
                    shaderType = ShaderTypeDefOf.Transparent
                }
            });
        }

        public static SkillBehaviorMode GetBehaviorMode(SkillType skillType, string modeId)
        {
            if (behaviorModes.TryGetValue(skillType, out var skillModes) &&
                skillModes.TryGetValue(modeId, out var mode))
            {
                return mode;
            }

            // 默认值
            return new SkillBehaviorMode();
        }
    }

    public static class GenAttackCells
    {
        public static List<IntVec3> NineCells = new List<IntVec3>
        {
            new IntVec3(1, 0, 1),
            new IntVec3(1, 0, 0),
            new IntVec3(1, 0, -1),
            new IntVec3(0, 0, 1),
            new IntVec3(0, 0, 0),
            new IntVec3(0, 0, -1),
            new IntVec3(-1, 0, 1),
            new IntVec3(-1, 0, 0),
            new IntVec3(-1, 0, -1)
        };

        public static List<IntVec3> TwentyFiveCells = new List<IntVec3>
        {
            new IntVec3(-2, 0, -2),
            new IntVec3(-2, 0, -1),
            new IntVec3(-2, 0, 0),
            new IntVec3(-2, 0, 1),
            new IntVec3(-2, 0, 2),
            new IntVec3(-1, 0, -2),
            new IntVec3(-1, 0, -1),
            new IntVec3(-1, 0, 0),
            new IntVec3(-1, 0, 1),
            new IntVec3(-1, 0, 2),
            new IntVec3(0, 0, -2),
            new IntVec3(0, 0, -1),
            new IntVec3(0, 0, 0),
            new IntVec3(0, 0, 1),
            new IntVec3(0, 0, 2),
            new IntVec3(1, 0, -2),
            new IntVec3(1, 0, -1),
            new IntVec3(1, 0, 0),
            new IntVec3(1, 0, 1),
            new IntVec3(1, 0, 2),
            new IntVec3(2, 0, -2),
            new IntVec3(2, 0, -1),
            new IntVec3(2, 0, 0),
            new IntVec3(2, 0, 1),
            new IntVec3(2, 0, 2)
        };

        // 本地九宫格模式，避免静态访问
        public static readonly IntVec3[] NineCellsLocal =
        {
            new IntVec3(1, 0, 1),
            new IntVec3(1, 0, 0),
            new IntVec3(1, 0, -1),
            new IntVec3(0, 0, 1),
            new IntVec3(0, 0, 0),
            new IntVec3(0, 0, -1),
            new IntVec3(-1, 0, 1),
            new IntVec3(-1, 0, 0),
            new IntVec3(-1, 0, -1)
        };
    }

    public class Hediff_BeatenOff : Hediff
    {
        private int _flyingTime;
        private int _maxFlyingTime;
        private Vector3 _startPos;
        private Vector3 _direction;
        private float _speed;

        public Vector3 DrawPosOverride =>
            this._startPos + this._direction * this._speed * this._flyingTime / 60f;

        public override void Tick()
        {
            try
            {
                if (this._flyingTime >= this._maxFlyingTime ||
                    !this.pawn.Spawned ||
                    this.pawn.Dead)
                {
                    RemoveHediff();
                    return;
                }

                this._flyingTime++;
                IntVec3 targetCell = this.DrawPosOverride.ToIntVec3();

                if (!targetCell.InBounds(this.pawn.Map) ||
                    targetCell.Impassable(this.pawn.Map))
                {
                    ApplyCollisionDamage();
                    RemoveHediff();
                }
                else if (this.pawn.Position != targetCell)
                {
                    this.pawn.Position = targetCell;
                    this.pawn.pather.Notify_Teleported_Int();
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error in Hediff_BeatenOff.Tick: {ex}");
                RemoveHediff();
            }
        }

        private void RemoveHediff()
        {
            this.pawn.pather.Notify_Teleported_Int();
            this.pawn.health.RemoveHediff(this);
        }

        private void ApplyCollisionDamage()
        {
            int hitCount = Rand.RangeInclusive(4, 10);
            float damagePerHit = 50f / hitCount;

            for (int i = 0; i < hitCount; i++)
            {
                if (!this.pawn.Spawned || this.pawn.Dead) return;

                this.pawn.pather.Notify_Teleported_Int();
                this.pawn.TakeDamage(new DamageInfo(
                    DamageDefOf.Blunt,
                    damagePerHit,
                    1.5f,
                    this._direction.AngleFlat(),
                    null,
                    null,
                    null,
                    DamageInfo.SourceCategory.ThingOrUnknown,
                    null,
                    true,
                    true,
                    QualityCategory.Normal,
                    true));
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref this._flyingTime, "_flyingTime", 0);
            Scribe_Values.Look(ref this._maxFlyingTime, "_maxFlyingTime", 0);
            Scribe_Values.Look(ref this._startPos, "_startPos");
            Scribe_Values.Look(ref this._direction, "_direction");
            Scribe_Values.Look(ref this._speed, "_speed", 0f);
        }

        public static void BeatOff(Pawn target, Pawn instigator, float distance, float speed = 10f)
        {
            BeatOff(target, target.DrawPos - instigator.DrawPos, distance, speed);
        }

        public static void BeatOff(Pawn pawn, Vector3 direction, float distance, float speed = 10f)
        {
            try
            {
                if (pawn == null || pawn.Destroyed || !pawn.Spawned || pawn.Dead)
                    return;

                // 安全检查Hediff存在
                if (pawn.health.hediffSet.HasHediff(HediffDef.Named("NCL_BeatenOff")))
                    return;

                Hediff_BeatenOff hediff = HediffMaker.MakeHediff(
                    HediffDef.Named("NCL_BeatenOff"), pawn) as Hediff_BeatenOff;

                if (hediff == null) return;

                hediff._startPos = pawn.TrueCenter();
                hediff._direction = direction.normalized;
                hediff._speed = speed;
                hediff._maxFlyingTime = Mathf.RoundToInt(distance / speed * 60f);
                pawn.health.AddHediff(hediff);
            }
            catch (Exception ex)
            {
                Log.Error($"Error applying BeatOff: {ex}");
            }
        }
    }

    #endregion
}

namespace NCL
{
    #region FlyingKick Skill

    public class Verb_FlyingKick : Verb
    {
        public override bool MultiSelect => true;

        public override bool ValidateTarget(LocalTargetInfo target, bool showMessages = true)
        {
            return this.caster != null &&
                   this.CanHitTarget(target) &&
                   JumpUtility.ValidJumpTarget(this.caster, this.caster.Map, target.Cell);
        }

        public override bool CanHitTargetFrom(IntVec3 root, LocalTargetInfo targ)
        {
            float num = this.EffectiveRange * this.EffectiveRange;
            IntVec3 cell = targ.Cell;
            return (float)this.caster.Position.DistanceToSquared(cell) <= num &&
                   GenSight.LineOfSight(root, cell, this.caster.Map, false, null, 0, 0);
        }

        public override void DrawHighlight(LocalTargetInfo target)
        {
            base.DrawHighlight(target);
            GenDraw.DrawLineBetween(this.caster.TrueCenter(), target.CenterVector3);
        }

        public override void OnGUI(LocalTargetInfo target)
        {
            if (this.CanHitTarget(target) && JumpUtility.ValidJumpTarget(this.caster, this.caster.Map, target.Cell))
            {
                base.OnGUI(target);
            }
            else
            {
                GenUI.DrawMouseAttachment(TexCommand.CannotShoot);
            }
        }

        public override void OrderForceTarget(LocalTargetInfo target)
        {
            Job job = JobMaker.MakeJob(JobDefOf.CastJump, target);
            job.verbToUse = this;
            this.CasterPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
        }

        protected override bool TryCastShot()
        {
            if (this.CasterPawn == null ||
                !this.CasterPawn.Spawned ||
                this.CasterPawn.stances.FullBodyBusy)
            {
                return false;
            }

            this.Attack();
            return true;
        }

        public void Attack()
        {
            if (!(base.DirectOwner as Ability)?.CanCast ?? true)
            {
                this.CasterPawn.jobs.StopAll(false, true);
                return;
            }

            Pawn casterPawn = this.CasterPawn;
            IntVec3 cell = this.currentTarget.Cell;
            Map map = casterPawn.Map;

            (base.DirectOwner as Ability).Activate(this.currentTarget, cell);

            FlyingKick flyingKick = FlyingKick.Make(
                ThingDef.Named("NCL_FlyingKick"),
                base.DirectOwner as Ability,
                casterPawn,
                cell);

            if (flyingKick != null)
            {
                GenSpawn.Spawn(flyingKick, cell, map, WipeMode.Vanish);
            }
        }
    }

    public class FlyingKick : PawnFlyer
    {
        private IntVec3 _lastCell;
        private Vector3 _lastPos;
        private Vector3 _newPos;
        private const float LandingDistanceThreshold = 2.5f;
        private const float DamageAmount = 30f;
        private const float DamageArmorPenetration = 1.5f;
        private const float BeatOffDistance = 6f;
        private const float BeatOffSpeed = 10f;
        private const float EffectRadius = 4f;

        private SkillBehaviorMode _behaviorMode;

        protected override void Tick()
        {
            if (this.DrawPos != this._newPos)
            {
                this._lastPos = this._newPos;
                this._newPos = this.DrawPos;
            }
            this.AttackTick();
            base.Tick();
        }

        private Vector3 GroundPos => base.DestinationPos;

        protected override void RespawnPawn()
        {
            // 特效和声音
            DefDatabase<EffecterDef>.GetNamed("NCL_Crack", true).Spawn(base.Position, base.Map, 1f);
            SoundDef.Named("Explosion_Bomb").PlayOneShot(new TargetInfo(base.Position, base.Map, false));

            Pawn flyingPawn = base.FlyingPawn;
            if (flyingPawn == null) return;

            // 使用局部变量
            List<Thing> targets = new List<Thing>();
            int cellsCount = GenRadial.NumCellsInRadius(EffectRadius);

            // 收集目标
            for (int i = 0; i < cellsCount; i++)
            {
                IntVec3 cell = base.Position + GenRadial.RadialPattern[i];
                if (!cell.InBounds(base.Map)) continue;

                foreach (Thing thing in cell.GetThingList(base.Map))
                {
                    if (IsValidTarget(thing))
                    {
                        targets.Add(thing);
                    }
                }
            }

            // 处理目标
            foreach (Thing target in targets)
            {
                if (target is Pawn pawn && !pawn.Destroyed && pawn.Spawned)
                {
                    ApplyDamage(pawn, flyingPawn);
                }
            }

            base.RespawnPawn();

            // 冲击波特效
            FleckMaker.AttachedOverlay(
                flyingPawn,
                DefDatabase<FleckDef>.GetNamed("NCL_Stump_ShockWave", true),
                Vector3.zero, 1f, -1f);
        }

        private void AttackTick()
        {
            if (base.Position != this._lastCell)
            {
                this._lastCell = base.Position;
                List<Thing> targets = new List<Thing>();
                HashSet<Thing> localHurtedTargets = new HashSet<Thing>();

                foreach (IntVec3 intVec in GenAttackCells.NineCellsLocal)
                {
                    IntVec3 cell = intVec + this._lastCell;
                    if (!cell.InBounds(base.Map)) continue;

                    foreach (Thing thing in cell.GetThingList(base.Map))
                    {
                        if (IsValidTarget(thing) && !localHurtedTargets.Contains(thing))
                        {
                            localHurtedTargets.Add(thing);
                            targets.Add(thing);
                        }
                    }
                }

                foreach (Thing target in targets)
                {
                    if (target is Pawn pawn && !pawn.Destroyed && pawn.Spawned)
                    {
                        ApplyDamage(pawn, base.FlyingPawn);
                    }
                }
            }
        }

        private bool IsValidTarget(Thing thing)
        {
            if (thing == null || base.FlyingPawn == null) return false;

            return base.FlyingPawn.Faction == null
                ? thing != base.FlyingPawn
                : base.FlyingPawn.HostileTo(thing);
        }

        private void ApplyDamage(Pawn targetPawn, Pawn instigator)
        {
            try
            {
                Vector3 vector = targetPawn.DrawPos - this.GroundPos;

                // 应用击退效果
                Hediff_BeatenOff.BeatOff(targetPawn, vector, BeatOffDistance, BeatOffSpeed);

                // 造成伤害
                targetPawn.TakeDamage(new DamageInfo(
                    DamageDefOf.Blunt,
                    DamageAmount,
                    DamageArmorPenetration,
                    vector.AngleFlat(),
                    instigator,
                    null,
                    null,
                    DamageInfo.SourceCategory.ThingOrUnknown,
                    null,
                    true,
                    true,
                    QualityCategory.Normal,
                    true));
            }
            catch (Exception ex)
            {
                Log.Error($"Error applying damage in FlyingKick: {ex}");
            }
        }

        public override void DynamicDrawPhaseAt(DrawPhase phase, Vector3 drawLoc, bool flip = false)
        {
            try
            {
                float angle = (this._newPos - this._lastPos).AngleFlat();
                bool isLanding = (base.DestinationPos - this.DrawPos).sqrMagnitude < LandingDistanceThreshold;

                // 获取行为模式
                _behaviorMode = SkillGraphics.GetBehaviorMode(SkillType.FlyingKick,
                    isLanding ? "FlyingKickB" : "FlyingKickA");

                // 选择图形数据
                GraphicData graphicData = isLanding ?
                    _behaviorMode.graphicDataB :
                    _behaviorMode.graphicDataA;

                if (graphicData == null) return;

                Graphic graphic = graphicData.Graphic;
                Quaternion rotation = Quaternion.AngleAxis(isLanding ? 0f : angle, Vector3.up);
                Mesh mesh = (angle > 180f) ?
                    MeshPool.GridPlaneFlip(graphic.drawSize) :
                    MeshPool.GridPlane(graphic.drawSize);

                Graphics.DrawMesh(mesh, this.DrawPos, rotation, graphic.MatSingle, 0);
            }
            catch (Exception ex)
            {
                Log.Error($"Error in FlyingKick drawing: {ex}");
            }
        }

        public static FlyingKick Make(ThingDef flyingDef, Ability ability, Pawn pawn, IntVec3 destCell)
        {
            pawn.rotationTracker.FaceCell(destCell);
            return PawnFlyer.MakeFlyer(
                flyingDef,
                pawn,
                destCell,
                null,
                null,
                false,
                null,
                null,
                default(LocalTargetInfo)) as FlyingKick;
        }
    }

    #endregion
}

namespace NCL
{
    #region Charge Skill

    public class Verb_Charge : Verb
    {
        public override bool MultiSelect => true;

        public override bool ValidateTarget(LocalTargetInfo target, bool showMessages = true)
        {
            return caster != null &&
                   CanHitTarget(target) &&
                   JumpUtility.ValidJumpTarget(caster, caster.Map, target.Cell);
        }

        public override bool CanHitTargetFrom(IntVec3 root, LocalTargetInfo targ)
        {
            float rangeSqr = EffectiveRange * EffectiveRange;
            return (float)IntVec3Utility.DistanceToSquared(caster.Position, targ.Cell) <= rangeSqr &&
                   GenSight.LineOfSight(root, targ.Cell, caster.Map, false);
        }

        public override void DrawHighlight(LocalTargetInfo target)
        {
            base.DrawHighlight(target);
            GenDraw.DrawLineBetween(caster.TrueCenter(), target.CenterVector3);
        }

        public override void OnGUI(LocalTargetInfo target)
        {
            if (CanHitTarget(target) && JumpUtility.ValidJumpTarget(caster, caster.Map, target.Cell))
            {
                base.OnGUI(target);
            }
            else
            {
                GenUI.DrawMouseAttachment(TexCommand.CannotShoot);
            }
        }

        public override void OrderForceTarget(LocalTargetInfo target)
        {
            Job job = JobMaker.MakeJob(JobDefOf.CastJump, target);
            job.verbToUse = this;
            CasterPawn.jobs.TryTakeOrderedJob(job);
        }

        protected override bool TryCastShot()
        {
            if (CasterPawn == null ||
                !CasterPawn.Spawned ||
                CasterPawn.stances.FullBodyBusy)
            {
                return false;
            }

            // 保存关键数据（此时Pawn尚未消失）
            IntVec3 spawnPosition = CasterPawn.Position;
            Map map = CasterPawn.Map;
            LocalTargetInfo target = currentTarget;

            // 创建冲锋实体
            CreateChargeEffect(spawnPosition, map, target);
            return true;
        }

        private void CreateChargeEffect(IntVec3 spawnPosition, Map map, LocalTargetInfo target)
        {
            if (!(DirectOwner is Ability ability) || !ability.CanCast)
            {
                CasterPawn.jobs.StopAll(false);
                return;
            }

            ability.Activate(target, target.Cell);
            Charge charge = Charge.Make(
                ThingDef.Named("NCL_Charge"),
                ability,
                CasterPawn,
                target.Cell,
                null,
                null,
                false
            );

            if (charge != null)
            {
                // 使用保存的地图生成实体
                GenSpawn.Spawn(charge, spawnPosition, map);

                // 初始设置为冲锋状态
                charge.SetBehaviorMode("ChargeA");
            }
        }
    }

    public class Charge : PawnFlyer
    {
        private int positionLastComputedTick = -1;
        private Vector3 groundPos;
        private float angle = -1f;
        private IntVec3 lastPos;
        private HashSet<Thing> hurtedTargets = new HashSet<Thing>();
        private static List<Thing> targets = new List<Thing>();
        private SkillBehaviorMode _behaviorMode;

        protected override void Tick()
        {
            AttackTick();
            base.Tick();
        }

        private void AttackTick()
        {
            RecomputePosition();

            if (IntVec3Utility.ToIntVec3(groundPos) != lastPos)
            {
                lastPos = IntVec3Utility.ToIntVec3(groundPos);
                CheckAndApplyDamage(GenAttackCells.NineCells);
            }
        }

        protected override void RespawnPawn()
        {
            // 视觉效果
            DefDatabase<EffecterDef>.GetNamed("NCL_Crack").Spawn(Position, Map, 1f);
            SoundStarter.PlayOneShot(SoundDef.Named("Explosion_Bomb"), new TargetInfo(Position, Map));

            // 落地伤害
            CheckAndApplyDamage(GenAttackCells.TwentyFiveCells);

            // 切换到落地状态
            SetBehaviorMode("ChargeB");

            base.RespawnPawn();
        }

        public void SetBehaviorMode(string modeId)
        {
            _behaviorMode = SkillGraphics.GetBehaviorMode(SkillType.Charge, modeId);
        }

        private void CheckAndApplyDamage(List<IntVec3> cellOffsets)
        {
            foreach (IntVec3 offset in cellOffsets)
            {
                IntVec3 cell = Position + offset;
                if (!cell.InBounds(Map)) continue;

                // 创建物体列表的副本
                List<Thing> things = new List<Thing>(cell.GetThingList(Map));

                foreach (Thing thing in things) // 遍历副本
                {
                    if (ShouldDamageTarget(thing))
                    {
                        ApplyDamage(thing);
                    }
                }
            }
        }
        private bool ShouldDamageTarget(Thing target)
        {
            // 移除敌对关系检查，改为伤害所有单位
            return FlyingPawn != null &&
                   !hurtedTargets.Contains(target) &&
                   target != FlyingPawn; // 仍然排除自身
        }

        private void ApplyDamage(Thing target)
        {
            hurtedTargets.Add(target);

            if (target is Pawn pawn)
            {
                Vector3 direction = pawn.DrawPos - groundPos;

                // 击退效果
                Hediff_BeatenOff.BeatOff(pawn, direction, 6f, 10f);

                // 伤害应用
                DamageInfo damage = new DamageInfo(
                    DamageDefOf.Blunt,
                    40f,
                    armorPenetration: 0.88f,
                    angle: Vector3Utility.AngleFlat(direction),
                    instigator: FlyingPawn,
                    hitPart: null,
                    weapon: null,
                    category: DamageInfo.SourceCategory.ThingOrUnknown
                );

                pawn.TakeDamage(damage);
            }
        }

        private void RecomputePosition()
        {
            if (positionLastComputedTick == ticksFlying) return;

            if (angle < 0f)
            {
                angle = Vector3Utility.AngleFlat(DestinationPos - startVec);
            }

            positionLastComputedTick = ticksFlying;
            float progress = (float)ticksFlying / ticksFlightTime;
            groundPos = Vector3.Lerp(startVec, DestinationPos, progress);
        }

        public override void DynamicDrawPhaseAt(DrawPhase phase, Vector3 drawLoc, bool flip = false)
        {
            RecomputePosition();

            // 使用行为模式的图形
            if (_behaviorMode?.graphicDataA != null)
            {
                Graphic graphic = _behaviorMode.graphicDataA.Graphic;
                Mesh mesh = (angle > 180f) ?
                    MeshPool.GridPlaneFlip(graphic.drawSize) :
                    MeshPool.GridPlane(graphic.drawSize);

                Graphics.DrawMesh(
                    mesh,
                    groundPos,
                    Quaternion.AngleAxis(0f, Vector3.up),
                    graphic.MatSingle,
                    0
                );
            }
            // 后备方案：使用pawn的图形
            else if (FlyingPawn != null)
            {
                base.DynamicDrawPhaseAt(phase, groundPos, flip);
            }
        }

        public static Charge Make(ThingDef flyingDef, Ability ability, Pawn pawn, IntVec3 destCell,
                                 EffecterDef flightEffecterDef, SoundDef landingSound, bool flyWithCarriedThing)
        {
            pawn.rotationTracker.FaceCell(destCell);

            Charge charge = PawnFlyer.MakeFlyer(
                flyingDef,
                pawn,
                destCell,
                flightEffecterDef,
                landingSound,
                flyWithCarriedThing,
                null,
                null,
                default(LocalTargetInfo)
            ) as Charge;

            return charge;
        }
    }

    #endregion
}
