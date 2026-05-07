using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using UnityEngine;
using HarmonyLib;
using System.Linq;
using Verse.Sound;
using static System.Collections.Specialized.BitVector32;

namespace NCL
{
    // 定义弹药系统属性类
    public class CompProperties_AdvancedAmmo : CompProperties
    {
        // 主要弹药定义和属性
        public GG_Properties_RandomProjectile projectile1, projectile2, projectile3;

        // 每种弹药类型对应的次要弹药定义（包括默认弹药）
        public ThingDef secondaryProjectile0, secondaryProjectile1, secondaryProjectile2, secondaryProjectile3;

        // 每种弹药类型对应的主要和次要发射数量（包括默认弹药）
        public int primaryProjectileCount0 = 1, secondaryProjectileCount0 = 1;
        public int primaryProjectileCount1 = 1, secondaryProjectileCount1 = 1;
        public int primaryProjectileCount2 = 1, secondaryProjectileCount2 = 1;
        public int primaryProjectileCount3 = 1, secondaryProjectileCount3 = 1;

        // 次要弹药音效
        public SoundDef secondarySoundCast, secondarySoundCastTail;

        // 发射模式设置
        public bool isBonusShot, isSimultaneousShot;

        // 每种弹药类型对应的自定义UI按钮纹理路径
        public string customTexturePath0;  // 默认弹药的自定义纹理路径
        public string customTexturePath1;  // 弹药类型1的自定义纹理路径
        public string customTexturePath2;  // 弹药类型2的自定义纹理路径
        public string customTexturePath3;  // 弹药类型3的自定义纹理路径
        public CompProperties_AdvancedAmmo() => this.compClass = typeof(Comp_AdvancedAmmo);
    }

    // 随机弹药属性类
    public class GG_Properties_RandomProjectile
    {
        public ThingDef projectile = null;
        public float weight = 1f;
    }

    // 高级弹药系统组件
    public class Comp_AdvancedAmmo : ThingComp
    {
        public int selectedProjectileType = 0;
        private bool usingSecondaryProjectile = false;

        // 属性访问器
        public CompProperties_AdvancedAmmo Props => (CompProperties_AdvancedAmmo)this.props;

        // 获取持有武器的角色
        protected virtual Pawn GetUser => base.ParentHolder is Pawn_EquipmentTracker ? (Pawn)base.ParentHolder.ParentHolder : null;

        // 初始化或加载后的设置
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (respawningAfterLoad) return;

            // 构建有效弹药列表
            var defaultProjectile = new GG_Properties_RandomProjectile();
            List<GG_Properties_RandomProjectile> list = new List<GG_Properties_RandomProjectile> { defaultProjectile };

            if (Props.projectile1?.projectile != null) list.Add(Props.projectile1);
            if (Props.projectile2?.projectile != null) list.Add(Props.projectile2);
            if (Props.projectile3?.projectile != null) list.Add(Props.projectile3);

            // 随机选择弹药类型
            var selectedProjectile = list.RandomElementByWeight(p => p.weight);

            // 设置初始弹药类型
            if (selectedProjectile == Props.projectile1) selectedProjectileType = 1;
            else if (selectedProjectile == Props.projectile2) selectedProjectileType = 2;
            else if (selectedProjectile == Props.projectile3) selectedProjectileType = 3;
            else selectedProjectileType = 0; // 明确设置默认弹药
        }

        // 查找默认弹药
        public virtual ThingDef FindDefaultAmmo
        {
            get
            {
                foreach (VerbProperties verb in parent.def.Verbs)
                    if (verb.defaultProjectile != null)
                        return verb.defaultProjectile;
                return null;
            }
        }

        // 获取当前选择的主要弹药
        public ThingDef GetPrimaryProjectile() => selectedProjectileType switch
        {
            1 => Props.projectile1?.projectile,
            2 => Props.projectile2?.projectile,
            3 => Props.projectile3?.projectile,
            _ => FindDefaultAmmo
        };

        // 获取当前选择的次要弹药
        public ThingDef GetSecondaryProjectile() => selectedProjectileType switch
        {
            0 => Props.secondaryProjectile0,
            1 => Props.secondaryProjectile1,
            2 => Props.secondaryProjectile2,
            3 => Props.secondaryProjectile3,
            _ => null
        };

        // 获取当前主要弹药的发射数量
        public int GetPrimaryProjectileCount() => selectedProjectileType switch
        {
            0 => Props.primaryProjectileCount0,
            1 => Props.primaryProjectileCount1,
            2 => Props.primaryProjectileCount2,
            3 => Props.primaryProjectileCount3,
            _ => 1
        };

