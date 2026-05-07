using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;
using Verse.AI;


namespace NyarsModPackTwo
{
    public class CompAbilityEffect_ShootRandomBullet : CompAbilityEffect
    {
        private static List<ThingDef> bulletCache = new List<ThingDef>();

        public CompProperties_ShootRandomBullet Props =>
            (CompProperties_ShootRandomBullet)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent.pawn;

            // 填充子弹缓存（如果需要）
            if (bulletCache.Count == 0)
            {
                bulletCache.AddRange(Props.bullets);
            }

            // 随机发射子弹
            for (int i = 0; i < Props.castCount.RandomInRange; i++)
            {
                if (bulletCache.Count == 0)
                {
                    bulletCache.AddRange(Props.bullets);
                }

                int index = Rand.Range(0, bulletCache.Count);
                ThingDef bulletDef = bulletCache[index];
                bulletCache.RemoveAt(index);

                LaunchBullet(caster, bulletDef);
            }
        }

        private void LaunchBullet(Pawn caster, ThingDef bulletDef)
        {
            Map map = caster.Map;
            Projectile projectile =
                (Projectile)GenSpawn.Spawn(bulletDef, caster.Position, map, WipeMode.Vanish);

            // 如果有追踪逻辑的特殊子弹
            if (projectile is Bullet_TracingEnemies tracingBullet)
            {
                tracingBullet.flyingAngle = Rand.Value * 360f;
                tracingBullet.trackingPosNow = caster.TrueCenter();
            }

            projectile.Launch(
                launcher: caster,
                origin: caster.TrueCenter(),
                usedTarget: default(LocalTargetInfo),
                intendedTarget: default(LocalTargetInfo),
                hitFlags: ProjectileHitFlags.IntendedTarget | ProjectileHitFlags.NonTargetWorld,
                preventFriendlyFire: false,
                equipment: null,
                targetCoverDef: null
            );
        }
    }

    public class CompProperties_ShootRandomBullet : CompProperties_AbilityEffect
    {
        public List<ThingDef> bullets;
        public IntRange castCount = new IntRange(1, 1);

        public CompProperties_ShootRandomBullet()
        {
            compClass = typeof(CompAbilityEffect_ShootRandomBullet);
        }
    }
}


namespace NyarsModPackTwo
{
    // Token: 0x02000002 RID: 2
    public class Bullet_TracingEnemies : Bullet
    {
        // Token: 0x17000001 RID: 1
        // (get) Token: 0x06000001 RID: 1 RVA: 0x00002050 File Offset: 0x00000250
        public override Vector3 ExactPosition
        {
            get
            {
                return this.trackingPosNow + Vector3.up * this.def.Altitude;
            }
        }

        // Token: 0x17000002 RID: 2
        // (get) Token: 0x06000002 RID: 2 RVA: 0x00002072 File Offset: 0x00000272
        public override Quaternion ExactRotation
        {
            get
            {
                return Quaternion.AngleAxis(this.flyingAngle, Vector3.up);
            }
        }

        // Token: 0x17000003 RID: 3
        // (get) Token: 0x06000003 RID: 3 RVA: 0x00002084 File Offset: 0x00000284
        private float TargetAngle
        {
            get
            {
                return (this.trackingCell.ToVector3() - this.trackingPosNow).AngleFlat();
            }
        }

        // Token: 0x17000004 RID: 4
        // (get) Token: 0x06000004 RID: 4 RVA: 0x000020A4 File Offset: 0x000002A4
        private ModExtension_BulletProperties Props
        {
            get
            {
                ModExtension_BulletProperties result;
                if ((result = this._props) == null)
                {
                    result = (this._props = this.def.GetModExtension<ModExtension_BulletProperties>());
                }
                return result;
            }
        }

