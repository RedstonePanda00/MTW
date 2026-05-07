using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using Verse;

namespace NoBody
{
    public class NoBodyApparel : Apparel
    {
        private Command_ActionWithFloat cachedBodyTypeCommand;

        public override IEnumerable<Gizmo> GetWornGizmos()
        {
            if (Find.Selector.SingleSelectedThing == Wearer)
            {
                if (DebugSettings.godMode)
                {
                    if (cachedBodyTypeCommand != null && cachedBodyTypeCommand.action != null)
                    {
                        yield return cachedBodyTypeCommand;
                    }
                    else
                    {
                        cachedBodyTypeCommand = new Command_ActionWithFloat
                        {
                            defaultLabel = "切换身形",
                            defaultDesc = "为这个角色选择不同的身形。",
                            floatMenuGetter = GetBodyTypeOptions,
                            action = () => Messages.Message(
                                "请选择一个新的身形",
                                Wearer,
                                MessageTypeDefOf.PositiveEvent)
                        };
                        yield return cachedBodyTypeCommand;
                    }
                }
            }
        }

        private List<FloatMenuOption> GetBodyTypeOptions()
        {
            List<FloatMenuOption> bodyTypeOptions = new List<FloatMenuOption>();
            Pawn pawn = Wearer;
            if (pawn?.story == null)
                return bodyTypeOptions;

            foreach (BodyTypeDef bodyTypeDef in DefDatabase<BodyTypeDef>.AllDefsListForReading)
            {
                BodyTypeDef captured = bodyTypeDef;
                bodyTypeOptions.Add(new FloatMenuOption(
                    captured.defName,
                    () =>
                    {
                        pawn.story.bodyType = captured;
                        PortraitsCache.SetDirty(pawn);
                        Messages.Message(
                            $"{pawn.LabelShortCap} 的身形已切换为 {captured.LabelCap}",
                            pawn,
                            MessageTypeDefOf.PositiveEvent);
                    },
                    MenuOptionPriority.Default));
            }

            return bodyTypeOptions;
        }
    }
}
