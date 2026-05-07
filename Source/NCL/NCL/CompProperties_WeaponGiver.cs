using System.Collections.Generic;
using RimWorld;
using Verse;

namespace NCL
{
    public class CompProperties_WeaponGiver : CompProperties
    {
        public List<ThingDef> weaponPool; // 武器池（最多3个）
        public List<int> cooldownTicks;   // 每个武器的冷却时间（ticks）
        public int checkIntervalTicks = 60; // 检查间隔（默认60 ticks = 1秒）
        public bool randomizeIfMultipleAvailable = false; // 多个武器可用时是否随机选择

        public CompProperties_WeaponGiver()
        {
            compClass = typeof(CompWeaponGiver);
        }

        public override void ResolveReferences(ThingDef parentDef)
        {
            base.ResolveReferences(parentDef);

            // 确保武器池不超过3个
            if (weaponPool != null && weaponPool.Count > 3)
            {
                Log.Warning($"CompProperties_WeaponGiver: weaponPool has more than 3 weapons, truncating to first 3.");
                weaponPool = weaponPool.GetRange(0, 3);
            }

            // 如果 cooldownTicks 未设置，默认每个武器冷却 300 ticks（5秒）
            if (cooldownTicks == null || cooldownTicks.Count != weaponPool.Count)
            {
                cooldownTicks = new List<int>();
                for (int i = 0; i < weaponPool.Count; i++)
                {
                    cooldownTicks.Add(300); // 默认 5秒
                }
            }
        }
    }
}



namespace NCL
{
    public class CompWeaponGiver : ThingComp
    {
        private CompProperties_WeaponGiver Props => (CompProperties_WeaponGiver)props;
        private int lastCheckTick = -1;
        private List<int> lastUsedTicks; // 记录每个武器最后使用的时间

        public override void Initialize(CompProperties props)
        {
            base.Initialize(props);
            lastUsedTicks = new List<int>();
            for (int i = 0; i < Props.weaponPool.Count; i++)
            {
                lastUsedTicks.Add(-1); // -1 表示从未使用过
            }
        }

        public override void CompTick()
        {
            base.CompTick();

            // 检查是否到达检查间隔
            if (Find.TickManager.TicksGame < lastCheckTick + Props.checkIntervalTicks)
                return;

            lastCheckTick = Find.TickManager.TicksGame;

            // 确保 parent 是一个 Pawn
            if (!(parent is Pawn pawn))
                return;

            // 检查是否已经有武器
            if (pawn.equipment?.Primary != null)
                return;

            // 如果武器池为空，直接返回
            if (Props.weaponPool == null || Props.weaponPool.Count == 0)
            {
                Log.Warning($"CompWeaponGiver: No weapons in pool for {pawn}");
                return;
            }

            // 收集所有可用的武器索引
            List<int> availableWeapons = new List<int>();
            for (int i = 0; i < Props.weaponPool.Count; i++)
            {
                int lastUsedTick = lastUsedTicks[i];
                int cooldownTicks = Props.cooldownTicks[i];

                if (lastUsedTick == -1 || Find.TickManager.TicksGame >= lastUsedTick + cooldownTicks)
                {
                    availableWeapons.Add(i);
                }
            }

            // 如果有可用武器
            if (availableWeapons.Count > 0)
            {
                int weaponIndexToGive;
                if (Props.randomizeIfMultipleAvailable && availableWeapons.Count > 1)
                {
                    weaponIndexToGive = availableWeapons.RandomElement(); // 随机选择
                }
                else
                {
                    weaponIndexToGive = availableWeapons[0]; // 默认选第一个可用的
                }
                GiveWeapon(pawn, weaponIndexToGive);
            }
        }

        private void GiveWeapon(Pawn pawn, int weaponIndex)
        {
            ThingDef weaponDef = Props.weaponPool[weaponIndex];
            Thing weapon = ThingMaker.MakeThing(weaponDef);
            pawn.equipment.AddEquipment(weapon as ThingWithComps);

            // 记录使用时间
            lastUsedTicks[weaponIndex] = Find.TickManager.TicksGame;
        }
    }
}
