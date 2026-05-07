using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace NCL
{
    // 辅助静态类，用于存放扩展方法
    public static class DictionaryExtensions
    {
        public static void RemoveAll<TKey, TValue>(this Dictionary<TKey, TValue> dict,
            System.Func<KeyValuePair<TKey, TValue>, bool> predicate)
        {
            foreach (var pair in dict.Where(predicate).ToList())
            {
                dict.Remove(pair.Key);
            }
        }
    }

    public class Verb_JumpAndCloak : Verb_Jump
    {
        private const int CooldownTicks = 60;
        private static Dictionary<Pawn, int> _lastUseTicks = new Dictionary<Pawn, int>();
        private static readonly HediffDef CloakedHediff = HediffDef.Named("GD_Hediff_CloakedEffect");

        private bool IsOnCooldown(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.Dead)
            {
                if (_lastUseTicks.ContainsKey(pawn))
                    _lastUseTicks.Remove(pawn);
                return false;
            }

            return _lastUseTicks.TryGetValue(pawn, out int lastTick) &&
                   Find.TickManager.TicksGame - lastTick < CooldownTicks;
        }

        public override bool Available()
        {
            return base.Available() && !IsOnCooldown(this.CasterPawn);
        }

        protected override bool TryCastShot()
        {
            if (!base.TryCastShot())
                return false;

            Pawn caster = this.CasterPawn;
            if (CloakedHediff == null || caster == null)
                return false;

            // 移除现有同类效果
            Hediff existing = caster.health.hediffSet.GetFirstHediffOfDef(CloakedHediff);
            if (existing != null)
                caster.health.RemoveHediff(existing);

            // 应用新效果
            Hediff cloaked = HediffMaker.MakeHediff(CloakedHediff, caster, null);
            caster.health.AddHediff(cloaked, null, null, null);

            _lastUseTicks[caster] = Find.TickManager.TicksGame;
            return true;
        }

        public override void ExposeData()
        {
            base.ExposeData();

            // 修复字典序列化问题
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                List<Pawn> keys = _lastUseTicks.Keys.Where(p => p != null).ToList();
                List<int> values = keys.Select(k => _lastUseTicks[k]).ToList();
                Scribe_Collections.Look(ref keys, "_lastUseTicks_keys", LookMode.Reference);
                Scribe_Collections.Look(ref values, "_lastUseTicks_values", LookMode.Value);
            }
            else if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                List<Pawn> keys = null;
                List<int> values = null;
                Scribe_Collections.Look(ref keys, "_lastUseTicks_keys", LookMode.Reference);
                Scribe_Collections.Look(ref values, "_lastUseTicks_values", LookMode.Value);

                _lastUseTicks = new Dictionary<Pawn, int>();
                if (keys != null && values != null && keys.Count == values.Count)
                {
                    for (int i = 0; i < keys.Count; i++)
                    {
                        if (keys[i] != null)
                            _lastUseTicks[keys[i]] = values[i];
                    }
                }
            }

            // 加载后清理无效引用
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                var toRemove = _lastUseTicks.Where(pair => pair.Key == null || pair.Key.Destroyed).ToList();
                foreach (var pair in toRemove)
                {
                    _lastUseTicks.Remove(pair.Key);
                }
            }
        }
    }
}
