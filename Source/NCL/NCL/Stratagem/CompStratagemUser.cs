using System.Collections.Generic;
using NCL.Worm;
using RimWorld;
using UnityEngine;
using Verse;

namespace NCL.Stratagem
{
    public class CompProperties_StratagemUser : CompProperties
    {
        public CompProperties_StratagemUser()
        {
            compClass = typeof(CompStratagemUser);
        }
    }

    public class CompStratagemUser : ThingComp
    {
        private const int LoadoutSlotCount = 4;

        private bool isStratagemTargeting;

        private Pawn Pawn => parent as Pawn;

        public override void CompTick()
        {
            base.CompTick();
            if (isStratagemTargeting && !Find.Targeter.IsTargeting)
            {
                isStratagemTargeting = false;
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }

            Pawn pawn = Pawn;
            if (pawn == null || pawn.Faction != Faction.OfPlayer || pawn.Map == null)
            {
                yield break;
            }

            GameComponent_DiverRespawnManager manager = Current.Game?.GetComponent<GameComponent_DiverRespawnManager>();
            if (manager == null || !manager.DiverSystemEnabled)
            {
                yield break;
            }

            int diverSlotIndex = manager.GetSlotIndexForPawn(pawn);
            if (diverSlotIndex < 0)
            {
                yield break;
            }

            Map map = pawn.Map;
            StratagemDef reinforceDef = GetReinforceStratagemDef();
            if (reinforceDef != null)
            {
                Command_Action reinforce = new Command_Action
                {
                    icon = reinforceDef.IconTex ?? ContentFinder<Texture2D>.Get("Diver/SC-34/SC-34 Infiltrator_Head_south"),
                    defaultLabel = reinforceDef.LabelCap,
                    defaultDesc = reinforceDef.description,
                    action = () => BeginTargetingForStratagem(reinforceDef, loadoutIndex: -1, applyCooldown: false)
                };

                if (pawn.Dead)
                {
                    reinforce.Disable("MTW_Diver_ReinforceGizmo_Dead".Translate());
                }
                else
                {
                    string blockedReason = StratagemUtility.GetBlockedReason(reinforceDef, map);
                    if (!blockedReason.NullOrEmpty())
                    {
                        reinforce.Disable(blockedReason);
                    }
                }

                yield return reinforce;
            }

            if (!StratagemUtility.IsSystemEnabled())
            {
                yield break;
            }

            for (int loadoutIndex = 0; loadoutIndex < LoadoutSlotCount; loadoutIndex++)
            {
                int capturedLoadoutIndex = loadoutIndex;
                string defName = manager.GetSlotLoadoutDefName(diverSlotIndex, loadoutIndex);
                StratagemDef def = !defName.NullOrEmpty() ? DefDatabase<StratagemDef>.GetNamedSilentFail(defName) : null;
                StratagemDef capturedDef = def;

                Command_Action cmd = new Command_Action
                {
                    defaultLabel = def?.label ?? "NCL_Stratagem_EmptySlotLabel".Translate(loadoutIndex + 1).ToString(),
                    defaultDesc = def?.description ?? "NCL_Stratagem_EmptySlotDesc".Translate().ToString(),
                    icon = def?.IconTex,
                    action = () => BeginTargetingForStratagem(capturedDef, capturedLoadoutIndex, applyCooldown: true)
                };

                if (def == null)
                {
                    cmd.Disable("NCL_Stratagem_EmptySlotDisabled".Translate());
                    yield return cmd;
                    continue;
                }

                int now = Find.TickManager?.TicksGame ?? 0;
                int readyTick = manager.GetStratagemCooldownUntilTick(diverSlotIndex, loadoutIndex);
                if (now < readyTick)
                {
                    int remain = Mathf.Max(0, readyTick - now);
                    cmd.Disable("NCL_Stratagem_Cooldown".Translate((remain / 60f).ToString("F1")));
                }
                else
                {
                    string blockedReason = StratagemUtility.GetBlockedReason(def, map);
                    if (!blockedReason.NullOrEmpty())
                    {
                        cmd.Disable(blockedReason);
                    }
                }

                yield return cmd;
            }
        }

        private static StratagemDef GetReinforceStratagemDef()
        {
            return DefDatabase<StratagemDef>.GetNamedSilentFail(StratagemDef.ReinforceDefName);
        }