        // 获取当前次要弹药的发射数量
        public int GetSecondaryProjectileCount() => selectedProjectileType switch
        {
            0 => Props.secondaryProjectileCount0,
            1 => Props.secondaryProjectileCount1,
            2 => Props.secondaryProjectileCount2,
            3 => Props.secondaryProjectileCount3,
            _ => 0
        };

        // 检查是否有次要弹药
        public bool HasSecondaryProjectile() => GetSecondaryProjectile() != null;

        // 设置当前使用的弹药类型
        public void SetUsingSecondaryProjectile(bool value) => usingSecondaryProjectile = value;

        // 获取当前是否使用次要弹药
        public bool IsUsingSecondaryProjectile() => usingSecondaryProjectile;

        // 保存和加载组件状态
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref selectedProjectileType, "selectedProjectileType", 0);
            Scribe_Values.Look(ref usingSecondaryProjectile, "usingSecondaryProjectile", false);
        }

        // 切换弹药类型
        public void SetProjectileType(int newProjectileType)
        {
            selectedProjectileType = newProjectileType;
            usingSecondaryProjectile = false;  // 重置次要弹药状态
        }

        // 生成装备的额外UI控件
        // 然后，修改 CompGetGizmosExtra 方法以使用自定义纹理路径
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            // 获取当前组件的用户（角色）
            Pawn pawn = this.GetUser;

            // 如果角色为空，或者角色不属于玩家阵营，或者当前选中的对象不是该角色，则直接退出
            if (pawn?.Faction != Faction.OfPlayer || Find.Selector.SingleSelectedThing != pawn)
                yield break;

            // 定义默认弹药的标签文本
            string defaultAmmoLabel = "GG_Keyed_DefaultAmmo".Translate();

            // 根据当前选择的弹药类型执行不同的逻辑
            switch (selectedProjectileType)
            {
                // 当选择的弹药类型为0，并且第一个弹药类型有效时
                case 0 when Props.projectile1?.projectile != null:
                    // 获取纹理路径，优先使用自定义路径
                    string texPath0 = !string.IsNullOrEmpty(Props.customTexturePath1)
                        ? Props.customTexturePath1
                        : FindDefaultAmmo.graphicData.texPath;

                    // 创建一个Gizmo，用于切换到第一种弹药类型
                    yield return CreateGizmo("GG_Keyed_ChooseAmmo".Translate() + defaultAmmoLabel,
                                            texPath0,
                                            () => SetProjectileType(1));
                    break;

                // 当选择的弹药类型为1时
                case 1:
                    // 如果第二种弹药类型有效，则创建一个Gizmo，用于切换到第二种弹药类型
                    if (Props.projectile2?.projectile != null)
                    {
                        string texPath1 = !string.IsNullOrEmpty(Props.customTexturePath2)
                            ? Props.customTexturePath2
                            : Props.projectile1.projectile.graphicData.texPath;

                        yield return CreateGizmo("GG_Keyed_ChooseAmmo".Translate() + Props.projectile1.projectile.label.Translate(),
                                                texPath1,
                                                () => SetProjectileType(2));
                    }

                    // 如果第三种弹药类型有效，并且第二种弹药类型无效，则创建一个Gizmo，用于切换到第三种弹药类型
                    if (Props.projectile3?.projectile != null && (Props.projectile2 == null || Props.projectile2.projectile == null))
                    {
                        string texPath1to3 = !string.IsNullOrEmpty(Props.customTexturePath3)
                            ? Props.customTexturePath3
                            : Props.projectile1.projectile.graphicData.texPath;

                        yield return CreateGizmo("GG_Keyed_ChooseAmmo".Translate() + Props.projectile1.projectile.label.Translate(),
                                                texPath1to3,
                                                () => SetProjectileType(3));
                    }

                    // 创建一个Gizmo，用于切换回默认弹药类型
                    string texPath1to0 = !string.IsNullOrEmpty(Props.customTexturePath0)
                        ? Props.customTexturePath0
                        : Props.projectile1.projectile.graphicData.texPath;

                    yield return CreateGizmo("GG_Keyed_ChooseAmmo".Translate() + Props.projectile1.projectile.label.Translate(),
                                            texPath1to0,
                                            () => SetProjectileType(0));
                    break;

                // 当选择的弹药类型为2时
                case 2:
                    // 如果第三种弹药类型有效，则创建一个Gizmo，用于切换到第三种弹药类型
                    if (Props.projectile3?.projectile != null)
                    {
                        string texPath2to3 = !string.IsNullOrEmpty(Props.customTexturePath3)
                            ? Props.customTexturePath3
                            : Props.projectile2.projectile.graphicData.texPath;

                        yield return CreateGizmo("GG_Keyed_ChooseAmmo".Translate() + Props.projectile2.projectile.label.Translate(),
                                                texPath2to3,
                                                () => SetProjectileType(3));
                    }

                    // 创建一个Gizmo，用于切换回默认弹药类型
                    string texPath2to0 = !string.IsNullOrEmpty(Props.customTexturePath0)
                        ? Props.customTexturePath0
                        : Props.projectile2.projectile.graphicData.texPath;

                    yield return CreateGizmo("GG_Keyed_ChooseAmmo".Translate() + Props.projectile2.projectile.label.Translate(),
                                            texPath2to0,
                                            () => SetProjectileType(0));
                    break;

                // 当选择的弹药类型为3时
                case 3:
                    // 创建一个Gizmo，用于切换回默认弹药类型
                    string texPath3to0 = !string.IsNullOrEmpty(Props.customTexturePath0)
                        ? Props.customTexturePath0
                        : Props.projectile3.projectile.graphicData.texPath;

                    yield return CreateGizmo("GG_Keyed_ChooseAmmo".Translate() + Props.projectile3.projectile.label.Translate(),
                                            texPath3to0,
                                            () => SetProjectileType(0));
                    break;
            }
        }

        // 创建UI按钮辅助方法
        // 定义一个私有方法，用于创建 Gizmo（小工具）命令
        private Command_Action CreateGizmo(string label, string iconPath, Action action)
        {
            // 返回一个 Command_Action 对象，并初始化其属性
            return new Command_Action
            {
                // 设置命令的默认标签（显示名称）
                defaultLabel = label,
                // 通过 ContentFinder 查找并加载指定路径的图标纹理
                icon = ContentFinder<Texture2D>.Get(iconPath, true),
                // 设置命令执行时的操作（回调函数）
                action = action
            };
        }
    }

    // 高级射击动词类，整合双弹药和弹药切换功能
    public class Verb_AdvancedShoot : Verb_Shoot
    {
        private bool usingSecondaryProjectile = false;
        private int primaryProjectileShotsFired = 0, secondaryProjectileShotsFired = 0;
        private bool initialized = false;

        // 组件和属性访问器
        protected Comp_AdvancedAmmo AmmoComp => base.EquipmentSource?.GetComp<Comp_AdvancedAmmo>();
        protected int PrimaryProjectileCount => AmmoComp?.GetPrimaryProjectileCount() ?? 1;
        protected int SecondaryProjectileCount => AmmoComp?.GetSecondaryProjectileCount() ?? 0;
        protected bool IsBonusShot => AmmoComp?.Props.isBonusShot ?? false;
        protected bool IsSimultaneousShot => AmmoComp?.Props.isSimultaneousShot ?? false;
        protected SoundDef SecondarySoundCast => AmmoComp?.Props.secondarySoundCast;
        protected SoundDef SecondarySoundCastTail => AmmoComp?.Props.secondarySoundCastTail;

        // 重写Projectile属性，根据条件返回不同的弹药定义
        public override ThingDef Projectile
        {
            get
            {
                if (base.EquipmentSource != null)
                {
                    // 检查可更换弹丸组件
                    var comp = base.EquipmentSource.GetComp<CompChangeableProjectile>();
                    if (comp?.Loaded == true)
                        return comp.Projectile;

                    // 检查高级弹药组件
                    var ammoComp = AmmoComp;
                    if (ammoComp != null)
                    {
                        ammoComp.SetUsingSecondaryProjectile(usingSecondaryProjectile);

                        if (usingSecondaryProjectile && ammoComp.HasSecondaryProjectile())
                            return ammoComp.GetSecondaryProjectile();

                        var primaryProjectile = ammoComp.GetPrimaryProjectile();
                        if (primaryProjectile != null)
                            return primaryProjectile;
                    }
                }

                return verbProps.defaultProjectile;
            }
        }

        // 重写WarmupComplete方法，重置射击状态
        public override void WarmupComplete()
        {
            var ammoComp = AmmoComp;
            if (ammoComp?.HasSecondaryProjectile() == true)
            {
                usingSecondaryProjectile = false;
                primaryProjectileShotsFired = secondaryProjectileShotsFired = 0;
                initialized = false;
                ammoComp.SetUsingSecondaryProjectile(false);
            }

            base.WarmupComplete();
        }

        // 保存和加载射击状态
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref usingSecondaryProjectile, "usingSecondaryProjectile", defaultValue: false);
            Scribe_Values.Look(ref primaryProjectileShotsFired, "primaryProjectileShotsFired", 0);
            Scribe_Values.Look(ref secondaryProjectileShotsFired, "secondaryProjectileShotsFired", 0);
            Scribe_Values.Look(ref initialized, "initialized", defaultValue: false);
        }

        // 重写TryCastShot方法，根据不同模式处理射击
        protected override bool TryCastShot()
        {
            var ammoComp = AmmoComp;

            // 没有高级弹药组件或次要弹药，执行常规射击
            if (ammoComp == null || !ammoComp.HasSecondaryProjectile())
                return base.TryCastShot();

            // 初始化射击状态
            if (!initialized)
            {
                usingSecondaryProjectile = false;
                primaryProjectileShotsFired = secondaryProjectileShotsFired = 0;
                initialized = true;
                ammoComp.SetUsingSecondaryProjectile(false);
            }

            bool result = true;

            // 同时射击模式
            if (IsSimultaneousShot)
            {
                // 主要弹药射击
                bool primaryResult = base.TryCastShot();

                // 切换到次要弹药并射击
                usingSecondaryProjectile = true;
                ammoComp.SetUsingSecondaryProjectile(true);

                // 播放次要弹药音效
                PlaySecondarySound();

                // 次要弹药射击
                bool secondaryResult = base.TryCastShot();

                // 重置为主要弹药
                usingSecondaryProjectile = false;
                ammoComp.SetUsingSecondaryProjectile(false);

                result = primaryResult && secondaryResult;
            }
            // 顺序射击模式
            else if (!usingSecondaryProjectile)
            {
                // 主要弹药射击
                result = base.TryCastShot();
                primaryProjectileShotsFired++;

                // 检查是否完成主要弹药射击
                if (primaryProjectileShotsFired >= PrimaryProjectileCount)
                {
                    usingSecondaryProjectile = true;
                    ammoComp.SetUsingSecondaryProjectile(true);
                    secondaryProjectileShotsFired = 0;
                }
            }
            else
            {
                // 次要弹药射击
                PlaySecondarySound();
                result = base.TryCastShot();
                secondaryProjectileShotsFired++;

                // 检查是否完成次要弹药射击
                if (secondaryProjectileShotsFired >= SecondaryProjectileCount)
                {
                    usingSecondaryProjectile = false;
                    ammoComp.SetUsingSecondaryProjectile(false);
                    primaryProjectileShotsFired = 0;
                }

                // 奖励射击模式处理
                if (IsBonusShot)
                {
                    bool wasUsingSecondary = usingSecondaryProjectile;
                    usingSecondaryProjectile = false;
                    ammoComp.SetUsingSecondaryProjectile(false);
                    base.TryCastShot();
                    usingSecondaryProjectile = wasUsingSecondary;
                    ammoComp.SetUsingSecondaryProjectile(wasUsingSecondary);
                }
            }

            return result;
        }

        // 播放次要弹药音效辅助方法
        private void PlaySecondarySound()
        {
            SecondarySoundCast?.PlayOneShot(new TargetInfo(caster.Position, caster.Map));
            SecondarySoundCastTail?.PlayOneShotOnCamera(caster.Map);
        }
    }

    // Harmony补丁，为角色添加装备的额外UI控件
    [HarmonyPatch(typeof(Pawn), "GetGizmos")]
    public static class TY_Pawn_GetGizmos_Patch
    {
        // 添加装备的额外UI控件到角色的UI控件列表中
        [HarmonyPostfix]
        public static void GetEquippedGizmos(Pawn __instance, ref IEnumerable<Gizmo> __result)
        {
            // 获取角色装备的主要武器
            // 从角色的装备中获取其当前装备的主要武器（Primary）
            var primary = __instance.equipment?.Primary;

            // 如果主要武器为空，则直接返回，不进行后续操作
            if (primary == null) return;

            // 获取高级弹药组件
            // 检查主要武器是否装备了 `Comp_AdvancedAmmo` 组件
            var comp = primary.GetComp<Comp_AdvancedAmmo>();

            // 如果组件存在，并且角色属于玩家阵营，则执行以下操作
            if (comp != null && __instance.Faction == Faction.OfPlayer)
                // 将组件提供的额外 Gizmo（用户界面操作按钮）添加到结果集合中
                __result = __result.Concat(comp.CompGetGizmosExtra());
        }
    }
}