        // Token: 0x06000005 RID: 5 RVA: 0x000020D0 File Offset: 0x000002D0
        protected override void Tick()
        {
            bool landed = this.landed;
            if (!landed)
            {
                bool flag = this._flyingTime >= this.Props.maxFlyingTime;
                if (flag)
                {
                    this.Impact(null, false);
                }
                else
                {
                    bool flag2 = this._flyingTime >= this.Props.ticksBeforeTracing && (this._flyingTime - this.Props.ticksBeforeTracing) % this.Props.ticksBetweenFindTarget == 0;
                    if (flag2)
                    {
                        this.UpdateTarget();
                    }
                    this.UpdateTargetCell();
                    this._flyingTime++;
                    Vector3 vector = this.trackingCell.ToVector3();
                    float num = vector.x - this.trackingPosNow.x;
                    float num2 = vector.z - this.trackingPosNow.z;
                    float num3 = num * num + num2 * num2;
                    bool flag3 = num3 <= this.Props.flyingStep * this.Props.flyingStep * 3f;
                    if (flag3)
                    {
                        this.ticksToImpact = 0;
                        base.Position = this.trackingCell;
                        bool flag4 = this.trackingTargetThing != null && this.trackingTargetThing.Spawned;
                        if (flag4)
                        {
                            this.Impact(this.trackingTargetThing, false);
                        }
                        else
                        {
                            this.Impact(null, false);
                        }
                    }
                    else
                    {
                        Vector3 exactPosition = this.ExactPosition;
                        this.trackingPosNow += new Vector3((float)Math.Sin((double)(this.flyingAngle / 180f * 3.14159f)), 0f, (float)Math.Cos((double)(this.flyingAngle / 180f * 3.14159f))) * this.Props.flyingStep;
                        bool flag5 = !this.trackingPosNow.InBounds(base.Map);
                        if (flag5)
                        {
                            this.ticksToImpact = 0;
                            this.Destroy(DestroyMode.Vanish);
                        }
                        else
                        {
                            Vector3 exactPosition2 = this.ExactPosition;
                            bool flag6 = this.Props.trailFleck != null;
                            if (flag6)
                            {
                                FleckMaker.ConnectingLine(exactPosition, exactPosition2, this.Props.trailFleck, base.Map, 0.1f);
                            }
                            bool flag7 = (bool)Bullet_TracingEnemies._interceptCheck.Invoke(this, this._interceptParams);
                            if (!flag7)
                            {
                                base.Position = this.trackingPosNow.ToIntVec3();
                                this.Rotate();
                            }
                        }
                    }
                }
            }
        }

        // Token: 0x06000006 RID: 6 RVA: 0x00002333 File Offset: 0x00000533
        private void CleanupReferences()
        {
            this.trackingTargetThing = null;
            this.trackingCell = IntVec3.Invalid;
        }

        // Token: 0x06000007 RID: 7 RVA: 0x00002348 File Offset: 0x00000548
        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            this.CleanupReferences();
            bool flag = this.Props.impactFleck != null;
            if (flag)
            {
                FleckCreationData fleckData = new FleckCreationData
                {
                    def = this.Props.impactFleck,
                    spawnPosition = this.trackingPosNow,
                    scale = 1f,
                    rotation = (float)Rand.Range(0, 360),
                    ageTicksOverride = -1
                };
                base.Map.flecks.CreateFleck(fleckData);
            }
            base.Impact(hitThing, blockedByShield);
        }