        private void BeginTargetingForStratagem(StratagemDef def, int loadoutIndex, bool applyCooldown)
        {
            Pawn pawn = Pawn;
            if (pawn == null || pawn.Map == null || def == null)
            {
                return;
            }

            bool isReinforce = StratagemUtility.IsReinforceStratagem(def);
            if (!isReinforce && !StratagemUtility.IsSystemEnabled())
            {
                Messages.Message("NCL_Stratagem_SystemDisabled".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }

            Map map = pawn.Map;
            string blockedReason = StratagemUtility.GetBlockedReason(def, map);
            if (!blockedReason.NullOrEmpty())
            {
                Messages.Message(blockedReason, MessageTypeDefOf.RejectInput);
                return;
            }

            GameComponent_DiverRespawnManager manager = Current.Game?.GetComponent<GameComponent_DiverRespawnManager>();
            int diverSlotIndex = manager?.GetSlotIndexForPawn(pawn) ?? -1;
            if (manager == null || diverSlotIndex < 0)
            {
                return;
            }

            isStratagemTargeting = true;

            Find.Targeter.BeginTargeting(
                new TargetingParameters
                {
                    canTargetLocations = true,
                    canTargetPawns = false,
                    canTargetBuildings = false,
                    mapObjectTargetsMustBeAutoAttackable = false
                },
                target =>
                {
                    isStratagemTargeting = false;

                    string confirmBlocked = StratagemUtility.GetBlockedReason(def, map);
                    if (!confirmBlocked.NullOrEmpty())
                    {
                        Messages.Message(confirmBlocked, MessageTypeDefOf.RejectInput);
                        return;
                    }

                    if (!StratagemUtility.TryConsumePower(def, map))
                    {
                        Messages.Message(
                            "NCL_Stratagem_NotEnoughPower".Translate(def.powerCost.ToString("F0")),
                            MessageTypeDefOf.RejectInput);
                        return;
                    }

                    MapComponent_StratagemRuntime runtime = MapComponent_StratagemRuntime.GetOrCreate(pawn.Map);
                    if (runtime == null)
                    {
                        Log.Warning("[NCL] Stratagem runtime map component missing.");
                        return;
                    }

                    if (!runtime.TryQueueCast(pawn, target.Cell, def))
                    {
                        return;
                    }

                    if (applyCooldown && loadoutIndex >= 0 && def.cooldownSeconds > 0f)
                    {
                        int now = Find.TickManager?.TicksGame ?? 0;
                        manager.SetStratagemCooldownUntilTick(
                            diverSlotIndex,
                            loadoutIndex,
                            now + Mathf.RoundToInt(def.cooldownSeconds * 60f));
                    }
                },
                target => DrawStratagemTargetingHighlight(pawn, def, target),
                target =>
                {
                    if (!target.IsValid || !target.Cell.InBounds(pawn.Map))
                    {
                        Messages.Message("NCL_Stratagem_InvalidTarget".Translate(), MessageTypeDefOf.RejectInput);
                        return false;
                    }

                    float dist = (target.Cell - pawn.Position).LengthHorizontal;
                    if (dist > def.rangeRadius)
                    {
                        Messages.Message("NCL_Stratagem_OutOfRange".Translate(def.rangeRadius.ToString("F1")), MessageTypeDefOf.RejectInput);
                        return false;
                    }

                    if (applyCooldown && loadoutIndex >= 0)
                    {
                        int now = Find.TickManager?.TicksGame ?? 0;
                        int readyTick = manager.GetStratagemCooldownUntilTick(diverSlotIndex, loadoutIndex);
                        if (now < readyTick)
                        {
                            int remain = readyTick - now;
                            Messages.Message("NCL_Stratagem_Cooldown".Translate((remain / 60f).ToString("F1")), MessageTypeDefOf.RejectInput);
                            return false;
                        }
                    }

                    string blocked = StratagemUtility.GetBlockedReason(def, map);
                    if (!blocked.NullOrEmpty())
                    {
                        Messages.Message(blocked, MessageTypeDefOf.RejectInput);
                        return false;
                    }

                    return true;
                },
                pawn,
                () => isStratagemTargeting = false);
        }

        private static void DrawStratagemTargetingHighlight(Pawn pawn, StratagemDef def, LocalTargetInfo target)
        {
            if (pawn == null || !pawn.Spawned || pawn.Map == null || def == null)
            {
                return;
            }

            float rangeRadius = def.rangeRadius;
            GenDraw.DrawRadiusRing(
                pawn.Position,
                rangeRadius,
                Color.white,
                c => c.InBounds(pawn.Map) && (c - pawn.Position).LengthHorizontal <= rangeRadius);

            if (!target.IsValid || !target.Cell.InBounds(pawn.Map))
            {
                return;
            }

            if ((target.Cell - pawn.Position).LengthHorizontal <= rangeRadius)
            {
                GenDraw.DrawTargetHighlight(target);
            }
        }
    }
}