        // Token: 0x06000008 RID: 8 RVA: 0x000023DC File Offset: 0x000005DC
        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            this.CleanupReferences();
            base.Destroy(mode);
        }

        // Token: 0x06000009 RID: 9 RVA: 0x000023F0 File Offset: 0x000005F0
        private bool TryHitPawnAtCurrentCell()
        {
            Thing thing = base.Position.GetThingList(base.Map).Find(new Predicate<Thing>(this.CanBeHitAtCurrentCell));
            bool flag = thing == null;
            bool result;
            if (flag)
            {
                result = false;
            }
            else
            {
                this.Impact(thing, false);
                result = true;
            }
            return result;
        }

        // Token: 0x0600000A RID: 10 RVA: 0x0000243C File Offset: 0x0000063C
        private bool CanBeHitAtCurrentCell(Thing thing)
        {
            Pawn pawn = thing as Pawn;
            return pawn != null && this.IsEnemey(pawn);
        }

        // Token: 0x0600000B RID: 11 RVA: 0x0000245D File Offset: 0x0000065D
        private bool IsEnemey(Pawn pawn)
        {
            return this.launcher.HostileTo(pawn) && !pawn.Downed;
        }

        // Token: 0x0600000C RID: 12 RVA: 0x0000247C File Offset: 0x0000067C
        private void UpdateTarget()
        {
            bool flag = this.trackingTargetThing != null && (!this.trackingTargetThing.Spawned || this.trackingTargetThing.Map != base.Map);
            if (flag)
            {
                this.trackingTargetThing = null;
            }
            bool flag2 = this.trackingTargetThing != null;
            if (!flag2)
            {
                this._localTargetCache.Clear();
                this._localTargetCache.AddRange(Enumerable.Where<Pawn>(base.Map.mapPawns.AllPawnsSpawned, new Func<Pawn, bool>(this.IsEnemey)));
                bool flag3 = this._localTargetCache.Count == 0;
                if (!flag3)
                {
                    this._localTargetCache.SortBy((Thing x) => (x.Position - base.Position).SqrMagnitude, (Thing y) => y.thingIDNumber);
                    this.trackingTargetThing = this._localTargetCache[0];
                }
            }
        }

        // Token: 0x0600000D RID: 13 RVA: 0x00002574 File Offset: 0x00000774
        private void UpdateTargetCell()
        {
            bool flag = this.trackingTargetThing == null;
            if (flag)
            {
                this.trackingCell = IntVec3.Invalid;
            }
            else
            {
                bool spawned = this.trackingTargetThing.Spawned;
                if (spawned)
                {
                    this.trackingCell = this.trackingTargetThing.Position;
                }
            }
        }

        // Token: 0x0600000E RID: 14 RVA: 0x000025C0 File Offset: 0x000007C0
        private void Rotate()
        {
            bool flag = this.trackingCell == IntVec3.Invalid;
            if (!flag)
            {
                float targetAngle = this.TargetAngle;
                float num = this.flyingAngle - targetAngle;
                float num2 = this.Props.rotatingStep * ((this._flyingTime < 60 + this.Props.ticksBeforeTracing) ? 1f : ((float)(this._flyingTime - 60 - this.Props.ticksBeforeTracing) / 15f + 1f));
                bool flag2 = num > 180f;
                if (flag2)
                {
                    num -= 360f;
                }
                bool flag3 = num < -180f;
                if (flag3)
                {
                    num += 360f;
                }
                bool flag4 = num > num2;
                if (flag4)
                {
                    this.flyingAngle -= num2;
                }
                else
                {
                    bool flag5 = num < -num2;
                    if (flag5)
                    {
                        this.flyingAngle += num2;
                    }
                    else
                    {
                        this.flyingAngle = targetAngle;
                    }
                }
                this.flyingAngle %= 360f;
            }
        }

        // Token: 0x0600000F RID: 15 RVA: 0x000026C8 File Offset: 0x000008C8
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look<int>(ref this._flyingTime, "_flyingTime", 0, false);
            Scribe_References.Look<Thing>(ref this.trackingTargetThing, "trackingTargetThing", false);
            Scribe_Values.Look<IntVec3>(ref this.trackingCell, "trackingCell", default(IntVec3), false);
            Scribe_Values.Look<float>(ref this.flyingAngle, "flyingAngle", 0f, false);
            Scribe_Values.Look<Vector3>(ref this.trackingPosNow, "trackingPosNow", default(Vector3), false);
        }

        // Token: 0x04000001 RID: 1
        private static readonly MethodInfo _interceptCheck = typeof(Projectile).GetMethod("CheckForFreeInterceptBetween", BindingFlags.Instance | BindingFlags.NonPublic);

        // Token: 0x04000002 RID: 2
        private readonly object[] _interceptParams = new object[2];

        // Token: 0x04000003 RID: 3
        private readonly List<Thing> _localTargetCache = new List<Thing>();

        // Token: 0x04000004 RID: 4
        private ModExtension_BulletProperties _props;

        // Token: 0x04000005 RID: 5
        private int _flyingTime;

        // Token: 0x04000006 RID: 6
        public Thing trackingTargetThing;

        // Token: 0x04000007 RID: 7
        public IntVec3 trackingCell = IntVec3.Invalid;

        // Token: 0x04000008 RID: 8
        public float flyingAngle;

        // Token: 0x04000009 RID: 9
        public Vector3 trackingPosNow;
    }
}

namespace NyarsModPackTwo
{
    // Token: 0x02000003 RID: 3
    public class ModExtension_BulletProperties : DefModExtension
    {
        // Token: 0x0400000A RID: 10
        public FleckDef trailFleck;

        // Token: 0x0400000B RID: 11
        public FleckDef impactFleck;

        // Token: 0x0400000C RID: 12
        public int ticksBeforeTracing = 30;

        // Token: 0x0400000D RID: 13
        public int maxFlyingTime = 900;

        // Token: 0x0400000E RID: 14
        public float rotatingStep = 6f;

        // Token: 0x0400000F RID: 15
        public float flyingStep = 0.4f;

        // Token: 0x04000010 RID: 16
        public int ticksBetweenFindTarget = 30;
    }
}

namespace NyarsModPackTwo
{
    // Token: 0x02000005 RID: 5
    public class ModExtension_BulletsDefs : DefModExtension
    {
        // Token: 0x04000011 RID: 17
        public List<ThingDef> bullets = new List<ThingDef>();

        // Token: 0x04000012 RID: 18
        public IntRange castCount;
    }
}

namespace NyarsModPackTwo
{
    // Token: 0x02000004 RID: 4
    public class PlaceWorker_ShowTurretRadius : PlaceWorker
    {
        // Token: 0x06000014 RID: 20 RVA: 0x000027F8 File Offset: 0x000009F8
        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            VerbProperties verbProperties = ((ThingDef)checkingDef).building.turretGunDef.Verbs[0];
            bool flag = verbProperties.range > 0f;
            if (flag)
            {
                GenDraw.DrawRadiusRing(loc, verbProperties.range);
            }
            bool flag2 = verbProperties.minRange > 0f;
            if (flag2)
            {
                GenDraw.DrawRadiusRing(loc, verbProperties.minRange);
            }
            return true;
        }
    }
}
